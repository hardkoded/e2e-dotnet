// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepAbandon;

/// <summary>Upstream's <c>describe('StepRecorder.abandonRunning')</c>: the steps a test body returned without awaiting.</summary>
public sealed class StepRecorderAbandonRunningTests
{
    [Fact]
    public async Task Returns_nothing_when_every_step_was_awaited()
    {
        var (steps, _) = StepKit.Recorder();
        await steps.RunAsync(StepKind.App, "app.open", "/", () => Task.CompletedTask);
        Assert.Null(steps.AbandonRunning());
        Assert.Equal(StepStatus.Passed, steps.All()[0].Status);
        var cause = new InvalidOperationException("awaited and failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => steps.RunAsync(StepKind.App, "app.open", "/", () => Task.FromException(cause)));

        Assert.Same(cause, thrown);
        Assert.False(AbandonedWork.IsRejection(cause));
    }

    /// <summary>Partial: the failure names the line through the record's source. A .NET exception cannot be made to carry the frames of another call, so its own stack is not asserted.</summary>
    [Fact]
    public async Task Fails_the_running_step_at_the_line_it_was_called_from_and_observes_its_later_rejection_without_a_stack()
    {
        var (steps, heard) = StepKit.Recorder();
        var body = new Gate();
        var step = steps.RunAsync(StepKind.Agent, "agent.act", "go to the contact form", () => body.Task);

        var failure = steps.AbandonRunning();

        Assert.NotNull(failure);
        Assert.Equal("STEP_NOT_AWAITED", failure.Code);
        Assert.Equal("the test body returned before agent.act \"go to the contact form\" finished; put `await` in front of every step call", failure.Message);
        var record = steps.All()[0];
        Assert.Equal(StepStatus.Failed, record.Status);
        Assert.Equal("test", record.Error?.Category);
        Assert.Equal("STEP_NOT_AWAITED", record.Error?.Code);
        Assert.Equal("StepAbandon/StepRecorderAbandonRunningTests.cs", record.Error?.Source?.File);
        Assert.Equal([StepProgressPhase.Start, StepProgressPhase.End], heard.Select(progress => progress.Phase));
        Assert.Empty(steps.Completed());

        var cause = new InvalidOperationException("no attempt is running");
        Assert.False(AbandonedWork.IsRejection(cause));
        body.Fail(cause);
        await Assert.ThrowsAsync<InvalidOperationException>(() => step);
        Assert.True(AbandonedWork.IsRejection(cause));
        Assert.Equal(StepStatus.Failed, record.Status);
        Assert.Equal("STEP_NOT_AWAITED", record.Error?.Code);
        Assert.Equal([StepProgressPhase.Start, StepProgressPhase.End], heard.Select(progress => progress.Phase));
        Assert.Same(record, Assert.Single(steps.Completed()));
        Assert.Null(steps.AbandonRunning());
    }

    [Fact]
    public async Task Settles_once_every_abandoned_step_has_however_it_did()
    {
        var (steps, _) = StepKit.Recorder();
        var first = new Gate();
        var second = new Gate();
        _ = steps.RunAsync(StepKind.App, "app.open", "/", () => first.Task);
        _ = steps.RunAsync(StepKind.Agent, "agent.act", "pay", () => second.Task);
        steps.AbandonRunning();
        var wait = steps.SettleAbandonedAsync();

        first.Fail(new InvalidOperationException("cancelled"));
        await Task.Delay(50);

        Assert.False(wait.IsCompleted);
        second.Open();
        await wait;
        Assert.Equal([StepStatus.Failed, StepStatus.Failed], steps.All().Select(step => step.Status));
    }

    [Fact]
    public async Task Keeps_an_abandoned_step_failed_when_it_later_passes_and_never_counts_it_as_verified()
    {
        var (steps, _) = StepKit.Recorder();
        var body = new Gate();
        var step = steps.RunAsync(StepKind.Assertion, "expect.toBeVisible", "button \"Save\"", () => body.Task, new StepRunOptions { Verifies = true });
        steps.AbandonRunning();

        body.Open();
        await step;

        Assert.Equal(StepStatus.Failed, steps.All()[0].Status);
        Assert.Equal(-1, steps.LastVerifiedStepIndex);
    }

    [Fact]
    public async Task Names_the_first_step_and_counts_the_rest()
    {
        var (steps, _) = StepKit.Recorder();
        var first = new Gate();
        var second = new Gate();
        var a = steps.RunAsync(StepKind.App, "app.open", "/", () => first.Task);
        var b = steps.RunAsync(StepKind.Agent, "agent.act", new string('x', 100), () => second.Task);

        var failure = steps.AbandonRunning();

        Assert.Contains("before app.open \"/\" and 1 more finished", failure?.Message, StringComparison.Ordinal);
        Assert.Contains($"agent.act \"{new string('x', 77)}...\" finished", steps.All()[1].Error?.Message, StringComparison.Ordinal);
        first.Open();
        second.Open();
        await Task.WhenAll(a, b);
    }
}
