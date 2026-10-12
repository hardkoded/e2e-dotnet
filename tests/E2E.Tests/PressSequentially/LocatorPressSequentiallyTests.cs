// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.PressSequentially;

/// <summary>
/// <c>locator.pressSequentially</c>. Upstream reads the engine's keyboard log; the document engine has no keyboard, so
/// this test reads the values its fill hook saw, one per keystroke.
/// </summary>
public sealed class LocatorPressSequentiallyTests
{
    [Fact]
    public async Task Splits_by_code_point_so_a_surrogate_pair_is_one_keystroke()
    {
        var typed = new List<string>();
        var world = new DocumentWorld().Map("/city", page =>
        {
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "City", Value = "", OnFill = typed.Add });
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            BaseUrl = "https://city.test",
            TestTitle = "press sequentially",
        });
        await session.App.OpenAsync("/city");

        await session.Screen.GetByLabel("City").PressSequentiallyAsync("a\U0001F600", new PressSequentiallyOptions { Delay = TimeSpan.Zero });

        Assert.Equal(["a", "a\U0001F600"], typed);
    }
}
