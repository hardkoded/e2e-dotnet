// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// Upstream's <c>agent/reading.ts</c>: reading a screen for a text the agent pages toward. Which node reads it, and
/// what the screen reads as a whole, for telling a page that moved from one that did not. Upstream's <c>nodeReading</c>
/// is <see cref="NodeReading"/> and its <c>readingShape</c> is <see cref="ReadingShape"/>. Upstream's
/// <c>normalizeReading</c> is <see cref="TextRules.Normalize"/>, which <see cref="TextRules.Matches"/> applies.
/// </summary>
internal static class Reading
{
    /// <summary>
    /// The innermost visible node whose redacted name or text contains <paramref name="text"/>. With a
    /// <paramref name="scope"/>, the list being paged, only its descendants count. The port keeps the raw nodes, so the
    /// <paramref name="redactor"/> is applied to each field as it is read (see <see cref="RedactField"/>).
    /// </summary>
    public static SemanticNode? NodeReading(Observation observation, string text, Redactor redactor, SemanticNode? scope = null) =>
        Innermost(scope is null ? observation.Roots : scope.Children, text, redactor);

    /// <summary>What a screen reads and where, for telling a page that moved from one that did not: the tree, and the scroll position under it.</summary>
    public static string ReadingShape(Observation observation) =>
        // The tree can stay the same while the page moves under it (every node already fits the budget), so the scroll position counts too.
        SnapshotText.Render(observation, Redactor.None) + "\n" + observation.ScrollPosition;

    private static SemanticNode? Innermost(IReadOnlyList<SemanticNode> nodes, string text, Redactor redactor)
    {
        foreach (var node in nodes)
        {
            if (node.States.Hidden || node.States.AriaHidden)
            {
                continue;
            }

            var inner = Innermost(node.Children, text, redactor);
            if (inner is not null)
            {
                return inner;
            }

            if (TextRules.Matches(RedactField(node.Name, ObservationLimits.Name, redactor), text, exact: false) || TextRules.Matches(RedactField(node.Text, ObservationLimits.Text, redactor), text, exact: false))
            {
                return node;
            }
        }

        return null;
    }
}
