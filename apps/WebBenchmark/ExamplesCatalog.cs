// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.Examples;

namespace WebBenchmark;

/// <summary>
/// Registry of benchmark examples (upstream's <c>examples</c> array in <c>app/src/examples.ts</c>; named <c>ExamplesCatalog</c> because
/// the namespace <c>WebBenchmark.Examples</c> holds the components). Two kinds of entries:
/// <list type="bullet">
/// <item>Task scenarios: self-contained flows to write tests against; each ends in a deterministic success state (data-testid
/// <c>success-message</c> where the DOM semantics are intact, on-screen success text where they are deliberately broken).</item>
/// <item>Bug-book scenarios (<see cref="Example.PlantedBug"/> set): flows with exactly one intentional, deterministic product bug. A test
/// asserting the correct behavior must fail here; the rest of the flow works so a pass is a missed bug, not a blockage. Never "fix" a planted bug.</item>
/// </list>
/// Every upstream entry is listed, in upstream's order, so the home page matches. An entry whose <see cref="Example.Component"/> is null is
/// not ported yet: its page answers 404 until it is.
/// </summary>
public static class ExamplesCatalog
{
    public static IReadOnlyList<Example> All { get; } =
    [
        new(typeof(LoginForm), "login-form", "Login Form",
            "Fill credentials, handle validation errors, reach the logged-in state."),
        new(null, "shadow-dom-form", "Shadow DOM Form",
            "Fill a form buried in nested open and closed shadow roots where CSS selectors cannot reach."),
        new(null, "canvas-only", "Canvas Only",
            "Complete a flow rendered entirely on a canvas element - no DOM, pure vision and coordinates."),
        new(null, "canvas-whiteboard", "Canvas Whiteboard",
            "Drag shapes into matching slots on a Figma-style whiteboard painted entirely on one canvas, then save."),
        new(typeof(DivSoup), "div-soup", "Div Soup",
            "Operate a login built from bare divs with no semantic roles, labels, or test ids."),
        new(typeof(AriaHiddenFlow), "aria-hidden-flow", "Aria Hidden Flow",
            "Complete a plan-selection flow whose entire subtree is aria-hidden - the a11y snapshot is empty."),
        new(null, "image-only-ui", "Image Only UI",
            "Follow instructions rendered as images and click icon-only buttons - zero readable text in the DOM."),
        new(null, "css-content-ui", "CSS Content UI",
            "Operate a flow whose every label comes from CSS pseudo-element content - the DOM is empty divs."),
        new(null, "lying-labels", "Lying Labels",
            "Ignore an accessibility tree that lies - shuffled aria-labels and swapped placeholders; visible text is truth."),
        new(null, "iframe-form", "Iframe Form",
            "Read a code from a nested iframe and submit it through a form in another iframe."),
        new(typeof(InfiniteScroll), "infinite-scroll", "Infinite Scroll",
            "Scroll a paginated feed until a target item deep in the list loads, then claim it."),
        new(null, "virtualized-table", "Virtualized Table",
            "Find a row deep in a windowed table where off-screen rows are never in the DOM."),
        new(typeof(OverlayTrap), "overlay-trap", "Overlay Trap",
            "Dismiss a cookie banner and an invisible click-intercepting overlay before the target button works."),
        new(typeof(StickyChrome), "sticky-chrome", "Sticky Chrome",
            "Scroll a far-down accept button clear of sticky header and footer chrome before clicking it."),
        new(null, "async-states", "Async States",
            "Wait out a skeleton load, catch a transient toast, and submit once the button finally enables."),
        new(null, "debounced-search", "Debounced Search",
            "Type into a debounced combobox and pick the target from a portal-rendered listbox."),
        new(typeof(HoverMenu), "hover-menu", "Hover Menu",
            "Navigate a hover-only nested dropdown menu to reach a buried action."),
        new(null, "context-menu-trap", "Context Menu Trap",
            "Rename the file report.pdf to summary.pdf in the file manager."),
        new(null, "native-dialogs", "Native Dialogs",
            "Handle a chain of native alert, confirm, and prompt dialogs to finish checkout."),
        new(null, "deferred-dialog", "Deferred Dialog",
            "Accept a confirm dialog that opens on a timer after the click has already settled."),
        new(null, "new-tab-flow", "New Tab Flow",
            "Open a second tab carrying a verification code, then return and enter it in the original tab."),
        new(null, "drag-and-drop", "Drag and Drop",
            "Drag the right items into a dropzone using pointer events in the correct order."),
        new(null, "kanban-board", "Kanban Board",
            "Move cards across a kanban board driven purely by pointer events with a drag threshold - HTML5 drag events do nothing."),
        new(null, "stale-dom", "Stale DOM",
            "Click a target in a list whose nodes are torn down and remounted every second - stale refs break."),
        new(null, "otp-auto-advance", "OTP Auto-Advance",
            "Enter a 6-digit code into segmented inputs that auto-advance focus per digit."),
        new(null, "file-round-trip", "File Round Trip",
            "Download a voucher file, then upload the same file back to verify its contents."),
        new(typeof(FilterDeepLink), "filter-deep-link", "Filter Deep Link",
            "Apply a list filter and verify the page URL carries it as a shareable query param."),
        new(null, "date-picker", "Date Picker",
            "Navigate a custom calendar widget across months to select an exact required date."),
        new(null, "onboarding-wizard", "Onboarding Wizard",
            "Fill a five-step wizard from a brief only visible on step 1 - long-horizon recall across unlabeled fields, custom dropdowns, a hostile date picker, steppers, and trap toggles."),
        new(null, "rich-text-editor", "Rich Text Editor",
            "Type into a contenteditable editor and apply bold formatting to an exact phrase."),
        new(null, "playbook-cleanup", "Playbook Cleanup",
            "Delete 'Launch Approval Draft', confirm removal, then search for its exact name to verify it is gone."),
        new(null, "price-sorting", "Price Sorting",
            "Sort a small product grid by price and check the resulting order.",
            PlantedBug: "Price sorting compares original prices instead of sale prices, so discounted products land in the wrong position (e.g. the $49 backpack sorts as $120)."),
        new(typeof(CartTotals), "cart-totals", "Cart Totals",
            "Adjust item quantities in a shopping cart and check the order summary math.",
            PlantedBug: "The order summary is computed once from the initial quantities and never updates - changing a quantity updates the line total but subtotal and order total stay stale."),
        new(typeof(CheckoutReview), "checkout-review", "Checkout Review",
            "Review a prefilled order summary page and place the order.",
            PlantedBug: "The greeting shows a raw {{firstName}} template token and the delivery estimate reads 'Arrives by undefined'. Placing the order still works."),
        new(null, "saved-addresses", "Saved Addresses",
            "Manage an address book: add a new address and remove a saved one.",
            PlantedBug: "Deleting an address shows an 'Address deleted' toast but never removes the address from the list, even on retry. Adding addresses works."),
        new(null, "newsletter-signup", "Newsletter Signup",
            "Subscribe to a newsletter and confirm the signup feedback.",
            PlantedBug: "The Subscribe button's handler crashes (console TypeError) and renders no confirmation or error UI - subscribing is silently impossible."),
        new(null, "order-history", "Order History",
            "Open the account's orders section and review past orders.",
            PlantedBug: "The Orders tab is stuck on 'Loading your orders...' forever while Overview and Settings work - a broken feature inside a working app (product bug, not an environment blocker)."),
        new(typeof(ProductReviews), "product-reviews", "Product Reviews",
            "Read a product page and its customer reviews.",
            PlantedBug: "Every review on the running-shoe page is clearly about a coffee grinder - the wrong product's reviews are wired to the page."),
        new(null, "wishlist", "Wishlist",
            "Save products to a wishlist and check they appear on the wishlist tab.",
            PlantedBug: "The wishlist tab badge counts saved items correctly, but the tab itself always renders the 'No saved items yet.' empty state - saved products are lost."),
        new(null, "promo-storefront", "Promo Storefront",
            "Browse a promo-heavy storefront, handle its overlays, and complete a purchase."),
        new(null, "gift-card-purchase", "Gift Card Purchase",
            "Pick a gift card amount, enter a recipient, and complete the purchase."),
        new(null, "control-inventory", "Control Inventory",
            "The deterministic contract surface: plain controls with every state exposed, one exercise per agent verb. Not a hard surface."),

    ];

    /// <summary>The scenario served at <c>/e/&lt;slug&gt;</c>, or null (upstream's <c>examples.find</c>).</summary>
    public static Example? Find(string slug) => All.FirstOrDefault(entry => entry.Slug == slug);
}
