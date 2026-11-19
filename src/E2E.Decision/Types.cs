// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision;

/// <summary>Options for an executor that acts through a decision model and writes field text with a language model (upstream's <c>DecisionExecutorOptions</c>).</summary>
public sealed class DecisionExecutorOptions
{
    /// <summary>
    /// A decision model that answers <c>choice</c> questions with a probability distribution, such as an <see cref="OpenAIDecisionModel"/>.
    /// An answer without <see cref="DecisionAnswer.Probabilities"/> counts as a committed choice: probability 1, and the provider-reported confidence, which is 0 only when the provider reports none.
    /// </summary>
    public IDecisionModel? Model { get; init; }

    /// <summary>A deprecated evaluation model, used when <see cref="Model"/> is not set. It runs through the same loop.</summary>
    public IEvaluationModel? EvaluationModel { get; init; }

    /// <summary>
    /// A small language model that writes field values when the decision model picks <c>type</c>. It is exposed as the executor's model, so
    /// the agent entry's judgment tier (<c>WaitFor</c>, <c>Extract</c>) uses it too. Without either, <c>type</c> is never offered.
    /// </summary>
    public IAgentModel? TextModel { get; init; }

    /// <summary>Minimum probability for a selected operation, target, secret, or assertion verdict. Default 0 (off).</summary>
    public double MinProbability { get; init; }

    /// <summary>Minimum provider-reported confidence. Default 0 (off). A model that reports none counts as 0; a committed choice (an answer without a distribution) keeps the confidence the provider reports for it.</summary>
    public double MinConfidence { get; init; }

    /// <summary>
    /// Provider options sent with every decide call. They reach the decision model only: the text model keeps the agents entry's
    /// <c>ProviderOptions</c>.
    /// </summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? ProviderOptions { get; init; }

    /// <summary>Ask for masked pixels on every observation. The model must take images and answer <c>score</c> questions. Default off.</summary>
    public bool Vision { get; init; }
}

/// <summary>Options for a test filter that asks a decision model which tests a change can affect (upstream's <c>DecisionFilterOptions</c>).</summary>
public sealed class DecisionFilterOptions
{
    /// <summary>A decision model that answers <c>boolean</c> questions with a probability.</summary>
    public required IDecisionModel Model { get; init; }

    /// <summary>Provider options sent with every decide call.</summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? ProviderOptions { get; init; }
}
