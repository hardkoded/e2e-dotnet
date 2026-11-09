// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// Every selector engine the web engine registers, in a launched browser and in a CDP context alike (upstream's
/// <c>web/src/selector-engines.ts</c>).
/// </summary>
internal static class SelectorEngines
{
    /// <summary>The engines, each with the name selectors use and the source Playwright runs (upstream <c>SELECTOR_ENGINES</c>).</summary>
    public static readonly IReadOnlyList<(string Name, string Source)> All =
    [
        (PageScript.ClosedShadowSelectorEngine, PageScript.ClosedShadowSelectorEngineSource),
        (PageScript.SearchRootsSelectorEngine, PageScript.SearchRootsSelectorEngineSource),
        (LabelSelector.ExactLabelSelectorEngine, LabelSelector.ExactLabelSelectorEngineSource),
        (ReadSelector.ReadSelectorEngine, ReadSelector.ReadSelectorEngineSource),
        (ShownSelector.ShownSelectorEngine, ShownSelector.ShownSelectorEngineSource),
    ];
}
