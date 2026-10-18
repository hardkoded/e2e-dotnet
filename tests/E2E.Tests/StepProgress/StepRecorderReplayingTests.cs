// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepProgress;

/// <summary>Upstream's <c>describe('StepRecorder.replaying')</c>: what a reporter hears while the trace cache has a step.</summary>
public sealed class StepRecorderReplayingTests
{
    [Fact]
    public async Task Tells_reporters_the_cache_took_the_step_and_handed_it_to_the_model_recording_nothing()
    {
        var (steps, heard) = StepKit.UnrootedRecorder();

        await steps.RunAsync(StepKind.Agent, "agent.act", "pay", () =>
        {
            steps.Replaying(true);
            steps.Replaying(false);
            return Task.CompletedTask;
        });

        Assert.Equal(
            [StepProgressPhase.Start, StepProgressPhase.Replay, StepProgressPhase.Replay, StepProgressPhase.End],
            heard.Select(progress => progress.Phase));
        Assert.All(heard, progress => Assert.Equal("agent.act", progress.Api));
        Assert.Equal(StepKind.Agent, heard[0].Kind);
        Assert.Equal("pay", heard[0].Label);
        Assert.True(heard[1].Active);
        Assert.False(heard[2].Active);
        Assert.Equal(0, heard[3].ModelCalls);
        Assert.Empty(steps.All()[0].Events);
    }

    [Fact]
    public async Task Says_nothing_outside_a_running_step()
    {
        var (steps, heard) = StepKit.UnrootedRecorder();
        steps.Replaying(true);
        Action? late = null;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => steps.RunAsync(StepKind.Agent, "agent.act", "pay", () =>
        {
            late = () => steps.Replaying(false);
            throw new InvalidOperationException("step timed out");
        }));

        Assert.Equal("step timed out", thrown.Message);
        late?.Invoke();
        Assert.Equal([StepProgressPhase.Start, StepProgressPhase.End], heard.Select(progress => progress.Phase));
    }
}
