// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E;

/// <summary>
/// The configured model identity attached to a recording (upstream's <c>RecordedModel</c>, <c>cache/trace-fields.ts</c>).
/// <see cref="IAgentModel.Name"/> already spells the model, so the port records it in <see cref="Model"/> and leaves
/// <see cref="Provider"/> empty, as a step's model record does.
/// </summary>
public sealed class RecordedModel
{
    /// <summary>Who serves the model. Null when the model reports no provider apart from its name.</summary>
    public string? Provider { get; init; }

    public required string Model { get; init; }

    /// <summary><c>provider/model</c>, or the model alone when no provider is known.</summary>
    public string Label => string.IsNullOrEmpty(Provider) ? Model : Provider + "/" + Model;
}

/// <summary>Upstream's <c>cache/trace-fields.ts</c>: the scalar fields shared by the entry reader and the recorder.</summary>
internal static class TraceFields
{
    /// <summary>Upstream's <c>MAX_TRACE_DESCRIPTOR_CHARS</c>: the most characters a recorded descriptor may hold.</summary>
    public const int MaxDescriptorChars = 300;

    /// <summary>Nonblank text within <paramref name="maxChars"/>, preserved verbatim; otherwise null.</summary>
    public static string? ReadBoundedText(string? value, int maxChars) =>
        value is null || string.IsNullOrWhiteSpace(value) || value.Length > maxChars ? null : value;

    /// <summary>Malformed optional model attribution never makes a recording unreadable: it reads as absent.</summary>
    public static RecordedModel? ReadRecordedModel(string? provider, string? model, int maxChars)
    {
        var readModel = ReadBoundedText(model, maxChars);
        if (readModel is null)
        {
            return null;
        }

        if (provider is null)
        {
            return new RecordedModel { Model = readModel };
        }

        var readProvider = ReadBoundedText(provider, maxChars);
        return readProvider is null ? null : new RecordedModel { Provider = readProvider, Model = readModel };
    }
}

/// <summary>
/// No upstream equivalent: reads a stored <c>recordedBy</c> through <see cref="TraceFields.ReadRecordedModel"/>, so a malformed
/// one is absent instead of an unreadable entry, which is what upstream's hand-written reader does.
/// </summary>
internal sealed class RecordedModelConverter : JsonConverter<RecordedModel>
{
    public override RecordedModel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var provider = Text(element, "provider", out var providerValid);
        var model = Text(element, "model", out var modelValid);
        return providerValid && modelValid ? TraceFields.ReadRecordedModel(provider, model, TraceFields.MaxDescriptorChars) : null;
    }

    public override void Write(Utf8JsonWriter writer, RecordedModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Provider is not null)
        {
            writer.WriteString("provider", value.Provider);
        }

        writer.WriteString("model", value.Model);
        writer.WriteEndObject();
    }

    // A present field that is not a string is malformed; an absent one is simply missing.
    private static string? Text(JsonElement element, string name, out bool valid)
    {
        valid = true;
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        valid = value.ValueKind == JsonValueKind.String;
        return valid ? value.GetString() : null;
    }
}
