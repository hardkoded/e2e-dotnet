// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;

namespace E2E;

/// <summary>What <see cref="AttemptGuestSessions"/> is given. Upstream's <c>AttemptGuestOptions</c> (<c>run/guests.ts</c>).</summary>
internal sealed record AttemptGuestOptions
{
    /// <summary>The attempt's own video rule, null when it records none: a session's video is kept, or dropped, by the same rule.</summary>
    public Keep? Keep { get; init; }

    /// <summary>Goes before a video's file name. A serial group's members share one artifact directory and name their sessions alike, so each passes its own.</summary>
    public string VideoPrefix { get; init; } = "";

    /// <summary>How long closing one host or session may take, every phase of its own cleanup included.</summary>
    public TimeSpan CleanupTimeout { get; init; }
}

/// <summary>
/// The hosts and sessions a test opened beside its own with <see cref="Launch"/>, as one attempt keeps them: their steps in its record, each under the
/// session's name, the videos they recorded among its artifacts, and their end. What the test opened and left open is closed with it; a session opened
/// outside every test only has its steps recorded. Upstream's <c>AttemptGuestSessions</c> (<c>run/guests.ts</c>).
/// </summary>
internal sealed class AttemptGuestSessions
{
    // Where an attempt's videos go, beside its own video; a report path, so POSIX.
    private const string VideoDir = "video";

    private readonly StepRecorder _steps;
    private readonly Func<IArtifactSink?> _sinkOf;
    private readonly AttemptGuestOptions _options;
    private readonly Lock _gate = new();

    // The sessions the test opened, which the attempt closes.
    private readonly List<IGuestSession> _sessions = [];

    // The hosts the test launched, which the attempt closes.
    private readonly List<IGuestHost> _hosts = [];
    private readonly List<GuestMirror> _mirrors = [];

    // The openings in flight: seats reserved and not taken yet, by what cancels each, with the call it is.
    private readonly Dictionary<CancellationTokenSource, string> _opening = [];

    // Each close this attempt asked for, by what it closes: asked again, it is the same wait, not a second budget.
    private readonly Dictionary<object, Task> _closing = new(ReferenceEqualityComparer.Instance);
    private readonly List<HeldVideo> _videos = [];

    // Copies still being written; Settle waits for them, so none outlives it unregistered.
    private readonly HashSet<Task> _copying = [];
    private readonly List<Exception> _failures = [];

    // Sessions named so far: launch-1, launch-2, in the order they joined.
    private int _named;

    // Whether the attempt still takes hosts and sessions; false from the start of Settle.
    private bool _taking = true;

    // Whether a session may still hand over a video: until the ones Settle closes have.
    private bool _holding = true;

    /// <summary>Takes where the attempt registers what it keeps as a function, since the port makes the attempt's artifact directory on first use.</summary>
    public AttemptGuestSessions(StepRecorder steps, Func<IArtifactSink?> sinkOf, AttemptGuestOptions options)
    {
        _steps = steps;
        _sinkOf = sinkOf;
        _options = options;
        Offer = new Offered(this);
    }

    /// <summary>What the attempt publishes for <see cref="Launch"/> to find.</summary>
    public IAttemptGuests Offer { get; }

    /// <summary>
    /// Fails the steps the body left running on the sessions it opened and closes those sessions, which cancels the steps: teardown starts with none of
    /// them running, as it does for the test's own. Returns the errors naming them.
    /// </summary>
    public async Task<IReadOnlyList<Exception>> AbandonRunningAsync()
    {
        List<IGuestSession> sessions;
        List<string> calls;
        lock (_gate)
        {
            sessions = [.. _sessions];
            calls = [.. _opening.Values];
        }

        var errors = new List<Exception>();
        foreach (var session in sessions)
        {
            if (session.AbandonRunning() is { } error)
            {
                errors.Add(error);
            }
        }

        // An opening still in flight was not awaited either; Quiesce cancels it, and its rejection is expected.
        errors.AddRange(
            calls.Select(call => new TestException(
                "STEP_NOT_AWAITED",
                TextRules.WithHint($"the test body returned before {call} finished", "put `await` in front of it")
            ))
        );
        await QuiesceAsync().ConfigureAwait(false);
        return errors;
    }

