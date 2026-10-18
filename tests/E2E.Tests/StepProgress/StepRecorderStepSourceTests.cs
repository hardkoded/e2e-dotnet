// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepProgress;

/// <summary>Upstream's <c>describe('StepRecorder step source')</c>.</summary>
public sealed class StepRecorderStepSourceTests
{
    [Fact]
    public async Task Names_the_test_line_the_step_was_called_from_when_it_knows_the_project_root_and_nothing_otherwise()
    {
        var (located, _) = StepKit.Recorder();
        await located.RunAsync(StepKind.Locator, "locator.tap", "tap", () => Task.CompletedTask);

        Assert.Equal("StepProgress/StepRecorderStepSourceTests.cs", located.All()[0].Source?.File);

        var (steps, _) = StepKit.UnrootedRecorder();
        await steps.RunAsync(StepKind.Locator, "locator.tap", "tap", () => Task.CompletedTask);

        Assert.Null(steps.All()[0].Source);
    }
}
