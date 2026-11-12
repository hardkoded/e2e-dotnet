// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Tests.ReadNodeChecked;

[Collection(BrowserCollection.Name)]
public sealed class CheckedAndSelectedTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    [Fact]
    public async Task Lists_closed_select_choices_but_excludes_explicitly_hidden_options_and_optgroups()
    {
        await chromium.Page.SetContentAsync("""
            <select aria-label="Plan">
              <option data-testid="visible" selected value="internal-token">Visible</option>
              <option data-testid="disabled" disabled>Disabled</option>
              <option data-testid="hidden" hidden>Hidden</option>
              <option data-testid="display" style="display:none">No display</option>
              <option data-testid="visibility" style="visibility:hidden">No visibility</option>
              <option data-testid="aria" aria-hidden="true">No aria</option>
              <option data-testid="inert" style="interactivity:inert">Inert</option>
              <optgroup hidden label="Hidden group"><option data-testid="group">Hidden group choice</option></optgroup>
              <optgroup style="visibility:hidden" label="Restored group"><option data-testid="restored" style="visibility:visible">Restored</option></optgroup>
            </select>
            """);
        var nodes = await chromium.CaptureAsync();
        var visible = Node(nodes, "visible");
        Assert.Equal("Visible", visible.Name);
        Assert.True(visible.Selected);
        Assert.Null(visible.Value);
        Assert.False(visible.Hidden);
        var disabled = Node(nodes, "disabled");
        Assert.Equal("Disabled", disabled.Name);
        Assert.True(disabled.Disabled);
        Assert.Contains(nodes, node => node.TestId == "restored");
        Assert.Contains(nodes, node => node.TestId == "visibility");
        foreach (var id in new[] { "hidden", "display", "aria", "group", "inert" })
        {
            Assert.DoesNotContain(nodes, node => node.TestId == id);
        }

        Assert.Equal(["Visible", "Disabled", "No visibility", "Inert", "Restored"], await chromium.Page.GetByRole(AriaRole.Option).AllTextContentsAsync());
    }

    [Fact]
    public async Task Applies_the_option_limit_to_offered_choices_after_hidden_options_are_excluded()
    {
        var excluded = string.Concat(Enumerable.Repeat("<option hidden>Excluded</option>", 60));
        var choices = string.Concat(Enumerable.Range(0, 61).Select(index => $"<option data-testid=\"choice-{index}\">Choice {index}</option>"));
        await chromium.Page.SetContentAsync($"<select aria-label=\"Many choices\">{excluded}{choices}</select>");
        var nodes = await chromium.CaptureAsync();
        Assert.Equal("Choice 0", Node(nodes, "choice-0").Name);
        Assert.Equal("Choice 59", Node(nodes, "choice-59").Name);
        Assert.DoesNotContain(nodes, node => node.TestId == "choice-60");
        Assert.Equal(60, nodes.Count(node => node.Role == "option"));
        Assert.Equal(61, await chromium.Page.GetByRole(AriaRole.Option).CountAsync());
    }

    [Fact]
    public async Task Reads_selected_boolean_values_without_case_sensitivity_while_native_option_state_wins()
    {
        await chromium.Page.SetContentAsync("""
            <div role="tablist">
              <button role="tab" aria-selected="TRUE" data-testid="upper">All</button>
              <button role="tab" aria-selected="TrUe" data-testid="mixed">Recent</button>
              <button role="tab" aria-selected="FALSE" data-testid="off">Archived</button>
            </div>
            <select aria-label="Plan" size="3">
              <option data-testid="native-on" selected aria-selected="FALSE">Team</option>
              <option data-testid="native-off" aria-selected="TRUE">Solo</option>
            </select>
            <div role="button" data-testid="embedded">Pick <div role="listbox"><div role="option" aria-selected="true">Chosen</div><div role="option" aria-selected="false">Other</div></div></div>
            """);
        var nodes = await chromium.CaptureAsync();
        Assert.True(Node(nodes, "upper").Selected);
        Assert.True(Node(nodes, "mixed").Selected);
        Assert.False(Node(nodes, "off").Selected);
        Assert.True(Node(nodes, "native-on").Selected);
        Assert.False(Node(nodes, "native-off").Selected);
        Assert.Equal("Pick Chosen", Node(nodes, "embedded").Name);
        Assert.Equal(["All", "Recent"], await chromium.Page.GetByRole(AriaRole.Tab, new() { Selected = true }).AllTextContentsAsync());
        Assert.Equal(1, await chromium.Page.GetByRole(AriaRole.Button, new() { Name = "Pick Chosen", Exact = true }).CountAsync());
    }

    [Fact]
    public async Task Excludes_uppercase_hidden_subtrees_from_observations_and_names_but_reads_hidden_references_whole()
    {
        await chromium.Page.SetContentAsync("""
            <div aria-hidden="TRUE"><button data-testid="hidden" aria-hidden="false">Hidden action</button></div>
            <button data-testid="save">Save<span aria-hidden="TrUe"> decoration</span></button>
            <button data-testid="shown" aria-hidden="FALSE">Shown</button>
            <button data-testid="referenced" aria-labelledby="reference">Fallback</button>
            <div aria-hidden="TRUE"><span id="reference">Label <i style="display:none">whole</i></span></div>
            """);
        var nodes = await chromium.CaptureAsync();
        Assert.True(Node(nodes, "hidden").AriaHidden);
        Assert.Equal("Save", Node(nodes, "save").Name);
        Assert.Equal("Shown", Node(nodes, "shown").Name);
        Assert.Equal("Label whole", Node(nodes, "referenced").Name);
        Assert.Equal(1, await chromium.Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).CountAsync());
        Assert.Equal(0, await chromium.Page.GetByRole(AriaRole.Button, new() { Name = "Hidden action", Exact = true }).CountAsync());
        Assert.Equal(1, await chromium.Page.GetByRole(AriaRole.Button, new() { Name = "Label whole", Exact = true }).CountAsync());
    }

    [Fact]
    public async Task Reads_a_native_checkbox_radio_and_option_from_the_control_even_when_a_stale_aria_attribute_disagrees_as_Playwright_does()
    {
        await chromium.Page.SetContentAsync("""
            <input type="checkbox" data-testid="stale-off" aria-checked="false" checked aria-label="Stale off">
            <input type="checkbox" data-testid="stale-on" aria-checked="true" aria-label="Stale on">
            <input type="radio" name="r" data-testid="radio-stale-off" aria-checked="false" checked aria-label="Radio stale off">
            <input type="radio" name="r2" data-testid="radio-stale-on" aria-checked="true" aria-label="Radio stale on">
            <input type="checkbox" data-testid="plain-on" checked aria-label="Plain on">
            <input type="checkbox" data-testid="plain-off" aria-label="Plain off">
            <div role="checkbox" aria-checked="true" data-testid="aria-on" tabindex="0">Aria on</div>
            <div role="switch" aria-checked="false" data-testid="aria-off" tabindex="0">Aria off</div>
            <select data-testid="select" aria-label="Plan" size="3">
              <option data-testid="option-stale-off" aria-selected="false" selected>Team</option>
              <option data-testid="option-stale-on" aria-selected="true">Solo</option>
            </select>
            <div role="tablist"><button role="tab" aria-selected="true" data-testid="tab">All</button></div>
            """);
        var nodes = await chromium.CaptureAsync();

        Assert.True(Node(nodes, "stale-off").Checked);
        Assert.False(Node(nodes, "stale-on").Checked);
        Assert.True(Node(nodes, "radio-stale-off").Checked);
        Assert.False(Node(nodes, "radio-stale-on").Checked);
        Assert.True(Node(nodes, "plain-on").Checked);
        Assert.False(Node(nodes, "plain-off").Checked);
        Assert.True(Node(nodes, "aria-on").Checked);
        Assert.False(Node(nodes, "aria-off").Checked);
        Assert.True(Node(nodes, "option-stale-off").Selected);
        Assert.False(Node(nodes, "option-stale-on").Selected);
        Assert.True(Node(nodes, "tab").Selected);

        foreach (var testId in new[] { "stale-off", "stale-on", "radio-stale-off", "radio-stale-on", "plain-on", "plain-off", "aria-on", "aria-off" })
        {
            Assert.True(Node(nodes, testId).Checked == await chromium.Page.GetByTestId(testId).IsCheckedAsync(), testId);
        }
    }

    [Fact]
    public async Task Lists_a_checkbox_or_radio_its_own_label_draws_over_no_box_as_shown_with_its_checked_state()
    {
        await chromium.Page.SetContentAsync("""
            <style>.shrunk { opacity: 0; width: 0; height: 0; margin: 0; position: absolute }</style>
            <label><input class="shrunk" type="checkbox" data-testid="agree" checked>Agree</label>
            <label><input class="shrunk" type="radio" data-testid="large">Large</label>
            <input class="shrunk" type="checkbox" id="unlabelled" data-testid="hidden-label"><label for="unlabelled" style="display:none">Gone</label>
            """);
        var nodes = await chromium.CaptureAsync();
        var agree = Node(nodes, "agree");
        Assert.Equal(("checkbox", "Agree", true), (agree.Role, agree.Name, agree.Checked));
        var large = Node(nodes, "large");
        Assert.Equal(("radio", "Large", false), (large.Role, large.Name, large.Checked));
        foreach (var id in new[] { "agree", "large" })
        {
            Assert.False(Node(nodes, id).Hidden, id);
        }

        Assert.DoesNotContain(nodes, node => node.TestId == "hidden-label");
    }

    // Partial: upstream's `text` input (a box-less text field) is also left out of the observation. A box with no size is not hidden here
    // (COMPATIBILITY.md), so the port lists every box-less element but a checkbox or radio; this test checks the checkboxes.
    [Fact]
    public async Task Partial_Leaves_out_a_box_less_control_no_label_of_its_own_draws_in_a_way_a_tap_can_click()
    {
        await chromium.Page.SetContentAsync("""
            <style>.shrunk { opacity: 0; width: 0; height: 0; margin: 0; padding: 0; border: 0; position: absolute }</style>
            <label><input class="shrunk" type="checkbox" data-testid="invisible" style="visibility:hidden">Invisible</label>
            <skip-slot><input class="shrunk" type="checkbox" id="skipped" data-testid="skipped"></skip-slot>
            <label for="skipped">Skipped</label>
            <label style="display:contents"><input class="shrunk" type="checkbox" data-testid="contents"><span>Contents</span></label>
            <label><input class="shrunk" type="checkbox" data-testid="link">Accept <a href="#terms">the terms</a></label>
            <label role="button"><input class="shrunk" type="checkbox" data-testid="button-label">Button</label>
            <label role="link"><input class="shrunk" type="checkbox" data-testid="link-label">Link</label>
            <label contenteditable><input class="shrunk" type="checkbox" data-testid="editable-label">Editable</label>
            <script>
              customElements.define('skip-slot', class extends HTMLElement {
                connectedCallback() {
                  this.attachShadow({ mode: 'open' }).innerHTML = '<div style="content-visibility:hidden;width:50px;height:50px"><slot></slot></div>';
                }
              });
            </script>
            """);
        var nodes = await chromium.CaptureAsync();
        foreach (var id in new[] { "invisible", "skipped", "contents", "link", "button-label", "link-label", "editable-label" })
        {
            Assert.DoesNotContain(nodes, node => node.TestId == id);
        }
    }

    private static ReadNode Node(IReadOnlyList<ReadNode> nodes, string testId)
    {
        return nodes.Single(node => node.TestId == testId);
    }
}
