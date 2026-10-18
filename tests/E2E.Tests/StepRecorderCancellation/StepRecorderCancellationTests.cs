// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepRecorderCancellation;

/// <summary>
/// .NET-only: upstream passes the recorder an <c>AbortSignal</c> and its tests never fire it. These check the same option, as a
/// <see cref="CancellationToken"/>, and the status of a step that ends in a cancellation.
/// </summary>
public sealed class StepRecorderCancellationTests
{
    [Fact]
    public async Task Cuts_a_running_step_when_the_cancellation_fires_and_abandons_its_body()
    {
        using var cancellation = new CancellationTokenSource();
        var steps = new StepRecorder("attempt", new StepRecorderOptions { Cancellation = cancellation.Token });
        var body = new Gate();
        var step = steps.RunAsync(StepKind.Agent, "agent.act", "pay", () => body.Task);

        await cancellation.CancelAsync();
        var cut = await Assert.ThrowsAsync<TestException>(() => step);

        Assert.Equal("CANCELLED", cut.Code);
        Assert.Equal("agent.act cancelled", cut.Message);
        Assert.Equal(StepStatus.Cancelled, steps.All()[0].Status);
        body.Fail(new InvalidOperationException("late"));
        await Task.Delay(20);
        Assert.Equal("CANCELLED", steps.All()[0].Error?.Code);
    }

    [Fact]
    public async Task Records_a_step_that_ends_in_a_cancellation_as_cancelled()
    {
        var steps = new StepRecorder("attempt");

        await Assert.ThrowsAsync<OperationCanceledException>(() => steps.RunAsync(StepKind.App, "app.open", "/", () => throw new OperationCanceledException()));

        var record = steps.All()[0];
        Assert.Equal(StepStatus.Cancelled, record.Status);
        Assert.Equal("interrupted", record.Error?.Category);
    }

    [Fact]
    public async Task Records_the_failure_of_a_step_with_the_code_of_the_exception()
    {
        var steps = new StepRecorder("attempt");

        await Assert.ThrowsAsync<EngineException>(() => steps.RunAsync(StepKind.Locator, "locator.tap", "button", () => throw new EngineException(EngineErrorCodes.NodeStale, "gone", retryable: true)));

        var error = steps.All()[0].Error;
        Assert.Equal(StepStatus.Failed, steps.All()[0].Status);
        Assert.Equal(("infrastructure", EngineErrorCodes.NodeStale, "gone", true), (error?.Category, error?.Code, error?.Message, error?.Retryable));
    }
}
