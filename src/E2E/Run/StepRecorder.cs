// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>StepRecorder</c> (<c>run/steps.ts</c>): the attempt-scoped step timeline. Every public API call that touches the app
/// runs as one recorded step, whose record keeps what it was, where the test called it from, how long it took, and how it ended.
/// Work a step starts is attributed to it through an <see cref="AsyncLocal{T}"/>, so overlapping steps and nested ones keep their
/// own events and artifacts. Not ported yet: the turns of an agent step.
/// </summary>
internal sealed class StepRecorder
{
    /// <summary>Characters of a step label an error message quotes before clipping it.</summary>
    private const int MaxQuotedLabelChars = 80;

    /// <summary>Characters of an event's detail the report admits.</summary>
    internal const int MaxEventDetailChars = 300;

    private readonly string _attemptId;
    private readonly (string Id, int Index) _attempt;
    private readonly int _maxEventsPerStep;
    private readonly Action<StepProgress>? _onProgress;
    private readonly Action<StepProgress, StepRecord>? _mirror;
    private readonly string? _projectRoot;
    private readonly Func<string, string>? _redact;
    private readonly CancellationToken _cancellation;
    private readonly Func<CancellationToken>? _signal;
    private readonly Func<Exception?>? _reason;
    private readonly AiTraceScope? _aiTrace;
    private readonly bool _redactThrown;
    private readonly int? _retainSteps;
    private readonly Lock _gate = new();
    private readonly List<StepRecord> _steps = [];
    private readonly AsyncLocal<StepRecord?> _scope = new();

    // The Step this code runs inside, with the ones around it. Apart from _scope: a step called in a Step is still a top-level step, held and verified as one.
    private readonly AsyncLocal<ActiveGroup?> _groups = new();

    // Steps opened so far, the next step's index: _steps may have dropped its oldest.
    private int _opened;

    // The hold steps and pauses wait at; null when the attempt holds nothing.
    private readonly RecorderHold? _hold;

    // Whether a top-level step started now waits at the hold; see Holdable.
    private bool _holdable;

    private string _owner = "the test body";

    // Step calls waiting at their hold, in call order.
    private readonly HashSet<HeldCall> _held = [];

    // The owner each running step was started by, as the error for one left running names it.
    private readonly Dictionary<string, string> _owners = [];

    // Pauses waiting at the hold, ordered by when they were called.
    private readonly HashSet<HeldPause> _pauses = [];

    // IDs of steps whose bodies are still executing.
    private readonly HashSet<string> _running = [];

    // The task each running step returned to its caller, to observe when the caller did not.
    private readonly Dictionary<string, Task> _pending = [];

    // Steps the body returned without awaiting: recorded as failed already, whatever they do next.
    private readonly HashSet<string> _abandoned = [];

    // The tasks of abandoned steps, for the wait before teardown.
    private readonly List<Task> _abandonedTasks = [];

    // What each step named by NameRunning fails with once the cancellation it was named for reaches it.
    private readonly Dictionary<string, Exception> _named = [];

    // The reason a close aborts with, once NameRunning was called before any other abort reached the signal; null when none did.
    private Exception? _closeReason;

    // Completes with a step's name when NameRunning reaches it, so a step whose work ignores the cancellation is cut at once.
    private readonly Dictionary<string, TaskCompletionSource<Exception>> _cutsOnClose = [];

    // Highest timeline index among passed verification steps, or -1 when none has.
    private int _lastVerified = -1;

    // What the app logged this attempt, within AppLogLimits.
    private readonly List<AppLogRecord> _logged = [];

    // App log entries of each kind AppLogLimits counts, kept so far: errors and warnings, then info.
    private int _loggedErrorsAndWarnings;
    private int _loggedInfo;

    // Where the app went before the attempt's first step, told under it once it starts.
    private readonly List<(string Line, DateTimeOffset At)> _earlyNavigations = [];

    // The last screen each running step saw, read only when the step ends.
    private readonly Dictionary<string, Func<ScreenText?>> _seenScreens = [];

    // The last screen a finished step saw, the one the next step's is compared with.
    private (int Step, ScreenText Screen)? _lastScreen;

    public StepRecorder(string attemptId, StepRecorderOptions? options = null)
    {
        options ??= new StepRecorderOptions();
        _attemptId = attemptId;
        _attempt = options.Attempt ?? (attemptId, 0);
        _maxEventsPerStep = options.MaxEventsPerStep;
        _onProgress = options.OnProgress;
        _mirror = options.Mirror;
        _projectRoot = options.ProjectRoot;
        _redact = options.Redact;
        _cancellation = options.Cancellation;
        _signal = options.Signal;
        _reason = options.Reason;
        _aiTrace = options.AiTrace;
        _redactThrown = options.RedactThrown;
        _retainSteps = options.RetainSteps;
        _hold = options.Hold;
    }

    /// <summary>
    /// Whether a top-level step started now waits at the hold: in <c>BeforeEach</c> and the body, never in launch or teardown. A pause fails outside
    /// these parts of the attempt, and after the body ends. Upstream's <c>holdable</c>.
    /// </summary>
    public bool Holdable
    {
        get => Volatile.Read(ref _holdable);
        set => Volatile.Write(ref _holdable, value);
    }

    /// <summary>
    /// The code that starts steps from now on, as the error for one it leaves running names it: it completes "... returned before", as in
    /// <c>the beforeEach hook</c>. Upstream's <c>owner</c>.
    /// </summary>
    public string Owner
    {
        get => Volatile.Read(ref _owner);
        set => Volatile.Write(ref _owner, value);
    }

    /// <summary>
    /// The hook steps run in from now on; null while the test body runs. A step recorded while this is null runs in the hook its
    /// call stack names, when a test framework's setup or teardown method called it (see <see cref="StepHooks"/>).
    /// </summary>
    public StepPhase? Phase { get; set; }

    /// <summary>The root step sources are read against.</summary>
    public string? ProjectRoot => _projectRoot;

    /// <summary>Whether a step is still executing.</summary>
    public bool Busy
    {
        get
        {
            lock (_gate)
            {
                return _running.Count > 0;
            }
        }
    }

    /// <summary>The step currently executing, when inside <see cref="RunAsync{T}"/>.</summary>
    public string? CurrentStepId => Current()?.Id;

    /// <summary>Timeline index of the currently executing step.</summary>
    public int? CurrentStepIndex => Current()?.Index;

