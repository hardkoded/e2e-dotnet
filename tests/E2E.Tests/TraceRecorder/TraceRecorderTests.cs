// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceRecorder;

public sealed class TraceRecorderTests
{
    /// <summary>
    /// Upstream drives the recorder directly. The port records inside the agent, so
    /// this records five scrolls down and one up through an act and reads the entry back.
    /// </summary>
    [Fact]
    public async Task Folds_consecutive_identical_scrolls_into_one_action_with_a_repeat_count()
    {
        var directory = ScrollTests.TempCache();
        var model = ScrollTests.Sequence(
            ModelResponses.Call("scroll", new { direction = "down" }),
            ModelResponses.Call("scroll", new { direction = "down" }),
            ModelResponses.Call("scroll", new { direction = "down" }),
            ModelResponses.Call("scroll", new { direction = "down" }),
            ModelResponses.Call("scroll", new { direction = "down" }),
            ModelResponses.Call("scroll", new { direction = "up" }),
            ModelResponses.Done("passed", "scrolled"));
        var result = await ScrollTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/rows");
                await ctx.Agent.ActAsync("scroll down five screens, then up one");
                await Expect.That(ctx.Screen.GetByRole("button", "Row 30")).ToBeVisibleAsync();
            },
            ScrollTests.Rows(),
            model,
            directory);

        Assert.Null(result.Error);
        var entry = ScrollTests.ReadEntry(directory);
        Assert.Equal([("down", (int?)5), ("up", null)], entry.Actions.Select(action => (action.Direction, action.Times)));
    }
}
