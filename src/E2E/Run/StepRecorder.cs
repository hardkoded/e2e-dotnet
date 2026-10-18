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
/// own events and artifacts. Not ported yet: the app log and the screen comparison, which come with the trace feed.
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
    }

    /// <summary>The hook steps run in from now on; null while the test body runs.</summary>
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
        var source = _projectRoot is null ? null : Source.Locate(new StackTrace(1, fNeedFileInfo: true), _projectRoot);
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
                Phase = Phase,
            };
            _steps.Add(record);
            _running.Add(record.Id);
        }

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
            var result = await CutAsync(body(), record).ConfigureAwait(false);
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
            }

            if (!wasAbandoned)
            {
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
        if (details.Explanation is not null)
        {
            current.Explanation = RedactText(details.Explanation);
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
