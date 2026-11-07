// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E;

/// <summary>
/// Upstream's <c>TraceKeyContext</c> (<c>cache/trace.ts</c>): every part of a cache key outside the step's own identity, as it was
/// when a step was recorded. An entry stores it as <c>keyedBy</c>: provenance, never read by a replay. When a step's key no longer
/// finds the entry, comparing it with the run's says what changed.
/// </summary>
public sealed record TraceKeyContext(
    string CacheSchema,
    string PolicyVersion,
    string Project,
    string Platform,
    string EngineName,
    int EngineSpiVersion,
    string AppIdentity,
    string AgentContextDigest);

/// <summary>Upstream's <c>cache/trace.ts</c> constants and key context fields that the trace cache identity uses.</summary>
internal static class Trace
{
    /// <summary>Upstream's <c>TRACE_SCHEMA_VERSION</c>.</summary>
    public const string TraceSchemaVersion = "trace-1";

    /// <summary>Upstream's <c>MAX_TRACE_ACTIONS</c>: mirrors the default step action budget; overflow marks the trace truncated.</summary>
    public const int MaxTraceActions = 50;

    /// <summary>Upstream's <c>MAX_TRACE_ANCHORS</c>: the most anchors one side of the recorded delta holds.</summary>
    public const int MaxTraceAnchors = 8;

    /// <summary>Upstream's <c>TRACE_ANCHOR_STATES</c>: the node states an anchor records, the ones a step sets on a control it leaves on screen.</summary>
    public static readonly IReadOnlyList<string> TraceAnchorStates = ["checked", "expanded", "pressed", "selected"];

    /// <summary>Upstream's <c>MAX_TRACE_END_WAIT_MS</c>: the longest end wait a recording may ask a replay for.</summary>
    public const int MaxTraceEndWaitMs = 120_000;

    /// <summary>Upstream's <c>MAX_TRACE_UPLOAD_PATHS</c>: most files one upload carries; more is a script, not a form.</summary>
    public const int MaxTraceUploadPaths = 16;

    /// <summary>Upstream's <c>MAX_TRACE_POSITION_OF</c>: most twins a positional recording distinguishes between; beyond it a list is data, not a control set.</summary>
    public const int MaxTracePositionOf = 1000;

    /// <summary>
    /// Upstream's <c>MAX_TRACE_INPUT_CHARS</c>: the longest replay input (a typed value, an upload path, a location) a recording may hold.
    /// Inputs are never truncated to fit, since a trimmed URL or half a typed value would replay a different action than the one recorded,
    /// so an oversized input poisons the trace (<see cref="CacheEntry.Truncated"/>) instead of bending the value.
    /// </summary>
    public const int MaxTraceInputChars = 4_096;

    /// <summary>Upstream's <c>MAX_TRACE_DESCRIPTOR_CHARS</c>: the longest test id, target id, or agent name a recording's provenance holds.</summary>
    public const int MaxTraceDescriptorChars = 300;

    /// <summary>
    /// Upstream's <c>mapTraceText</c>: applies <paramref name="map"/> to every recorded string a screen value can appear in: typed and
    /// selected values, navigated URLs, both routes, target and anchor text, and the upload paths. Keys (<c>press</c>), secret names,
    /// roles, executor identity, and provenance are vocabulary and are left alone. The one list of text-bearing fields lives here,
    /// beside the entry it walks; a null from <paramref name="map"/> makes the whole result null. The port records no action
    /// summaries and no verdict summary, so it has none to map. Returns a copy; the entry is not changed.
    /// </summary>
    public static CacheEntry? MapTraceText(CacheEntry trace, Func<string, string?> map)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(map);
        var actions = Each(trace.Actions, action => MapActionText(action, map));
        var appeared = Each(trace.Appeared, anchor => MapDescriptorText(anchor, map));
        var gone = Each(trace.Gone, anchor => MapDescriptorText(anchor, map));
        if (actions is null || appeared is null || gone is null || !TryMap(trace.Route, map, out var route) || !TryMap(trace.EndRoute, map, out var endRoute))
        {
            return null;
        }

