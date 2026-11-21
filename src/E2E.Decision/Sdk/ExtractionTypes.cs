// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>
/// A typed value to extract from the shared state (the AI SDK's <c>Experimental_ExtractionModelV4Field</c>). <see cref="Type"/> is
/// <c>text</c>, <c>number</c>, <c>date</c>, <c>time</c>, <c>datetime</c>, <c>duration</c>, or <c>list</c>. Instructions are a
/// <see cref="DecisionInput"/> (upstream's <c>Experimental_ExtractionModelV4Input</c>).
/// </summary>
public sealed class ExtractionField
{
    public required string Type { get; init; }

    /// <summary>What to extract. <see cref="DecisionInput.Null"/> writes an explicit JSON <c>null</c>; a C# null leaves the field out.</summary>
    public DecisionInput? Instructions { get; init; }

    /// <summary>The unit of a <c>number</c> value (such as <c>USD</c> or <c>kg</c>), or of a <c>duration</c> (<c>seconds</c> through <c>years</c>; defaults to <c>seconds</c>).</summary>
    public string? Unit { get; init; }

    /// <summary>Requires an integer value (<c>number</c>, <c>duration</c>, and <c>list</c> of numbers).</summary>
    public bool? Integer { get; init; }

    /// <summary>The maximum decimal places, an integer from 0 to 6 (<c>number</c>, <c>duration</c>, and <c>list</c> of numbers).</summary>
    public int? Decimals { get; init; }

    /// <summary>The item type of a <c>list</c>: <c>text</c>, <c>number</c>, or <c>date</c>.</summary>
    public string? Items { get; init; }

    /// <summary>Free text, returned as a string.</summary>
    public static ExtractionField Text(DecisionInput? instructions = null) =>
        new() { Type = "text", Instructions = instructions };

    /// <summary>A number, returned as a finite number.</summary>
    public static ExtractionField Number(
        DecisionInput? instructions = null,
        string? unit = null,
        bool? integer = null,
        int? decimals = null
    ) =>
        new()
        {
            Type = "number",
            Instructions = instructions,
            Unit = unit,
            Integer = integer,
            Decimals = decimals,
        };

    /// <summary>A calendar date, returned as <c>YYYY-MM-DD</c>.</summary>
    public static ExtractionField Date(DecisionInput? instructions = null) =>
        new() { Type = "date", Instructions = instructions };

    /// <summary>A time of day, returned as <c>HH:MM</c> or <c>HH:MM:SS</c>.</summary>
    public static ExtractionField Time(DecisionInput? instructions = null) =>
        new() { Type = "time", Instructions = instructions };

    /// <summary>An ISO 8601 date and time, with an offset when the source has one.</summary>
    public static ExtractionField Datetime(DecisionInput? instructions = null) =>
        new() { Type = "datetime", Instructions = instructions };

    /// <summary>A nonnegative length of time, returned as a number in <paramref name="unit"/>.</summary>
    public static ExtractionField Duration(
        DecisionInput? instructions = null,
        string? unit = null,
        bool? integer = null,
        int? decimals = null
    ) =>
        new()
        {
            Type = "duration",
            Instructions = instructions,
            Unit = unit,
            Integer = integer,
            Decimals = decimals,
        };

    /// <summary>Zero or more values of the same item type: <c>text</c>, <c>number</c>, or <c>date</c>.</summary>
    public static ExtractionField List(
        string items,
        DecisionInput? instructions = null,
        bool? integer = null,
        int? decimals = null
    ) =>
        new()
        {
            Type = "list",
            Instructions = instructions,
            Items = items,
            Integer = integer,
            Decimals = decimals,
        };
}

/// <summary>Where in the state a value was found (the AI SDK's <c>Experimental_ExtractionModelV4Source</c>).</summary>
/// <param name="Type"><c>text</c> or <c>file</c>.</param>
/// <param name="Part">The index of the part in the state.</param>
/// <param name="Start">For a <c>text</c> source, the start offset in UTF-16 code units, inclusive.</param>
/// <param name="End">For a <c>text</c> source, the end offset in UTF-16 code units, exclusive.</param>
public sealed record ExtractionSource(string Type, int Part, int? Start = null, int? End = null);

/// <summary>One entry of a list answer's <c>items</c>.</summary>
public sealed record ExtractionListItem(double? Confidence = null, ExtractionSource? Source = null);

