// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Security;
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
        return Collect(tests, filter.Pass);
    }

    public IReadOnlyList<SessionTestInfo> List(Assembly tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        return Collect(tests, _ => true);
    }

    private static List<SessionTestInfo> Collect(Assembly tests, Func<ITest, bool> pass)
    {
        var root = new DefaultTestAssemblyBuilder().Build(tests, new Dictionary<string, object>());
        List<SessionTestInfo> found = [];
        void Walk(ITest test)
        {
            if (!test.IsSuite)
            {
                if (pass(test))
                {
                    found.Add(new SessionTestInfo(test.FullName, UnsupportedReason(test), test.Method?.MethodInfo));
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
        // Tests take turns per process (upstream's takeTurn): a call waits for the tests queued before it, and a cancel while it waits runs none of it.
        return await SessionHandover.TakeTurnAsync(() => RunTurnAsync(tests, name, host, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SessionTestOutcome?> RunTurnAsync(Assembly tests, string name, E2EConfig? host, CancellationToken cancellationToken)
    {
        using var request = SessionHandover.Begin(name, host);

        // NUnitLite's console is stdout, which carries the MCP protocol: its report goes nowhere. The turn holds until NUnitLite has ended, so a
        // test still running is never overlapped by the next one; a caller that cancelled meanwhile has already been answered.
        using var output = new StringWriter();
        await Task.Run(
            () => new AutoRun(tests).Execute(["--test=" + name, "--noresult", "--noheader", "--labels=Off", "--trace=Off"], new ExtendedTextWrapper(output), TextReader.Null)).ConfigureAwait(false);

        // A cancel after the fixture handed its session over leaves nobody to take it: the session is disposed here.
        if (request.Outcome is { } handed && cancellationToken.IsCancellationRequested)
        {
            await handed.Session.DisposeAsync().ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }

        return request.Outcome;
    }
}
