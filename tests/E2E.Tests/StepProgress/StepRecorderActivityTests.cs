// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepProgress;

/// <summary>Upstream's <c>describe('StepRecorder.activity')</c>.</summary>
public sealed class StepRecorderActivityTests
{
    [Fact]
    public async Task Announces_what_the_running_step_waits_on_recording_nothing()
    {
        var (steps, heard) = StepKit.UnrootedRecorder();

        await steps.RunAsync(StepKind.Agent, "agent.act", "pay", () =>
        {
            steps.Activity(StepActivity.Action);
            steps.Activity(StepActivity.Observe);
            return Task.CompletedTask;
        });

        Assert.Equal(
            [StepProgressPhase.Start, StepProgressPhase.Activity, StepProgressPhase.Activity, StepProgressPhase.End],
            heard.Select(progress => progress.Phase));
        Assert.Equal([StepActivity.Action, StepActivity.Observe], heard.Where(progress => progress.Phase == StepProgressPhase.Activity).Select(progress => progress.Activity));
        Assert.Equal(0, heard[3].ModelCalls);
        Assert.Empty(steps.All()[0].Events);
    }
}