        var mapped = trace.Copy();
        mapped.Actions = actions;
        mapped.Appeared = appeared;
        mapped.Gone = gone;
        mapped.Route = route;
        mapped.EndRoute = endRoute;
        return mapped;
    }

    /// <summary>Upstream's <c>each</c>: reads every item, or null when one reads as null.</summary>
    private static List<U>? Each<T, U>(IEnumerable<T> items, Func<T, U?> read)
        where U : class
    {
        var output = new List<U>();
        foreach (var item in items)
        {
            if (read(item) is not { } value)
            {
                return null;
            }

            output.Add(value);
        }

        return output;
    }

    /// <summary>Maps one optional text: false when the text exists and maps to nothing, true for an absent one.</summary>
    private static bool TryMap(string? text, Func<string, string?> map, out string? mapped)
    {
        mapped = text is null ? null : map(text);
        return text is null || mapped is not null;
    }

    /// <summary>
    /// Upstream's <c>mapDescriptorText</c>: the fields of an anchor that carry text read off the screen, where a value a test supplied
    /// can reappear (<c>DESCRIPTOR_TEXT_FIELDS</c>). The role, the input purpose, and the states are vocabulary.
    /// </summary>
    private static RecordedTarget? MapDescriptorText(RecordedTarget descriptor, Func<string, string?> map)
    {
        if (!TryMap(descriptor.Name, map, out var name)
            || !TryMap(descriptor.Text, map, out var text)
            || !TryMap(descriptor.TestId, map, out var testId)
            || !TryMap(descriptor.Placeholder, map, out var placeholder)
            || !TryMap(descriptor.Within, map, out var within)
            || !TryMap(descriptor.Value, map, out var value))
        {
            return null;
        }

        var mapped = descriptor.Copy();
        mapped.Name = name;
        mapped.Text = text;
        mapped.TestId = testId;
        mapped.Placeholder = placeholder;
        mapped.Within = within;
        mapped.Value = value;
        return mapped;
    }

    /// <summary>
    /// Upstream's <c>mapActionText</c>. An action holds its target as <c>Name</c>, <c>TestId</c>, and <c>Within</c>, and a drag its destination the same way. A typed or selected value, a
    /// navigated URL, a scroll text and upload paths are replay inputs like a typed value. A <c>fill</c> of a secret holds its marker,
    /// which is a name, not text a param produced (upstream's <c>typeSecret</c> maps its target only).
    /// </summary>
    private static RecordedAction? MapActionText(RecordedAction action, Func<string, string?> map)
    {
        var mapped = action.Copy();
        // The text of a scroll-until and the name of a tool gap are not target text: the first maps below, the second is vocabulary.
        var targetText = action.Kind is "scrollUntil" or "tool" ? action.Text : null;
        if (!TryMap(action.Name, map, out var name) || !TryMap(action.TestId, map, out var testId) || !TryMap(action.Within, map, out var within)
            || !TryMap(action.Placeholder, map, out var placeholder)
            || (action.Kind is not ("scrollUntil" or "tool") && !TryMap(action.Text, map, out targetText)))
        {
            return null;
        }

        mapped.Name = name;
        mapped.TestId = testId;
        mapped.Within = within;
        mapped.Placeholder = placeholder;
        mapped.Text = targetText;
        switch (action.Kind)
        {
            case "tapAt" or "hoverAt":
                // The node a point sat in is a target like any other, whose text a param can reappear in.
                if (!TryMapPlace(action.Under, map, out var under))
                {
                    return null;
                }

                mapped.Under = under;
                break;
            case "dragAt":
                if (!TryMapEnd(action.From, map, out var from) || !TryMapEnd(action.To, map, out var to))
                {
                    return null;
                }

                mapped.From = from;
                mapped.To = to;
                break;
            case "navigate":
                if (!TryMap(action.Url, map, out var url))
                {
                    return null;
                }

                mapped.Url = url;
                break;
            case "fill" when IsSecretMarker(action):
                break;
            case "fill" or "select" or "typeText":
                if (!TryMap(action.Value, map, out var value))
                {
                    return null;
                }

                mapped.Value = value;
                break;
            case "scrollUntil":
                if (!TryMap(action.Text, map, out var text))
                {
                    return null;
                }

                mapped.Text = text;
                break;
            case "drag" when action.Destination is { } destination:
                // Both ends are re-found by what they read, so a value a param produced reappears in either.
                var mappedDestination = MapDescriptorText(destination, map);
                if (mappedDestination is null)
                {
                    return null;
                }

                mapped.Destination = mappedDestination;
                break;
            case "upload" when action.Paths is { } paths:
                // Paths are replay inputs like a typed value: a file named through unique() must be re-resolved from each run's value,
                // or the replay would upload the recording run's file.
                var mappedPaths = Each(paths, path => map(path));
                if (mappedPaths is null)
                {
                    return null;
                }

                mapped.Paths = mappedPaths;
                break;
        }

        return mapped;
    }

    // Upstream's mapPointEnd: a point end with its node's text mapped, unchanged without a node; false when that text maps to nothing.
    private static bool TryMapPlace(RecordedPointPlace? place, Func<string, string?> map, out RecordedPointPlace? mapped)
    {
        mapped = place;
        if (place?.Target is not { } target)
        {
            return true;
        }

        if (MapDescriptorText(target, map) is not { } descriptor)
        {
            return false;
        }

        mapped = place.Copy();
        mapped.Target = descriptor;
        return true;
    }

    private static bool TryMapEnd(RecordedPointEnd? end, Func<string, string?> map, out RecordedPointEnd? mapped)
    {
        mapped = end;
        if (end is null)
        {
            return true;
        }

        if (!TryMapPlace(end.Under, map, out var under))
        {
            return false;
        }

        mapped = end.Copy();
        mapped.Under = under;
        return true;
    }

    internal static bool IsSecretMarker(RecordedAction action) =>
        action.Kind == "fill" && action.Value is { } value && value.StartsWith("<secret:", StringComparison.Ordinal) && value.EndsWith('>');

    /// <summary>Upstream's <c>KEY_CONTEXT_FIELDS</c>, in upstream's order, each with the value it holds.</summary>
    public static readonly IReadOnlyList<(string Field, Func<TraceKeyContext, object> Get)> KeyContextFields =
    [
        ("cacheSchema", key => key.CacheSchema),
        ("policyVersion", key => key.PolicyVersion),
        ("project", key => key.Project),
        ("platform", key => key.Platform),
        ("engineName", key => key.EngineName),
        ("engineSpiVersion", key => key.EngineSpiVersion),
        ("appIdentity", key => key.AppIdentity),
        ("agentContextDigest", key => key.AgentContextDigest),
    ];
}

