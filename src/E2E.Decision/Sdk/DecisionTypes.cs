// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>
/// An instruction or a criterion description (the AI SDK's <c>DecisionModelV4Input</c>): text, or a JSON value the provider
/// serializes into the prompt. A string converts implicitly.
/// </summary>
public sealed class DecisionInput
{
    // JSON.stringify leaves non-ASCII and HTML characters unescaped.
    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private DecisionInput(string? text, JsonElement? json)
    {
        Text = text;
        Json = json;
    }

    /// <summary>
    /// An explicit JSON <c>null</c>, as upstream's <c>instructions: null</c> or <c>criteria: { false: null }</c> reads. A C# <c>null</c> leaves the
    /// field out. .NET-only: JavaScript tells an absent field from a <c>null</c> one, C# needs a value for the second.
    /// </summary>
    public static DecisionInput Null { get; } = new(null, JsonDocument.Parse("null").RootElement);

    /// <summary>The text, when the input is a string.</summary>
    public string? Text { get; }

    /// <summary>The value, when the input is JSON.</summary>
    public JsonElement? Json { get; }

    /// <summary>Converts a string to an input; null stays null.</summary>
    public static implicit operator DecisionInput?(string? text) => text is null ? null : new DecisionInput(text, null);

    /// <summary>A structured (object, array, number, boolean, or null) input.</summary>
    public static DecisionInput FromJson(JsonElement value) => new(null, value.Clone());

    /// <summary>The text a provider puts in its prompt: a string as is, JSON compact (<c>JSON.stringify</c>).</summary>
    public string ToText() => Text ?? JsonSerializer.Serialize(Json!.Value, Compact);

    /// <inheritdoc />
    public override string ToString() => ToText();
}

/// <summary>
/// One decision question (the AI SDK's <c>DecisionModelV4Question</c>). A <c>choice</c> question carries its options, a
/// <c>score</c> question its ordered levels, and a <c>boolean</c> question instructions or at least one criterion.
/// </summary>
public sealed class DecisionQuestion
{
    /// <summary><c>choice</c>, <c>score</c>, <c>boolean</c>, or <c>box</c>.</summary>
    public required string Type { get; init; }

    /// <summary>What to decide. Null when the criteria say it all.</summary>
    public DecisionInput? Instructions { get; init; }

    /// <summary>The options of a <c>choice</c> question, in order.</summary>
    public IReadOnlyList<DecisionChoice>? Choices { get; init; }

    /// <summary>
    /// A <c>boolean</c> question whose <c>criteria</c> is an explicit JSON <c>null</c> (upstream's <c>criteria: null</c>). .NET-only: C# has no
    /// way to tell it from absent criteria.
    /// </summary>
    public bool NullCriteria { get; init; }

    /// <summary>The descriptions of the ordered levels of a <c>score</c> question: level <c>i</c> is the <c>i</c>th entry.</summary>
    public IReadOnlyList<DecisionInput?>? Levels { get; init; }

    /// <summary>What makes a <c>boolean</c> question true.</summary>
    public DecisionInput? TrueCriteria { get; init; }

    /// <summary>What makes a <c>boolean</c> question false.</summary>
    public DecisionInput? FalseCriteria { get; init; }

    /// <summary>A <c>choice</c> question.</summary>
    public static DecisionQuestion Choice(DecisionInput? instructions, IReadOnlyList<DecisionChoice> choices) =>
        new()
        {
            Type = "choice",
            Instructions = instructions,
            Choices = choices,
        };

    /// <summary>A <c>score</c> question over ordered levels.</summary>
    public static DecisionQuestion Score(DecisionInput? instructions, IReadOnlyList<DecisionInput?> levels) =>
        new()
        {
            Type = "score",
            Instructions = instructions,
            Levels = levels,
        };

    /// <summary>A <c>box</c> question: where in an image something is.</summary>
    public static DecisionQuestion Box(DecisionInput? instructions) =>
        new() { Type = "box", Instructions = instructions };

    /// <summary>A <c>boolean</c> question.</summary>
    public static DecisionQuestion Boolean(
        DecisionInput? instructions,
        DecisionInput? trueCriteria = null,
        DecisionInput? falseCriteria = null
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
public sealed record DecisionChoice(string Value, DecisionInput? Description = null);

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

/// <summary>
/// A file, such as a screenshot, as bytes with its media type (the AI SDK's <c>file</c> part). The data is the bytes, or the same bytes as
/// base64 text in <see cref="Base64"/>; <see cref="DataType"/> names another kind (<c>url</c>, <c>reference</c>, or <c>text</c>) a provider may reject.
/// </summary>
public sealed record DecisionFilePart(string MediaType, byte[] Data) : DecisionStatePart
{
    /// <summary>The file's name.</summary>
    public string? Filename { get; init; }

    /// <summary>The data as base64 text, when it came that way; <see cref="Data"/> is then ignored.</summary>
    public string? Base64 { get; init; }

    /// <summary>The kind of data the part carries: <c>data</c>, <c>url</c>, <c>reference</c>, or <c>text</c>.</summary>
    public string DataType { get; init; } = "data";

    /// <summary>The URL of a <c>url</c> part.</summary>
    public Uri? Url { get; init; }

    /// <summary>The provider references of a <c>reference</c> part.</summary>
    public JsonElement? Reference { get; init; }

    /// <summary>The text of a <c>text</c> part.</summary>
    public string? Text { get; init; }

    /// <summary>The options for this part, keyed by provider name. Each value is a JSON value.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>The file's bytes.</summary>
    public byte[] GetBytes() => Base64 is null ? Data : Convert.FromBase64String(Base64);

    /// <summary>The file's bytes as base64 text.</summary>
    public string GetBase64() => Base64 ?? Convert.ToBase64String(Data);
}

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

    /// <summary>The provider's response headers (upstream's <c>response.headers</c>), lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>The provider's response body, as it sent it (upstream's <c>response.body</c>).</summary>
    public JsonElement? ResponseBody { get; init; }

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

/// <summary>
/// A deprecated decision model shape (the AI SDK's <c>Experimental_EvaluationModelV4</c>): the same model with <c>doEvaluate</c> in place
/// of <c>doDecide</c>. <see cref="ResolveModel.ResolveDecisionModel"/> adapts it to an <see cref="IDecisionModel"/>.
/// </summary>
public interface IEvaluationModel
{
    string Provider { get; }

    string ModelId { get; }

    /// <summary>The specification version this model implements. Only <c>v4</c> is accepted.</summary>
    string SpecificationVersion { get; }

    /// <summary>The question types this model can answer.</summary>
    IReadOnlyList<string> SupportedQuestionTypes { get; }

    /// <summary>Answers every question of <paramref name="call"/> in one request.</summary>
    Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken);
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

    /// <summary>The provider's response headers (upstream's <c>response.headers</c>), lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>The provider's response body, as it sent it (upstream's <c>response.body</c>).</summary>
    public JsonElement? ResponseBody { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