    /// <summary>
    /// Stops what the body left in flight once it is over, however it ended: an opening still under way is cancelled, and a session the test opened with a
    /// step still running is closed, which cuts the step. A timeout or an interrupt cuts the test's own steps at once, and a session's must not go on
    /// driving the app through teardown.
    /// </summary>
    public async Task QuiesceAsync()
    {
        CancelOpenings();
        List<IGuestSession> busy;
        lock (_gate)
        {
            busy = [.. _sessions.Where(session => session.Busy())];
        }

        await Task.WhenAll(busy.Select(CloseAsync)).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the attempt's part in what the test opened: closes its sessions and hosts, then keeps or drops the videos they handed over by how the attempt
    /// ended. Returns what closing found. Call it once the status is set and before the artifacts settle.
    /// </summary>
    public async Task<IReadOnlyList<Exception>> SettleAsync(AttemptStatus status)
    {
        List<IGuestSession> sessions;
        lock (_gate)
        {
            _taking = false;
            sessions = [.. _sessions];
        }

        CancelOpenings();
        await Task.WhenAll(sessions.Select(CloseAsync)).ConfigureAwait(false);

        // A host closes its open session first, so that session's video is handed over before the copies are waited for.
        List<IGuestHost> hosts;
        lock (_gate)
        {
            hosts = [.. _hosts];
        }

        await Task.WhenAll(hosts.Select(CloseAsync)).ConfigureAwait(false);
        List<Task> copying;
        lock (_gate)
        {
            _holding = false;
            copying = [.. _copying];
        }

        await Task.WhenAll(copying).ConfigureAwait(false);
        List<GuestMirror> mirrors;
        List<HeldVideo> videos;
        lock (_gate)
        {
            mirrors = [.. _mirrors];
            videos = [.. _videos];
            _videos.Clear();
        }

        foreach (var mirror in mirrors)
        {
            mirror.Cut();
        }

        var kept = _options.Keep is { } keep && RecordingModes.Keeps(keep, status);
        var sink = videos.Count == 0 ? null : _sinkOf();
        foreach (var video in videos)
        {
            if (video.Url is not null)
            {
                if (kept)
                {
                    sink?.Link(
                        video.Url,
                        new ArtifactLink(video.MediaType!, video.StartedAt ?? DateTimeOffset.UtcNow, video.Actor)
                    );
                }
            }
            else if (kept)
            {
                sink?.Register(
                    E2E.Reports.ArtifactKind.Video,
                    video.File!,
                    new ArtifactRegistration { Actor = video.Actor, StartedAt = video.StartedAt }
                );
            }
            else
            {
                if (sink is not null)
                {
                    File.Delete(Path.Combine(sink.Dir, video.File!));
                }
            }
        }

        lock (_gate)
        {
            return [.. _failures];
        }
    }

    /// <summary>
    /// Stops sharing the secrets of the hosts the test launched. Call it last, once the artifacts have settled: a download still being rewritten is
    /// redacted with them.
    /// </summary>
    public void Release()
    {
        List<IGuestHost> hosts;
        lock (_gate)
        {
            hosts = [.. _hosts];
        }

        foreach (var host in hosts)
        {
            host.Release();
        }
    }

    private void CancelOpenings()
    {
        List<CancellationTokenSource> openings;
        lock (_gate)
        {
            openings = [.. _opening.Keys];
        }

        foreach (var opening in openings)
        {
            opening.Cancel();
        }
    }

    private string Name() => $"launch-{Interlocked.Increment(ref _named).ToString(CultureInfo.InvariantCulture)}";

    // A seat whose opening is cancelled when the body ends or the attempt settles before it is taken.
    private Seat<TOpened, TTaken> SeatFor<TOpened, TTaken>(string call, Func<TOpened, TTaken> take)
    {
        var opening = new CancellationTokenSource();
        lock (_gate)
        {
            if (_taking)
            {
                _opening[opening] = call;
            }
            else
            {
                opening.Cancel();
            }
        }

        return new Seat<TOpened, TTaken>(opening.Token, opened =>
        {
            bool taking;
            lock (_gate)
            {
                _opening.Remove(opening);
                taking = _taking && !opening.IsCancellationRequested;
            }

            return taking ? take(opened) : default;
        });
    }

    private IGuestSteps MirrorOf(string actor, string root)
    {
        var mirror = GuestSteps.MirrorGuestSteps(_steps, actor, root);
        lock (_gate)
        {
            _mirrors.Add(mirror);
        }

        return new Steps(mirror, recording => HoldAsync(actor, recording));
    }

    // Closes a host or session within the cleanup budget, once; a failure is the attempt's cleanup failure, not a throw.
    private Task CloseAsync(object what)
    {
        TaskCompletionSource done;
        lock (_gate)
        {
            if (_closing.TryGetValue(what, out var existing))
            {
                return existing;
            }

            done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _closing[what] = done.Task;
        }

        _ = CloseOnceAsync(what, done);
        return done.Task;
    }

    private async Task CloseOnceAsync(object what, TaskCompletionSource done)
    {
        try
        {
            var closing = what is IGuestSession session ? session.CloseAsync() : ((IGuestHost)what).CloseAsync();

            // A close the timeout gives up on must not fault unobserved.
            _ = closing.ContinueWith(
                static finished => _ = finished.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            await Time.WithTimeout(
                    Wait(closing),
                    _options.CleanupTimeout > TimeSpan.Zero ? _options.CleanupTimeout : Timeout.InfiniteTimeSpan,
                    () =>
                        new EngineException(
                            "CLEANUP_TIMEOUT",
                            "a host or session the test opened with launch() did not close"
                        )
                )
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A close that fails is the attempt's cleanup failure, not a throw.
        catch (Exception cause)
#pragma warning restore CA1031
        {
            lock (_gate)
            {
                _failures.Add(cause);
            }
        }
        finally
        {
            done.SetResult();
        }
    }

    private static async Task<bool> Wait(Task work)
    {
        await work.ConfigureAwait(false);
        return true;
    }

    // Takes a video a session recorded. A file is copied under the attempt's artifacts first: its host deletes the original when it closes.
    private Task HoldAsync(string actor, SessionVideo recording)
    {
        if (_options.Keep is null || _sinkOf() is not { } sink)
        {
            return Task.CompletedTask;
        }

        string name;
        string file;
        Task copying;
        lock (_gate)
        {
            if (!_holding)
            {
                return Task.CompletedTask;
            }

            if (recording is SessionVideoLink link)
            {
                _videos.Add(new HeldVideo(actor, null, link.Url, link.MediaType, link.StartedAt));
                return Task.CompletedTask;
            }

            var source = (SessionVideoFile)recording;
            var earlier = _videos.Count(video => video.Actor == actor && video.File is not null);
            name =
                _options.VideoPrefix
                + actor
                + (earlier == 0 ? "" : $"-part{(earlier + 1).ToString(CultureInfo.InvariantCulture)}")
                + Path.GetExtension(source.Path);
            file = VideoDir + "/" + name;

            // Counted before the copy starts, so a second part of the same session takes the next name.
            _videos.Add(new HeldVideo(actor, file, null, null, source.StartedAt));
            copying = CopyAsync(sink.Dir, source.Path, name, file);
            _copying.Add(copying);
        }

        return copying;
    }

    private async Task CopyAsync(string dir, string from, string name, string file)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, VideoDir));
            await using var source = new FileStream(
                from,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous
            );
            await using var target = new FileStream(
                Path.Combine(dir, VideoDir, name),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous
            );
            await source.CopyToAsync(target).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A copy that fails is the attempt's cleanup failure, and its video is dropped.
        catch (Exception cause)
#pragma warning restore CA1031
        {
            lock (_gate)
            {
                _failures.Add(cause);
                _videos.RemoveAll(video => video.File == file);
            }
        }
    }