/// <summary>
/// Upstream's <c>readKeyContext</c>: a stored <c>keyedBy</c>, or null for anything else. A malformed one is dropped, never
/// failing the entry.
/// </summary>
internal sealed class KeyContextConverter : JsonConverter<TraceKeyContext>
{
    public override TraceKeyContext? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        if (Text("cacheSchema") is not { } schema
            || Text("policyVersion") is not { } policy
            || Text("project") is not { } project
            || Text("platform") is not { } platform
            || Text("engineName") is not { } engine
            || Text("appIdentity") is not { } app
            || Text("agentContextDigest") is not { } agentContext
            || !root.TryGetProperty("engineSpiVersion", out var spi)
            || spi.ValueKind != JsonValueKind.Number
            || !spi.TryGetInt32(out var spiVersion))
        {
            return null;
        }

        return new TraceKeyContext(schema, policy, project, platform, engine, spiVersion, app, agentContext);
    }

    public override void Write(Utf8JsonWriter writer, TraceKeyContext value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("cacheSchema", value.CacheSchema);
        writer.WriteString("policyVersion", value.PolicyVersion);
        writer.WriteString("project", value.Project);
        writer.WriteString("platform", value.Platform);
        writer.WriteString("engineName", value.EngineName);
        writer.WriteNumber("engineSpiVersion", value.EngineSpiVersion);
        writer.WriteString("appIdentity", value.AppIdentity);
        writer.WriteString("agentContextDigest", value.AgentContextDigest);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Upstream's <c>DerivedReason</c> (<c>cache/trace.ts</c>): why a typed value was this run's data rather than the flow's, by the rule
/// that flagged it (<c>agent/derived.ts</c>): read off a screenshot, the whole name or text of a node, a token with a digit shown as a
/// word of its own, or a date or time reckoned from today. An entry stores it as <c>pixels</c>, <c>whole-node</c>,
/// <c>minted-token</c>, or <c>date</c> on a <c>tool</c> action.
/// </summary>
[JsonConverter(typeof(DerivedReasonConverter))]
public enum DerivedReason
{
    /// <summary>The model was shown a screenshot this step, so a value that is not literal may have been read off the image.</summary>
    Pixels,

    /// <summary>The value is the whole of what some node said.</summary>
    WholeNode,

    /// <summary>The value is data-shaped and some node showed it as a word of its own.</summary>
    MintedToken,

    /// <summary>A date or a clock time, which the model reckons from today.</summary>
    Date,
}

/// <summary>Writes a <see cref="DerivedReason"/> as upstream spells it (<c>whole-node</c>) and refuses a number or a name it lacks (upstream's <c>isDerivedReason</c>).</summary>
internal sealed class DerivedReasonConverter() : JsonStringEnumConverter<DerivedReason>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false);
