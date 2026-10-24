// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.Examples;

namespace WinAppBenchmark.App;

/// <summary>
/// One benchmark scenario: the name the home list and the screen title show, the description that is
/// the agentic suite's goal, and the component that renders it. <paramref name="HeaderShown"/> is
/// false for the one scenario that draws its own chrome.
/// </summary>
public sealed record Example(string Name, string Description, Func<Element> Component, bool HeaderShown = true);

/// <summary>
/// Registry of benchmark examples, the port of upstream's <c>src/examples.ts</c>. Each entry is a
/// self-contained scenario the QA agent must complete; every scenario ends in a deterministic success
/// state (AutomationId "success-message" where the UI Automation tree is intact, on-screen success
/// text where it is deliberately hidden). Scenarios upstream builds on a mobile-only surface (the OS
/// permission dialog, the photo picker, Stripe's sheet, Apple Pay, a WebView) are not here.
/// </summary>
public static class Examples
{
    /// <summary>The home list's title and route.</summary>
    public const string HomeTitle = "Benchmark Examples";

    public static readonly IReadOnlyList<Example> All =
    [
        new("Login Form", "Fill credentials, handle validation errors, reach the logged-in state.", Component<LoginForm>),
        new("Infinite Scroll List", "Scroll a paginated list until a target item deep in the list is found.", Component<InfiniteScrollList>),
        new("Modal Flow", "Open a modal, confirm a dialog, and land on the success state.", Component<ModalFlow>),
        new("Bottom Tabs", "Navigate between tabs and complete an action on the last tab.", Component<BottomTabs>, HeaderShown: false),
        new("Text Input Variations", "Fill secure, multiline, and numeric inputs to unlock the submit button.", Component<TextInputs>),
        new("Broken Accessibility", "Whole screen hidden from the UI Automation tree: the snapshot is useless, vision required.", Component<BrokenAccessibility>),
        new("Vision Only", "UI Automation tree fully hidden like a canvas-rendered app: a pure pixel-driven flow.", Component<VisionOnly>),
        new("Gestures", "Long-press, swipe left, and double-tap targets in sequence.", Component<Gestures>),
        new("Async States", "Wait out a loading screen, refresh, and catch a transient toast.", Component<AsyncStates>),
        new("Debounced Search", "Type into a debounced search field, wait for async results, select the target.", Component<DebouncedSearch>),
        new("Huge Virtualized List", "600 preloaded virtualized rows: the target sits far down and off-screen rows never enter the UI Automation tree, so the agent must size long scroll jumps from the scroll extent.", Component<HugeVirtualizedList>),
        new("Flattened Registration Form", "Multi-field registration form hidden from the UI Automation tree: fields must be focused from vision and typed into via focused typing.", Component<FlattenedForm>),
        new("Flattened Login", "Login form hidden from the UI Automation tree: credentials must be filled from vision via focused fill, success visible only in pixels.", Component<FlattenedLogin>),
        new("Sticky Chrome Target", "Accept button about three screens down in a non-virtualized scroll (present as an off-screen ref) under a sticky footer: tapping it must scroll it clear of the footer.", Component<StickyChrome>),
        new("Product Catalog", "Disambiguate near-identical cards, drill into Trail Mix 500 g, set quantity 2, add to cart, review it.", Component<ProductCatalog>),
        new("Error Recovery", "Recover from a failed load via retry, then delete a draft through a destructive-confirm modal.", Component<ErrorRecovery>),
        new("Choice Controls", "Select the exact size, toppings, and switch combination, then place the order.", Component<ChoiceControls>),
        new("Sequential Onboarding", "Multi-step signup wizard where each step auto-focuses a field and the Continue button sits at the bottom; the success screen echoes the entered values exactly.", Component<SequentialOnboarding>),
        new("Control Inventory", "The deterministic contract surface: plain controls with every state exposed, one exercise per runner verb. Not a hard surface.", Component<ControlInventory>),
    ];

    /// <summary>The example named <paramref name="name"/>, or null for the home route.</summary>
    public static Example? Find(string name) => All.FirstOrDefault(example => example.Name == name);
}
