// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>
/// The fields of an extraction call as the AI SDK's <c>ExtractionModelV4Field</c> serializes them (what <c>JSON.stringify</c> writes for a
/// field): the wire shape the Gateway takes as is.
/// </summary>
internal static class ExtractionFieldJson
{
    public static JsonObject ToJson(IReadOnlyDictionary<string, ExtractionField> fields)
    {
        var result = new JsonObject();
        foreach (var (id, field) in fields)
        {
            result[id] = ToJson(field);
        }

        return result;
    }

    public static JsonObject ToJson(ExtractionField field)
    {
        var json = new JsonObject { ["type"] = field.Type };
        if (field.Instructions is { } instructions)
        {
            json["instructions"] = instructions.Text is { } text
                ? JsonValue.Create(text)
                : JsonNode.Parse(instructions.Json!.Value.GetRawText());
        }

        if (field.Unit is not null)
        {
            json["unit"] = field.Unit;
        }

        if (field.Items is not null)
        {
            json["items"] = field.Items;
        }

        if (field.Integer is { } integer)
        {
            json["integer"] = integer;
        }

        if (field.Decimals is { } decimals)
        {
            json["decimals"] = decimals;
        }

        return json;
    }
}
