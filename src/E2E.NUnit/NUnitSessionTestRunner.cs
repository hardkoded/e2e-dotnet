// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NUnit.Common;
using NUnitLite;

namespace E2E.NUnit;

/// <summary>
/// Runs one NUnit test of a test assembly for <c>e2e mcp</c>'s <c>open_session {test}</c> (.NET-only). NUnitLite runs the test by its full
/// name, with its set-up, body, and tear-down. The fixture's teardown then offers its session to <see cref="SessionHandover"/> instead of
/// disposing it. The <c>e2e</c> tool finds this type through the test assembly's reference to E2E.NUnit and names no test framework.
/// </summary>
internal sealed class NUnitSessionTestRunner : ISessionTestRunner
{
    public async Task<SessionTestOutcome?> RunAsync(Assembly tests, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tests);
        using var request = SessionHandover.Begin(name);

        // NUnitLite's console is stdout, which carries the MCP protocol: its report goes nowhere.
        using var output = new StringWriter();
        try
        {
            await Task.Run(
                () => new AutoRun(tests).Execute(["--test=" + name, "--noresult", "--noheader", "--labels=Off", "--trace=Off"], new ExtendedTextWrapper(output), TextReader.Null),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancel after the fixture handed its session over leaves nobody to dispose it.
            if (request.Outcome is { } handed)
            {
                await handed.Session.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        return request.Outcome;
    }
}
