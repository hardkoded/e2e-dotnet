// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>What a session offers the attempt it joins. Upstream's <c>GuestSession</c> (<c>run/guest-steps.ts</c>).</summary>
internal interface IGuestSession
{
    /// <summary>The directory the session's step sources are relative to.</summary>
    string Root { get; }

    /// <summary>Whether a step of the session is still running.</summary>
    bool Busy();

    /// <summary>Fails the steps the test body left running, as the session's recorder fails its own, and returns the error that names them.</summary>
    Exception? AbandonRunning();

    /// <summary>Closes the session: cancels what runs on it, and hands over the video it recorded.</summary>
    Task CloseAsync();
}

/// <summary>What a host a test launched offers the attempt that takes charge of it. Upstream's <c>GuestHost</c> (<c>run/guest-steps.ts</c>).</summary>
internal interface IGuestHost
{
    Task CloseAsync();

    /// <summary>Stops sharing the host's secrets. The attempt calls it at its own end, not at the host's: a test may close the host early and go on reading screens that show them.</summary>
    void Release();
}

/// <summary>Where a joined session's steps and videos go. Upstream's <c>GuestSteps</c> (<c>run/guest-steps.ts</c>).</summary>
internal interface IGuestSteps
{
    /// <summary>Takes one progress notification of the session's own recorder, with its record of the step.</summary>
    void Progress(StepProgress progress, StepRecord record);

    /// <summary>
    /// Takes a video the session recorded. Completes once the attempt holds its own copy of a file: the session's host removes the original when it
    /// closes.
    /// </summary>
    Task VideoAsync(SessionVideo recording);
}

/// <summary>
/// A place in an attempt, reserved the moment a test starts opening a host or a session, so what is opened is that test's however long the opening
/// takes, and sessions are named in the order the test opened them. Upstream's <c>GuestSeat</c> (<c>run/guest-steps.ts</c>).
/// </summary>
/// <typeparam name="TOpened">What the opening produces.</typeparam>
/// <typeparam name="TTaken">What the attempt hands back when it takes it; the default value when the attempt gave the seat up.</typeparam>
internal interface IGuestSeat<in TOpened, out TTaken>
{
    /// <summary>Cancelled when the attempt will no longer take what is being opened: the opening is cancelled with it.</summary>
    CancellationToken Signal { get; }

    /// <summary>
    /// Hands over what was opened: the attempt owns its end from here, and closes it if the test does not. Returns the default value when the
    /// attempt gave the seat up meanwhile, and what was opened is the caller's to close.
    /// </summary>
    TTaken? Take(TOpened opened);
}

/// <summary>What an attempt offers the hosts and sessions a test opens beside its own with <see cref="Launch"/>. Upstream's <c>AttemptGuests</c> (<c>run/guest-steps.ts</c>).</summary>
internal interface IAttemptGuests
{
    /// <summary>Whether the attempt keeps a video of each session opened in it, so one opened now records.</summary>
    bool Video { get; }

    /// <summary>Reserves a seat for a host the test is launching. Taking it returns <c>true</c>.</summary>
    IGuestSeat<IGuestHost, bool> Host();

    /// <summary>Reserves a seat, and the next name, for a session the test is opening.</summary>
    IGuestSeat<IGuestSession, IGuestSteps> Session();

    /// <summary>
    /// Says an opening failed because the attempt gave up its seat: the error is the attempt's doing and already told, so a caller that never awaited the
    /// opening must not meet it as a rejection nobody handled.
    /// </summary>
    void GaveUp(Exception error);

    /// <summary>
    /// Takes in a session opened outside every test, for the steps it runs during this attempt. Nothing else of it is the attempt's: it is neither closed
    /// nor recorded on video, and a step it still runs when the attempt ends is told as cancelled and left running.
    /// </summary>
    IGuestSteps Visit(string root);
}

