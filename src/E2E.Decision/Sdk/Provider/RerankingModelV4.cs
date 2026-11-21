// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>The documents of a <see cref="RerankingModelV4CallOptions"/>: text, or JSON objects.</summary>
public abstract record RerankingModelV4Documents;

/// <summary>Text documents (<c>type: 'text'</c>).</summary>
public sealed record RerankingModelV4TextDocuments(IReadOnlyList<string> Values) : RerankingModelV4Documents;

/// <summary>Object documents (<c>type: 'object'</c>).</summary>
public sealed record RerankingModelV4ObjectDocuments(IReadOnlyList<JsonElement> Values) : RerankingModelV4Documents;

/// <summary>The AI SDK's <c>RerankingModelV4CallOptions</c>.</summary>
public sealed class RerankingModelV4CallOptions
{
    public required RerankingModelV4Documents Documents { get; init; }

    public required string Query { get; init; }

    /// <summary>The number of top documents to return.</summary>
    public int? TopN { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    public IReadOnlyDictionary<string, string?>? Headers { get; init; }
}

/// <summary>One document of a ranking.</summary>
public sealed record RerankingModelV4Ranking(double Index, double RelevanceScore);

/// <summary>The AI SDK's <c>RerankingModelV4Result</c>.</summary>
public sealed class RerankingModelV4Result
{
    public required IReadOnlyList<RerankingModelV4Ranking> Ranking { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }

    public IReadOnlyList<SharedV4Warning>? Warnings { get; init; }

    public RerankingModelV4Response? Response { get; init; }
}

/// <summary>The <c>response</c> of a <see cref="RerankingModelV4Result"/>.</summary>
public sealed class RerankingModelV4Response
{
    public string? Id { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string? ModelId { get; init; }

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public JsonElement? Body { get; init; }
}

/// <summary>The AI SDK's <c>RerankingModelV4</c>: a model that ranks documents against a query.</summary>
public interface IRerankingModelV4
{
    string SpecificationVersion { get; }

    string Provider { get; }

    string ModelId { get; }

    Task<RerankingModelV4Result> DoRerankAsync(
        RerankingModelV4CallOptions options,
        CancellationToken cancellationToken = default
    );
}
