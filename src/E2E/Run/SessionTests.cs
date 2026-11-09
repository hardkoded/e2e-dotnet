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

    /// <summary>
    /// Runs the test named <paramref name="name"/> in <paramref name="tests"/>; null when no test has that name. <paramref name="host"/> is the
    /// config the host loaded: the test runs on its agent model and its replay cache, not on the fixture's own (see <see cref="SessionHandover.HostConfig"/>).
    /// </summary>
    Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken, E2EConfig? host = null);
}

/// <summary>A test <see cref="ISessionTestRunner.Select"/> found: its full name, and why a session cannot run it alone (null when it can).</summary>
internal sealed record SessionTestInfo(string FullName, string? Unsupported);

/// <summary>A test run for a session: its live session and engine, the test's name, and its failure, null when it passed.</summary>
internal sealed record SessionTestOutcome(string TestName, E2ESession Session, IEngine Engine, Exception? Failure);

/// <summary>
/// The one test a host asked to keep open (.NET-only). A fixture offers its session at teardown; when its test is the requested one, the
/// host owns the session from then on and the fixture does not complete or dispose it. One request at a time, as the host runs one.
/// </summary>
internal static class SessionHandover
{
    private static readonly Lock Gate = new();
    private static Request? _current;

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

    /// <summary>Asks for the session of the test named <paramref name="testName"/>. Dispose the request to withdraw it.</summary>
    public static Request Begin(string testName, E2EConfig? host = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
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

            return request.Taken.TrySetResult(new SessionTestOutcome(testName, session, engine, failure));
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
