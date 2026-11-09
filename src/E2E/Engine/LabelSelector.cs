// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;

namespace E2E.Engine;

/// <summary>
/// The selector engine an exact label query resolves through, standing alone and inside Playwright's own chain (a scope for child queries,
/// a <c>has</c> filter) alike (upstream's <c>web/src/label-selector.ts</c>): it runs the reader on every labelable control under the query
/// root and keeps the ones labelled with the pattern, in the one task that finds them. A control the page replaces a frame later is then no
/// candidate of a query for another label and cannot make it re-resolve. Playwright's <c>getByLabel</c> could not stand in for it: its
/// exact form reads a label's aria-hidden text and misses <c>Display name*</c>, and its substring form accepts <c>Last Name</c> for <c>Name</c>.
/// </summary>
internal static class LabelSelector
{
    /// <summary>Playwright selector engine name; <c>e2e-label=&lt;json&gt;</c> matches the controls one exact label names (upstream's <c>EXACT_LABEL_SELECTOR_ENGINE</c>).</summary>
    public const string ExactLabelSelectorEngine = "e2e-label";

    // Every control getByLabel can name: labelable form controls plus anything carrying its own label attributes. The candidates of an
    // exact label query; the engine below keeps those whose labels match (upstream's LABELABLE_SELECTOR).
    private const string LabelableSelector =
        "button, input:not([type=\"hidden\"]), textarea, select, meter, output, progress, [aria-label], [aria-labelledby]";

    // As JSON.stringify writes it: a quote or a backslash escaped, no other character (the default encoder also escapes < > & ' +).
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The engine behind <c>e2e-label=&lt;json&gt;</c>: the labelable controls under the query root, across the closed shadow roots recorded
    /// under it, whose labels as the reader names them (<c>labelsOf</c>, upstream's <c>RawNodeData.labels</c>) include the body's value
    /// once both are whitespace-normalized, the comparison the locator resolver makes with <c>TextMatch.Matches</c> on an exact string
    /// pattern. The reader is embedded as source (<see cref="ReadSemantics.Prelude"/>) because a selector engine, like the reader, is one
    /// self-contained page function. Upstream's <c>EXACT_LABEL_SELECTOR_ENGINE_SOURCE</c>.
    /// </summary>
    public static readonly string ExactLabelSelectorEngineSource = $$"""
        () => {
        {{PageScript.ClosedShadowHelpersSource}}
        {{ReadSemantics.Prelude}}
          const normalize = (text) => text.replace(/\s+/gu, " ").trim();
          const queryAll = (root, selector) => {
            const body = JSON.parse(selector);
            const expected = normalize(body.value);
            const candidates = matchesIn(root, {{JsonSerializer.Serialize(LabelableSelector, Json)}}, []);
            for (const closed of closedRootsUnder(root, [])) matchesIn(closed, {{JsonSerializer.Serialize(LabelableSelector, Json)}}, candidates);
            return candidates.filter((el) => labelsOf(el).some((label) => normalize(label) === expected));
          };
          return { queryAll, query: (root, selector) => queryAll(root, selector)[0] ?? null };
        }
        """;

    /// <summary>
    /// The selector for the controls whose label reads exactly <c>body.Value</c> (upstream's <c>exactLabelSelector</c>). The body is JSON:
    /// Playwright's selector parser keeps a quoted region whole, so the quotes and <c>&gt;&gt;</c> a label may contain travel inside the
    /// JSON strings.
    /// </summary>
    public static string ExactLabelSelector(ExactLabelSelectorBody body) =>
        ExactLabelSelectorEngine + "=" + JsonSerializer.Serialize(body, Json);
}

/// <summary>What the <c>e2e-label</c> selector body carries into the page: the label to equal and the reader's options (upstream's <c>ExactLabelSelectorBody</c>).</summary>
internal sealed record ExactLabelSelectorBody(string Value, string TestIdAttribute, string SecureFieldSelector);
