// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.StepHooks;

/// <summary>
/// .NET-only: a step knows the hook it ran in. Upstream's runner sets the hook itself (<c>enter</c> in <c>run/execute.ts</c>, tested
/// by <c>integration/traces.test.ts</c>); here NUnit calls the hooks, so the step's call stack names them. The nested run keeps a
/// trace page, which says <c>in beforeEach</c> and <c>in afterEach</c> for the steps of the hooks and nothing for the test's own.
/// </summary>
public sealed class StepHooksTests
{
    [Test]
    public void Tells_on_the_trace_page_that_a_step_of_a_SetUp_ran_in_beforeEach_and_one_of_a_TearDown_in_afterEach()
    {
        var results = Path.Combine(Path.GetTempPath(), "e2e-hooks-" + Guid.NewGuid().ToString("N"), "results");
        HookedFixture.Results = results;
        try
        {
            var result = NestedRun.Run(nameof(HookedFixture.Reads_the_loads));

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Passed), result.Message);
            var page = File.ReadAllText(Directory.GetFiles(results, "trace.md", SearchOption.AllDirectories).Single());
            var steps = page.Split('\n').Where(line => Regex.IsMatch(line, @"^\d+\. ")).ToList();
            Assert.That(steps, Has.Count.EqualTo(3), page);
            Assert.That(steps[0], Does.Contain("`app.open`").And.Contain("in beforeEach)"));
            Assert.That(steps[1], Does.Contain("`expect.toHaveText`").And.Not.Contain("in beforeEach").And.Not.Contain("in afterEach"));
            Assert.That(steps[2], Does.Contain("`expect.toBeVisible`").And.Contain("in afterEach)"));
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(results)))
            {
                Directory.Delete(Path.GetDirectoryName(results)!, recursive: true);
            }
        }
    }
}

/// <summary>A test with a step in its SetUp, one in its body, and one in its TearDown.</summary>
[Explicit("Run by StepHooksTests")]
public sealed class HookedFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    public static string Results { get; set; } = "";

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => Results;

    protected override ResolvedRecording Trace => new(RecordingMode.On, RecordingSource.Target);

    [SetUp]
    public async Task OpenBrowserPageAsync() => await App.OpenAsync("/browser");

    [TearDown]
    public async Task LookAtThePageAsync() => await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();

    [Test]
    public async Task Reads_the_loads() => await Expect.That(Screen.GetByLabel("Loads")).ToHaveTextAsync("loads: 1");
}
