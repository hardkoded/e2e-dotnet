// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentAct;
using static E2E.Tests.AgentActVerbs.GesturesSession;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E.Tests.AgentActVerbs;

/// <summary>
/// <c>agent.act</c> grammar verbs on the gestures page, driven by a scripted model. Upstream also checks the changes a tool
/// result lists. The port's tool result lists the whole screen and names the node by its role and name where upstream names it by id.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActGrammarVerbsTests
{
    [Fact]
    public async Task Hovers_by_id_reports_the_control_the_hover_revealed_and_records_the_hover()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(HoverFlow);
        var (_, steps) = await RunFlowWithStepsAsync(site, HoverFlow, model, NewCacheDirectory());

        var actions = EngineEvents(steps);
        Assert.Equal(["hover:Passed", "tap:Passed"], actions.Select(item => item.Name + ":" + item.Status));
        Assert.Matches("^hover over \\S+ \"Account\"$", actions[0].Detail);
        Assert.Equal("tap button \"Redeem\"", actions[1].Detail);
        var second = CounterPage.ToolResults(model)[0];
        Assert.Matches("^Hovered over #e\\d+\\.", second);
        Assert.Contains("button \"Redeem\"", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatches_the_tap_variants_as_their_own_engine_actions()
    {
        using var site = await StartSiteAsync();
        var menuModel = ModelFor(RightClickFlow);
        var (_, menuSteps) = await RunFlowWithStepsAsync(site, RightClickFlow, menuModel, NewCacheDirectory());

        var menu = EngineEvents(menuSteps);
        Assert.Equal(["secondaryTap", "tap"], menu.Select(item => item.Name));
        Assert.Matches("^secondary-tap \\S+ \"report\\.pdf\"$", menu[0].Detail);
        Assert.Matches("^Right-clicked #e\\d+\\.", CounterPage.ToolResults(menuModel)[0]);

        var gesturesModel = ModelFor(DoubleTapAndLongPressFlow);
        var (_, gestureSteps) = await RunFlowWithStepsAsync(site, DoubleTapAndLongPressFlow, gesturesModel, NewCacheDirectory());

        var gestures = EngineEvents(gestureSteps);
        Assert.Equal(["doubleTap", "longPress"], gestures.Select(item => item.Name));
        Assert.Equal("double-tap button \"Tap me twice\"", gestures[0].Detail);
        Assert.Equal("long-press button \"Hold me\"", gestures[1].Detail);
        var toolResults = CounterPage.ToolResults(gesturesModel);
        Assert.Matches("^Double-tapped #e\\d+\\.", toolResults[0]);
        Assert.Contains("status \"Gesture state\" text=\"double-tapped\"", toolResults[0], StringComparison.Ordinal);
        Assert.Matches("^Long-pressed #e\\d+\\.", toolResults[1]);
    }

    [Fact]
    public async Task Drags_one_node_onto_another_and_records_both_ends()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(DragFlow);
        var (result, steps) = await RunFlowWithStepsAsync(site, DragFlow, model, NewCacheDirectory());

        Assert.Equal(1, result.Actions);
        var actions = EngineEvents(steps);
        Assert.Equal(["dragTo"], actions.Select(item => item.Name));
        Assert.Matches("^drag \\S+ \"Design review\" to \\S+ \"Done column\"$", actions[0].Detail);
        Assert.Matches("^Dragged #e\\d+ to #e\\d+\\.", CounterPage.ToolResults(model)[0]);
    }

    [Fact]
    public async Task Checks_a_box_twice_and_the_second_check_flips_nothing()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "checks the box and leaves a checked box alone"));

        // Two check actions: the second finds the box already checked and flips nothing.
        Assert.Equal(2, result.Actions);
        var toolResults = CounterPage.ToolResults(model);
        Assert.Matches("^Checked #e\\d+\\.", toolResults[0]);
        Assert.Matches("changed #\\S+ status \"Gesture state\" text=\"agreed: true\"", toolResults[0]);
        Assert.Contains("No listed node changed", toolResults[1], StringComparison.Ordinal);
        Assert.DoesNotContain("failed", toolResults[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Checks_a_radio_the_pick_replaced_in_one_action()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "checks a radio the pick replaces with its summary"));

        Assert.Equal(1, result.Actions);
        Assert.Contains("status \"Gesture state\" text=\"delivery: Express\"", CounterPage.ToolResults(model)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uploads_a_project_file()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(UploadFlow);
        var (result, steps) = await RunFlowWithStepsAsync(site, UploadFlow, model, NewCacheDirectory());

        Assert.Equal(1, result.Actions);
        var step = steps.Single(candidate => candidate.Api == "agent.act");
        var actions = step.Events.Where(item => item.Kind == StepEventKind.Engine).ToList();
        Assert.Equal(["setInputFiles"], actions.Select(item => item.Name));
        Assert.Matches("^upload \"fixtures/attachment\\.txt\" to button \"Attachment\"$", actions[0].Detail);
        var policy = Assert.Single(step.Events, item => item.Kind == StepEventKind.Policy);
        Assert.Equal(("upload.path", PolicyDecision.Allowed), (policy.Name, policy.Decision));
        Assert.Matches("^Uploaded \"fixtures/attachment\\.txt\" to #e\\d+\\.", CounterPage.ToolResults(model)[0]);
    }

    [Fact]
    public async Task Refuses_a_hidden_file()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(HiddenFileFlow);
        var (error, steps) = await RunFailingFlowAsync(site, HiddenFileFlow, model, NewCacheDirectory());

        Assert.Equal("POLICY_DENIED", error.Code);
        var step = steps.Single(candidate => candidate.Api == "agent.act");
        Assert.DoesNotContain(step.Events, item => item.Kind == StepEventKind.Engine && item.Name == "setInputFiles");
        var policy = Assert.Single(step.Events, item => item.Kind == StepEventKind.Policy);
        Assert.Equal(("upload.path", PolicyDecision.Denied, "POLICY_DENIED"), (policy.Name, policy.Decision, policy.Code));
        Assert.Equal(0, step.Metrics!.ActionSteps);
        // The refusal reaches the model as the action's failure, naming the path it refused.
        Assert.Matches("^upload \"\\.env\" to #e\\d+ failed: POLICY_DENIED: \"\\.env\" is", CounterPage.ToolResults(model)[0]);
    }

    [Fact]
    public async Task Scrolls_a_listed_node_into_view_in_one_action()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "scrolls a listed node into view"));

        Assert.Equal(1, result.Actions);
        Assert.Matches("^Scrolled into view #e\\d+\\.", CounterPage.ToolResults(model)[0]);
    }

    [Fact]
    public async Task Pages_a_windowed_list_to_a_row_by_its_text_in_one_action()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "pages a windowed list to a row it has not rendered"));

        // Several pages of the list, one action of the budget.
        Assert.Equal(1, result.Actions);
        var toolResult = CounterPage.ToolResults(model)[0];
        Assert.StartsWith("Scrolled down until \"Row 24\" was in view.", toolResult, StringComparison.Ordinal);
        Assert.Contains("Row 24 · Golden", toolResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Goes_back_and_shows_the_screen_it_returned_to()
    {
        var (result, model) = await RunOneAsync(ReplayFlows.Single(entry => entry.Title == "opens a page and comes back"));

        // The tap, then the back.
        Assert.Equal(2, result.Actions);
        var toolResults = CounterPage.ToolResults(model);
        Assert.Contains("heading \"About\"", toolResults[0], StringComparison.Ordinal);
        Assert.StartsWith("Navigated back.", toolResults[1], StringComparison.Ordinal);
        Assert.Contains("heading \"Gestures\"", toolResults[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeats_a_key_in_one_call_one_engine_action_per_press_and_shows_the_selection_it_made()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(SelectWordFlow);
        var (result, steps) = await RunFlowWithStepsAsync(site, SelectWordFlow, model, NewCacheDirectory());

        // The first press addresses the node; the seven after it ride the focus it took.
        var actions = EngineEvents(steps);
        Assert.Equal(["tap", "press", .. Enumerable.Repeat("pressKey", 7)], actions.Select(item => item.Name));
        Assert.Equal("press textbox \"Memo\"", actions[1].Detail);
        Assert.Equal(9, result.Actions);
        var toolResult = CounterPage.ToolResults(model)[1];
        Assert.Matches("^Pressed Shift\\+ArrowLeft 8 times on #e\\d+\\.", toolResult);
        Assert.Matches("textbox \"Memo\" .*value=\"release approved\" selection=\"approved\"", toolResult);
    }

    [Fact]
    public async Task Walks_focus_on_with_a_repeated_Tab_instead_of_returning_to_the_node_each_time()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(TabFlow);
        var (_, steps) = await RunFlowWithStepsAsync(site, TabFlow, model, NewCacheDirectory());

        var actions = EngineEvents(steps);
        Assert.Equal(["press", "pressKey"], actions.Select(item => item.Name));
        Assert.Equal("press checkbox \"Agree to terms\"", actions[0].Detail);
        var toolResult = CounterPage.ToolResults(model)[0];
        Assert.Matches("^Pressed Tab 2 times on #e\\d+\\.", toolResult);
        Assert.Matches("textbox \"Memo\" .*value=\"release approved\" selection=\"release approved\"", toolResult);
    }

    [Fact]
    public async Task Hovers_a_bare_point_through_the_engine_when_nothing_listed_is_a_control_there()
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(HoverAtFlow);
        var (_, steps) = await RunFlowWithStepsAsync(site, HoverAtFlow, model, NewCacheDirectory());

        var actions = EngineEvents(steps);
        Assert.Equal(["hoverAt", "tap"], actions.Select(item => item.Name));
        // The port words the event detail as `hover the point (x, y) on node ...`, where upstream's reads `hover over the point (x, y)`.
        Assert.Matches("^hover the point \\(140, 55\\)", actions[0].Detail);
        var third = CounterPage.ToolResults(model);
        Assert.Contains("Hovered over the point (140, 55); no listed control is there", third[1], StringComparison.Ordinal);
        Assert.Contains("button \"Redeem\"", third[1], StringComparison.Ordinal);
    }

    private static async Task<(ActResult Result, ScriptedModel Model)> RunOneAsync(Flow flow)
    {
        using var site = await StartSiteAsync();
        var model = ModelFor(flow);
        var result = await RunFlowAsync(site, flow, model, NewCacheDirectory());
        return (result, model);
    }

    private static List<StepEvent> EngineEvents(IReadOnlyList<StepRecord> steps) =>
        [.. steps.Single(candidate => candidate.Api == "agent.act").Events.Where(item => item.Kind == StepEventKind.Engine)];

    private static string NewCacheDirectory() => Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
}
