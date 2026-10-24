// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.Flows;

/// <summary>
/// Locator-only runs through the scenarios whose semantics survive in the UI Automation tree: every control
/// carries a test id, so no model is needed to finish them. Each test is the deterministic floor the agentic
/// suite's <c>Agent.ActAsync</c> must match.
/// </summary>
public sealed class FlowsTests : BenchmarkTest
{
    [Test]
    public async Task Text_inputs_unlock_the_submit_button()
    {
        await OpenScenarioAsync("Text Input Variations");
        var submit = Screen.GetByTestId("submit-button");
        await Expect.That(submit).ToBeDisabledAsync();
        await Screen.GetByTestId("username-input").FillAsync("tester");
        await Screen.GetByTestId("pin-input").FillAsync("1234");
        await Screen.GetByTestId("notes-input").FillAsync("Notes long enough to count");
        await Expect.That(submit).ToBeEnabledAsync();
        await submit.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Form submitted");
    }

    [Test]
    public async Task Filled_text_inputs_report_their_values()
    {
        await OpenScenarioAsync("Text Input Variations");
        await Screen.GetByTestId("username-input").FillAsync("tester");
        await Expect.That(Screen.GetByTestId("username-input")).ToHaveValueAsync("tester");
        await Screen.GetByTestId("notes-input").FillAsync("Notes long enough to count");
        await Expect.That(Screen.GetByTestId("notes-input")).ToHaveValueAsync("Notes long enough to count");
    }

    // The dialog is the desktop's own alert: a ContentDialog, whose buttons are in the app's tree.
    [Test]
    public async Task Modal_flow_confirms_through_the_native_alert()
    {
        await OpenScenarioAsync("Modal Flow");
        await Screen.GetByTestId("open-modal-button").TapAsync();
        await Screen.GetByTestId("continue-button").TapAsync();
        await Screen.GetByRole("button", new System.Text.RegularExpressions.Regex("^confirm$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Flow completed");
    }

    // The tab buttons carry the scenario's test ids. The active tab shows in the content it reveals.
    [Test]
    public async Task Bottom_tabs_act_inside_the_last_tab()
    {
        await OpenScenarioAsync("Bottom Tabs");
        await Expect.That(Screen.GetByTestId("home-tab-content")).ToBeVisibleAsync();
        var home = Screen.GetByTestId("tab-home");
        var actions = Screen.GetByTestId("tab-actions");
        await Expect.That(home).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("complete-action-button")).ToHaveCountAsync(0);
        await actions.TapAsync();
        await Expect.That(Screen.GetByTestId("complete-action-button")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("home-tab-content")).ToHaveCountAsync(0);
        await Screen.GetByTestId("complete-action-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Action completed");
    }

    [Test]
    public async Task Error_recovery_retries_then_confirms_the_delete()
    {
        await OpenScenarioAsync("Error Recovery");
        await Expect.That(Screen.GetByTestId("error-banner")).ToBeVisibleAsync();
        await Screen.GetByTestId("retry-button").TapAsync();
        await Screen.GetByTestId("delete-draft").TapAsync();
        await Screen.GetByTestId("confirm-delete").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Draft deleted");
    }

    [Test]
    public async Task Choice_controls_place_the_exact_order()
    {
        await OpenScenarioAsync("Choice Controls");
        await Expect.That(Screen.GetByTestId("place-order")).ToBeDisabledAsync();
        await Screen.GetByTestId("size-medium").TapAsync();
        await Screen.GetByTestId("topping-cheese").TapAsync();
        await Screen.GetByTestId("topping-olives").TapAsync();
        await Screen.GetByTestId("rush-delivery").TapAsync();
        await Expect.That(Screen.GetByTestId("place-order")).ToBeEnabledAsync();
        await Screen.GetByTestId("place-order").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Order placed: Medium with Cheese, Olives (rush)");
    }

    // The results list is replaced by a spinner while the fetch runs, so the result row appears once it loads.
    [Test]
    public async Task Debounced_search_selects_the_target_once_results_arrive()
    {
        await OpenScenarioAsync("Debounced Search");
        await Screen.GetByTestId("search-input").FillAsync("benchmark target");
        await Screen.GetByTestId("search-input").PressAsync("Enter");
        await Screen.GetByTestId("result-Benchmark Target").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Selected Benchmark Target");
    }
}
