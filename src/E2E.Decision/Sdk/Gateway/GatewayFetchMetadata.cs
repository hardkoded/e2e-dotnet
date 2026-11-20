// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>GatewayFetchMetadataResponse</c>.</summary>
public sealed record GatewayFetchMetadataResponse(IReadOnlyList<GatewayLanguageModelEntry> Models);

/// <summary>Upstream's <c>GatewayCreditsResponse</c>.</summary>
public sealed record GatewayCreditsResponse(string Balance, string TotalUsed)
{
    // The remaining gateway credit balance available for API usage is Balance; the total consumed is TotalUsed.
}

/// <summary>The AI SDK's <c>GatewayFetchMetadata</c> (<c>gateway-fetch-metadata.ts</c>): the models and the credits of the Gateway.</summary>
public sealed class GatewayFetchMetadata(GatewayConfig config)
{
    /// <summary>Upstream's <c>getAvailableModels</c>: <c>GET {baseURL}/config</c>.</summary>
    public async Task<GatewayFetchMetadataResponse> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var headers = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
            var response = await ProviderUtils
                .GetFromApiAsync(
                    GatewayHttp.Client(config),
                    config.BaseUrl + "/config",
                    headers,
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, ReadAvailableModels);
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error);
        }
    }

    /// <summary>Upstream's <c>getCredits</c>: <c>GET {origin}/v1/credits</c>, scoped to the team of the team header.</summary>
    public async Task<GatewayCreditsResponse> GetCreditsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = new Uri(config.BaseUrl);
            var headers = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
            var url = baseUrl.GetLeftPart(UriPartial.Authority) + "/v1/credits";
            if (GetTeamIdOrSlug(headers) is { } teamIdOrSlug)
            {
                url +=
                    "?"
                    + GatewayHttp.QueryString([
                        new(
                            teamIdOrSlug.StartsWith("team_", StringComparison.Ordinal) ? "teamId" : "slug",
                            teamIdOrSlug
                        ),
                    ]);
            }

            var response = await ProviderUtils
                .GetFromApiAsync(GatewayHttp.Client(config), url, headers, GatewayHttp.ErrorMessage, cancellationToken)
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, ReadCredits);
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error);
        }
    }

    private static string? GetTeamIdOrSlug(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (var (name, value) in headers)
        {
            if (string.Equals(name, GatewayHeaders.VercelAiGatewayTeamHeader, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }

        return null;
    }

    // gatewayAvailableModelsResponseSchema
    private static GatewayFetchMetadataResponse ReadAvailableModels(JsonElement root)
    {
        const string Path = "response";
        var models = new List<GatewayLanguageModelEntry>();
        foreach (
            var model in Zod.Array(Zod.Property(Zod.Object(root, Path), "models", Path), Path + ".models")
                .EnumerateArray()
        )
        {
            var item = Zod.Object(model, Path + ".models[]");
            var modelType = Zod.NullishString(item, "modelType", Path + ".models[]");
            var specification = Zod.Object(
                Zod.Property(item, "specification", Path + ".models[]"),
                Path + ".models[].specification"
            );
            var entry = new GatewayLanguageModelEntry
            {
                Id = Zod.String(item, "id", Path + ".models[]"),
                Name = Zod.String(item, "name", Path + ".models[]"),
                Description = Zod.NullishString(item, "description", Path + ".models[]"),
                Pricing = ReadPricing(item),
                Specification = new GatewayLanguageModelSpecification(
                    specification.TryGetProperty("specificationVersion", out var version)
                    && version.ValueKind == JsonValueKind.String
                    && version.GetString() == "v4"
                        ? "v4"
                        : throw Zod.Invalid(Path + ".models[].specification.specificationVersion", "expected 'v4'"),
                    Zod.String(specification, "provider", Path + ".models[].specification"),
                    Zod.String(specification, "modelId", Path + ".models[].specification")
                ),
                ModelType = modelType,
            };

            // Models of a type this SDK does not know are dropped.
            if (modelType is null || GatewayModelEntryTypes.KnownModelTypes.Contains(modelType))
            {
                models.Add(entry);
            }
        }

        return new GatewayFetchMetadataResponse(models);
    }

    private static GatewayModelPricing? ReadPricing(JsonElement item)
    {
        const string Path = "response.models[].pricing";
        if (!item.TryGetProperty("pricing", out var pricing) || pricing.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Zod.Object(pricing, Path);
        var cacheRead = Zod.NullishString(pricing, "input_cache_read", Path);
        var cacheWrite = Zod.NullishString(pricing, "input_cache_write", Path);
        return new GatewayModelPricing
        {
            Input = Zod.String(pricing, "input", Path),
            Output = Zod.String(pricing, "output", Path),
            CachedInputTokens = string.IsNullOrEmpty(cacheRead) ? null : cacheRead,
            CacheCreationInputTokens = string.IsNullOrEmpty(cacheWrite) ? null : cacheWrite,
        };
    }

    // gatewayCreditsResponseSchema
    private static GatewayCreditsResponse ReadCredits(JsonElement root)
    {
        const string Path = "response";
        Zod.Object(root, Path);
        return new GatewayCreditsResponse(Zod.String(root, "balance", Path), Zod.String(root, "total_used", Path));
    }

    private async Task<IReadOnlyDictionary<string, string>?> ResolveHeadersAsync(CancellationToken cancellationToken) =>
        config.Headers is null
            ? null
            : ProviderUtils.CombineHeaders(await config.Headers(cancellationToken).ConfigureAwait(false));
}
