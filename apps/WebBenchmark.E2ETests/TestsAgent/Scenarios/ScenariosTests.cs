// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark.E2ETests.TestsAgent.Scenarios;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests-agent/scenarios.e2e.ts</c> (no <c>describe</c>). Each scenario test gives the
/// catalog description as its goal and reads the rest off the page; the deterministic check on the scenario's success message decides
/// the test. The scenarios that need pixels, native dialogs, a second tab, or a downloaded file are left out upstream, and so here.
/// </summary>
public sealed class ScenariosTests : TestsAgentTest
{
    [Test, Category("shadow-dom-form")]
    public Task Act_completes_shadow_dom_form() =>
        CompleteAsync(
            "shadow-dom-form",
            "fill the form buried in the nested shadow roots and submit it to get access",
            "Access granted");

    [Test, Category("lying-labels")]
    public Task Act_completes_lying_labels() =>
        CompleteAsync(
            "lying-labels",
            "send the payment; the visible text on the controls is the truth, the accessible names are not",
            "Payment sent");

    [Test, Category("iframe-form")]
    public Task Act_completes_iframe_form() =>
        CompleteAsync(
            "iframe-form",
            "read the coupon code shown in one frame and apply it through the form in the other frame",
            "Coupon applied");

    [Test, Category("infinite-scroll")]
    public Task Act_completes_infinite_scroll() =>
        // Item 138 of 200 arrives after seven lazy loads; that is about thirty scrolls, more than the default action budget of 25.
        CompleteAsync(
            "infinite-scroll",
            "scroll the feed until the Golden Ticket loads, then claim it",
            "Golden Ticket claimed",
            maxSteps: 60);

    [Test, Category("virtualized-table")]
    public Task Act_completes_virtualized_table() =>
        CompleteAsync(
            "virtualized-table",
            "scroll the windowed table until the Golden Row (row 4322) is rendered, then claim it",
            "Golden Row claimed");

    [Test, Category("overlay-trap")]
    public Task Act_completes_overlay_trap() =>
        CompleteAsync(
            "overlay-trap",
            "dismiss the cookie banner and the overlay in the way, then press Continue",
            "You made it past the overlay");

    [Test, Category("sticky-chrome")]
    public Task Act_completes_sticky_chrome() =>
        CompleteAsync(
            "sticky-chrome",
            "scroll down to the accept button and accept the terms",
            "Terms accepted");

    [Test, Category("async-states")]
    public Task Act_completes_async_states() =>
        CompleteAsync(
            "async-states",
            "wait for the page to finish loading, read the verification code it shows, enter it, and submit once the button enables",
            "Code verified successfully");

    [Test, Category("debounced-search")]
    public Task Act_completes_debounced_search() =>
        CompleteAsync(
            "debounced-search",
            "search for trail mix and add exactly the item the page asks for to the cart",
            "Added Trail Mix 500 g to the cart");

    [Test, Category("hover-menu")]
    public Task Act_completes_hover_menu() =>
        CompleteAsync(
            "hover-menu",
            "find Redeem voucher under the Account menu and redeem the voucher",
            "Voucher redeemed");

    [Test, Category("drag-and-drop")]
    public Task Act_completes_drag_and_drop() =>
        CompleteAsync(
            "drag-and-drop",
            "drag the right items into the dropzone in the order the page asks for",
            "Items dropped in the right order");

    [Test, Category("kanban-board")]
    public Task Act_completes_kanban_board() =>
        CompleteAsync(
            "kanban-board",
            "move the cards so the board matches the goal layout the page describes, then submit the board",
            "Board matches the goal");

    [Test, Category("context-menu-trap")]
    public Task Act_completes_context_menu_trap() =>
        CompleteAsync(
            "context-menu-trap",
            "rename the file report.pdf to summary.pdf in the file manager",
            "File renamed to summary.pdf");

    [Test, Category("stale-dom")]
    public Task Act_completes_stale_dom() =>
        CompleteAsync(
            "stale-dom",
            "click the Tap me button three times; the list rebuilds itself every second",
            "Target clicked 3 times");

    [Test, Category("otp-auto-advance")]
    public Task Act_completes_otp_auto_advance() =>
        CompleteAsync(
            "otp-auto-advance",
            "enter the 6-digit code the page shows into the segmented code inputs and verify it",
            "Code verified");

    [Test, Category("date-picker")]
    public Task Act_completes_date_picker() =>
        CompleteAsync(
            "date-picker",
            "book an appointment for the exact date the page requires, navigating the calendar to it",
            "Appointment booked for September 17, 2026");

    [Test, Category("rich-text-editor")]
    public Task Act_completes_rich_text_editor() =>
        CompleteAsync(
            "rich-text-editor",
            "type \"release approved\" into the editor, make exactly the word approved bold, and check the document",
            "Document approved");

    [Test, Category("playbook-cleanup")]
    public Task Act_completes_playbook_cleanup() =>
        CompleteAsync(
            "playbook-cleanup",
            "delete the playbook named \"Launch Approval Draft\", confirm the removal, then search for its exact name to verify it is gone",
            "No playbooks match \"Launch Approval Draft\".");

    [Test, Category("promo-storefront")]
    public Task Act_completes_promo_storefront() =>
        CompleteAsync(
            "promo-storefront",
            "buy any one product, dismissing the promotional overlays that get in the way, until the order is confirmed",
            "Order confirmed");

    [Test, Category("gift-card-purchase")]
    public Task Act_completes_gift_card_purchase() =>
        CompleteAsync(
            "gift-card-purchase",
            "buy a gift card for friend@example.com and complete the purchase",
            "Gift card sent to friend@example.com");

    [Test]
    public async Task Act_filters_the_list_and_the_URL_carries_the_filter()
    {
        await App.OpenAsync("/e/filter-deep-link");
        await Agent.ActAsync("filter the product list down to the mug");
        await Expect.That(Browser).ToHaveURLAsync("/e/filter-deep-link?q=mug");
    }

    /// <summary>Opens a scenario, has the agent complete it, and checks its success message (upstream's <c>complete</c>).</summary>
    private async Task CompleteAsync(string slug, string goal, string success, int? maxSteps = null)
    {
        await App.OpenAsync($"/e/{slug}");
        // Upstream's act has no step deadline of its own (its test timeout bounds it). This port's default act deadline is 30 s, which a
        // model-driven scroll exceeds, so the act gets the five-minute test budget as its deadline.
        await Agent.ActAsync(
            $"Complete this scenario as the page instructs: {goal}",
            new ActOptions { Timeout = TimeSpan.FromMinutes(5), MaxSteps = maxSteps });
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync(success);
    }
}
