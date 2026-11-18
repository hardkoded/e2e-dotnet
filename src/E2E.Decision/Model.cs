// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;

namespace E2E.Decision;

/// <summary>What a factory needs from its decision model, and how its errors name the fix (upstream's <c>ModelNeeds</c>).</summary>
/// <param name="Caller">The factory, as every error names it, such as <c>DecisionExecutor</c>.</param>
/// <param name="QuestionType">The question type the factory asks: <c>choice</c> or <c>boolean</c>.</param>
/// <param name="Example">How a valid model reads, ending the errors about a value that is no decision model.</param>
public sealed record ModelNeeds(string Caller, string QuestionType, string Example);

/// <summary>Upstream's <c>model.ts</c>.</summary>
public static class Model
{
    /// <summary>
    /// Upstream's <c>checkModel</c>. Rejects anything the decision call cannot decide with, at config load: a model id string, a language
    /// model, or a decision model that cannot answer the caller's question type.
    /// </summary>
    public static void CheckModel(object? model, ModelNeeds needs)
    {
        ArgumentNullException.ThrowIfNull(needs);
        var (caller, questionType, example) = needs;
        if (model is Delegate)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                caller + "({ model }) got a function, not a model; call it with a model id, " + example
            );
        }

        if (model is null or string)
        {
            var got = model is string text ? "the string " + JsonSerializer.Serialize(text) : "null";
            throw new ConfigurationException("INVALID_CONFIG", caller + "({ model }) got " + got + "; " + example);
        }

        if (model is not IDecisionModel decision)
        {
            var kind = model is IAgentModel language
                ? "the language model " + language.Name
                : "an object that is not a decision model";
            throw new ConfigurationException("INVALID_CONFIG", caller + "({ model }) got " + kind + "; " + example);
        }

        var name = decision.Provider + "/" + decision.ModelId;
        if (decision.SpecificationVersion != "v4")
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                caller
                    + "({ model }) got "
                    + name
                    + ", a "
                    + decision.SpecificationVersion
                    + " model; the decision call decides with v4 decision models"
            );
        }

        if (!decision.SupportedQuestionTypes.Contains(questionType, StringComparer.Ordinal))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                caller + "({ model }) got " + name + ", which does not answer " + questionType + " questions"
            );
        }
    }

    /// <summary>
    /// Upstream's <c>checkProviderOptions</c>. Rejects <c>providerOptions</c> other than provider names mapped to option objects, the shape
    /// the decision call takes, at config load.
    /// </summary>
    public static void CheckProviderOptions(IReadOnlyDictionary<string, JsonElement>? value, string caller)
    {
        if (value is null || value.Values.All(option => option.ValueKind == JsonValueKind.Object))
        {
            return;
        }

        throw new ConfigurationException(
            "INVALID_CONFIG",
            caller
                + "({ providerOptions }) maps provider names to option objects, e.g. { gateway: { zeroDataRetention: true } }"
        );
    }
}
