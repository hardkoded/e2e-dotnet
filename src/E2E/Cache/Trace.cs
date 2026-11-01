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

    /// <summary>Upstream's <c>MAX_TRACE_END_WAIT_MS</c>: the longest end wait a recording may ask a replay for.</summary>
    public const int MaxTraceEndWaitMs = 120_000;

    /// <summary>Upstream's <c>MAX_TRACE_UPLOAD_PATHS</c>: most files one upload carries; more is a script, not a form.</summary>
    public const int MaxTraceUploadPaths = 16;

    /// <summary>Upstream's <c>MAX_TRACE_INPUT_CHARS</c>: the longest replay input (a typed value, an upload path) a recording may hold.</summary>
    public const int MaxTraceInputChars = 4_096;

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
            || !TryMap(descriptor.Value, map, out var value))
        {
            return null;
        }

        var mapped = descriptor.Copy();
        mapped.Name = name;
        mapped.Text = text;
        mapped.TestId = testId;
        mapped.Placeholder = placeholder;
        mapped.Value = value;
        return mapped;
    }

    /// <summary>
    /// Upstream's <c>mapActionText</c>. An action holds its target as <c>Name</c> and <c>TestId</c>. A typed or selected value, a
    /// navigated URL, a scroll text and upload paths are replay inputs like a typed value. A <c>fill</c> of a secret holds its marker,
    /// which is a name, not text a param produced (upstream's <c>typeSecret</c> maps its target only).
    /// </summary>
    private static RecordedAction? MapActionText(RecordedAction action, Func<string, string?> map)
    {
        var mapped = action.Copy();
        if (!TryMap(action.Name, map, out var name) || !TryMap(action.TestId, map, out var testId))
        {
            return null;
        }

        mapped.Name = name;
        mapped.TestId = testId;
        switch (action.Kind)
        {
            case "navigate":
                if (!TryMap(action.Url, map, out var url))
                {
                    return null;
                }

                mapped.Url = url;
                break;
            case "fill" when IsSecretMarker(action):
                break;
            case "fill" or "select":
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

    private static bool IsSecretMarker(RecordedAction action) =>
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
