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
