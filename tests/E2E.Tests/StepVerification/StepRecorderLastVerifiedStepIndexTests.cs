// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepVerification;

/// <summary>Upstream's <c>describe('StepRecorder.lastVerifiedStepIndex')</c>: which steps may confirm a staged action trace, whatever their kind or api name.</summary>
public sealed class StepRecorderLastVerifiedStepIndexTests
{
    private static readonly Func<Task> Pass = () => Task.CompletedTask;

    [Fact]
    public async Task Is_minus_one_until_a_verification_step_passes()
    {
        var steps = new StepRecorder("attempt");
        Assert.Equal(-1, steps.LastVerifiedStepIndex);

        await steps.RunAsync(StepKind.Agent, "agent.act", "add an expense", Pass);
        await steps.RunAsync(StepKind.App, "app.open", "/", Pass);
        await steps.RunAsync(StepKind.Locator, "locator.click", "button \"Save\"", Pass);

        Assert.Equal(-1, steps.LastVerifiedStepIndex);
    }

    [Fact]
    public async Task Advances_to_each_passed_verification_step_whatever_its_kind()
    {
        var steps = new StepRecorder("attempt");
        await steps.RunAsync(StepKind.Agent, "agent.act", "add an expense", Pass);
        await steps.RunAsync(StepKind.Assertion, "expect.toBeVisible", "the new row", Pass, new StepRunOptions { Verifies = true });
        Assert.Equal(1, steps.LastVerifiedStepIndex);

        await steps.RunAsync(StepKind.Agent, "agent.act", "approve it", Pass);
        await steps.RunAsync(StepKind.Agent, "agent.assert", "the row reads approved", Pass, new StepRunOptions { Verifies = true });
        Assert.Equal(3, steps.LastVerifiedStepIndex);

        await steps.RunAsync(StepKind.Locator, "locator.waitFor", "toast → visible", Pass, new StepRunOptions { Verifies = true });
        Assert.Equal(4, steps.LastVerifiedStepIndex);
    }

    [Fact]
    public async Task Does_not_advance_on_a_verification_step_that_failed()
    {
        var steps = new StepRecorder("attempt");
        await steps.RunAsync(StepKind.Assertion, "expect.toBeVisible", "the row", Pass, new StepRunOptions { Verifies = true });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => steps.RunAsync(
            StepKind.Assertion,
            "expect.toHaveText",
            "the row",
            () => throw new InvalidOperationException("mismatch"),
            new StepRunOptions { Verifies = true }));

        Assert.Equal("mismatch", thrown.Message);
        Assert.Equal(0, steps.LastVerifiedStepIndex);
    }
}
