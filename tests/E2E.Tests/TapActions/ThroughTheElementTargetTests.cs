// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.TapActions;

/// <summary>
/// <c>multiTap</c> against a real page, ported from upstream <c>tap-actions.test.ts</c> (<c>describe.each</c> of the two targets), on the element an observation names, which the agent acts through:
/// <c>count</c> clicks arrive as one sequence with a rising <c>detail</c>, as a person's triple click does, so a triple click selects a
/// paragraph, and the held keys ride every click of it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ThroughTheElementTargetTests
{
    /// <summary>Logs every click's <c>detail</c> and Shift state, and every <c>dblclick</c>, on <c>window.events</c>.</summary>
    private const string LogClicks = """
        <script>
          window.events = [];
          document.addEventListener('click', (event) => window.events.push('click:' + event.detail + (event.shiftKey ? '+shift' : '')), true);
          document.addEventListener('dblclick', () => window.events.push('dblclick'), true);
        </script>
        """;

    /// <summary>The node of an observation whose text or name is <paramref name="text"/>: the innermost one, as a locator by text picks.</summary>
    private static async Task<SemanticNode> FindTextAsync(IEngineSession session, string text)
    {
        var observation = await session.ObserveAsync(CancellationToken.None);
        return Flatten(observation.Roots).Last(node => node.Text == text || node.Name == text);
    }

    [Fact]
    public async Task Clicks_count_times_as_one_sequence_as_a_triple_click_does()
    {
        await RunAsync("<button id=\"go\">Go</button>" + LogClicks, async session =>
        {
            await session.PerformAsync(
                await FindAsync(session, "button", "Go"),
                new LocatorAction.MultiTap(3),
                CancellationToken.None
            );
            Assert.Equal(
                ["click:1", "click:2", "dblclick", "click:3"],
                await EvaluateAsync<string[]>(session, "() => window.events")
            );
        });
    }

    [Fact]
    public async Task Selects_a_whole_paragraph_with_a_triple_click()
    {
        await RunAsync("<p id=\"para\">Three little words and more</p><p>Next paragraph</p>", async session =>
        {
            await session.PerformAsync(
                await FindTextAsync(session, "Three little words and more"),
                new LocatorAction.MultiTap(3),
                CancellationToken.None
            );
            var selected = await EvaluateAsync<string>(session, "() => window.getSelection()?.toString() ?? ''");
            Assert.Equal("Three little words and more", selected.Trim());
        });
    }

    [Fact]
    public async Task Holds_the_modifiers_for_every_click()
    {
        await RunAsync("<button id=\"go\">Go</button>" + LogClicks, async session =>
        {
            await session.PerformAsync(
                await FindAsync(session, "button", "Go"),
                new LocatorAction.MultiTap(4, [KeyModifier.Shift]),
                CancellationToken.None
            );
            Assert.Equal(
                ["click:1+shift", "click:2+shift", "dblclick", "click:3+shift", "click:4+shift"],
                await EvaluateAsync<string[]>(session, "() => window.events")
            );
        });
    }
}