    /// <summary>Highest timeline index among passed verification steps, or -1 when none has.</summary>
    public int LastVerifiedStepIndex => Volatile.Read(ref _lastVerified);

    /// <summary>Replaces secret values in text the way every record is redacted; the text as it is without a redactor.</summary>
    public string RedactText(string text) => _redact?.Invoke(text) ?? text;

    /// <summary>Every step so far, in timeline order.</summary>
    public IReadOnlyList<StepRecord> All()
    {
        lock (_gate)
        {
            return [.. _steps];
        }
    }

    /// <summary>Steps of the test's own session whose execution has finished, e.g. as prior-step prompt context.</summary>
    public IReadOnlyList<StepRecord> Completed()
    {
        lock (_gate)
        {
            return [.. _steps.Where(step => OwnSteps.IsOwnStep(step) && !_running.Contains(step.Id))];
        }
    }

    /// <summary>
    /// Files a step another session runs (<see cref="GuestSteps"/>) and tells reporters it started. The step takes the next index, so the report tells
    /// both sessions' steps in the order they began. It is the other session's in every other way: never the running step here, so it is not held and
    /// it verifies nothing. Upstream's <c>guest</c>.
    /// </summary>
    public GuestRecord Guest(StepKind kind, string api, string label, string actor, SourceLocation? source)
    {
        StepRecord record;
        var group = EnclosingGroup();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            record = File(kind, api, RedactText(label), actor, source, null, null, group?.Record.Index);
        }

        RunsInside(group, ended.Task);