/// <summary>
/// How a session's step went, as the test's record of it takes it: what becomes of each field of a session's step record in the test's record of the
/// step. Upstream's <c>GuestResult</c> and its <c>FIELDS</c> table (<c>run/guest-steps.ts</c>):
/// <list type="bullet">
/// <item><description>own: the test's: its place in the test's record (<c>Id</c>, <c>Index</c>, <c>Actor</c>, <c>StartedAt</c>, <c>Phase</c>).</description></item>
/// <item><description>start: told when the step starts (<c>Kind</c>, <c>Api</c>, <c>Label</c>, <c>Source</c>).</description></item>
/// <item><description>told: gathered as the step runs (<c>Events</c>).</description></item>
/// <item><description>taken: copied when the step ends, the members below.</description></item>
/// <item><description>dropped: counts in the session's own step indexes or artifact ids (<c>Screen</c>, <c>Artifacts</c>, <c>CacheWrite</c>, <c>Usage</c>).</description></item>
/// </list>
/// </summary>
internal sealed class GuestResult(StepStatus status, long durationMs)
{
    public StepStatus Status { get; } = status;

    public long DurationMs { get; } = durationMs;

    public string? ObservationRevision { get; init; }

    public bool? ObservationTruncated { get; init; }

    public string? Explanation { get; init; }

    public bool? VisionInput { get; init; }

    public VisionDegradation? VisionDegraded { get; init; }

    public StepViewport? Viewport { get; init; }

    public StepMetrics? Metrics { get; init; }

    public CacheInfo? Cache { get; init; }

    public IReadOnlyList<StepTurn>? Turns { get; init; }

    public StepModelInfo? Model { get; init; }

    public string? Agent { get; init; }

    public SerializedError? Error { get; init; }

    /// <summary>Sets what the session took of the step on the test's record of it.</summary>
    public void ApplyTo(StepRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.Status = Status;
        record.DurationMs = DurationMs;
        record.ObservationRevision = ObservationRevision;
        record.ObservationTruncated = ObservationTruncated;
        record.Explanation = Explanation;
        record.VisionInput = VisionInput;
        record.VisionDegraded = VisionDegraded;
        record.Viewport = Viewport;
        record.Metrics = Metrics;
        record.Cache = Cache;
        record.Turns = Turns;
        record.Model = Model;
        record.Agent = Agent;
        record.Error = Error;
    }
}

/// <summary>A step of the session as the test's recorder lets it be told, once filed there (<see cref="StepRecorder.Guest"/>). Upstream's <c>GuestRecord</c>.</summary>
/// <param name="Tell">Tells of the step's progress; an event is kept on the step's record.</param>
/// <param name="End">Ends the step with how it went.</param>
internal sealed record GuestRecord(Action<StepProgress> Tell, Action<GuestResult> End);

/// <summary>The mirror of one session's steps in a test's record. Upstream's <c>GuestMirror</c>.</summary>
internal sealed class GuestMirror
{
    private readonly Action<StepProgress, StepRecord> _progress;
    private readonly Action _cut;

    internal GuestMirror(Action<StepProgress, StepRecord> progress, Action cut)
    {
        _progress = progress;
        _cut = cut;
    }

    /// <summary>Takes one progress notification of the session's own recorder, with its record of the step.</summary>
    public void Progress(StepProgress progress, StepRecord record) => _progress(progress, record);

    /// <summary>Ends the mirror: a step still running is told as cancelled, and what the session reports next is dropped.</summary>
    public void Cut() => _cut();
}

