// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E;

/// <summary>
/// What a test framework package gives <c>e2e mcp</c> so <c>open_session</c> can run one test of the project's test assembly in the
/// session (.NET-only: upstream runs the test itself, with its own runner). The tool finds the implementation in a package the test
/// assembly references, so the tool names no test framework. <see cref="SessionHandover"/> is how the test's fixture hands over its session.
/// </summary>
internal interface ISessionTestRunner
{
    /// <summary>
    /// The tests that <paramref name="name"/> selects in <paramref name="tests"/>, without running any: none for a name that selects nothing,
    /// several for a class or a parameterized method. Upstream's <c>selectStandaloneTest</c> collects the same way.
    /// </summary>
    IReadOnlyList<SessionTestInfo> Select(Assembly tests, string name);

    /// <summary>Every test in <paramref name="tests"/>, without running any, each with its method (upstream collects the test files for <c>file:line</c>).</summary>
    IReadOnlyList<SessionTestInfo> List(Assembly tests);

    /// <summary>
    /// Runs the test named <paramref name="name"/> in <paramref name="tests"/>; null when no test has that name. <paramref name="host"/> is the
    /// config the host loaded: the test runs on its agent model and its replay cache, not on the fixture's own (see <see cref="SessionHandover.HostConfig"/>).
    /// </summary>
    Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken, E2EConfig? host = null);
}

/// <summary>A test <see cref="ISessionTestRunner.Select"/> found: its full name, why a session cannot run it alone (null when it can), and its method.</summary>
internal sealed record SessionTestInfo(string FullName, string? Unsupported, MethodInfo? Method = null);

/// <summary>The phases a standalone run moves through before its teardown (upstream's <c>RunPhase</c>, <c>run/standalone-test.ts</c>).</summary>
internal enum RunPhase
{
    BeforeAll,
    BeforeEach,
    Body,
}

/// <summary>
/// A test run for a session: its live session and engine, the test's name, and its failure, null when it passed (upstream's
/// <c>StandaloneTestOutcome</c>). <see cref="Phase"/> is where the failure landed. A test whose <c>beforeAll</c> failed never started a session:
/// <see cref="Session"/> and <see cref="Engine"/> are null, and the host opens a fresh one. <see cref="Secondary"/> are the failures that landed
/// after the first, such as another promise the test left rejected. <see cref="Run"/> is the run the host ends at <c>close_session</c>, null when
/// the test had no session. <see cref="ExpectedFailure"/> is the reason the test is marked as expected to fail (upstream's <c>fail</c> option or
/// <c>test.fail</c>), null when it is not; the marker is an NUnit property, so the verdict of the run itself is not inverted (see COMPATIBILITY.md).
/// </summary>
internal sealed record SessionTestOutcome(string TestName, E2ESession? Session, IEngine? Engine, Exception? Failure, RunPhase Phase = RunPhase.Body, IReadOnlyList<Exception>? Secondary = null, LiveRun? Run = null, string? ExpectedFailure = null) : IAsyncDisposable
{
    /// <summary>Closes what the test left: ends its run, then disposes its session. A host that reports the close's failures does both itself.</summary>
    public async ValueTask DisposeAsync()
    {
        Run?.End();
        if (Session is { } session)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A run whose turn ended and whose session is still open (upstream's <c>liveRuns</c> entries): a promise its test left can still reject.
/// What lands is kept until <see cref="End"/>, which the host calls when it closes the session.
/// </summary>
internal sealed class LiveRun(string test)
{
    private readonly List<Exception> _late = [];
    private int _ended;

    public string Test { get; } = test;

    /// <summary>Upstream's <c>late</c>: a promise the test left rejected after its turn ended.</summary>
    public void Late(Exception cause)
    {
        lock (_late)
        {
            _late.Add(cause);
        }
    }

    /// <summary>
    /// Ends the run at its session's close: what the test left rejected lands now, and is returned as the failures the close reports. A promise
    /// that rejects after this is the operator's warning.
    /// </summary>
    public IReadOnlyList<Exception> End()
    {
        // Once: a close after a dispose finds the failures told and the look already under way.
        if (Interlocked.Exchange(ref _ended, 1) == 1)
        {
            return [];
        }

        StrayRejections.Collect();
        StrayRejections.Remove(this);
        lock (_late)
        {
            return [.. _late.Select(cause => (Exception)new TestException("UNHANDLED_REJECTION", "a promise rejected after the test's body ended, most likely left by \"" + Test + "\": " + cause.Message, cause))];
        }
    }
}

/// <summary>
/// Where a promise nobody observed goes once a session has run a test (upstream's <c>claimStrayRejection</c>, <c>run/standalone-test.ts</c>; a .NET
/// task that faults unobserved is reported when it is collected, so <see cref="Collect"/> forces the collection where upstream's event loop reports at
/// once): test code taking its turn fails with it; between turns the newest run still open reports it at its close; once every such session closed, the
/// operator is warned.
/// </summary>
internal static class StrayRejections
{
    private static readonly Lock Gate = new();
    private static readonly List<LiveRun> LiveRuns = [];
    private static Action<Exception>? _inTurn;
    private static string? _lastTest;
    private static bool _installed;
    private static int _looking;

    /// <summary>How often, and how many times, a closed session's tasks are looked at again.</summary>
    private static readonly TimeSpan LookEvery = TimeSpan.FromMilliseconds(500);

    private const int LooksAfterClose = 8;

    /// <summary>Tells the operator of a rejection that landed once every session closed; stderr by default.</summary>
    public static Action<string> Warn { get; set; } = message => Console.Error.WriteLine("e2e: [warning] " + message);

    /// <summary>Starts taking the tasks the process left faulted. Once; before any session ran a test, they are the process's own.</summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
        }

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            var cause = args.Exception.Flatten().InnerExceptions is [var only] ? only : args.Exception;
            if (Claim(cause))
            {
                args.SetObserved();
            }
        };
    }

