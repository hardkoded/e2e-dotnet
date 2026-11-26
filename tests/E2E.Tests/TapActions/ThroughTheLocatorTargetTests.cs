// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.TapActions;

/// <summary>
/// <c>multiTap</c> against a real page, ported from upstream <c>tap-actions.test.ts</c> (<c>describe.each</c> of the two targets), through a screen locator:
/// <c>count</c> clicks arrive as one sequence with a rising <c>detail</c>, as a person's triple click does, so a triple click selects a
/// paragraph, and the held keys ride every click of it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughTheLocatorTargetTests
{
    /// <summary>Logs every click's <c>detail</c> and Shift state, and every <c>dblclick</c>, on <c>window.events</c>.</summary>
    private const string LogClicks = """
        <script>
          window.events = [];
          document.addEventListener('click', (event) => window.events.push('click:' + event.detail + (event.shiftKey ? '+shift' : '')), true);
          document.addEventListener('dblclick', () => window.events.push('dblclick'), true);
        </script>
        """;

    [Fact]
    public async Task Clicks_count_times_as_one_sequence_as_a_triple_click_does()
    {
        await RunWithScreenAsync("<button id=\"go\">Go</button>" + LogClicks, async session =>
        {
            await session.Screen.GetByRole("button", "Go").TapAsync(new TapOptions { Count = 3 });
            Assert.Equal(
                ["click:1", "click:2", "dblclick", "click:3"],
                (await session.Browser.EvaluateAsync<string[]>("() => window.events"))!
            );
        });
    }

    [Fact]
    public async Task Selects_a_whole_paragraph_with_a_triple_click()
    {
        await RunWithScreenAsync("<p id=\"para\">Three little words and more</p><p>Next paragraph</p>", async session =>
        {
            await session.Screen.GetByText("Three little words and more").TapAsync(new TapOptions { Count = 3 });
            var selected = await session.Browser.EvaluateAsync<string>("() => window.getSelection()?.toString() ?? ''");
            Assert.Equal("Three little words and more", selected!.Trim());
        });
    }

    [Fact]
    public async Task Holds_the_modifiers_for_every_click()
    {
        await RunWithScreenAsync("<button id=\"go\">Go</button>" + LogClicks, async session =>
        {
            await session
                .Screen.GetByRole("button", "Go")
                .TapAsync(new TapOptions { Count = 4, Modifiers = [KeyModifier.Shift] });
            Assert.Equal(
                ["click:1+shift", "click:2+shift", "dblclick", "click:3+shift", "click:4+shift"],
                (await session.Browser.EvaluateAsync<string[]>("() => window.events"))!
            );
        });
    }
}