    // A video a session handed over: the attempt's own copy of a file, or a hosted link.
    private sealed record HeldVideo(
        string Actor,
        string? File,
        string? Url,
        string? MediaType,
        DateTimeOffset? StartedAt
    );

    private sealed class Seat<TOpened, TTaken>(CancellationToken signal, Func<TOpened, TTaken?> take)
        : IGuestSeat<TOpened, TTaken>
    {
        public CancellationToken Signal { get; } = signal;

        public TTaken? Take(TOpened opened) => take(opened);
    }

    private sealed class Steps(GuestMirror mirror, Func<SessionVideo, Task> video) : IGuestSteps
    {
        public void Progress(StepProgress progress, StepRecord record) => mirror.Progress(progress, record);

        public Task VideoAsync(SessionVideo recording) => video(recording);
    }

    private sealed class Offered(AttemptGuestSessions owner) : IAttemptGuests
    {
        public bool Video => owner._options.Keep is not null;

        public IGuestSeat<IGuestHost, bool> Host() => owner.SeatFor<IGuestHost, bool>("launch()", host =>
            {
                lock (owner._gate)
                {
                    owner._hosts.Add(host);
                }

                return true;
            });

        public IGuestSeat<IGuestSession, IGuestSteps> Session()
        {
            var actor = owner.Name();
            return owner.SeatFor<IGuestSession, IGuestSteps>("host.NewSessionAsync()", session =>
            {
                lock (owner._gate)
                {
                    owner._sessions.Add(session);
                }

                return owner.MirrorOf(actor, session.Root);
            });
        }

        public IGuestSteps Visit(string root) => owner.MirrorOf(owner.Name(), root);

        public void GaveUp(Exception error) => AbandonedWork.MarkRejection(error);
    }
}
