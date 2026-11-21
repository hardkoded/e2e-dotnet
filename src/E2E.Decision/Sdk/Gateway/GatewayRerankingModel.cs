// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The config of a <see cref="GatewayRerankingModel"/> (upstream's <c>GatewayConfig &amp; { provider, o11yHeaders }</c>).</summary>
public sealed class GatewayRerankingConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>Reranks documents through the Vercel AI Gateway, <c>POST /reranking-model</c> (the AI SDK's <c>GatewayRerankingModel</c>).</summary>
public sealed class GatewayRerankingModel : IRerankingModelV4, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayRerankingModel(string modelId, GatewayRerankingConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        Config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    public string ModelId { get; }

    public string Provider => Config.Provider;

    /// <summary>Upstream's private <c>config</c>.</summary>
    internal GatewayRerankingConfig Config { get; }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    public async Task<RerankingModelV4Result> DoRerankAsync(
        RerankingModelV4CallOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedHeaders = Config.Headers is null
            ? null
            : await Config.Headers(cancellationToken).ConfigureAwait(false);
        try
        {
            var headers = ProviderUtils.CombineHeaders(
                resolvedHeaders,
                options.Headers,
                new Dictionary<string, string?>
                {
                    ["ai-reranking-model-specification-version"] = "4",
                    ["ai-model-id"] = ModelId,
                },
                await Config.O11yHeaders(cancellationToken).ConfigureAwait(false)
            );
            var body = new JsonObject { ["documents"] = DocumentsJson(options.Documents), ["query"] = options.Query };
            if (options.TopN is { } topN)
            {
                body["topN"] = topN;
            }

            if (options.ProviderOptions is { } providerOptions)
            {
                body["providerOptions"] = JsonSerializer.SerializeToNode(providerOptions);
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    Config.BaseUrl + "/reranking-model",
                    headers,
                    body.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, value => ReadResponse(value, response));
        }
        catch (Exception error) when (error is ApiCallException or GatewayException)
        {
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    private static JsonObject DocumentsJson(RerankingModelV4Documents documents) =>
        documents switch
        {
            RerankingModelV4TextDocuments text => new JsonObject
            {
                ["type"] = "text",
                ["values"] = new JsonArray(text.Values.Select(v => (JsonNode?)v).ToArray()),
            },
            RerankingModelV4ObjectDocuments objects => new JsonObject
            {
                ["type"] = "object",
                ["values"] = new JsonArray(objects.Values.Select(v => JsonSerializer.SerializeToNode(v)).ToArray()),
            },
            _ => throw new ArgumentException("Unknown documents type.", nameof(documents)),
        };

    // gatewayRerankingResponseSchema.
    private static RerankingModelV4Result ReadResponse(JsonElement root, ApiResponse response)
    {
        const string path = "response";
        Zod.Object(root, path);
        var ranking = new List<RerankingModelV4Ranking>();
        var index = 0;
        foreach (var entry in Zod.Array(Zod.Property(root, "ranking", path), path + ".ranking").EnumerateArray())
        {
            var entryPath = path + ".ranking[" + index + "]";
            Zod.Object(entry, entryPath);
            ranking.Add(
                new RerankingModelV4Ranking(
                    Zod.Number(entry, "index", entryPath),
                    Zod.Number(entry, "relevanceScore", entryPath)
                )
            );
            index++;
        }

        return new RerankingModelV4Result
        {
            Ranking = ranking,
            ProviderMetadata = Zod.OptionalProviderMetadata(root, path),
            Response = new RerankingModelV4Response { Headers = response.ResponseHeaders, Body = response.Value },
            Warnings = GatewayWarningSchema.ReadOptionalArray(root, "warnings", path),
        };
    }
}
