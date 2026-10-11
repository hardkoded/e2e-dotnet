// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentActVerbs.GesturesSession;

namespace E2E.Tests.AgentActVerbs;

/// <summary>
/// <c>agent.act</c> grammar verbs on the gestures page, driven by a scripted model. Upstream also runs hover,
/// the right-click and long-press variants, drag, upload, a repeated key press, and a hover at a bare point.
/// This port has no such tool. Upstream also checks each step's engine events, their detail line, the policy
/// events, and the changes a tool result lists. The port has no events, and its tool result lists the whole screen.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActGrammarVerbsTests
{
    [Fact]
    public async Task Dispatches_the_double_tap_variant_as_its_own_action()
    {
        var (result, model) = await RunOneAsync(DoubleTapFlow);

        Assert.Equal(1, result.Actions);
        var toolResult = ToolResults(model)[0];
        Assert.StartsWith("double-tapped button \"Tap me twice\"", toolResult, StringComparison.Ordinal);
        Assert.Contains("status \"Gesture state\" text=\"double-tapped\"", toolResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sets_a_checkbox_state_and_leaves_it_alone_when_it_already_holds()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "checks the box and leaves a checked box alone"));

        // Two check actions: the second finds the box already checked and flips nothing.
        Assert.Equal(2, result.Actions);
        var toolResults = ToolResults(model);
        Assert.StartsWith("checked checkbox \"Agree to terms\"", toolResults[0], StringComparison.Ordinal);
        Assert.Contains("status \"Gesture state\" text=\"agreed: true\"", toolResults[0], StringComparison.Ordinal);
        Assert.Contains("status \"Gesture state\" text=\"agreed: true\"", toolResults[1], StringComparison.Ordinal);
        Assert.DoesNotContain("failed", toolResults[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Records_a_check_whose_radio_the_pick_replaced_the_click_landed()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "checks a radio the pick replaces with its summary"));

        Assert.Equal(1, result.Actions);
        Assert.Contains("status \"Gesture state\" text=\"delivery: Express\"", ToolResults(model)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scrolls_a_listed_node_into_view_as_one_action()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "scrolls a listed node into view"));

        Assert.Equal(1, result.Actions);
        Assert.StartsWith("scrolled paragraph into view", ToolResults(model)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_a_windowed_list_to_a_row_by_its_text_as_one_action_and_brings_it_into_view()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "pages a windowed list to a row it has not rendered"));

        // Several pages of the list, one action of the budget.
        Assert.Equal(1, result.Actions);
        var toolResult = ToolResults(model)[0];
        Assert.StartsWith("scrolled down until \"Row 24\" was in view", toolResult, StringComparison.Ordinal);
        Assert.Contains("Row 24 · Golden", toolResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Goes_back_through_the_engine_session_and_reports_the_screen_it_returned_to()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "opens a page and comes back"));

        // The tap, then the back.
        Assert.Equal(2, result.Actions);
        var toolResults = ToolResults(model);
        Assert.Contains("heading \"About\"", toolResults[0], StringComparison.Ordinal);
        Assert.StartsWith("navigated back", toolResults[1], StringComparison.Ordinal);
        Assert.Contains("heading \"Gestures\"", toolResults[1], StringComparison.Ordinal);
    }

    private static async Task<(ActResult Result, ScriptedModel Model)> RunOneAsync(Flow flow)
    {
        using var site = await StartSiteAsync();
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        var model = ModelFor(flow);
        var result = await RunFlowAsync(site, flow, model, directory);
        return (result, model);
    }

    /// <summary>The tool results the model was sent, in order.</summary>
    private static List<string> ToolResults(ScriptedModel model)
    {
        return model.Requests[^1].Messages.Where(message => message.Role == "tool").Select(message => message.Content ?? "").ToList();
    }
}
