// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>GatewayGenerationInfoParams</c>.</summary>
public sealed class GatewayGenerationInfoParams
{
    /// <summary>The generation ID to look up (format: <c>gen_&lt;ulid&gt;</c>).</summary>
    public required string Id { get; init; }
}

/// <summary>Upstream's <c>GatewayGenerationInfo</c>: the cost, token usage, latency, and provider of one generation.</summary>
public sealed record GatewayGenerationInfo
{
    /// <summary>The generation ID.</summary>
    public required string Id { get; init; }

    /// <summary>Total cost in USD.</summary>
    public required double TotalCost { get; init; }

    /// <summary>Market cost in USD, excluding surcharges. Null when unavailable.</summary>
    public double? MarketCost { get; init; }

    /// <summary>Total surcharges applied in USD. Null when unavailable.</summary>
    public double? SurchargeCost { get; init; }

    /// <summary>Total amount debited in USD. Null when unavailable.</summary>
    public double? GatewayCost { get; init; }

    /// <summary>Upstream inference cost in USD (BYOK only).</summary>
    public required double UpstreamInferenceCost { get; init; }

    /// <summary>Usage cost in USD (same as <see cref="TotalCost"/>).</summary>
    public required double Usage { get; init; }

    /// <summary>ISO 8601 timestamp when the generation was created.</summary>
    public required string CreatedAt { get; init; }

    /// <summary>Model identifier.</summary>
    public required string Model { get; init; }

    /// <summary>Whether BYOK credentials were used.</summary>
    public required bool IsByok { get; init; }

    /// <summary>Provider that served this generation.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Whether streaming was used.</summary>
    public required bool Streamed { get; init; }

    /// <summary>Finish reason, such as <c>stop</c>.</summary>
    public required string FinishReason { get; init; }

    /// <summary>Time to first token in milliseconds.</summary>
    public required double Latency { get; init; }

    /// <summary>Total generation time in milliseconds.</summary>
    public required double GenerationTime { get; init; }

    /// <summary>Number of prompt tokens.</summary>
    public required double PromptTokens { get; init; }

    /// <summary>Number of completion tokens.</summary>
    public required double CompletionTokens { get; init; }

    /// <summary>Reasoning tokens used.</summary>
    public required double ReasoningTokens { get; init; }

    /// <summary>Cached tokens used.</summary>
    public required double CachedTokens { get; init; }

    /// <summary>Cache creation input tokens.</summary>
    public required double CacheCreationTokens { get; init; }

    /// <summary>Billable web search calls.</summary>
    public required double BillableWebSearchCalls { get; init; }
}

/// <summary>The AI SDK's <c>GatewayGenerationInfoFetcher</c> (<c>gateway-generation-info.ts</c>): <c>GET {origin}/v1/generation?id=...</c>.</summary>
public sealed class GatewayGenerationInfoFetcher(GatewayConfig config)
{
    /// <summary>Upstream's <c>getGenerationInfo</c>.</summary>
    public async Task<GatewayGenerationInfo> GetGenerationInfoAsync(
        GatewayGenerationInfoParams parameters,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parameters);
        try
        {
            var baseUrl = new Uri(config.BaseUrl);
            var headers = config.Headers is null
                ? null
                : ProviderUtils.CombineHeaders(await config.Headers(cancellationToken).ConfigureAwait(false));
            var response = await ProviderUtils
                .GetFromApiAsync(
                    GatewayHttp.Client(config),
                    baseUrl.GetLeftPart(UriPartial.Authority)
                        + "/v1/generation?id="
                        + GatewayHttp.EncodeUriComponent(parameters.Id),
                    headers,
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, ReadResponse);
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error);
        }
    }

    // gatewayGenerationInfoResponseSchema
    private static GatewayGenerationInfo ReadResponse(JsonElement root)
    {
        const string Path = "response.data";
        var data = Zod.Object(Zod.Property(Zod.Object(root, "response"), "data", "response"), Path);
        return new GatewayGenerationInfo
        {
            Id = Zod.String(data, "id", Path),
            TotalCost = Zod.Number(data, "total_cost", Path),
            MarketCost = Zod.NullishNumber(data, "market_cost", Path),
            SurchargeCost = Zod.NullishNumber(data, "surcharge_cost", Path),
            GatewayCost = Zod.NullishNumber(data, "gateway_cost", Path),
            UpstreamInferenceCost = Zod.Number(data, "upstream_inference_cost", Path),
            Usage = Zod.Number(data, "usage", Path),
            CreatedAt = Zod.String(data, "created_at", Path),
            Model = Zod.String(data, "model", Path),
            IsByok = Zod.Boolean(data, "is_byok", Path),
            ProviderName = Zod.String(data, "provider_name", Path),
            Streamed = Zod.Boolean(data, "streamed", Path),
            FinishReason = Zod.String(data, "finish_reason", Path),
            Latency = Zod.Number(data, "latency", Path),
            GenerationTime = Zod.Number(data, "generation_time", Path),
            PromptTokens = Zod.Number(data, "native_tokens_prompt", Path),
            CompletionTokens = Zod.Number(data, "native_tokens_completion", Path),
            ReasoningTokens = Zod.Number(data, "native_tokens_reasoning", Path),
            CachedTokens = Zod.Number(data, "native_tokens_cached", Path),
            CacheCreationTokens = Zod.Number(data, "native_tokens_cache_creation", Path),
            BillableWebSearchCalls = Zod.Number(data, "billable_web_search_calls", Path),
        };
    }
}
