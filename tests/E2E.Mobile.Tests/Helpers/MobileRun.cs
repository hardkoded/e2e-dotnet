// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile.Tests;

/// <summary>
/// Runs in-memory tests through the port's run pipeline (<see cref="Runner.RunAsync"/>, which upstream's built runner's <c>run</c> is) on a
/// mobile engine, with the agents given in code. The test's source is a closure, not a file: the port has no test-file loader, as the web
/// fixture projects of <c>E2E.Tests</c> do not. Each test runs one attempt in a session of its own, and the run finishes the engine's hooks
/// the way a run does.
/// </summary>
internal static class MobileRun
{
    /// <summary>Runs the tests on <paramref name="engine"/> in a throwaway project, and returns the run's outcome.</summary>
    public static async Task<RunOutcome> RunAsync(IEngine engine, IReadOnlyList<InMemoryTest> tests, IReadOnlyDictionary<string, AgentOptions>? agents = null)
    {
        var project = Path.Combine(Path.GetTempPath(), "e2e-mobile-run", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(project);
        try
        {
            return await Runner.RunAsync(new RunOptions
            {
                Cwd = project,
                Tests = new InMemoryTests("tests/mobile.e2e", tests),
                Engine = engine,
                Agents = agents,
                Env = new Dictionary<string, string?> { ["CI"] = "" },
            });
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }
}
