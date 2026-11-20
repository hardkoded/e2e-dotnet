// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// The AI SDK's <c>gateway-provider-options.ts</c>: the check of the <c>gateway</c> provider options a decision call takes. The
/// <c>models</c> fallback list is validated (at most one conditional entry, and it comes first); every other option is the Gateway's own and
/// passes through.
/// </summary>
public static class GatewayProviderOptions
{
    public const int DecisionFallbackMaxConditionDepth = 5;
    public const int DecisionFallbackMaxConditionsPerList = 20;
    public const int DecisionFallbackMaxQuestionLength = 256;

    /// <summary>
    /// Upstream's <c>gatewayDecisionProviderOptionsSchema</c>, as a check: null when <paramref name="options"/> is valid, else the first problem
    /// found.
    /// </summary>
    public static string? ValidateDecisionProviderOptions(JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Object)
        {
            return "expected an object";
        }

        return options.TryGetProperty("models", out var models) ? ValidateModels(models) : null;
    }

    private static string? ValidateModels(JsonElement models)
    {
        if (models.ValueKind != JsonValueKind.Array)
        {
            return "models: expected an array";
        }

        var conditional = new List<int>();
        var index = 0;
        foreach (var entry in models.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
            {
                var problem = ValidateConditionalFallback(entry);
                if (problem is not null)
                {
                    return "models["
                        + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "]: "
                        + problem;
                }

                conditional.Add(index);
            }

            index++;
        }

        if (conditional.Count > 1)
        {
            return "models supports at most one conditional decision fallback";
        }

        return conditional is [var first, ..] && first != 0
            ? "a conditional decision fallback must be the first models entry"
            : null;
    }

    private static string? ValidateConditionalFallback(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return "expected a model id or a { model, when } fallback";
        }

        if (entry.EnumerateObject().Any(property => property.Name is not ("model" or "when")))
        {
            return "unexpected key";
        }

        if (
            !entry.TryGetProperty("model", out var model)
            || model.ValueKind != JsonValueKind.String
            || model.GetString()!.Length == 0
        )
        {
            return "model must be a non-empty string";
        }

        return entry.TryGetProperty("when", out var when) ? ValidateCondition(when, 1) : "when is required";
    }

    private static string? ValidateCondition(JsonElement condition, int depth)
    {
        if (condition.ValueKind != JsonValueKind.Object)
        {
            return "a condition must be an object";
        }

        var keys = condition.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (keys.Contains("any") || keys.Contains("all") || keys.Contains("atLeast"))
        {
            var group =
                keys.Contains("any") ? "any"
                : keys.Contains("all") ? "all"
                : "atLeast";
            if (keys.Count != 1)
            {
                return "a condition cannot mix " + group + " with other keys";
            }

            if (depth == DecisionFallbackMaxConditionDepth)
            {
                return "conditions can be nested at most "
                    + DecisionFallbackMaxConditionDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " levels deep";
            }

            var value = condition.GetProperty(group);
            if (group != "atLeast")
            {
                return ValidateConditionList(value, depth);
            }

            if (
                value.ValueKind != JsonValueKind.Object
                || value.EnumerateObject().Any(property => property.Name is not ("count" or "conditions"))
                || !value.TryGetProperty("count", out var count)
                || count.ValueKind != JsonValueKind.Number
                || !count.TryGetInt64(out var number)
                || number < 1
                || !value.TryGetProperty("conditions", out var conditions)
            )
            {
                return "atLeast needs an integer count of at least 1 and a conditions list";
            }

            var problem = ValidateConditionList(conditions, depth);
            if (problem is not null)
            {
                return problem;
            }

            return number > conditions.GetArrayLength() ? "atLeast count cannot exceed the number of conditions" : null;
        }

        if (keys.Any(key => key is not ("question" or "confidenceBelow" or "probabilityBetween")))
        {
            return "unexpected key";
        }

        if (
            condition.TryGetProperty("question", out var question)
            && (
                question.ValueKind != JsonValueKind.String
                || question.GetString()!.Length is 0 or > DecisionFallbackMaxQuestionLength
            )
        )
        {
            return "question must be 1 to " + DecisionFallbackMaxQuestionLength + " characters";
        }

        var hasBelow = keys.Contains("confidenceBelow");
        var hasBetween = keys.Contains("probabilityBetween");
        if (hasBelow == hasBetween)
        {
            return "a condition needs exactly one of confidenceBelow and probabilityBetween";
        }

        if (hasBelow)
        {
            return IsProbability(condition.GetProperty("confidenceBelow"))
                ? null
                : "confidenceBelow must be a number between 0 and 1";
        }

        var between = condition.GetProperty("probabilityBetween");
        if (
            between.ValueKind != JsonValueKind.Array
            || between.GetArrayLength() != 2
            || between.EnumerateArray().Any(value => !IsProbability(value))
        )
        {
            return "probabilityBetween must be two numbers between 0 and 1";
        }

        return between[0].GetDouble() <= between[1].GetDouble()
            ? null
            : "probabilityBetween minimum must be less than or equal to maximum";
    }

    private static string? ValidateConditionList(JsonElement list, int depth)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            return "expected a list of conditions";
        }

        if (list.GetArrayLength() is 0 or > DecisionFallbackMaxConditionsPerList)
        {
            return "a condition list takes 1 to " + DecisionFallbackMaxConditionsPerList + " conditions";
        }

        foreach (var child in list.EnumerateArray())
        {
            var problem = ValidateCondition(child, depth + 1);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static bool IsProbability(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.GetDouble() is >= 0 and <= 1;
}