        Publish(
            record,
            new StepProgress
            {
                Phase = StepProgressPhase.Start,
                Api = record.Api,
                Kind = record.Kind,
                Label = record.Label,
            }
        );
        return new GuestRecord(
            progress =>
            {
                if (progress.Phase == StepProgressPhase.Event && progress.Event is { } stepEvent)
                {
                    Push(record, stepEvent);
                }
                else
                {
                    Publish(record, progress);
                }
            },
            result =>
            {
                lock (_gate)
                {
                    result.ApplyTo(record);
                }

                PublishEnd(record);
                ended.TrySetResult();
            }
        );
    }

    // The last call on the test's own session, the one an event with no running step belongs to; a Step is a name over calls, not one.
    private StepRecord? LastOwnLocked() =>
        _steps.LastOrDefault(step => OwnSteps.IsOwnStep(step) && step.Kind != StepKind.Step);

    // Mints a step's record at the next index, started now in the running phase. Upstream's `file`. Holds _gate.
    private StepRecord File(
        StepKind kind,
        string api,
        string label,
        string? actor,
        SourceLocation? source,
        string? agent,
        StepPhase? phase,
        int? parent
    )
    {
        var index = _opened++;
        var record = new StepRecord
        {
            Id = _attemptId + ":" + index.ToString(CultureInfo.InvariantCulture),
            Index = index,
            Kind = kind,
            Api = api,
            Label = label,
            Actor = actor,
            Parent = parent,
            Source = source,
            StartedAt = DateTimeOffset.UtcNow,
            Agent = agent,
            Phase = phase ?? Phase,
        };
        _steps.Add(record);
        return record;
    }

    /// <summary>Runs one public API call as a recorded top-level step.</summary>
    public Task<T> RunAsync<T>(
        StepKind kind,
        string api,
        string rawLabel,
        Func<Task<T>> body,
        StepRunOptions? options = null
    )
    {
        options ??= new StepRunOptions();

        // A step the body starts after its own timeout cut the signal is refused, and not recorded.
        if (Refused<T>() is { } refused)
        {
            return refused;
        }

        // The label is published, reported, and traced as is: a secret the test spelled into an instruction or a locator stops here, once.
        var label = RedactText(rawLabel);
        var group = EnclosingGroup();
        // Every step pays the capture, since the failing one is not known until it fails, and a report that names the line is worth it.
        var stack = new StackTrace(1, fNeedFileInfo: true);

        // A step inside another (an OnAction hook's) or inside an expect.poll read is part of what holds it, so only a top-level step waits.
        if (_hold is { Steps: true } hold && Holdable && Current() is null && !PollScopes.InsidePollRead())
        {
            return RunHeld(
                hold,
                new HeldCall(group, kind, api, label, stack, options, new CancellationTokenSource()),
                body
            );
        }

        return Start(kind, api, label, stack, body, options, group);
    }

    /// <summary>
    /// Runs <paramref name="body"/> as one step the test names (<c>Step</c>). The steps <paramref name="body"/> starts are recorded as they would be
    /// outside it, each with this step as its <see cref="StepRecord.Parent"/>, so the group itself is never held and verifies nothing. It fails with
    /// what <paramref name="body"/> throws, keeping the error only when no step inside it already did. Upstream's <c>group</c>.
    /// </summary>
    public Task<T> GroupAsync<T>(string rawTitle, Func<Task<T>> body, StepGroupOptions? options = null)
    {
        options ??= new StepGroupOptions();
        if (Refused<T>() is { } refused)
        {
            return refused;
        }

        var stack = new StackTrace(1, fNeedFileInfo: true);
        var signal = _signal?.Invoke() ?? default;
        var enclosing = EnclosingGroup();
        var record = Open(
            StepKind.Step,
            StepApis.TestStepApi,
            RedactText(rawTitle),
            stack,
            new StepRunOptions(),
            enclosing
        );
        var group = new ActiveGroup(record, enclosing);
        var task = ExecuteGroupAsync(group, body, options, signal);
        lock (_gate)
        {
            if (_running.Contains(record.Id))
            {
                _pending[record.Id] = task;
            }
        }

        RunsInside(enclosing, task);
        return task;
    }

    /// <inheritdoc cref="GroupAsync{T}(string, Func{Task{T}}, StepGroupOptions?)"/>
    public Task GroupAsync(string rawTitle, Func<Task> body, StepGroupOptions? options = null) =>
        GroupAsync(
            rawTitle,
            async () =>
            {
                await body().ConfigureAwait(false);
                return true;
            },
            options
        );

    // The rejection of a step started after the running phase's signal cut, or null when the step may start: the attempt has ended by then, so the
    // timeline keeps the steps that ran.
    private Task<T>? Refused<T>()
    {
        if (_signal?.Invoke() is not { IsCancellationRequested: true } cut)
        {
            return null;
        }

        // The close's abort carries its reason, as upstream's signal does; any other abort is a plain cancellation.
        Exception? reason;
        lock (_gate)
        {
            reason = _closeReason;
        }

        reason ??= _reason?.Invoke();
        return reason is null ? Task.FromCanceled<T>(cut) : Task.FromException<T>(reason);
    }

    private async Task<T> ExecuteGroupAsync<T>(
        ActiveGroup group,
        Func<Task<T>> body,
        StepGroupOptions options,
        CancellationToken signal
    )
    {
        var record = group.Record;
        var started = Stopwatch.GetTimestamp();
        try
        {
            // The change stays with this call and what it starts: the caller's own context is restored when the call yields.
            _groups.Value = group;
            return await CutGroupAsync(group, body(), signal, options.Signal).ConfigureAwait(false);
        }
        catch (Exception thrown)
        {
            var cause = NamedFailure(record, thrown);
            if (IsAbandoned(record))
            {
                AbandonedWork.MarkRejection(cause);
                if (ReferenceEquals(cause, thrown))
                {
                    throw;
                }

                ExceptionDispatchInfo.Throw(cause);
            }

            if (options.PassesThrough?.Invoke(thrown) != true)
            {
                record.Status = FailedStatus(cause);
                bool again;
                lock (_gate)
                {
                    again = group.Rethrown.Contains(thrown);
                }

                if (!again)
                {
                    record.Error = ErrorSerializer.Serialize(
                        cause,
                        _projectRoot,
                        _redact,
                        IsNamed(record, cause) ? record.Source : null
                    );
                }
            }

            Rethrown(group.Group, thrown);
            if (!ReferenceEquals(cause, thrown))
            {
                Rethrown(group.Group, cause);
                ExceptionDispatchInfo.Throw(cause);
            }

            throw;
        }
        finally
        {
            bool wasAbandoned;
            lock (_gate)
            {
                _running.Remove(record.Id);
                _owners.Remove(record.Id);
                _pending.Remove(record.Id);
                _named.Remove(record.Id);
                _cutsOnClose.Remove(record.Id);
                wasAbandoned = _abandoned.Remove(record.Id);
            }

            // An abandoned step reported its end when the body returned; what it did since is not the test's.
            if (!wasAbandoned)
            {
                record.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                PublishEnd(record);
            }
        }
    }

    // Waits for the group's work, or cuts it once the recorder's cancellation or the running phase's signal has fired and the calls running inside
    // the group have ended. Their engine calls are cut by the same abort, and each keeps its own outcome: a group that ended first would leave them
    // running past the body, to be recorded as not awaited. A group that waited for such a call does not record the cancellation again.
    private async Task<T> CutGroupAsync<T>(
        ActiveGroup group,
        Task<T> work,
        CancellationToken signal,
        CancellationToken also
    )
    {
        TaskCompletionSource<Exception>? closed;
        lock (_gate)
        {
            _cutsOnClose.TryGetValue(group.Record.Id, out closed);
        }

        if (!_cancellation.CanBeCanceled && !signal.CanBeCanceled && !also.CanBeCanceled && closed is null)
        {
            return await work.ConfigureAwait(false);
        }

        var cancelled = new TestException(ErrorSerializer.Cancelled, group.Record.Api + " cancelled");
        var cut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cutting = 0;
        void Cut()
        {
            if (Interlocked.Exchange(ref cutting, 1) == 0)
            {
                _ = CutAfterCallsInsideAsync(group, cancelled, cut);
            }
        }

        using var own = _cancellation.Register(Cut);
        using var session = signal.Register(Cut);
        using var phase = also.Register(Cut);
        var first = await Task.WhenAny(work, cut.Task, closed?.Task ?? cut.Task).ConfigureAwait(false);
        if (first == work)
        {
            return await work.ConfigureAwait(false);
        }

        Observe(work);
        throw closed is { Task.IsCompletedSuccessfully: true } ? await closed.Task.ConfigureAwait(false) : cancelled;
    }

    private async Task CutAfterCallsInsideAsync(ActiveGroup group, Exception cancelled, TaskCompletionSource cut)
    {
        Rethrown(group, cancelled, onlyWhenInside: true);

        // A body may answer a cut call by starting another.
        while (true)
        {
            Task[] inside;
            lock (_gate)
            {
                inside = [.. group.Inside];
            }

            if (inside.Length == 0)
            {
                // .NET-only: the body's own continuation after the call it awaited can run after this one (on a synchronization context, or a
                // pool thread that is busy), where upstream's microtask order puts it first. Give it a moment, so a call it starts in answer to the
                // cut is seen. Only a cut group waits.
                await Task.Delay(20).ConfigureAwait(false);
                lock (_gate)
                {
                    if (group.Inside.Count == 0)
                    {
                        break;
                    }
                }

                continue;
            }

            await Task.WhenAll(inside).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        cut.TrySetResult();
    }

    // Keeps what a step inside the group threw, so the group does not record that error again.
    private void Rethrown(ActiveGroup? group, object thrown, bool onlyWhenInside = false)
    {
        if (group is null)
        {
            return;
        }

        lock (_gate)
        {
            if (!onlyWhenInside || group.Inside.Count > 0)
            {
                group.Rethrown.Add(thrown);
            }
        }
    }

    // Keeps `call` among what runs inside `group` until it settles.
    private void RunsInside(ActiveGroup? group, Task call)
    {
        if (group is null)
        {
            return;
        }

        lock (_gate)
        {
            group.Inside.Add(call);
        }

        _ = call.ContinueWith(
            finished =>
            {
                lock (_gate)
                {
                    group.Inside.Remove(finished);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    // The innermost Step still running that this call is inside; one that ended is no parent to work it left behind.
    private ActiveGroup? EnclosingGroup()
    {
        for (var group = _groups.Value; group is not null; group = group.Group)
        {
            lock (_gate)
            {
                if (_running.Contains(group.Record.Id) && !_abandoned.Contains(group.Record.Id))
                {
                    return group;
                }
            }
        }

        return null;
    }

    // How a step that threw ended. A blocked verdict is a distinct outcome, not a product failure: the step's error names what stood in the way.
    private static StepStatus FailedStatus(Exception cause) =>
        string.Equals(ErrorSerializer.Classify(cause).Code, ErrorSerializer.Cancelled, StringComparison.Ordinal)
            ? StepStatus.Cancelled
        : cause is AgentException { Blocked: true } ? StepStatus.Blocked
        : StepStatus.Failed;

    /// <summary>Records the step as started now and runs its body (upstream's <c>start</c>); <paramref name="group"/> is the <c>Step</c> it runs inside.</summary>
    private Task<T> Start<T>(
        StepKind kind,
        string api,
        string label,
        StackTrace stack,
        Func<Task<T>> body,
        StepRunOptions options,
        ActiveGroup? group
    )
    {
        // The signal is read once, before the step registers for close: a session that closes after that has its named error win in CutAsync.
        var signal = _signal?.Invoke() ?? default;
        var record = Open(kind, api, label, stack, options, group);
        var task = ExecuteAsync(record, body, options, signal, group);
        RunsInside(group, task);
        lock (_gate)
        {
            // A step that settled already has nothing to observe.
            if (_running.Contains(record.Id))
            {
                _pending[record.Id] = task;
            }
        }

        return task;
    }

    /// <summary>Mints a step's record as started now and tells reporters (upstream's <c>open</c>).</summary>
    private StepRecord Open(
        StepKind kind,
        string api,
        string label,
        StackTrace stack,
        StepRunOptions options,
        ActiveGroup? group
    )
    {
        var source = _projectRoot is null ? null : Source.Locate(stack, _projectRoot);
        var phase = Phase ?? StepHooks.PhaseOf(stack);
        StepRecord record;
        lock (_gate)
        {
            record = File(kind, api, label, null, source, options.Agent, phase, group?.Record.Index);

            // Only a finished step is dropped, and only from the front, so a long-running one keeps its record.
            while (
                _retainSteps is { } retain
                && _steps.Count > retain
                && !ReferenceEquals(_steps[0], record)
                && !_running.Contains(_steps[0].Id)
            )
            {
                _steps.RemoveAt(0);
            }

            _running.Add(record.Id);
            _owners[record.Id] = Owner;
            _cutsOnClose[record.Id] = new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
        }

        FileEarlyNavigations(record);
        Publish(
            record,
            new StepProgress
            {
                Phase = StepProgressPhase.Start,
                Api = api,
                Kind = kind,
                Label = label,
            }
        );
        return record;
    }

    /// <summary>
    /// Pauses the attempt at <c>screen.pause()</c> until the hold's <c>Ask</c> completes. Without a hold, as in a program or an MCP session, it
    /// returns right away. With one, the call fails outside a holdable part of the attempt or inside another step or an expect.poll read: the timers
    /// there keep running during a pause. Upstream's <c>pause</c>.
    /// </summary>
    /// <exception cref="TestException"><c>INVALID_ARGUMENT</c> for a pause the attempt cannot hold.</exception>
    public async Task PauseAsync()
    {
        if (_hold is not { } hold)
        {
            return;
        }

        const string Api = "screen.pause";
        var stack = new StackTrace(1, fNeedFileInfo: true);
        var source = _projectRoot is null ? null : Source.Locate(stack, _projectRoot);
        var request = new StepHoldRequest
        {
            Kind = StepHoldKind.Pause,
            Number = 0,
            Api = Api,
            Label = "",
            Source = source,
        };
        var pause = new HeldPause(Api, Owner, new CancellationTokenSource());
        var added = false;
        try
        {
            // Teardown's separate timer would count time spent paused. Code the test didn't await would pause a test that has ended.
            if (!Holdable)
            {
                throw new TestException(
                    "INVALID_ARGUMENT",
                    TextRules.WithHint(
                        $"{Api}() was called after the test body ended (in an AfterEach hook, a fixture teardown, or code the test did not await)",
                        "call it in fixture setup, a BeforeEach hook, or the test body, and await it"
                    )
                );
            }

            // Another step or an expect.poll read has a separate timer that counts the pause.
            if (Current() is not null || PollScopes.InsidePollRead())
            {
                throw new TestException(
                    "INVALID_ARGUMENT",
                    TextRules.WithHint(
                        $"{Api}() was called inside another step (an OnAction hook or a project tool), an expect.poll read, or a toPass block",
                        "call it between steps in fixture setup, a BeforeEach hook, or the test body"
                    )
                );
            }

            lock (_gate)
            {
                _pauses.Add(pause);
                added = true;
            }

            pause.Promise = hold.Ask(request, pause.Cancel.Token);
            await pause.Promise.ConfigureAwait(false);
        }
        catch (Exception cause) when (pause.Cancel.IsCancellationRequested)
        {
            // Only AbandonRunning cancels a pause and records its failure.
            AbandonedWork.MarkRejection(cause);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                // A pause AbandonRunning took cancelled its source: that one is not disposed here.
                if (!added || _pauses.Remove(pause))
                {
                    pause.Cancel.Dispose();
                }
            }
        }
    }

    // Waits at the hold, then starts the step as if it were called now: its index, start, and verification boundary are those of its release, so
    // steps called together in one Task.WhenAll keep their call order. The prompt uses the position the step would get if the pending holds
    // started in order with no other steps recorded between them.
    private Task<T> RunHeld<T>(RecorderHold hold, HeldCall call, Func<Task<T>> body)
    {
        var source = _projectRoot is null ? null : Source.Locate(call.Stack, _projectRoot);
        int number;
        lock (_gate)
        {
            _held.Add(call);
            number = _steps.Count + _held.Count;
        }

        var promise = Held();
        call.Promise = promise;
        return promise;

        async Task<T> Held()
        {
            try
            {
                await hold.Ask(
                        new StepHoldRequest
                        {
                            Kind = StepHoldKind.Step,
                            Number = number,
                            Api = call.Api,
                            Label = call.Label,
                            Source = source,
                        },
                        call.Cancel.Token
                    )
                    .ConfigureAwait(false);

                // Released in the same turn the body abandoned it: it must not run.
                call.Cancel.Token.ThrowIfCancellationRequested();
            }
            catch (Exception cause) when (call.Cancel.IsCancellationRequested)
            {
                // Only AbandonRunning cancels a call: its failure is recorded, so whatever wrapped the call must not reach the runner as a stray.
                AbandonedWork.MarkRejection(cause);
                throw;
            }
            finally
            {
                lock (_gate)
                {
                    // A call AbandonRunning took cancelled its source: that one is not disposed here.
                    if (_held.Remove(call))
                    {
                        call.Cancel.Dispose();
                    }
                }
            }

            return await Start(call.Kind, call.Api, call.Label, call.Stack, body, call.Options, call.Group)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc cref="RunAsync{T}(StepKind, string, string, Func{Task{T}}, StepRunOptions?)"/>
    public Task RunAsync(StepKind kind, string api, string rawLabel, Func<Task> body, StepRunOptions? options = null) =>
        RunAsync(
            kind,
            api,
            rawLabel,
            async () =>
            {
                await body().ConfigureAwait(false);
                return true;
            },
            options
        );

    private async Task<T> ExecuteAsync<T>(
        StepRecord record,
        Func<Task<T>> body,
        StepRunOptions options,
        CancellationToken signal,
        ActiveGroup? group
    )
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // The change stays with this call and what it starts: the caller's own context is restored when the call yields.
            _scope.Value = record;
            // Model calls made inside the body are attributed to this step.
            var work = _aiTrace is { } attempt
                ? AiTraceScopes.WithAiTraceScope(attempt with { Agent = options.Agent ?? attempt.Agent }, () =>
                    AiTraceScopes.WithAiTraceStep(record.Api, record.Label, body)
                )
                : body();
            var result = await CutAsync(work, record, signal).ConfigureAwait(false);
            if (IsAbandoned(record))
            {
                return result;
            }

            record.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (options.Verifies)
            {
                InterlockedMax(ref _lastVerified, record.Index);
            }

            return result;
        }
        catch (Exception thrown)
        {
            // A step that failed on its own keeps that failure: only the cancellation a close caused is replaced by the name.
            var cause = NamedFailure(record, thrown);
            if (IsAbandoned(record))
            {
                AbandonedWork.MarkRejection(cause);
                if (ReferenceEquals(cause, thrown))
                {
                    throw;
                }

                ExceptionDispatchInfo.Throw(cause);
            }

            record.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // A blocked verdict is a distinct outcome, not a product failure: the step's error names what stood in the way, and the report says so.
            record.Status = FailedStatus(cause);
            record.Error = ErrorSerializer.Serialize(
                cause,
                _projectRoot,
                _redact,
                IsNamed(record, cause) ? record.Source : null
            );
            if (!ReferenceEquals(cause, thrown))
            {
                Rethrown(group, cause);
                ExceptionDispatchInfo.Throw(cause);
            }

            if (!_redactThrown || _redact is null || record.Status == StepStatus.Cancelled)
            {
                Rethrown(group, thrown);
                throw;
            }

            // A .NET exception's message is read-only, so one that carries a secret is rebuilt with the redacted text, keeping its type and code.
            var safe = ErrorRedaction.Cause(cause, _redact)!;
            if (ReferenceEquals(safe, cause))
            {
                // Classified too: an error with no code is a test failure with code ERROR, as ErrorSerializer.Classify reads it.
                if (cause is E2EException or OperationCanceledException)
                {
                    Rethrown(group, thrown);
                    throw;
                }

                safe = new TestException(record.Error.Code, cause.Message, cause);
            }

            Rethrown(group, safe);
            ExceptionDispatchInfo.Throw(safe);
            throw safe;
        }
        finally
        {
            bool wasAbandoned;
            lock (_gate)
            {
                _running.Remove(record.Id);
                _owners.Remove(record.Id);
                _pending.Remove(record.Id);
                _named.Remove(record.Id);
                _cutsOnClose.Remove(record.Id);

                // An abandoned step reported its end when the body returned; what it did since is not the test's.
                wasAbandoned = _abandoned.Remove(record.Id);
                if (wasAbandoned)
                {
                    _seenScreens.Remove(record.Id);
                }
            }

            if (!wasAbandoned)
            {
                SettleScreen(record);
                PublishEnd(record);
            }
        }
    }

    // Waits for the step's work, or cuts it when the recorder's cancellation fires: the step fails and its body is abandoned.
    private async Task<T> CutAsync<T>(Task<T> work, StepRecord record, CancellationToken signal)
    {
        TaskCompletionSource<Exception>? closed;
        lock (_gate)
        {
            _cutsOnClose.TryGetValue(record.Id, out closed);
        }

        // Either the recorder's own cancellation or the signal its session gives it cuts the step (upstream's `this.signal`, which `execute` races the work against).
        if (!_cancellation.CanBeCanceled && !signal.CanBeCanceled && closed is null)
        {
            return await work.ConfigureAwait(false);
        }

        var cut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var own = _cancellation.Register(() => cut.TrySetResult());
        using var session = signal.Register(() => cut.TrySetResult());
        var first = await Task.WhenAny(work, cut.Task, closed?.Task ?? cut.Task).ConfigureAwait(false);
        if (first == work)
        {
            return await work.ConfigureAwait(false);
        }

        Observe(work);

        // A close names the step and cuts it, though its work never looks at the cancellation.
        throw closed is { Task.IsCompletedSuccessfully: true }
            ? await closed.Task.ConfigureAwait(false)
            : _reason?.Invoke() ?? new TestException(ErrorSerializer.Cancelled, record.Api + " cancelled");
    }

    /// <summary>
    /// Fails every step still running once the test body has settled, and returns the error that names them, or null when every step
    /// was awaited. Each such step is recorded as failed at the line it was called on, its later outcome is dropped, and its failure
    /// is observed here: a step the body abandoned must not take the process down as an unobserved task exception.
    /// </summary>
    public TestException? AbandonRunning()
    {
        // A step still held never ran: it is recorded here, at its call, so it fails like one the body left running, and its hold is let go.
        HeldCall[] heldCalls;
        HeldPause[] pauses;
        lock (_gate)
        {
            heldCalls = [.. _held];
            _held.Clear();
            pauses = [.. _pauses];
            _pauses.Clear();
        }

        var held = new List<StepRecord>();
        foreach (var call in heldCalls)
        {
            call.Cancel.Cancel();
            var opened = Open(call.Kind, call.Api, call.Label, call.Stack, call.Options, call.Group);
            lock (_gate)
            {
                _pending[opened.Id] = call.Promise;
            }

            held.Add(opened);
        }

        // A pause doesn't record a step, so it's cancelled here.
        foreach (var pause in pauses)
        {
            pause.Cancel.Cancel();
            lock (_gate)
            {
                _abandonedTasks.Add(pause.Promise);
            }

            Observe(pause.Promise);
        }

        List<StepRecord> running;
        lock (_gate)
        {
            running = [.. _steps.Where(step => _running.Contains(step.Id) && !_abandoned.Contains(step.Id))];
        }

        var count = running.Count + pauses.Length;
        TestException? first = null;
        if (pauses.Length > 0)
        {
            var extra = count > 1 ? " and " + (count - 1).ToString(CultureInfo.InvariantCulture) + " more" : "";
            first = new TestException(
                "STEP_NOT_AWAITED",
                TextRules.WithHint(
                    $"{pauses[0].Owner} returned before {pauses[0].Api}(){extra} finished",
                    "put `await` in front of screen.pause()"
                )
            );
        }

        foreach (var step in running)
        {
            var more =
                first is null && count > 1
                    ? " and " + (count - 1).ToString(CultureInfo.InvariantCulture) + " more"
                    : "";
            string owner;
            lock (_gate)
            {
                owner = _owners.GetValueOrDefault(step.Id) ?? Owner;
            }

            var error = new TestException(
                "STEP_NOT_AWAITED",
                TextRules.WithHint(
                    owner + " returned before " + CallName(step) + more + " finished",
                    step.Kind == StepKind.Step
                        ? "put `await` in front of Step(); a fixture can't call use() inside one"
                        : "put `await` in front of every step call"
                )
            );
            first ??= error;
            Task? task;
            lock (_gate)
            {
                _abandoned.Add(step.Id);
                _pending.TryGetValue(step.Id, out task);
                if (task is not null)
                {
                    _abandonedTasks.Add(task);
                }
            }

            step.Status = StepStatus.Failed;
            step.DurationMs = (long)Math.Max(0, (DateTimeOffset.UtcNow - step.StartedAt).TotalMilliseconds);

            // The line the step was called on stands in for the frames of the failure, which the runner raises from its own.
            step.Error = ErrorSerializer.Serialize(error, _projectRoot, _redact, step.Source);
            if (task is not null)
            {
                Observe(task);
            }

            PublishEnd(step);
        }

        // No body runs for a held step, so nothing else ends what it started.
        lock (_gate)
        {
            foreach (var record in held)
            {
                _running.Remove(record.Id);
                _owners.Remove(record.Id);
                _abandoned.Remove(record.Id);
                _pending.Remove(record.Id);
                _cutsOnClose.Remove(record.Id);
            }
        }

        return first;
    }

    /// <summary>
    /// Names every step still running in the error it fails with once the cancellation the session's close causes reaches it: what
    /// <paramref name="error"/> builds for the call, whose argument is the step's api and, when it has one, its quoted label. Call it right before
    /// that cancellation. <paramref name="reason"/> is what the close aborts with: a step started after it fails with the reason itself, and when
    /// another abort reached the signal first, no step is named and each keeps its failure. Unlike <see cref="AbandonRunning"/>, nothing is recorded as not awaited and no rejection is observed: a program closes a
    /// session to cancel what runs on it, and the calls it cut are its own to handle. Upstream's <c>nameRunning</c>.
    /// </summary>
    public void NameRunning(Exception reason, Func<string, Exception> error)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(error);
        List<StepRecord> running;
        lock (_gate)
        {
            if (_signal?.Invoke().IsCancellationRequested == true)
            {
                return;
            }

            _closeReason = reason;
            running = [.. _steps.Where(step => _running.Contains(step.Id))];
        }

        foreach (var step in running)
        {
            var named = error(CallName(step));
            TaskCompletionSource<Exception>? cut;
            lock (_gate)
            {
                _named[step.Id] = named;
                _cutsOnClose.TryGetValue(step.Id, out cut);
            }

            cut?.TrySetResult(named);
        }
    }

    /// <summary>Completes once every abandoned step has settled, however it did; the caller bounds the wait.</summary>
    public async Task SettleAbandonedAsync()
    {
        Task[] tasks;
        lock (_gate)
        {
            tasks = [.. _abandonedTasks];
        }

        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>
    /// Attaches an artifact ID to the running step, or to the most recent step when none is running. The running step wins so a step
    /// that ran a nested step (a fixture call that clicked a link, say) still owns what it produced.
    /// </summary>
    public void AttachArtifact(string artifactId)
    {
        // Cleanup outside a scope may attach to the last step. Work inherited from a closed scope must never attach to a newer step.
        lock (_gate)
        {
            var target = _scope.Value is null ? LastOwnLocked() : CurrentLocked();
            target?.Artifacts.Add(artifactId);
        }
    }

    /// <summary>The id of the step <see cref="AttachArtifact"/> would attach to, which a store is told produced the artifact; null when there is none.</summary>
    public string? ArtifactStepId()
    {
        lock (_gate)
        {
            return (_scope.Value is null ? LastOwnLocked() : CurrentLocked())?.Id;
        }
    }

    /// <summary>Records one child event of the running step.</summary>
    public void RecordEvent(StepEvent stepEvent)
    {
        if (Current() is { } current)
        {
            Push(current, stepEvent);
        }
    }

    /// <summary>
    /// Keeps one thing the app did on its own, with the step running in this async context, else the step that started last: an
    /// engine reports from its own event callbacks, outside any step's context, so a request that fails after a tap resolved lands
    /// with the tap. A log line goes to the attempt's app log (<see cref="AppLog"/>), within <see cref="AppLogLimits"/>; where the
    /// app went is an event of the step, and one before the first step waits for it.
    /// </summary>
    public void RecordAppEvent(AppEvent appEvent, DateTimeOffset? at = null)
    {
        ArgumentNullException.ThrowIfNull(appEvent);
        var when = at ?? DateTimeOffset.UtcNow;
        StepRecord? target;
        lock (_gate)
        {
            target = CurrentLocked() ?? LastOwnLocked();
            if (appEvent is AppEvent.Navigation waiting && target is null)
            {
                // A step takes no more events than this, so no more wait for one.
                if (_earlyNavigations.Count < _maxEventsPerStep)
                {
                    _earlyNavigations.Add((waiting.Line, when));
                }

                return;
            }
        }

        switch (appEvent)
        {
            case AppEvent.Navigation went:
                FileNavigation(target!, went.Line, when);
                break;
            case AppEvent.Log { Entry: var entry }:
                // Redacted before it is cut, so no cut leaves the head of a secret behind.
                var text = TextRules.Bound(RedactText(TextRules.Normalize(entry.Text)), MaxEventDetailChars);
                var info = entry.Level == AppLogLevel.Info;
                lock (_gate)
                {
                    if (
                        (info ? _loggedInfo : _loggedErrorsAndWarnings)
                        >= (info ? AppLogLimits.Info : AppLogLimits.ErrorsAndWarnings)
                    )
                    {
                        return;
                    }

                    if (info)
                    {
                        _loggedInfo++;
                    }
                    else
                    {
                        _loggedErrorsAndWarnings++;
                    }

                    _logged.Add(new AppLogRecord(entry.Source, entry.Level, text, when, target?.Index));
                }

                break;
        }
    }

    /// <summary>What the app logged this attempt, oldest first.</summary>
    public IReadOnlyList<AppLogRecord> AppLog()
    {
        lock (_gate)
        {
            return [.. _logged];
        }
    }

    private void FileEarlyNavigations(StepRecord record)
    {
        List<(string Line, DateTimeOffset At)> waiting;
        lock (_gate)
        {
            waiting = [.. _earlyNavigations];
            _earlyNavigations.Clear();
        }

        foreach (var (line, at) in waiting)
        {
            FileNavigation(record, line, at);
        }
    }

    private void FileNavigation(StepRecord target, string line, DateTimeOffset at) =>
        Push(
            target,
            new StepEvent
            {
                Kind = StepEventKind.Navigation,
                StartedAt = at,
                Status = StepEventStatus.Passed,
                Detail = TextRules.Normalize(line),
            }
        );

    /// <summary>
    /// Tells reporters the trace cache has the running step (<c>true</c>), or has handed it to the model (<c>false</c>). Nothing is
    /// recorded: the step's cache detail says how the replay went. Outside a running step this does nothing.
    /// </summary>
    public void Replaying(bool active)
    {
        if (Current() is { } current)
        {
            Publish(
                current,
                new StepProgress
                {
                    Phase = StepProgressPhase.Replay,
                    Api = current.Api,
                    Active = active,
                }
            );
        }
    }

    /// <summary>
    /// Tells reporters what the running step waits on now; the phase's event later reports how it went. Nothing is recorded.
    /// Outside a running step this does nothing.
    /// </summary>
    public void Activity(StepActivity activity)
    {
        if (Current() is { } current)
        {
            Publish(
                current,
                new StepProgress
                {
                    Phase = StepProgressPhase.Activity,
                    Api = current.Api,
                    Activity = activity,
                }
            );
        }
    }

    /// <summary>Merges agent metrics, cache detail, and the judgment's explanation into the running step.</summary>
    public void AttachAgentDetails(StepAgentDetails details)
    {
        if (Current() is not { } current)
        {
            return;
        }

        // The step keeps the largest observation it read, whichever of the two carries it.
        var largest = Math.Max(current.Metrics?.ObservationBytes ?? 0, details.ObservationBytes ?? 0);
        current.Metrics =
            details.Metrics is { } metrics
                ? metrics with
                {
                    ObservationBytes = Math.Max(metrics.ObservationBytes, largest),
                }
            : details.ObservationBytes is null ? current.Metrics
            : (current.Metrics ?? new StepMetrics()) with { ObservationBytes = largest };
        current.ObservationRevision = details.ObservationRevision ?? current.ObservationRevision;
        current.Cache = details.Cache ?? current.Cache;
        current.Turns = details.Turns ?? current.Turns;
        if (details.ObservationTruncated is { } truncated)
        {
            current.ObservationTruncated = truncated;
        }

        if (details.VisionInput is { } visionInput)
        {
            current.VisionInput = visionInput;
        }

        if (details.VisionDegraded is { } degraded)
        {
            current.VisionDegraded = degraded;
        }

        if (details.Explanation is not null)
        {
            current.Explanation = RedactText(details.Explanation);
        }
    }

    /// <summary>
    /// Adds one model call to the running step's usage (upstream's <c>StepAccounting.usage</c>, which the step's model info reports),
    /// and records it as a <c>model</c> event, as upstream's <c>recordModelCall</c> does. Outside a running step this does nothing.
    /// </summary>
    public void AddModelCall(ExecutorModelCall? call)
    {
        StepRecord current;
        long tokens;
        (long? InputTokens, long? OutputTokens) fields;
        lock (_gate)
        {
            if (CurrentLocked() is not { } running)
            {
                return;
            }

            current = running;
            var before = current.Model;
            var usage = new ModelUsage.Usage(
                call?.InputTokens,
                call?.OutputTokens,
                call?.CacheReadTokens,
                call?.CacheWriteTokens,
                EstimatedCostUsd: call?.EstimatedCostUsd
            );
            tokens = current.Usage.Record(usage);
            fields = ModelUsage.TokenFields(usage);
            current.Model = current.Usage.Report(
                new StepModelInfo
                {
                    Provider = call?.Provider ?? before?.Provider,
                    Model = call?.ModelId ?? before?.Model,
                },
                (before?.Calls ?? 0) + 1
            );
        }

        Push(
            current,
            new StepEvent
            {
                Kind = StepEventKind.Model,
                StartedAt = call?.StartedAt ?? DateTimeOffset.UtcNow,
                DurationMs = Math.Max(0, call?.DurationMs ?? 0),
                Name = "executor",
                Count = tokens,
                InputTokens = fields.InputTokens,
                OutputTokens = fields.OutputTokens,
                Reasoning = ModelUsage.ReasoningField(call?.Reasoning, RedactText),
            }
        );
    }

    /// <summary>
    /// Notes a screen the running step saw; the newest one stands. <paramref name="read"/> renders it, redacted, and runs once, when
    /// the step ends, so a step that observes many times pays for one. A screen no step is running for (the failure evidence, a
    /// launch) is the trace's only through the screen at failure.
    /// </summary>
    public void RecordScreen(Func<ScreenText?> read)
    {
        lock (_gate)
        {
            if (CurrentLocked() is { } target)
            {
                _seenScreens[target.Id] = read;
            }
        }
    }

    // Compares the last screen a step saw with the one before it, once the step has ended.
    private void SettleScreen(StepRecord record)
    {
        Func<ScreenText?>? read;
        lock (_gate)
        {
            if (!_seenScreens.Remove(record.Id, out read))
            {
                return;
            }
        }

        ScreenText? screen;
        try
        {
            screen = read();
        }
        catch (Exception)
        {
            return;
        }

        if (screen is null)
        {
            return;
        }

        lock (_gate)
        {
            record.Screen = StepScreens.CompareScreens(_lastScreen, screen);
            _lastScreen = (record.Index, screen);
        }
    }

    /// <summary>Records the viewport a step established.</summary>
    public void AttachViewport(StepViewport viewport)
    {
        if (Current() is { } current)
        {
            current.Viewport = viewport;
        }
    }

    // Appends an event to a step, its prose redacted, within the per-step cap.
    private void Push(StepRecord step, StepEvent stepEvent)
    {
        // Redacted before it is cut, so no cut leaves the head of a secret behind.
        var redacted = stepEvent.Detail is null
            ? stepEvent
            : stepEvent with
            {
                Detail = TextRules.Bound(RedactText(stepEvent.Detail), MaxEventDetailChars),
            };
        lock (_gate)
        {
            if (step.Events.Count >= _maxEventsPerStep)
            {
                return;
            }

            step.Events.Add(redacted);
        }

        Publish(
            step,
            new StepProgress
            {
                Phase = StepProgressPhase.Event,
                Api = step.Api,
                Event = redacted,
            }
        );
    }

    private StepRecord? Current()
    {
        lock (_gate)
        {
            return CurrentLocked();
        }
    }

    private StepRecord? CurrentLocked() =>
        _scope.Value is { } record && _running.Contains(record.Id) && !_abandoned.Contains(record.Id) ? record : null;

    // The name a close cut the step by, when what the step threw is the cancellation: a step that failed on its own keeps its failure.
    private Exception NamedFailure(StepRecord record, Exception thrown)
    {
        Exception? named;
        lock (_gate)
        {
            _named.TryGetValue(record.Id, out named);
        }

        return
            named is not null
            && (
                thrown is OperationCanceledException
                || string.Equals(
                    ErrorSerializer.Classify(thrown).Code,
                    ErrorSerializer.Cancelled,
                    StringComparison.Ordinal
                )
            )
            ? named
            : thrown;
    }

    // Whether the failure is the name a close gave the step: the line it was called on stands in for the frames of the failure.
    private bool IsNamed(StepRecord record, Exception cause)
    {
        lock (_gate)
        {
            return _named.TryGetValue(record.Id, out var named) && ReferenceEquals(named, cause);
        }
    }

    private bool IsAbandoned(StepRecord record)
    {
        lock (_gate)
        {
            return _abandoned.Contains(record.Id);
        }
    }

    private void PublishEnd(StepRecord record) =>
        Publish(
            record,
            new StepProgress
            {
                Phase = StepProgressPhase.End,
                Api = record.Api,
                Kind = record.Kind,
                Label = record.Label,
                Status = record.Status,
                DurationMs = record.DurationMs,
                ModelCalls = record.Events.Count(item => item.Kind == StepEventKind.Model),
                Error = record.Error,
                Explanation = record.Explanation,
            }
        );

    // Publishes a phase with the owning record's identity, including nested and finishing steps.
    private void Publish(StepRecord record, StepProgress fact)
    {
        if (_onProgress is null && _mirror is null)
        {
            return;
        }

        var progress = fact with
        {
            Identity = new StepProgressIdentity(
                _attempt.Id,
                _attempt.Index,
                record.Id,
                record.Index,
                record.Actor,
                record.Parent is { } parent ? _attemptId + ":" + parent.ToString(CultureInfo.InvariantCulture) : null
            ),
        };
        _onProgress?.Invoke(progress);
        _mirror?.Invoke(progress, record);
    }

    // A step as an error message names it: its api, and its quoted label when it has one.
    private static string CallName(StepRecord step) =>
        step.Label.Length == 0 ? step.Api : step.Api + " " + Quoted(step.Label);

    private static string Quoted(string label) =>
        "\"" + (label.Length > MaxQuotedLabelChars ? label[..(MaxQuotedLabelChars - 3)] + "..." : label) + "\"";

    // A task nobody awaits must not fault unobserved.
    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            static finished => _ = finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
        } while (value > seen && Interlocked.CompareExchange(ref target, value, seen) != seen);
    }
}

/// <summary>A step call waiting at its hold, kept so a body that returns without awaiting it still fails as <c>STEP_NOT_AWAITED</c> (upstream's <c>HeldCall</c>).</summary>
internal sealed class HeldCall(
    ActiveGroup? group,
    StepKind kind,
    string api,
    string label,
    StackTrace stack,
    StepRunOptions options,
    CancellationTokenSource cancel
)
{
    /// <summary>The <c>Step</c> the call was made in, kept for a call recorded from outside it.</summary>
    public ActiveGroup? Group { get; } = group;

    public StepKind Kind { get; } = kind;

    public string Api { get; } = api;

    public string Label { get; } = label;

    public StackTrace Stack { get; } = stack;

    public StepRunOptions Options { get; } = options;

    public CancellationTokenSource Cancel { get; } = cancel;

    public Task Promise { get; set; } = Task.CompletedTask;
}

/// <summary>A pause waiting at the hold. If the body returns without awaiting it, the test fails with <c>STEP_NOT_AWAITED</c> (upstream's <c>HeldPause</c>).</summary>
internal sealed class HeldPause(string api, string owner, CancellationTokenSource cancel)
{
    public string Api { get; } = api;

    /// <summary>The <see cref="StepRecorder.Owner"/> the pause was started by.</summary>
    public string Owner { get; } = owner;

    public CancellationTokenSource Cancel { get; } = cancel;

    public Task Promise { get; set; } = Task.CompletedTask;
}

/// <summary>A running <c>Step</c>: what the recorder keeps about it while its body runs (upstream's <c>ActiveGroup</c>).</summary>
internal sealed class ActiveGroup(StepRecord record, ActiveGroup? group)
{
    public StepRecord Record { get; } = record;

    /// <summary>The <c>Step</c> this one runs inside.</summary>
    public ActiveGroup? Group { get; } = group;

    /// <summary>What the steps that failed inside it threw. Each is that step's error, which this one does not record again.</summary>
    public HashSet<object> Rethrown { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The calls still running directly inside it, a <c>Launch</c> session's included.</summary>
    public HashSet<Task> Inside { get; } = [];
}

/// <summary>Per-call options for <see cref="StepRecorder.GroupAsync{T}"/> (upstream's <c>StepGroupOptions</c>).</summary>
internal sealed record StepGroupOptions
{
    /// <summary>Whether a thrown value ends the test without failing it (a skip): the step it passes through then passes.</summary>
    public Func<Exception, bool>? PassesThrough { get; init; }

    /// <summary>Cuts the step when it aborts, as the engine cuts the calls inside it, beside what cuts any step.</summary>
    public CancellationToken Signal { get; init; }
}