/// <summary>
/// The steps of another session a test drives beside its own, kept in the test's own record: a <see cref="Launch"/> host's session tells the attempt
/// running around it of each step it takes, the way the test's own steps are told. So the report, the live output, and the model usage of the test
/// cover every session it drove, each step under its session's name. Upstream's <c>run/guest-steps.ts</c>.
/// </summary>
internal static class GuestSteps
{
    /// <summary>
    /// Mirrors the steps of a session whose sources are relative to <paramref name="root"/> into <paramref name="steps"/>, under <paramref name="actor"/>.
    /// The prose the session reports (a label, an event, an explanation, a model turn, a replay's detail, an error) is redacted again by the test's
    /// recorder on the way in: the session may outlive the test's own, and then need not redact what the test's does. Upstream's <c>mirrorGuestSteps</c>.
    /// </summary>
    public static GuestMirror MirrorGuestSteps(StepRecorder steps, string actor, string root)
    {
        ArgumentNullException.ThrowIfNull(steps);
        // The session's running steps, by the id its own recorder gave them, each with when it started here.
        var running = new Dictionary<string, (GuestRecord Filed, long StartedMs)>(StringComparer.Ordinal);
        var gate = new Lock();
        var cut = false;

        string Redact(string text) => steps.RedactText(text);

        // A line of the session's, read against the test's project root; nothing when it lies outside it.
        SourceLocation? Relocated(SourceLocation? source)
        {
            if (source is null || steps.ProjectRoot is null)
            {
                return null;
            }

            var file = Source.ProjectRelativePath(steps.ProjectRoot, Path.GetFullPath(Path.Combine(root, source.File)));
            return file is null ? null : source with { File = file };
        }

        GuestResult ResultOf(StepRecord theirs)
        {
            var error = theirs.Error;
            if (error is not null)
            {
                var redacted = ErrorRedaction.RedactSerializedError(error, Redact);
                error = redacted with { Source = Relocated(error.Source) };
            }

            return new GuestResult(theirs.Status, theirs.DurationMs)
            {
                ObservationRevision = theirs.ObservationRevision,
                ObservationTruncated = theirs.ObservationTruncated,
                Explanation = theirs.Explanation is { } explanation ? Redact(explanation) : null,
                VisionInput = theirs.VisionInput,
                VisionDegraded = theirs.VisionDegraded,
                Viewport = theirs.Viewport,
                Metrics = theirs.Metrics,
                Cache = theirs.Cache is { } cache ? Redacted(cache, Redact) : null,
                Turns = theirs
                    .Turns?.Select(turn => new StepTurn(
                        turn.Index,
                        [.. turn.Calls.Select(Redact)],
                        Redact(turn.Outcome)
                    ))
                    .ToList(),
                Model = theirs.Model,
                Agent = theirs.Agent,
                Error = error,
            };
        }

        return new GuestMirror(
            (progress, record) =>
            {
                if (progress.Phase == StepProgressPhase.Start)
                {
                    lock (gate)
                    {
                        if (cut)
                        {
                            return;
                        }

                        var filed = steps.Guest(
                            progress.Kind ?? record.Kind,
                            progress.Api,
                            progress.Label ?? record.Label,
                            actor,
                            Relocated(record.Source)
                        );
                        running[record.Id] = (filed, Environment.TickCount64);
                    }

                    return;
                }

                GuestRecord? step;
                lock (gate)
                {
                    running.TryGetValue(record.Id, out var found);
                    step = found.Filed;
                    if (step is not null && progress.Phase == StepProgressPhase.End)
                    {
                        running.Remove(record.Id);
                    }
                }

                if (step is null)
                {
                    return;
                }

                switch (progress.Phase)
                {
                    case StepProgressPhase.Event:
                        // The recorder redacts an event's detail as it keeps it; its reasoning is the other prose it carries.
                        var stepEvent = progress.Event!;
                        step.Tell(
                            progress with
                            {
                                Identity = null,
                                Event = stepEvent.Reasoning is { } reasoning
                                    ? stepEvent with
                                    {
                                        Reasoning = Redact(reasoning),
                                    }
                                    : stepEvent,
                            }
                        );
                        break;
                    case StepProgressPhase.End:
                        step.End(ResultOf(record));
                        break;
                    default:
                        step.Tell(progress with { Identity = null });
                        break;
                }
            },
            () =>
            {
                List<(GuestRecord Filed, long StartedMs)> open;
                lock (gate)
                {
                    cut = true;
                    open = [.. running.Values];
                    running.Clear();
                }

                foreach (var (filed, startedMs) in open)
                {
                    filed.End(new GuestResult(StepStatus.Cancelled, Environment.TickCount64 - startedMs));
                }
            }
        );
    }

    private static CacheInfo Redacted(CacheInfo cache, Func<string, string> redact) =>
        new()
        {
            Mode = cache.Mode,
            Reason = cache.Reason,
            Derived = cache.Derived,
            ReplayedActions = cache.ReplayedActions,
            TotalActions = cache.TotalActions,
            Detail = cache.Detail is { } detail ? redact(detail) : null,
            NotRecorded = cache.NotRecorded,
            Entry = cache.Entry,
            RecordedBy = cache.RecordedBy,
        };
}
