// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// Upstream's <c>engine/computed-style.ts</c> (.NET-only holder name: C# has no free functions, and <c>ComputedStyle</c> is the tests' namespace): the rendered node properties an engine's <c>readStyle</c> may be asked for. A property name
/// is a plain string (upstream's <c>ComputedStyleProperty</c> union of literals), and the values an engine returns are computed CSS strings
/// keyed by the properties requested (upstream's <c>ComputedStyleValues</c>, here <see cref="IReadOnlyDictionary{TKey, TValue}"/>).
/// </summary>
public static class ComputedStyles
{
    /// <summary>
    /// Upstream's <c>COMPUTED_STYLE_PROPERTIES</c>: the CSS properties whose computed values contain presentation data rather than URLs or
    /// generated content, in kebab case.
    /// </summary>
    public static IReadOnlyList<string> ComputedStyleProperties { get; } =
        Array.AsReadOnly([
            "color",
            "background-color",
            "font-size",
            "font-weight",
            "font-style",
            "text-align",
            "text-decoration-line",
            "display",
            "visibility",
            "opacity",
        ]);

    /// <summary>
    /// Upstream's <c>validateComputedStyleProperties</c>: checks, deduplicates, and copies the requested properties before any engine or page
    /// access. An empty list, or a property outside <see cref="ComputedStyleProperties"/>, is <c>INVALID_ARGUMENT</c>.
    /// </summary>
    /// <param name="properties">The requested property names.</param>
    public static IReadOnlyList<string> ValidateComputedStyleProperties(IReadOnlyList<string>? properties)
    {
        var requested = new List<string>();
        foreach (var property in properties ?? [])
        {
            if (property is null || !ComputedStyleProperties.Contains(property, StringComparer.Ordinal))
            {
                throw Refused();
            }

            if (!requested.Contains(property, StringComparer.Ordinal))
            {
                requested.Add(property);
            }
        }

        if (requested.Count == 0)
        {
            throw Refused();
        }

        return requested;
    }

    private static TestException Refused() =>
        new("INVALID_ARGUMENT", "style must list at least one of " + string.Join(", ", ComputedStyleProperties));
}
