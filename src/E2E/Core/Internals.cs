// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// What <see cref="CoreAgents.CreateAgent"/> reads off a session it did not create: the attempt behind it, the host's project root, and the
/// session's close hooks. Upstream's <c>SessionInternals</c> (<c>core/internals.ts</c>), which keeps it in a <c>WeakMap</c> so that a session
/// object carries none of it; the port's <see cref="Session"/> holds it in an internal member.
/// </summary>
internal sealed class SessionInternals(StandaloneAttempt attempt, string projectRoot, FailureWatch failures)
{
    public StandaloneAttempt Attempt { get; } = attempt;

    /// <summary>The directory the host was launched from, which a relative cache directory resolves against. Upstream's <c>config.projectRoot</c>.</summary>
    public string ProjectRoot { get; } = projectRoot;

    /// <summary>Run in order when the session closes, before its attempt ends: an agent settles its replay cache here.</summary>
    public List<Func<Task>> Closers { get; } = [];

    /// <summary>How the session's steps went, as the replay cache settles what its agents recorded.</summary>
    public AttemptSettlement Settlement() => failures.Settlement(Attempt.Session.Recorder);
}

/// <summary>
/// Watches a session's steps for the agent steps that failed. A test ends at its failure; a program may catch one and go on. So each failure
/// settles only the recordings it found unconfirmed, as a run's failure does: dropped, unless only a missing model explains it. Recordings made
/// after it are confirmed by the checks that pass after them. Upstream's <c>FailureWatch</c> (<c>core/internals.ts</c>).
/// </summary>
internal sealed class FailureWatch
{
    private readonly List<SurvivedFailure> _failures = [];
    private StepRecorder? _steps;

    /// <summary>The <c>onProgress</c> hook the session's attempt reports its steps to.</summary>
    public void OnProgress(StepProgress progress)
    {
        // Only an agent step's failure implicates recordings: a program catches a locator's timeout on purpose, to branch.
        if (
            progress.Phase != StepProgressPhase.End
            || progress.Kind != StepKind.Agent
            || progress.Status is null or StepStatus.Passed
            || _steps is null
        )
        {
            return;
        }

        var code = progress.Error?.Code;
        lock (_failures)
        {
            _failures.Add(
                new SurvivedFailure(
                    _steps.LastVerifiedStepIndex,
                    progress.Identity?.StepIndex ?? int.MaxValue,
                    code is null || !AgentError.ModelUnreachableCodes.Contains(code)
                )
            );
        }
    }

    public void Watch(StepRecorder steps) => _steps = steps;

    /// <summary>
    /// How the session's recordings settle when it closes. A close is not a verdict: a program may end, or be interrupted, between a replay and
    /// the check after it, so what no check confirmed is left as it was, neither written nor evicted.
    /// </summary>
    public AttemptSettlement Settlement(StepRecorder steps)
    {
        lock (_failures)
        {
            return new AttemptSettlement(steps.LastVerifiedStepIndex, false, [.. _failures]);
        }
    }
}

/// <summary>
/// A session's part in the tests around it (<see cref="GuestSteps"/>): it tells a test of each step it takes, so the test's report covers them. Which
/// test is decided once, when the session starts opening; see <see cref="SessionGuests.SessionGuest"/>. Upstream's <c>SessionGuest</c> (<c>core/internals.ts</c>).
/// </summary>
internal interface ISessionGuest
{
    /// <summary>Whether the session records a video for the test it is part of.</summary>
    bool RecordsVideo { get; }

    /// <summary>Cancelled when the test the session is opening for gives it up; a token that never cancels for a session no test owns.</summary>
    CancellationToken Signal { get; }

    /// <summary>Takes the error the opening failed with, which the test that gave it up has told already.</summary>
    void Failed(Exception error);

    /// <summary>Takes each progress of the session's recorder with its record of the step.</summary>
    void Mirror(StepProgress progress, StepRecord record);

    /// <summary>Hands over the open session. False when the test it was opening for gave it up: it is the caller's to close.</summary>
    bool Attach(IGuestSession session);

    /// <summary>Hands on the videos the session recorded.</summary>
    Task VideosAsync(IReadOnlyList<SessionVideo> recordings);
}

/// <summary>What <see cref="Launch"/> asks of the test around it. Upstream's <c>currentGuests</c> and <c>sessionGuest</c> (<c>core/internals.ts</c>).</summary>
internal static class SessionGuests
{
    /// <summary>
    /// What the test running now offers the caller, when the caller is part of it. Upstream also offers nothing to a body past its timeout and to a worker
    /// fixture's setup, which no test ends (<c>outlivesTest()</c> and <c>PollScope.over</c>): the port has neither a poll scope nor a worker fixture, so
    /// there is no such work to tell apart yet.
    /// </summary>
    public static IAttemptGuests? CurrentGuests() => Attempts.CurrentAttempt()?.Guests;

    /// <summary>
    /// The guest of a session being opened now. Opened by a running test, it is that test's for good: named now, recorded on video if the test keeps
    /// videos, and closed with the test. Opened outside every test, on a host from a hook or the top of a file, it visits whichever test is running
    /// when it starts a step, and a step outside every test is told to nobody.
    /// </summary>
    public static ISessionGuest SessionGuest() =>
        CurrentGuests() is { } opener ? new OwnedGuest(opener) : new VisitingGuest();

    private sealed class OwnedGuest : ISessionGuest
    {
        private readonly IAttemptGuests _opener;
        private readonly IGuestSeat<IGuestSession, IGuestSteps> _seat;
        private IGuestSteps? _steps;

        public OwnedGuest(IAttemptGuests opener)
        {
            _opener = opener;
            _seat = opener.Session();
        }

        public bool RecordsVideo => _opener.Video;

        public CancellationToken Signal => _seat.Signal;

        public void Failed(Exception error)
        {
            if (_seat.Signal.IsCancellationRequested)
            {
                _opener.GaveUp(error);
            }
        }

        public void Mirror(StepProgress progress, StepRecord record) =>
            Volatile.Read(ref _steps)?.Progress(progress, record);

        public bool Attach(IGuestSession session)
        {
            var steps = _seat.Take(session);
            Volatile.Write(ref _steps, steps);
            return steps is not null;
        }

        public async Task VideosAsync(IReadOnlyList<SessionVideo> recordings)
        {
            foreach (var recording in recordings)
            {
                if (Volatile.Read(ref _steps) is { } steps)
                {
                    await steps.VideoAsync(recording).ConfigureAwait(false);
                }
            }
        }
    }

    private sealed class VisitingGuest : ISessionGuest
    {
        private readonly Lock _gate = new();
        private IGuestSession? _session;

        // The test the session last started a step in, and where that test takes its steps.
        private (IAttemptGuests? Guests, IGuestSteps? Steps) _visited;

        public bool RecordsVideo => false;

        public CancellationToken Signal => CancellationToken.None;

        public void Failed(Exception error) { }

        public void Mirror(StepProgress progress, StepRecord record)
        {
            IGuestSteps? steps;
            lock (_gate)
            {
                if (progress.Phase == StepProgressPhase.Start)
                {
                    var guests = CurrentGuests();
                    if (!ReferenceEquals(guests, _visited.Guests))
                    {
                        _visited = (guests, _session is null ? null : guests?.Visit(_session.Root));
                    }
                }

                steps = _visited.Steps;
            }

            steps?.Progress(progress, record);
        }

        public bool Attach(IGuestSession session)
        {
            lock (_gate)
            {
                _session = session;
            }

            return true;
        }

        public Task VideosAsync(IReadOnlyList<SessionVideo> recordings) => Task.CompletedTask;
    }
}
