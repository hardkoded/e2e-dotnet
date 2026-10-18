// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepVerification;

/// <summary>Upstream's <c>describe('async step ownership')</c>.</summary>
public sealed class AsyncStepOwnershipTests
{
    [Fact]
    public async Task Attributes_overlapping_siblings_and_nested_work_to_their_own_scopes()
    {
        var steps = new StepRecorder("attempt");
        var firstGate = new Gate();
        var secondGate = new Gate();
        var first = steps.RunAsync(StepKind.Resource, "first", "", async () =>
        {
            await firstGate.Task;
            steps.AttachArtifact("first");
        });
        var second = steps.RunAsync(StepKind.Resource, "second", "", async () =>
        {
            await secondGate.Task;
            Assert.Equal("attempt:1", steps.CurrentStepId);
            await steps.RunAsync(StepKind.Resource, "nested", "", () =>
            {
                steps.AttachArtifact("nested");
                return Task.CompletedTask;
            });
            steps.AttachArtifact("second");
        });

        firstGate.Open();
        await first;
        Assert.Null(steps.CurrentStepId);
        secondGate.Open();
        await second;

        Assert.Null(steps.CurrentStepId);
        Assert.Equal([["first"], ["second"], ["nested"]], steps.All().Select(step => step.Artifacts));
    }

    [Fact]
    public async Task Ignores_late_work_inherited_from_a_completed_step_instead_of_attaching_it_to_a_newer_step()
    {
        var steps = new StepRecorder("attempt");
        var lateGate = new Gate();
        Task? late = null;
        await steps.RunAsync(StepKind.Resource, "finished", "", () =>
        {
            late = lateGate.Task.ContinueWith(
                _ =>
                {
                    steps.AttachArtifact("late");
                    steps.AttachViewport(new StepViewport(1, 1, 1));
                    steps.RecordEvent(new StepEvent { Kind = StepEventKind.Engine, StartedAt = DateTimeOffset.UtcNow });
                },
                TaskScheduler.Default);
            return Task.CompletedTask;
        });

        await steps.RunAsync(StepKind.Resource, "newer", "", async () =>
        {
            lateGate.Open();
            await late!;
        });

        foreach (var step in steps.All())
        {
            Assert.Empty(step.Artifacts);
            Assert.Empty(step.Events);
            Assert.Null(step.Viewport);
        }
    }
}
