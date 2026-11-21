// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>
/// The call options every batch operation takes (upstream's <c>BatchV4CallOptions</c>). The upstream <c>abortSignal</c> is the
/// <see cref="CancellationToken"/> of the call.
/// </summary>
public record BatchV4CallOptions
{
    /// <summary>Provider-specific options, keyed by provider name.</summary>
    public JsonObject? ProviderOptions { get; init; }

    /// <summary>Additional HTTP headers. A null value drops the header.</summary>
    public IReadOnlyDictionary<string, string?>? Headers { get; init; }
}

/// <summary>Upstream's <c>BatchV4StartOptions</c> (<c>BatchV4StartCallOptions &amp; { requests }</c>).</summary>
public sealed record BatchV4StartOptions : BatchV4CallOptions
{
    /// <summary>A URL the provider calls when the batch ends.</summary>
    public string? WebhookUrl { get; init; }

    /// <summary>The requests of the batch.</summary>
    public required IReadOnlyList<BatchV4Request> Requests { get; init; }
}

/// <summary>Upstream's <c>BatchV4OperationOptions</c>.</summary>
public sealed record BatchV4OperationOptions : BatchV4CallOptions
{
    /// <summary>The id of the batch.</summary>
    public required string BatchId { get; init; }
}

/// <summary>Upstream's <c>BatchV4Error</c>.</summary>
public sealed record BatchV4Error
{
    public required string Message { get; init; }

    public string? Type { get; init; }

    public string? Code { get; init; }

    public int? StatusCode { get; init; }
}

/// <summary>Upstream's <c>BatchV4Status['requestCounts']</c>.</summary>
public sealed record BatchV4RequestCounts(long Total, long Pending, long Completed, long Failed);

/// <summary>Upstream's <c>BatchV4Status</c>.</summary>
public record BatchV4Status
{
    /// <summary><c>pending</c>, <c>completed</c>, or <c>failed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The provider's own status.</summary>
    public string? RawStatus { get; init; }

    public BatchV4RequestCounts? RequestCounts { get; init; }

    public BatchV4Error? Error { get; init; }

    public string? CreatedAt { get; init; }

    public string? ExpiresAt { get; init; }

    public JsonObject? ProviderMetadata { get; init; }
}

/// <summary>One warning of a batch start (an element of upstream's <c>BatchV4StartResult['warnings']</c>).</summary>
public sealed record BatchV4Warning(string? RequestId, JsonNode? Warning);

/// <summary>Upstream's <c>BatchV4StartResult</c>.</summary>
public sealed record BatchV4StartResult : BatchV4Status
{
    public required string BatchId { get; init; }

    public required IReadOnlyList<BatchV4Warning> Warnings { get; init; }
}

/// <summary>Upstream's <c>BatchV4CancelResult</c>.</summary>
public sealed record BatchV4CancelResult
{
    public JsonObject? ProviderMetadata { get; init; }
}

/// <summary>
/// One result of a batch (upstream's <c>BatchV4ItemResult</c>, <c>TextBatchV4ItemResult | ImageBatchV4ItemResult</c>). <see cref="Result"/> is
/// upstream's <c>LanguageModelV4GenerateResult</c> (or <c>ImageModelV4Result</c>) as the JSON the provider sent, because this port has no model
/// result types for them; <c>response.timestamp</c> is revived into a <see cref="DateTimeOffset"/> value.
/// </summary>
public sealed record BatchV4ItemResult
{
    /// <summary><c>text</c> or <c>image</c>.</summary>
    public required string Type { get; init; }

    public required string Id { get; init; }

    /// <summary><c>succeeded</c>, <c>failed</c>, <c>cancelled</c>, or <c>expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The result of a <c>succeeded</c> item.</summary>
    public JsonObject? Result { get; init; }

    public BatchV4Error? Error { get; init; }

    public JsonObject? ProviderMetadata { get; init; }

    /// <summary>The whole line, as the provider sent it, with the revived timestamp.</summary>
    public required JsonObject Raw { get; init; }
}

/// <summary>Upstream's <c>BatchV4ModelIds</c> is the type parameter of <see cref="IBatchV4"/>; the port types ids as strings.</summary>
public interface IBatchV4
{
    /// <summary>Always <c>v4</c>.</summary>
    string SpecificationVersion { get; }

    /// <summary>The provider id.</summary>
    string Provider { get; }

    /// <summary>Starts a batch.</summary>
    Task<BatchV4StartResult> DoStartBatchAsync(
        BatchV4StartOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets the status of a batch.</summary>
    Task<BatchV4Status> DoGetBatchStatusAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Streams the results of a batch.</summary>
    Task<IAsyncEnumerable<BatchV4ItemResult>> DoGetBatchResultsAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Requests the cancellation of a batch.</summary>
    Task<BatchV4CancelResult> DoCancelBatchAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    );
}
