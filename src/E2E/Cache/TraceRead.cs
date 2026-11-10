// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>cache/trace-read.ts</c>: reading a stored trace entry back. Every field is checked on the way in, and a document this runner
/// cannot trust or replay is a miss, never a repaired or partly trusted entry. The port's stored entry is already typed, so the checks run on it;
/// the document overload deserializes first, the way upstream's <c>readTraceEntry(document: unknown)</c> takes a parsed document.
/// </summary>
internal static class TraceRead
{
    /// <summary>The action kinds this runner records and replays. Upstream's <c>readAction</c> makes any other name a miss.</summary>
    private static readonly HashSet<string> KnownKinds = new(
        ["tap", "doubleTap", "secondaryTap", "longPress", "hover", "scrollTo", "fill", "clear", "press", "select", "check", "uncheck", "upload", "drag",
            "navigate", "scroll", "scrollUntil", "tapAt", "hoverAt", "dragAt", "back", "resize", "typeText", "pressKey", "dismissKeyboard", "tool"],
        StringComparer.Ordinal);

    /// <summary>Upstream's <c>readTraceEntry</c> over a parsed document: the entry it reads as, or null for a document it cannot read.</summary>
    internal static CacheEntry? ReadTraceEntry(JsonNode? document)
    {
        if (document is null)
        {
            return null;
        }

        try
        {
            return ReadTraceEntry(document.Deserialize<CacheEntry>(JsonDefaults.Options));
        }
        catch (JsonException)
        {
            // A field of the wrong JSON type is not an entry this runner wrote.
            return null;
        }
    }

    /// <summary>
    /// Upstream's <c>readTraceEntry</c> over a stored entry: the entry when every field is one this runner writes, else null. Each action is
    /// checked by its own <c>Has*</c> validator, and the entry by the ones that span its fields.
    /// </summary>
    internal static CacheEntry? ReadTraceEntry(CacheEntry? entry)
    {
        if (entry is null
            || entry.Schema != FileStepCache.SchemaVersion
            || entry.Actions is not { Count: > 0 }
            || entry.Appeared is null
            || entry.Gone is null
            || HasNull(entry.Actions)
            || HasNull(entry.Appeared)
            || HasNull(entry.Gone)
            || !entry.HasValidEndWait()
            || !entry.HasValidCaps()
            || !entry.HasValidProvenance()
            || !entry.Actions.All(IsValidAction))
        {
            return null;
        }

        // Upstream's readRecordedModel: a blank or oversized attribution is dropped, and the entry still reads.
        var read = entry.Copy();
        read.RecordedBy = CacheEntry.ReadRecordedBy(entry.RecordedBy);
        return read;
    }

    // Upstream's readAction: a folded scroll repeats at least twice, and a scrollUntil or navigate holds its text or url, never an empty one.
    private static bool IsValidAction(RecordedAction action) =>
        KnownKinds.Contains(action.Kind)
        && (action.Times is null or >= 2)
        && (action.Kind != "scrollUntil" || !string.IsNullOrEmpty(action.Text))
        && (action.Kind != "navigate" || !string.IsNullOrEmpty(action.Url))
        && action.HasValidInputs()
        && action.HasValidSummary()
        && action.HasValidModifiers()
        && action.HasValidTarget()
        && action.HasValidPosition()
        && action.HasValidSpans()
        && action.HasValidPaths()
        && action.HasValidPoints()
        && action.HasValidKeyboard()
        && action.HasValidDestination()
        && action.HasValidSize();

    private static bool HasNull<T>(List<T> items) where T : class => items.Any(item => item is null);
}
