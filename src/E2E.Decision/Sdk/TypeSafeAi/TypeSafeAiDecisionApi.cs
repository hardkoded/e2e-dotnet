// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>One answer of a TypeSafe response (the AI SDK's <c>typesafeDecisionResponseSchema</c>).</summary>
internal sealed record TypeSafeAnswer(string Type, DecisionAnswer Answer, double? Confidence, JsonElement? Legend);

/// <summary>A TypeSafe response (the AI SDK's <c>typesafeDecisionResponseSchema</c>).</summary>
internal sealed record TypeSafeResponse(
    string? Model,
    IReadOnlyList<KeyValuePair<string, TypeSafeAnswer>> Answers,
    int? InputTokens,
    int? OutputTokens
);

/// <summary>The AI SDK's <c>typesafe-ai-decision-api.ts</c>: the response TypeSafe sends and the message of its failures.</summary>
internal static class TypeSafeAiDecisionApi
{
    /// <summary>Upstream's <c>typesafeDecisionResponseSchema</c>: reads the response, rejecting one that breaks the schema.</summary>
    public static TypeSafeResponse ReadResponse(JsonElement root)
    {
        static InvalidResponseDataException Malformed(string reason) => new("Type validation failed: " + reason);

        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("answers", out var list)
            || list.ValueKind != JsonValueKind.Object
        )
        {
            throw Malformed("answers must be an object.");
        }

        var model = NullishString(root, "model", Malformed);
        var answers = new List<KeyValuePair<string, TypeSafeAnswer>>();
        foreach (var property in list.EnumerateObject())
        {
            answers.Add(new(property.Name, ReadAnswer(property.Value, Malformed)));
        }

        int? input = null;
        int? output = null;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
        {
            if (usage.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("usage must be an object.");
            }

            input = NullishInt(usage, "input_tokens", Malformed);
            output = NullishInt(usage, "output_tokens", Malformed);
        }

        return new TypeSafeResponse(model, answers, input, output);
    }

    /// <summary>
    /// Upstream's <c>typesafeFailedResponseHandler</c> message: the first of <c>message</c>, <c>error</c>, <c>detail</c>, and <c>error_type</c>
    /// the body carries, else <c>TypeSafe request failed</c>.
    /// </summary>
    public static string ErrorMessage(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return string.IsNullOrEmpty(body) ? "TypeSafe request failed" : body;
        }

        using var _ = document;
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return "TypeSafe request failed";
        }

        if (String(root, "message") is { } message)
        {
            return message;
        }

        if (root.TryGetProperty("error", out var error))
        {
            var text = error.ValueKind == JsonValueKind.String ? error.GetString() : String(error, "message");
            if (text is not null)
            {
                return text;
            }
        }

        if (root.TryGetProperty("detail", out var detail) && detail.ValueKind != JsonValueKind.Undefined)
        {
            return detail.ValueKind == JsonValueKind.String ? detail.GetString()! : Stringify(detail);
        }

        return String(root, "error_type") ?? "TypeSafe request failed";
    }

    private static TypeSafeAnswer ReadAnswer(JsonElement element, Func<string, InvalidResponseDataException> malformed)
    {
        var type = String(element, "type") ?? throw malformed("answer type is required.");
        double? confidence = type == "noul" ? null : NullishNumber(element, "confidence", malformed);
        switch (type)
        {
            case "choice":
                return new TypeSafeAnswer(
                    type,
                    new DecisionAnswer
                    {
                        Type = "choice",
                        Choice = String(element, "choice") ?? throw malformed("choice must be a string."),
                        Probabilities = Probabilities(element, malformed),
                    },
                    confidence,
                    null
                );
            case "score":
                JsonElement? legend = null;
                if (element.TryGetProperty("legend", out var rawLegend) && rawLegend.ValueKind != JsonValueKind.Null)
                {
                    legend =
                        rawLegend.ValueKind == JsonValueKind.Object
                            ? rawLegend.Clone()
                            : throw malformed("legend must be an object.");
                }

                return new TypeSafeAnswer(
                    type,
                    new DecisionAnswer
                    {
                        Type = "score",
                        Score = Number(element, "score", malformed),
                        Probabilities = Probabilities(element, malformed),
                    },
                    confidence,
                    legend
                );
            case "box":
                if (!element.TryGetProperty("box", out var box) || box.ValueKind != JsonValueKind.Object)
                {
                    throw malformed("box must be an object.");
                }

                return new TypeSafeAnswer(
                    type,
                    new DecisionAnswer
                    {
                        Type = "box",
                        Box = new DecisionBox(
                            Number(box, "top", malformed),
                            Number(box, "bottom", malformed),
                            Number(box, "left", malformed),
                            Number(box, "right", malformed)
                        ),
                    },
                    confidence,
                    null
                );
            case "noul":
                return new TypeSafeAnswer(
                    type,
                    new DecisionAnswer { Type = "boolean", Probability = Number(element, "noul", malformed) },
                    null,
                    null
                );
            default:
                throw malformed("unknown answer type.");
        }
    }

    private static Dictionary<string, double> Probabilities(
        JsonElement element,
        Func<string, InvalidResponseDataException> malformed
    )
    {
        if (!element.TryGetProperty("probabilities", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw malformed("probabilities must be an object.");
        }

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            result[property.Name] =
                property.Value.ValueKind == JsonValueKind.Number
                    ? property.Value.GetDouble()
                    : throw malformed("probabilities must be numbers.");
        }

        return result;
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NullishString(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    ) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw malformed(name + " must be a string.");

    private static double Number(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    ) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw malformed(name + " must be a number.");

    private static double? NullishNumber(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    ) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.Number ? value.GetDouble()
        : throw malformed(name + " must be a number.");

    private static int? NullishInt(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    )
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw malformed(name + " must be an integer.");
    }

    private static string Stringify(JsonElement value) => JsonSerializer.Serialize(value);
}