    /// <summary>Takes a rejection nobody handled; false before any session ran a test.</summary>
    public static bool Claim(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        Action<Exception>? inTurn;
        LiveRun? newest;
        string? last;
        lock (Gate)
        {
            inTurn = _inTurn;
            newest = LiveRuns.Count > 0 ? LiveRuns[^1] : null;
            last = _lastTest;
        }

        if (inTurn is not null)
        {
            inTurn(cause);
            return true;
        }

        if (newest is not null)
        {
            newest.Late(cause);
            return true;
        }

        if (last is null)
        {
            return false;
        }

        Warn("a promise rejected after its session closed, most likely left by test \"" + last + "\": " + cause.Message);
        return true;
    }

    /// <summary>Sends what lands while test code takes its turn to <paramref name="take"/>; dispose to end the turn.</summary>
    public static IDisposable Turn(Action<Exception> take)
    {
        lock (Gate)
        {
            _inTurn = take;
        }

        return new Ended(() =>
        {
            lock (Gate)
            {
                _inTurn = null;
            }
        });
    }

    /// <summary>The run of <paramref name="test"/> whose turn ended and whose session stays open.</summary>
    public static LiveRun Keep(string test)
    {
        var run = new LiveRun(test);
        lock (Gate)
        {
            LiveRuns.Add(run);
        }

        return run;
    }

    internal static void Remove(LiveRun run)
    {
        lock (Gate)
        {
            LiveRuns.Remove(run);
            _lastTest = run.Test;
        }

        // What the test left for after the close is reported when its task is collected: look again for a few seconds after, one loop at a time.
        if (Interlocked.Exchange(ref _looking, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                for (var look = 0; look < LooksAfterClose; look++)
                {
                    await Task.Delay(LookEvery).ConfigureAwait(false);
                    Collect();
                }
            }
            finally
            {
                Volatile.Write(ref _looking, 0);
            }
        });
    }

    /// <summary>Collects the garbage so the tasks nobody observed report their failure now.</summary>
    public static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class Ended(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}

/// <summary>
/// The one test a host asked to keep open (.NET-only). A fixture offers its session at teardown; when its test is the requested one, the
/// host owns the session from then on and the fixture does not complete or dispose it. One test runs at a time per process: a request
/// waits for its turn (<see cref="TakeTurnAsync{T}"/>), as upstream's test runs take turns in <c>standalone-test.ts</c>.
/// </summary>
internal static class SessionHandover
{
    private static readonly Lock Gate = new();
    private static Request? _current;

