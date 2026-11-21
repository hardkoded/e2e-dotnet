// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// The AI SDK's <c>GatewayAsyncJobMetadata</c> (<c>gateway-provider-metadata.ts</c>): metadata for an asynchronous AI
/// Gateway job.
/// </summary>
public sealed record GatewayAsyncJobMetadata
{
    /// <summary>The id of the job.</summary>
    public required string JobId { get; init; }

    /// <summary>The status of the job.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Secret for verifying customer webhook deliveries. This is sensitive, start-response-only metadata and must not be
    /// logged or forwarded.
    /// </summary>
    public string? WebhookSigningSecret { get; init; }

    /// <summary>Upstream's index signature: any other field of the job metadata, as JSON.</summary>
    public IReadOnlyDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>
/// The AI SDK's <c>GatewayProviderMetadata</c>: the shape of the inner <c>providerMetadata.gateway</c> object returned
/// by AI Gateway operations. Additional fields may be added without a package update.
/// </summary>
public sealed record GatewayProviderMetadata
{
    /// <summary>The metadata of the asynchronous job the operation started.</summary>
    public GatewayAsyncJobMetadata? AsyncJob { get; init; }

    /// <summary>Upstream's index signature: any other field of the metadata, as JSON.</summary>
    public IReadOnlyDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
