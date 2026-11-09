// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// The selector engine a <c>visible</c> query narrows through inside Playwright's own chain, before an index, a filter, or an enclosing
/// scope runs (upstream's <c>web/src/shown-selector.ts</c>). It keeps the query root when the reader's hidden state says shown, so a node
/// the chain retains is one <c>toBeVisible()</c> accepts by construction. Playwright's <c>filter({ visible: true })</c> could not stand in
/// for it: it reads text under a <c>display: contents</c> element with <c>visibility: hidden</c> as visible, which paints nothing.
/// </summary>
internal static class ShownSelector
{
    /// <summary>Playwright selector engine name; <c>e2e-shown=</c> resolves to the query root when it reads as shown (upstream's <c>SHOWN_SELECTOR_ENGINE</c>).</summary>
    public const string ShownSelectorEngine = "e2e-shown";

    /// <summary>The selector step that keeps a match only when it reads as shown (upstream's <c>SHOWN_SELECTOR</c>).</summary>
    public const string Selector = ShownSelectorEngine + "=";

    /// <summary>
    /// The engine behind <c>e2e-shown=</c>: the query root itself when the reader's hidden mode answers shown, else nothing
    /// (<see cref="ReadSemantics.HiddenMode"/>). The reader is embedded as source because a selector engine, like the reader, is one
    /// self-contained page function. Upstream's <c>SHOWN_SELECTOR_ENGINE_SOURCE</c>.
    /// </summary>
    public static readonly string ShownSelectorEngineSource = $$"""
        () => {
        {{ReadSemantics.Prelude}}
        {{ReadSemantics.HiddenMode}}
          const queryAll = (root) => (root.nodeType === 1 && shownInTree(root) ? [root] : []);
          return { queryAll, query: (root) => queryAll(root)[0] ?? null };
        }
        """;
}
