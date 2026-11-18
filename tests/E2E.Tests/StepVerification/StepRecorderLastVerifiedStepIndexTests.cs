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
        await steps.RunAsync(
            StepKind.Assertion,
            "expect.toBeVisible",
            "the new row",
            Pass,
            new StepRunOptions { Verifies = true }
        );
        Assert.Equal(1, steps.LastVerifiedStepIndex);

        await steps.RunAsync(StepKind.Agent, "agent.act", "approve it", Pass);
        await steps.RunAsync(
            StepKind.Agent,
            "agent.assert",
            "the row reads approved",
            Pass,
            new StepRunOptions { Verifies = true }
        );
        Assert.Equal(3, steps.LastVerifiedStepIndex);

        await steps.RunAsync(
            StepKind.Locator,
            "locator.waitFor",
            "toast → visible",
            Pass,
            new StepRunOptions { Verifies = true }
        );
        Assert.Equal(4, steps.LastVerifiedStepIndex);
    }

    [Fact]
    public async Task Keeps_counting_step_indexes_when_it_retains_only_the_newest_steps()
    {
        var steps = new StepRecorder("attempt", new StepRecorderOptions { RetainSteps = 2 });
        for (var index = 0; index < 5; index++)
        {
            await steps.RunAsync(StepKind.Locator, "locator.textContent", "row " + index, Pass);
        }

        await steps.RunAsync(
            StepKind.Locator,
            "locator.waitFor",
            "toast → visible",
            Pass,
            new StepRunOptions { Verifies = true }
        );

        Assert.Equal([4, 5], steps.All().Select(step => step.Index));
        Assert.Equal(5, steps.LastVerifiedStepIndex);
    }

    // A .NET exception's message cannot be edited in place, so where upstream checks that the thrown error was redacted as it stands, this
    // checks that the error that reaches the caller, and its cause, carry no secret.
    [Fact]
    public async Task Throws_a_failed_steps_error_classified_and_redacted_when_asked_to()
    {
        static string Redact(string text) => text.Replace("hunter2", "[SECRET]", StringComparison.Ordinal);
        var thrown = new InvalidOperationException(
            "the page printed hunter2",
            new InvalidOperationException("driver saw hunter2")
        );
        var steps = new StepRecorder("attempt", new StepRecorderOptions { Redact = Redact, RedactThrown = true });

        var caught = await Assert.ThrowsAsync<TestException>(() =>
            steps.RunAsync(StepKind.App, "app.open", "/", () => Task.FromException(thrown))
        );

        Assert.Equal("ERROR", caught.Code);
        Assert.DoesNotContain("hunter2", caught.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", caught.InnerException!.Message, StringComparison.Ordinal);

        var kept = new InvalidOperationException("the page printed hunter2");
        var left = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StepRecorder("attempt", new StepRecorderOptions { Redact = Redact }).RunAsync(
                StepKind.App,
                "app.open",
                "/",
                () => Task.FromException(kept)
            )
        );
        Assert.Contains("hunter2", left.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_advance_on_a_verification_step_that_failed()
    {
        var steps = new StepRecorder("attempt");
        await steps.RunAsync(
            StepKind.Assertion,
            "expect.toBeVisible",
            "the row",
            Pass,
            new StepRunOptions { Verifies = true }
        );

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            steps.RunAsync(
                StepKind.Assertion,
                "expect.toHaveText",
                "the row",
                () => throw new InvalidOperationException("mismatch"),
                new StepRunOptions { Verifies = true }
            )
        );

        Assert.Equal("mismatch", thrown.Message);
        Assert.Equal(0, steps.LastVerifiedStepIndex);
    }
}
