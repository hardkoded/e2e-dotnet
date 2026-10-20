// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
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
    private readonly string? _projectRoot;
    private readonly Func<string, string>? _redact;
    private readonly CancellationToken _cancellation;
    private readonly AiTraceScope? _aiTrace;
    private readonly Lock _gate = new();
    private readonly List<StepRecord> _steps = [];
    private readonly AsyncLocal<StepRecord?> _scope = new();

    // IDs of steps whose bodies are still executing.
    private readonly HashSet<string> _running = [];

    // The task each running step returned to its caller, to observe when the caller did not.
    private readonly Dictionary<string, Task> _pending = [];

    // Steps the body returned without awaiting: recorded as failed already, whatever they do next.
    private readonly HashSet<string> _abandoned = [];

    // The tasks of abandoned steps, for the wait before teardown.
    private readonly List<Task> _abandonedTasks = [];

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
        _projectRoot = options.ProjectRoot;
        _redact = options.Redact;
        _cancellation = options.Cancellation;
        _aiTrace = options.AiTrace;
    }

    /// <summary>
    /// The hook steps run in from now on; null while the test body runs. A step recorded while this is null runs in the hook its
    /// call stack names, when a test framework's setup or teardown method called it (see <see cref="StepHooks"/>).
    /// </summary>
    public StepPhase? Phase { get; set; }

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

    /// <summary>Steps whose execution has finished, e.g. as prior-step prompt context.</summary>
    public IReadOnlyList<StepRecord> Completed()
    {
        lock (_gate)
        {
            return [.. _steps.Where(step => !_running.Contains(step.Id))];
        }
    }

    /// <summary>Runs one public API call as a recorded top-level step.</summary>
    public Task<T> RunAsync<T>(StepKind kind, string api, string rawLabel, Func<Task<T>> body, StepRunOptions? options = null)
    {
        options ??= new StepRunOptions();

        // The label is published, reported, and traced as is: a secret the test spelled into an instruction or a locator stops here, once.
        var label = RedactText(rawLabel);
        var startedAt = DateTimeOffset.UtcNow;

        // Every step pays the capture, since the failing one is not known until it fails, and a report that names the line is worth it.
        var stack = new StackTrace(1, fNeedFileInfo: true);
        var source = _projectRoot is null ? null : Source.Locate(stack, _projectRoot);
        var phase = Phase ?? StepHooks.PhaseOf(stack);
        StepRecord record;
        lock (_gate)
        {
            var index = _steps.Count;
            record = new StepRecord
            {
                Id = _attemptId + ":" + index.ToString(CultureInfo.InvariantCulture),
                Index = index,
                Kind = kind,
                Api = api,
                Label = label,
                Source = source,
                StartedAt = startedAt,
                Agent = options.Agent,
                Phase = phase,
            };
            _steps.Add(record);
            _running.Add(record.Id);
        }

        FileEarlyNavigations(record);
        Publish(record, new StepProgress { Phase = StepProgressPhase.Start, Api = api, Kind = kind, Label = label });
        var task = ExecuteAsync(record, body, options);
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

    /// <inheritdoc cref="RunAsync{T}(StepKind, string, string, Func{Task{T}}, StepRunOptions?)"/>
    public Task RunAsync(StepKind kind, string api, string rawLabel, Func<Task> body, StepRunOptions? options = null) =>
        RunAsync(kind, api, rawLabel, async () =>
        {
            await body().ConfigureAwait(false);
            return true;
        }, options);

    private async Task<T> ExecuteAsync<T>(StepRecord record, Func<Task<T>> body, StepRunOptions options)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // The change stays with this call and what it starts: the caller's own context is restored when the call yields.
            _scope.Value = record;
            // Model calls made inside the body are attributed to this step.
            var work = _aiTrace is { } attempt
                ? AiTraceScopes.WithAiTraceScope(attempt with { Agent = options.Agent ?? attempt.Agent }, () => AiTraceScopes.WithAiTraceStep(record.Api, record.Label, body))
                : body();
            var result = await CutAsync(work, record).ConfigureAwait(false);
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
        catch (Exception cause)
        {
            if (IsAbandoned(record))
            {
                AbandonedWork.MarkRejection(cause);
                throw;
            }

            record.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // A blocked verdict is a distinct outcome, not a product failure: the step's error names what stood in the way, and the report says so.
            record.Status = string.Equals(ErrorSerializer.Classify(cause).Code, ErrorSerializer.Cancelled, StringComparison.Ordinal)
                ? StepStatus.Cancelled
                : cause is AgentException { Blocked: true } ? StepStatus.Blocked : StepStatus.Failed;
            record.Error = ErrorSerializer.Serialize(cause, _projectRoot, _redact);
            throw;
        }
        finally
        {
            bool wasAbandoned;
            lock (_gate)
            {
                _running.Remove(record.Id);
                _pending.Remove(record.Id);

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
    private async Task<T> CutAsync<T>(Task<T> work, StepRecord record)
    {
        if (!_cancellation.CanBeCanceled)
        {
            return await work.ConfigureAwait(false);
        }

        var cut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _cancellation.Register(() => cut.TrySetResult());
        if (await Task.WhenAny(work, cut.Task).ConfigureAwait(false) == work)
        {
            return await work.ConfigureAwait(false);
        }

        Observe(work);
        throw new TestException(ErrorSerializer.Cancelled, record.Api + " cancelled");
    }

    /// <summary>
    /// Fails every step still running once the test body has settled, and returns the error that names them, or null when every step
    /// was awaited. Each such step is recorded as failed at the line it was called on, its later outcome is dropped, and its failure
    /// is observed here: a step the body abandoned must not take the process down as an unobserved task exception.
    /// </summary>
    public TestException? AbandonRunning()
    {
        List<StepRecord> running;
        lock (_gate)
        {
            running = [.. _steps.Where(step => _running.Contains(step.Id) && !_abandoned.Contains(step.Id))];
        }

        TestException? first = null;
        foreach (var step in running)
        {
            var more = first is null && running.Count > 1 ? " and " + (running.Count - 1).ToString(CultureInfo.InvariantCulture) + " more" : "";
            var error = new TestException(
                "STEP_NOT_AWAITED",
                TextRules.WithHint("the test body returned before " + step.Api + " " + Quoted(step.Label) + more + " finished", "put `await` in front of every step call"));
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

        return first;
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
            var target = _scope.Value is null ? _steps.LastOrDefault() : CurrentLocked();
            target?.Artifacts.Add(artifactId);
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
            target = CurrentLocked() ?? _steps.LastOrDefault();
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
                    if ((info ? _loggedInfo : _loggedErrorsAndWarnings) >= (info ? AppLogLimits.Info : AppLogLimits.ErrorsAndWarnings))
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
        Push(target, new StepEvent { Kind = StepEventKind.Navigation, StartedAt = at, Status = StepEventStatus.Passed, Detail = TextRules.Normalize(line) });

    /// <summary>
    /// Tells reporters the trace cache has the running step (<c>true</c>), or has handed it to the model (<c>false</c>). Nothing is
    /// recorded: the step's cache detail says how the replay went. Outside a running step this does nothing.
    /// </summary>
    public void Replaying(bool active)
    {
        if (Current() is { } current)
        {
            Publish(current, new StepProgress { Phase = StepProgressPhase.Replay, Api = current.Api, Active = active });
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
            Publish(current, new StepProgress { Phase = StepProgressPhase.Activity, Api = current.Api, Activity = activity });
        }
    }

    /// <summary>Merges agent metrics, cache detail, and the judgment's explanation into the running step.</summary>
    public void AttachAgentDetails(StepAgentDetails details)
    {
        if (Current() is not { } current)
        {
            return;
        }

        current.Metrics = details.Metrics ?? current.Metrics;
        current.Cache = details.Cache ?? current.Cache;
        current.Turns = details.Turns ?? current.Turns;
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
        lock (_gate)
        {
            if (CurrentLocked() is not { } running)
            {
                return;
            }

            current = running;
            var before = current.Model;
            current.Model = new StepModelInfo
            {
                Provider = call?.Provider ?? before?.Provider,
                Model = call?.ModelId ?? before?.Model,
                Calls = (before?.Calls ?? 0) + 1,
                InputTokens = (before?.InputTokens ?? 0) + (call?.InputTokens ?? 0),
                OutputTokens = (before?.OutputTokens ?? 0) + (call?.OutputTokens ?? 0),
                CacheReadTokens = Sum(before?.CacheReadTokens, call?.CacheReadTokens),
                CacheWriteTokens = Sum(before?.CacheWriteTokens, call?.CacheWriteTokens),
                EstimatedCostUsd = call?.EstimatedCostUsd is { } cost ? (before?.EstimatedCostUsd ?? 0) + cost : before?.EstimatedCostUsd,
            };
        }

        Push(
            current,
            new StepEvent
            {
                Kind = StepEventKind.Model,
                StartedAt = call?.StartedAt ?? DateTimeOffset.UtcNow,
                DurationMs = Math.Max(0, call?.DurationMs ?? 0),
                Name = "executor",
                Count = (call?.InputTokens ?? 0) + (call?.OutputTokens ?? 0),
            });
    }

    private static int? Sum(int? left, int? right) => left is null && right is null ? null : (left ?? 0) + (right ?? 0);

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
        var redacted = stepEvent.Detail is null ? stepEvent : stepEvent with { Detail = TextRules.Bound(RedactText(stepEvent.Detail), MaxEventDetailChars) };
        lock (_gate)
        {
            if (step.Events.Count >= _maxEventsPerStep)
            {
                return;
            }

            step.Events.Add(redacted);
        }

        Publish(step, new StepProgress { Phase = StepProgressPhase.Event, Api = step.Api, Event = redacted });
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

    private bool IsAbandoned(StepRecord record)
    {
        lock (_gate)
        {
            return _abandoned.Contains(record.Id);
        }
    }

    private void PublishEnd(StepRecord record) =>
        Publish(record, new StepProgress
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
        });

    // Publishes a phase with the owning record's identity, including nested and finishing steps.
    private void Publish(StepRecord record, StepProgress progress) =>
        _onProgress?.Invoke(progress with { Identity = new StepProgressIdentity(_attempt.Id, _attempt.Index, record.Id, record.Index) });

    private static string Quoted(string label) =>
        "\"" + (label.Length > MaxQuotedLabelChars ? label[..(MaxQuotedLabelChars - 3)] + "..." : label) + "\"";

    // A task nobody awaits must not fault unobserved.
    private static void Observe(Task task) =>
        _ = task.ContinueWith(static finished => _ = finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
        }
        while (value > seen && Interlocked.CompareExchange(ref target, value, seen) != seen);
    }
}