/// <summary>
/// One answer of an extraction (the AI SDK's <c>Experimental_ExtractionModelV4Answer</c>). <see cref="Type"/> is the field's type, or
/// <c>refusal</c> when the model declined the field. A value is null when the state does not contain one.
/// </summary>
public sealed class ExtractionAnswer
{
    public required string Type { get; init; }

    /// <summary>The value of a <c>text</c>, <c>date</c>, <c>time</c>, or <c>datetime</c> answer.</summary>
    public string? StringValue { get; init; }

    /// <summary>The value of a <c>number</c> or <c>duration</c> answer.</summary>
    public double? NumberValue { get; init; }

    /// <summary>The value of a <c>list</c> answer of strings. An empty list is read as this one.</summary>
    public IReadOnlyList<string>? StringListValue { get; init; }

    /// <summary>The value of a <c>list</c> answer of numbers.</summary>
    public IReadOnlyList<double>? NumberListValue { get; init; }

    /// <summary>The most likely value of a <c>text</c>, <c>date</c>, <c>time</c>, or <c>datetime</c> answer, also when the value is null.</summary>
    public string? StringGuess { get; init; }

    /// <summary>The most likely value of a <c>number</c> or <c>duration</c> answer, also when the value is null.</summary>
    public double? NumberGuess { get; init; }

    /// <summary>The probability in [0, 1] that the state does not contain a value.</summary>
    public double? PNull { get; init; }

    /// <summary>The confidence in [0, 1] that the value is correct.</summary>
    public double? Confidence { get; init; }

    /// <summary>Where the value was found (not on a <c>list</c> answer).</summary>
    public ExtractionSource? Source { get; init; }

    /// <summary>One entry per item of a <c>list</c> answer, when available.</summary>
    public IReadOnlyList<ExtractionListItem>? Items { get; init; }
}

/// <summary>What an extraction model answers a call with (the AI SDK's <c>Experimental_ExtractionModelV4Result</c>).</summary>
public sealed class ExtractionModelResult
{
    /// <summary>Exactly one answer per field, under the field ids.</summary>
    public required IReadOnlyDictionary<string, ExtractionAnswer> Answers { get; init; }

    public DecisionUsage? Usage { get; init; }

    public IReadOnlyList<SharedV4Warning> Warnings { get; init; } = [];

    /// <summary>What the provider reported beside the answers, by provider name.</summary>
    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The id of the response (upstream's <c>response.id</c>), when the provider sends one.</summary>
    public string? ResponseId { get; init; }

    /// <summary>The timestamp of the response (upstream's <c>response.timestamp</c>), when the provider sends one.</summary>
    public DateTimeOffset? ResponseTimestamp { get; init; }

    /// <summary>The id of the model that served the call (upstream's <c>response.modelId</c>).</summary>
    public string? ResponseModelId { get; init; }

    /// <summary>The provider's response headers (upstream's <c>response.headers</c>), lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>The provider's response body, as it sent it (upstream's <c>response.body</c>).</summary>
    public JsonElement? ResponseBody { get; init; }
}

/// <summary>What <see cref="IExtractionModel.DoExtractAsync"/> takes (the AI SDK's <c>Experimental_ExtractionModelV4CallOptions</c>).</summary>
public sealed class ExtractionModelCall
{
    /// <summary>Ordered text, file, and JSON parts. Answer sources index into this list.</summary>
    public required IReadOnlyList<DecisionStatePart> State { get; init; }

    public required IReadOnlyDictionary<string, ExtractionField> Fields { get; init; }

    /// <summary>A <c>YYYY-MM-DD</c> date that anchors relative dates and dates without a year.</summary>
    public string? ReferenceDate { get; init; }

    public int? MaxOutputTokens { get; init; }

    /// <summary>Reasoning effort for supported models: <c>provider-default</c>, <c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c>, or <c>max</c>.</summary>
    public string? Reasoning { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>The provider options, keyed by provider name. Each value is a JSON object.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
}

/// <summary>An extraction model (the AI SDK's <c>Experimental_ExtractionModelV4</c>). Only specification version <c>v4</c> exists.</summary>
public interface IExtractionModel
{
    string SpecificationVersion { get; }

    string Provider { get; }

    string ModelId { get; }

    /// <summary>The field types this model supports, used to reject unsupported calls before any I/O.</summary>
    IReadOnlyList<string> SupportedFieldTypes { get; }

    /// <summary>Extracts every field from the same state. No partial results.</summary>
    Task<ExtractionModelResult> DoExtractAsync(ExtractionModelCall call, CancellationToken cancellationToken);
}
