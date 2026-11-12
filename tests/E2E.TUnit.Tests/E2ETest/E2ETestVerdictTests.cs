// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.TUnit.Tests.EngineContract;
using TUnit.Core.Enums;
using TUnit.Core.Interfaces;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit.Tests.E2ETest;

/// <summary>
/// Marks a test whose session must be completed with a given error (the verdict the replay cache sees). It runs after the adapter's own end-of-test
/// receiver, so it reads the verdict the session was completed with, and reports the test as passed when it matches.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExpectedVerdictAttribute(Type error, string? part = null) : Attribute, ITestEndEventReceiver
{
    public int Order => 1;

    public EventReceiverStage Stage => EventReceiverStage.Late;

    public ValueTask OnTestEnd(TUnitContext context)
    {
        var verdict = ((E2E.TUnit.E2ETest)context.Metadata.TestDetails.ClassInstance).CompletedWith;
        var wrong = verdict is null || !error.IsInstanceOfType(verdict)
            ? $"The session was completed with {verdict?.GetType().Name ?? "no error"}, not {error.Name}."
            : part is not null && !verdict.Message.Contains(part, StringComparison.Ordinal)
                ? $"The verdict \"{verdict.Message}\" does not hold \"{part}\"."
                : null;
        if (wrong is null)
        {
            context.Execution.OverrideResult(TestState.Passed, "the session was completed as expected");
        }
        else
        {
            context.Execution.OverrideResult(TestState.Failed, wrong);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>A body that passes, and a derived <c>[After(Test)]</c> hook that throws.</summary>
public sealed class FailingDerivedTeardownFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit]
    [ExpectedVerdict(typeof(TestException), "teardown broke")]
    public async Task Passes_before_its_teardown_fails()
    {
        await App.OpenAsync("/browser");
    }

    [After(HookType.Test)]
    public void BreakTeardown() => throw new InvalidOperationException("teardown broke");
}

/// <summary>A body that passes while the engine's settle ends cancelled: the verdict is a cancellation, not a pass.</summary>
public sealed class CancelledSettleFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions())) { CancelSettle = true };

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit]
    [ExpectedVerdict(typeof(OperationCanceledException))]
    public async Task Passes_while_the_run_is_cancelled()
    {
        await App.OpenAsync("/browser");
    }
}

/// <summary>A body whose soft assertion failed, and a derived teardown that records how many looks at the app were taken before it ran.</summary>
public sealed class SoftEvidenceFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

        [Test, Explicit]
    public async Task Soft_failure_then_the_body_returns()
    {
        await App.OpenAsync("/browser");
        await Expect.Soft(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: TimeSpan.FromMilliseconds(200));
    }

    [After(HookType.Test)]
    public void RecordLooks() => Console.WriteLine("looks before teardown: " + Engine.Observed);
}

/// <summary>
/// .NET-only: the verdict the TUnit adapter gives the replay cache when the failure is not the test body's. A nested run starts each fixture,
/// because the verdict of one test cannot be read from inside it. The fixtures are explicit, so only these tests run them.
/// </summary>
public sealed class E2ETestVerdictTests
{
    [Test]
    public async Task A_failing_derived_teardown_fails_the_verdict_of_a_passing_body()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FailingDerivedTeardownFixture));
        var result = results[nameof(FailingDerivedTeardownFixture.Passes_before_its_teardown_fails)];
        await Assert.That(result.Result).IsEqualTo("Passed").Because(result.Message);
    }

    [Test]
    public async Task A_cancelled_settle_after_a_passing_body_is_not_a_pass()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(CancelledSettleFixture));
        var result = results[nameof(CancelledSettleFixture.Passes_while_the_run_is_cancelled)];
        await Assert.That(result.Result).IsEqualTo("Passed").Because(result.Message);
    }

    [Test]
    public async Task Soft_failures_are_looked_at_before_the_teardown_runs()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(SoftEvidenceFixture));
        var result = results[nameof(SoftEvidenceFixture.Soft_failure_then_the_body_returns)];
        await Assert.That(result.Result).IsEqualTo("Failed");
        await Assert.That(output).Contains("looks before teardown: 1");
    }
}
