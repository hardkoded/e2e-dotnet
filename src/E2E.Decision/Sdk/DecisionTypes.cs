// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>
/// One decision question (the AI SDK's <c>DecisionModelV4Question</c>). A <c>choice</c> question carries its options, a
/// <c>score</c> question its ordered levels, and a <c>boolean</c> question instructions or at least one criterion.
/// </summary>
public sealed class DecisionQuestion
{
    /// <summary><c>choice</c>, <c>score</c>, <c>boolean</c>, or <c>box</c>.</summary>
    public required string Type { get; init; }

    /// <summary>What to decide. Null when the criteria say it all.</summary>
    public string? Instructions { get; init; }

    /// <summary>The options of a <c>choice</c> question, in order.</summary>
    public IReadOnlyList<DecisionChoice>? Choices { get; init; }

    /// <summary>The descriptions of the ordered levels of a <c>score</c> question: level <c>i</c> is the <c>i</c>th entry.</summary>
    public IReadOnlyList<string?>? Levels { get; init; }

    /// <summary>What makes a <c>boolean</c> question true.</summary>
    public string? TrueCriteria { get; init; }

    /// <summary>What makes a <c>boolean</c> question false.</summary>
    public string? FalseCriteria { get; init; }

    /// <summary>A <c>choice</c> question.</summary>
    public static DecisionQuestion Choice(string? instructions, IReadOnlyList<DecisionChoice> choices) =>
        new()
        {
            Type = "choice",
            Instructions = instructions,
            Choices = choices,
        };

    /// <summary>A <c>score</c> question over ordered levels.</summary>
    public static DecisionQuestion Score(string? instructions, IReadOnlyList<string?> levels) =>
        new()
        {
            Type = "score",
            Instructions = instructions,
            Levels = levels,
        };

    /// <summary>A <c>boolean</c> question.</summary>
    public static DecisionQuestion Boolean(
        string? instructions,
        string? trueCriteria = null,
        string? falseCriteria = null
    ) =>
        new()
        {
            Type = "boolean",
            Instructions = instructions,
            TrueCriteria = trueCriteria,
            FalseCriteria = falseCriteria,
        };
}

/// <summary>One option of a <c>choice</c> question.</summary>
public sealed record DecisionChoice(string Value, string? Description = null);

/// <summary>Edges in [0, 1], measured from the image's top-left corner.</summary>
public sealed record DecisionBox(double Top, double Bottom, double Left, double Right);

/// <summary>
/// One model answer (the AI SDK's <c>DecisionAnswer</c>). Numbers are returned as the provider sent them.
/// <see cref="Type"/> is the question's type, or <c>refusal</c> when the model declined the question.
/// </summary>
public sealed class DecisionAnswer
{
    public required string Type { get; init; }

    /// <summary>The selected option of a <c>choice</c> answer.</summary>
    public string? Choice { get; init; }

    /// <summary>The score of a <c>score</c> answer.</summary>
    public double? Score { get; init; }

    /// <summary>P(true) of a <c>boolean</c> answer.</summary>
    public double? Probability { get; init; }

    /// <summary>The distribution of a <c>choice</c> or <c>score</c> answer: option ids, or level indexes, to probabilities. Null when the model gave none.</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>The normalized box of a <c>box</c> answer.</summary>
    public DecisionBox? Box { get; init; }
}

/// <summary>Declared rounding precision of the numbers a model returns (decimal places, 0 through 15).</summary>
public sealed record DecisionRounding(int? ProbabilityDecimals = null, int? ScoreDecimals = null);

/// <summary>Tokens a decision call used.</summary>
public sealed record DecisionUsage(int? InputTokens, int? OutputTokens)
{
    /// <summary>The sum of both counts, or null when either is unknown.</summary>
    public int? TotalTokens => InputTokens is { } input && OutputTokens is { } output ? input + output : null;
}

/// <summary>A part of the state every question of a decision call shares (the AI SDK's <c>DecisionModelV4State</c>).</summary>
public abstract record DecisionStatePart;

/// <summary>Plain text.</summary>
public sealed record DecisionTextPart(string Text) : DecisionStatePart;

/// <summary>A JSON value the model reads as data.</summary>
public sealed record DecisionJsonPart(JsonElement Value) : DecisionStatePart;

/// <summary>A file, such as a screenshot, as bytes with its media type.</summary>
public sealed record DecisionFilePart(string MediaType, byte[] Data) : DecisionStatePart;

/// <summary>What a decision model answers a call with (the AI SDK's <c>DecisionModelV4Result</c>).</summary>
public sealed class DecisionModelResult
{
    /// <summary>The answers by question id.</summary>
    public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }

    public DecisionUsage? Usage { get; init; }

    /// <summary>The rounding the numbers were reported at. Null when they were not rounded.</summary>
    public DecisionRounding? Rounding { get; init; }

    /// <summary>What the provider reported beside the answers, by provider name (for example the per-question <c>confidence</c>).</summary>
    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The id of the model that served the call, when the response names one.</summary>
    public string? ResponseModelId { get; init; }

    /// <summary>Settings the provider did not support, as text.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>What <see cref="DecisionModelCall"/> hands a decision model (the AI SDK's <c>DecisionModelV4CallOptions</c>).</summary>
public sealed class DecisionModelCall
{
    public required IReadOnlyList<DecisionStatePart> State { get; init; }

    public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>Reasoning effort: <c>provider-default</c>, <c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c>, or <c>max</c>.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Transport retries for this call. Null keeps the provider's own.</summary>
    public int? MaxRetries { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>The provider options, keyed by provider name. Each value is a JSON object.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
}

/// <summary>
/// A model that answers typed questions with probabilities (the AI SDK's <c>Experimental_DecisionModelV4</c>). Only
/// specification version <c>v4</c> is accepted.
/// </summary>
public interface IDecisionModel
{
    string Provider { get; }

    string ModelId { get; }

    /// <summary>The specification version this model implements. Only <c>v4</c> is accepted.</summary>
    string SpecificationVersion { get; }

    /// <summary>The question types this model can answer.</summary>
    IReadOnlyList<string> SupportedQuestionTypes { get; }

    /// <summary>Answers every question of <paramref name="call"/> in one request.</summary>
    Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken);
}

/// <summary>What <see cref="ExperimentalDecide.RunAsync"/> returns (the AI SDK's <c>DecisionResult</c>).</summary>
public sealed class DecisionResult
{
    public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }

    public required DecisionUsage Usage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DecisionRounding? Rounding { get; init; }

    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The id of the model that served the call: the response's, or the requested one.</summary>
    public required string ResponseModelId { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
