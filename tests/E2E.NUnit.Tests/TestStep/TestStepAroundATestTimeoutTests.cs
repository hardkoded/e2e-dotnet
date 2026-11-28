// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Interfaces;
using static E2E.NUnit.Tests.TestStep.TestStepKit;

namespace E2E.NUnit.Tests.TestStep;

/// <summary>
/// <c>describe('test.step around a test timeout')</c> of upstream's <c>tests/integration/test-step.test.ts</c> (tester-army/e2e#2312). The port's clock starts
/// before the engine launches, so the timeout is 10 s, not 1000 ms, and the bodies wait past it (12 s, and 5 s in <c>runs next</c>). The three tests run in one
/// NUnit run, in order, as upstream's run them one after the other in one worker. The first <c>it</c> is <c>Partial_</c>: upstream's call the timeout cuts
/// ends <c>cancelled</c> with the code <c>CANCELLED</c>, and the port records a call a test timeout cuts as <c>failed</c> with the code <c>TEST_TIMEOUT</c> (as it
/// does for a call outside any step), so the step around it is <c>failed</c> too. What the test checks of the step is the same: it ends after the call,
/// holds no error of its own, and nothing is reported as not awaited. The second <c>it</c> is <c>Partial_</c> too: NUnit awaits the timed-out method body to its end, so the
/// late <c>Step</c> finishes before <c>Runs_next</c> starts and the test cannot show an overlap; it checks that <c>Runs_next</c> records only its own calls.
/// </summary>
public sealed class TestStepAroundATestTimeoutTests
{
    private static JsonResults? _run;

    private sealed record JsonResults(IReadOnlyList<ITestResult> Results, System.Text.Json.Nodes.JsonNode Report);

    [OneTimeSetUp]
    public void RunTheFixture()
    {
        TestStepResults.Clear();
        var results = NestedTestStepRun.Run(
            nameof(StepAroundATestTimeoutFixture.Times_out_in_a_call),
            nameof(StepAroundATestTimeoutFixture.Times_out_then_steps_late),
            nameof(StepAroundATestTimeoutFixture.Runs_next)
        );
        _run = new JsonResults(results, Report(typeof(StepAroundATestTimeoutFixture)));
    }

    [OneTimeTearDown]
    public void RemoveResults() => TestStepResults.Clear();

    private static IEnumerable<(string Api, string Status, string? Code)> StepsOfTest(string method) =>
        StepsOf(ResultByTitle(_run!.Report, method))
            .Select(step => ((string)step!["api"]!, (string)step["status"]!, (string?)step["error"]?["code"]));

    [Test]
    public void Partial_Cuts_the_call_running_in_a_step_as_the_timeout_cuts_any_call_not_as_one_the_test_did_not_await()
    {
        Assert.That(
            (string?)ResultByTitle(_run!.Report, nameof(StepAroundATestTimeoutFixture.Times_out_in_a_call))["status"],
            Is.EqualTo("timed-out")
        );
        // The step ends with the call it holds, or with its own cut when that comes first: either way it is not passed and holds no error of
        // its own, and the call carries the timeout.
        var steps = StepsOfTest(nameof(StepAroundATestTimeoutFixture.Times_out_in_a_call)).Skip(1).ToList();
        Assert.That(steps, Has.Count.EqualTo(2));
        Assert.That(steps[0].Api, Is.EqualTo("test.step"));
        Assert.That(steps[0].Status, Is.AnyOf("failed", "cancelled"));
        Assert.That(steps[0].Code, Is.Null);
        Assert.That(steps[1].Api, Is.EqualTo("locator.waitFor"));
        Assert.That(steps[1].Status, Is.AnyOf("failed", "cancelled"));
        Assert.That(steps[1].Code, Is.AnyOf("TEST_TIMEOUT", "CANCELLED"));
    }

    [Test]
    public void Partial_Keeps_a_step_a_timed_out_body_calls_later_out_of_the_test_that_runs_then()
    {
        Assert.That(
            (string?)
                ResultByTitle(_run!.Report, nameof(StepAroundATestTimeoutFixture.Times_out_then_steps_late))["status"],
            Is.EqualTo("timed-out")
        );
        var next = ResultByTitle(_run.Report, nameof(StepAroundATestTimeoutFixture.Runs_next));
        Assert.That(
            (string?)next["status"],
            Is.EqualTo("passed"),
            next["attempts"]!.AsArray().Last()!["error"]?.ToJsonString()
        );
        Assert.That(
            StepsOfTest(nameof(StepAroundATestTimeoutFixture.Runs_next)).Select(step => step.Api),
            Is.EqualTo(new[] { "app.open", "locator.waitFor" })
        );
    }
}
