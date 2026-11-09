// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using NUnit.Common;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnitLite;

namespace E2E.NUnit;

/// <summary>
/// Runs one NUnit test of a test assembly for <c>e2e mcp</c>'s <c>open_session {test}</c> (.NET-only). NUnitLite runs the test by its full
/// name, with its set-up, body, and tear-down. The fixture's teardown then offers its session to <see cref="SessionHandover"/> instead of
/// disposing it. The <c>e2e</c> tool finds this type through the test assembly's reference to E2E.NUnit and names no test framework.
/// </summary>
internal sealed class NUnitSessionTestRunner : ISessionTestRunner
{
    public IReadOnlyList<SessionTestInfo> Select(Assembly tests, string name)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // The filter NUnitLite builds for --test=<name>: the test's full name, or the name of a class or namespace that holds it.
        var filter = TestFilter.FromXml("<filter><test>" + SecurityElement.Escape(name) + "</test></filter>");
        var root = new DefaultTestAssemblyBuilder().Build(tests, new Dictionary<string, object>());
        List<SessionTestInfo> found = [];
        void Walk(ITest test)
        {
            if (!test.IsSuite)
            {
                if (filter.Pass(test))
                {
                    found.Add(new SessionTestInfo(test.FullName, UnsupportedReason(test)));
                }

                return;
            }

            foreach (var child in test.Tests)
            {
                Walk(child);
            }
        }

        Walk(root);
        return found;
    }

    // A saved session comes from a setup test that ran earlier in the same run, which a run of one test does not have (upstream refuses it too).
    private static string? UnsupportedReason(ITest test) =>
        test.TypeInfo?.Type is { } type && SessionAttributes.Restore(type, test.Method?.MethodInfo) is { } saved
            ? "test \"" + test.FullName + "\" starts from session \"" + saved + "\", which a setup test saves first; a session does not run setup tests yet, so run it with dotnet test"
            : null;

    public async Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken, E2EConfig? host = null)
    {
        ArgumentNullException.ThrowIfNull(tests);
        using var request = SessionHandover.Begin(name, host);

        // A promise nobody observed fails the test that is taking its turn (upstream's strayInTurn).
        List<Exception> stray = [];
        try
        {
            ITestResult? result = null;
            using (StrayRejections.Turn(cause => stray.Add(cause)))
            {
                // NUnitLite's console is stdout, which carries the MCP protocol, so the assembly runs under the framework's own runner: no report.
                result = await Task.Run(
                    () =>
                    {
                        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                        runner.Load(tests, new Dictionary<string, object>());
                        var ran = runner.Run(TestListener.NULL, TestFilter.FromXml("<filter><test>" + SecurityElement.Escape(name) + "</test></filter>"));

                        // A task the body left faulted reports when it is collected; the turn is still the test's.
                        StrayRejections.Collect();
                        return ran;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            return Outcome(request, name, result, stray);
        }
        catch (OperationCanceledException)
        {
            // A cancel after the fixture handed its session over leaves nobody to dispose it.
            if (request.Outcome is { } handed)
            {
                await handed.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// The outcome of the run: the session the fixture handed over, failed by what a task left rejected during its turn when the test passed; else
    /// (the test never started a session) the <c>beforeAll</c> that failed, which NUnit reports on the test as a <c>OneTimeSetUp</c> failure.
    /// </summary>
    private static SessionTestOutcome? Outcome(SessionHandover.Request request, string name, ITestResult result, List<Exception> stray)
    {
        if (request.Outcome is { } handed)
        {
            if (stray.Count == 0)
            {
                return handed;
            }

            // The first failure stands; the rest land beside it.
            var first = handed.Failure is null ? stray[0] : null;
            return handed with { Failure = handed.Failure ?? first, Secondary = [.. stray.Where(cause => !ReferenceEquals(cause, first))] };
        }

        return HookFailure(result, name) is { } message
            ? new SessionTestOutcome(name, null, null, new TestException("HOOK_FAILED", "beforeAll failed: " + message), RunPhase.BeforeAll, [.. stray])
            : null;
    }

    /// <summary>The message of the <c>OneTimeSetUp</c> that failed the test <paramref name="name"/>, or null when its result says no such thing.</summary>
    private static string? HookFailure(ITestResult root, string name)
    {
        ITestResult? Find(ITestResult each) => !each.Test.IsSuite && string.Equals(each.Test.FullName, name, StringComparison.Ordinal)
            ? each
            : each.Children.Select(Find).FirstOrDefault(found => found is not null);

        var message = Find(root)?.Message;
        const string Prefix = "OneTimeSetUp: ";
        if (message is null || !message.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        // "OneTimeSetUp: SetUp : System.InvalidOperationException : database is down": the hook's own message follows the exception type.
        var cause = message[Prefix.Length..];
        return Regex.Replace(cause, @"^(?:SetUp\s+:\s+)?[\w.`+]+\s+:\s+", string.Empty).Trim();
    }
}
