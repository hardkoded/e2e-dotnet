// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

internal static partial class CacheKeys
{
    /// <summary>
    /// Upstream's <c>templateTrace</c> (<c>cache/template.ts</c>): the recording as stored, each template's value replaced by its
    /// placeholder wherever the recorded text spelled it, or <see langword="null"/> when the recording cannot be templated safely: the
    /// text already spelled a placeholder, which could not be told from a written one at replay, or two marked params share a spelling,
    /// so the text cannot say which one it came from. The port records no verdict summary, so there is none to keep as recorded.
    /// </summary>
    public static CacheEntry? TemplateTrace(CacheEntry trace, IReadOnlyList<ParamTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(templates);
        if (!SpellingsDistinct(templates))
        {
            return null;
        }

        var literalPlaceholder = false;
        var templated = Trace.MapTraceText(trace, text =>
        {
            literalPlaceholder |= text.Contains(PlaceholderPrefix, StringComparison.Ordinal);
            return TemplateText(text, templates);
        });
        return literalPlaceholder ? null : templated;
    }

    /// <summary>
    /// Upstream's <c>expandTrace</c> (<c>cache/template.ts</c>): the recording as replayed, placeholders filled from this call's
    /// templates, or <see langword="null"/> when the entry names a parameter this call did not mark.
    /// </summary>
    public static CacheEntry? ExpandTrace(CacheEntry trace, IReadOnlyList<ParamTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(templates);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var template in templates)
        {
            values[template.Pointer] = template.Value;
        }

        return Trace.MapTraceText(trace, text => ExpandText(text, values));
    }
}
