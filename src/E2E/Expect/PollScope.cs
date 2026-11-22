// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The polls one phase started: a test body with its fixtures and <c>BeforeEach</c> hooks. A poll is not a step and records nothing; what the
/// scope adds is that a poll the phase did not await ends with the phase instead of failing whatever runs when it times out. Upstream's
/// <c>PollScope</c> (<c>expect/poll-scope.ts</c>).
/// </summary>
internal sealed class PollScope(string owner)
{
    private readonly Lock _gate = new();
    private readonly List<RunningPoll> _running = [];
    private bool _closed;

    /// <summary>Whether the phase has ended: what its work still does then is no test's, whichever one runs now. Upstream's <c>over</c>.</summary>
    public bool Over
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>
    /// Runs one poll, owned by this scope until it settles. <paramref name="work"/> receives the token that cancels it when the scope closes first.
    /// A poll started after the scope closed, by work its phase left running (a body past its timeout), is cancelled before its first read: it
    /// belongs to no phase that could still fail, and must not fail the one running now. Upstream's <c>track</c>.
    /// </summary>
    public Task<T> Track<T>(string label, StackTrace? stack, Func<CancellationToken, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var poll = new RunningPoll(label, stack);
        lock (_gate)
        {
            if (_closed)
            {
                poll.Cancel();
            }
            else
            {
                _running.Add(poll);
            }
        }

        var promise = RunAsync(poll, work);
        poll.Promise = promise;
        if (poll.Abandoned)
        {
            Observe(promise);
        }

        return promise;
    }

    /// <summary>
    /// Ends the scope when its phase has returned: every poll still running is cancelled and its later rejection observed. Returns the error naming
    /// them, at the line of the first, or null when every poll had settled. A caller whose phase was cut short (a timeout, a throw) may drop the
    /// error: the phase may have been awaiting the poll. Closing twice returns null the second time. Upstream's <c>close</c>.
    /// </summary>
    public TestException? Close()
    {
        RunningPoll[] polls;
        lock (_gate)
        {
            _closed = true;
            polls = [.. _running];
            _running.Clear();
        }

        if (polls.Length == 0)
        {
            return null;
        }

        foreach (var poll in polls)
        {
            poll.Cancel();
            if (poll.Promise is { } promise)
            {
                Observe(promise);
            }
        }

        var first = polls[0];
        var more =
            polls.Length > 1
                ? " and " + (polls.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " more"
                : "";
        var error = new TestException(
            "STEP_NOT_AWAITED",
            TextRules.WithHint(
                $"{owner} returned before {first.Label}{more} finished",
                "put `await` in front of every expect.poll matcher and toPass call"
            )
        );
        AbandonedWork.RelocateStack(error, first.Stack);
        return error;
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

    private async Task<T> RunAsync<T>(RunningPoll poll, Func<CancellationToken, Task<T>> work)
    {
        try
        {
            return await work(poll.Token).ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            if (poll.Abandoned)
            {
                AbandonedWork.MarkRejection(cause);
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(poll);
            }
        }
    }

    private sealed class RunningPoll(string label, StackTrace? stack)
    {
        private readonly CancellationTokenSource _cancel = new();

        public string Label { get; } = label;

        public StackTrace? Stack { get; } = stack;

        public Task? Promise { get; set; }

        public volatile bool Abandoned;

        public CancellationToken Token => _cancel.Token;

        public void Cancel()
        {
            Abandoned = true;
            _cancel.Cancel();
        }
    }
}

/// <summary>
/// The marker of an <c>expect.poll</c> read, upstream's <c>pollRead</c> and <c>insidePollRead</c> (<c>expect/poll-scope.ts</c>), and the scope a
/// poll started in this flow belongs to (<c>runInPollScope</c>, <c>currentPollScope</c>). The marker flows
/// with the async call of the read, so every step the read calls sees it. <c>toPass</c>, the other block the marker covers, has no counterpart yet.
/// </summary>
internal static class PollScopes
{
    private static readonly AsyncLocal<bool> Reading = new();
    private static readonly AsyncLocal<PollScope?> Scope = new();

    /// <summary>The scope a poll started here belongs to, or null outside any phase (upstream's <c>currentPollScope</c>).</summary>
    public static PollScope? CurrentPollScope() => Scope.Value;

    /// <summary>Runs <paramref name="run"/> with <paramref name="scope"/> owning every poll it starts, however long that work outlives the phase (upstream's <c>runInPollScope</c>).</summary>
    public static T RunInPollScope<T>(PollScope scope, Func<T> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var before = Scope.Value;
        Scope.Value = scope;
        try
        {
            return run();
        }
        finally
        {
            Scope.Value = before;
        }
    }

    /// <summary>Runs one <c>expect.poll</c> read, marked so <see cref="InsidePollRead"/> sees it in every step the read calls (upstream's <c>pollRead</c>).</summary>
    public static async Task<T> PollReadAsync<T>(Func<Task<T>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var before = Reading.Value;
        Reading.Value = true;
        try
        {
            // Started here, so the marker is the one the read's own flow inherits; the caller's flow gets its value back.
            return await run().ConfigureAwait(false);
        }
        finally
        {
            Reading.Value = before;
        }
    }

    /// <summary>Whether this async flow is inside an <c>expect.poll</c> read (upstream's <c>insidePollRead</c>).</summary>
    public static bool InsidePollRead() => Reading.Value;
}