    /// <summary>The last turn queued; the next turn starts once it has settled. Guarded by <see cref="Gate"/>.</summary>
    private static Task _tail = Task.CompletedTask;

    /// <summary>
    /// Runs <paramref name="work"/> once every turn queued before it has settled, so one test runs at a time per process (upstream's
    /// <c>takeTurn</c>). A caller that cancels while it waits gets the cancel at once, and its work then never starts: a test whose open was
    /// cancelled while it waited for its turn runs none of its code. The turn holds until <paramref name="work"/> has ended, whatever the
    /// caller does, so a test still running never overlaps the next one.
    /// </summary>
    public static Task<T> TakeTurnAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        Task<T> turn;
        lock (Gate)
        {
            turn = _tail.ContinueWith(_ => Turn(work, cancellationToken), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
            // The queue goes on past a turn that failed or was cancelled, and observes its failure so none is left unobserved.
            _tail = turn.ContinueWith(static finished => { _ = finished.Exception; }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        return turn.WaitAsync(cancellationToken);
    }

    /// <summary>Completes once every turn queued so far has settled, so a test's assembly can be unloaded after the test has stopped running.</summary>
    public static Task Drained
    {
        get
        {
            lock (Gate)
            {
                return _tail;
            }
        }
    }

    private static Task<T> Turn<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        // A caller cancelled while it waited for its turn: none of its code runs.
        cancellationToken.ThrowIfCancellationRequested();
        return work();
    }

    /// <summary>Whether a host is running one test for its session; the test filters leave such a run alone, as upstream's open-session path never applies <c>changedSince</c>.</summary>
    public static bool IsActive
    {
        get
        {
            lock (Gate)
            {
                return _current is not null;
            }
        }
    }

    /// <summary>
    /// The config the host loaded for the test it runs, or null when no host asked or the host gave none. The test's fixture takes its agent
    /// model, judge, named agents, and replay cache from it, so a host decides them without the fixture's own config (.NET-only: upstream runs the
    /// test in the host's process with the host's resolved config).
    /// </summary>
    public static E2EConfig? HostConfig
    {
        get
        {
            lock (Gate)
            {
                return _current?.Host;
            }
        }
    }

    /// <summary>
    /// Asks for the session of the test named <paramref name="testName"/>. Dispose the request to withdraw it. Called within a turn
    /// (<see cref="TakeTurnAsync{T}"/>), so a second request while one is open is a bug, not a wait.
    /// </summary>
    public static Request Begin(string testName, E2EConfig? host = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        StrayRejections.Install();
        lock (Gate)
        {
            if (_current is not null)
            {
                throw new InvalidOperationException("another test is already being run for a session");
            }

            return _current = new Request(testName, host);
        }
    }

    /// <summary>Offers the session of <paramref name="testName"/>; true when a request took it and the caller must leave it alone.</summary>
    public static bool TryOffer(string testName, E2ESession session, IEngine engine, Exception? failure)
    {
        lock (Gate)
        {
            if (_current is not { } request || !string.Equals(request.TestName, testName, StringComparison.Ordinal))
            {
                return false;
            }

            // A second offer (a retried test) finds the first taken: it keeps no run of its own.
            if (request.Taken.Task.IsCompleted)
            {
                return false;
            }

            return request.Taken.TrySetResult(new SessionTestOutcome(testName, session, engine, failure, RunPhase.Body, null, StrayRejections.Keep(testName)));
        }
    }

    internal sealed class Request(string testName, E2EConfig? host) : IDisposable
    {
        public string TestName { get; } = testName;

        /// <summary>The config the host loaded; see <see cref="SessionHandover.HostConfig"/>.</summary>
        public E2EConfig? Host { get; } = host;

        public TaskCompletionSource<SessionTestOutcome> Taken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The outcome, or null when the test ended without offering a session.</summary>
        public SessionTestOutcome? Outcome => Taken.Task.IsCompletedSuccessfully ? Taken.Task.Result : null;

        public void Dispose()
        {
            lock (Gate)
            {
                if (ReferenceEquals(_current, this))
                {
                    _current = null;
                }
            }
        }
    }
}
