// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepScreens.ScreenKit;

namespace E2E.Tests.StepScreens;

/// <summary>Upstream's <c>describe('StepRecorder.recordScreen')</c> (<c>step-screens.test.ts</c>).</summary>
public sealed class StepRecorderRecordScreenTests
{
    [Fact]
    public async Task Compares_each_steps_last_screen_with_the_step_befores_reads_it_once_at_the_steps_end_and_ignores_a_screen_outside_a_step()
    {
        var steps = new StepRecorder("attempt");
        var reads = 0;
        Func<ScreenText?> Seen(string text) => () =>
        {
            reads++;
            return Screen(text);
        };

        steps.RecordScreen(Seen("#root document"));
        await steps.RunAsync(StepKind.App, "app.open", "/", () =>
        {
            steps.RecordScreen(Seen("#root document\n #n2 button \"Old\""));
            steps.RecordScreen(Seen("#root document\n #n2 button \"Add\""));
            return Task.CompletedTask;
        });
        await steps.RunAsync(StepKind.Assertion, "expect.toBeVisible", "x", () => Task.CompletedTask);
        await steps.RunAsync(StepKind.Screen, "locator.tap", "Add", () =>
        {
            steps.RecordScreen(Seen("#root document\n #n2 button \"Add\"\n #n3 listitem \"Milk\""));
            return Task.CompletedTask;
        });

        var all = steps.All();
        Assert.Equal(2, reads);
        Assert.Equal("http://app.test/todos", all[0].Screen!.Location);
        Assert.Equal(2, all[0].Screen!.Nodes);
        Assert.Empty(all[0].Screen!.Changes);
        Assert.Null(all[1].Screen);
        Assert.Equal(0, all[2].Screen!.Since);
        Assert.Equal(["added listitem \"Milk\""], all[2].Screen!.Changes);
    }
}
