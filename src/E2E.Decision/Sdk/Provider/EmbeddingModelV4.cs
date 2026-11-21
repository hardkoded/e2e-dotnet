// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>EmbeddingModelV4CallOptions</c>.</summary>
public sealed class EmbeddingModelV4CallOptions
{
    /// <summary>The values to embed.</summary>
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>The number of dimensions of the embeddings, for the models that support it.</summary>
    public int? Dimensions { get; init; }

    /// <summary>Provider-specific options, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>Additional HTTP headers for the request.</summary>
    public IReadOnlyDictionary<string, string?>? Headers { get; init; }
}

/// <summary>The AI SDK's <c>EmbeddingModelV4Result</c>.</summary>
public sealed class EmbeddingModelV4Result
{
    public required IReadOnlyList<IReadOnlyList<double>> Embeddings { get; init; }

    public EmbeddingModelV4Usage? Usage { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }

    public EmbeddingModelV4Response? Response { get; init; }

    public required IReadOnlyList<SharedV4Warning> Warnings { get; init; }
}

/// <summary>The <c>usage</c> of an <see cref="EmbeddingModelV4Result"/>.</summary>
public sealed record EmbeddingModelV4Usage(double Tokens);

/// <summary>The <c>response</c> of an <see cref="EmbeddingModelV4Result"/>: the headers and the body as the provider sent them.</summary>
public sealed record EmbeddingModelV4Response(IReadOnlyDictionary<string, string>? Headers, JsonElement? Body);

/// <summary>The AI SDK's <c>EmbeddingModelV4</c>: a model that turns values into embeddings.</summary>
public interface IEmbeddingModelV4
{
    string SpecificationVersion { get; }

    string Provider { get; }

    string ModelId { get; }

    int? MaxEmbeddingsPerCall { get; }

    bool SupportsParallelCalls { get; }

    Task<EmbeddingModelV4Result> DoEmbedAsync(
        EmbeddingModelV4CallOptions options,
        CancellationToken cancellationToken = default
    );
}
