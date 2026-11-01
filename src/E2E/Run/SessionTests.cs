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
    /// <summary>Runs the test named <paramref name="name"/> in <paramref name="tests"/>; null when no test has that name.</summary>
    Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken);
}

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

    /// <summary>Asks for the session of the test named <paramref name="testName"/>. Dispose the request to withdraw it.</summary>
    public static Request Begin(string testName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        lock (Gate)
        {
            if (_current is not null)
            {
                throw new InvalidOperationException("another test is already being run for a session");
            }

            return _current = new Request(testName);
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

    internal sealed class Request(string testName) : IDisposable
    {
        public string TestName { get; } = testName;

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
