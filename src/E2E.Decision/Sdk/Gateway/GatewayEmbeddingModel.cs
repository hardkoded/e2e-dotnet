// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The config of a <see cref="GatewayEmbeddingModel"/> (upstream's <c>GatewayEmbeddingConfig</c>: <c>GatewayConfig &amp; { provider, o11yHeaders }</c>).</summary>
public sealed class GatewayEmbeddingConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>Generates embeddings through the Vercel AI Gateway, <c>POST /embedding-model</c> (the AI SDK's <c>GatewayEmbeddingModel</c>).</summary>
public sealed class GatewayEmbeddingModel : IEmbeddingModelV4, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayEmbeddingModel(string modelId, GatewayEmbeddingConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        Config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    public int? MaxEmbeddingsPerCall => 2048;

    public bool SupportsParallelCalls => true;

    public string ModelId { get; }

    public string Provider => Config.Provider;

    /// <summary>Upstream's private <c>config</c>.</summary>
    internal GatewayEmbeddingConfig Config { get; }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    public async Task<EmbeddingModelV4Result> DoEmbedAsync(
        EmbeddingModelV4CallOptions options,
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
                    ["ai-embedding-model-specification-version"] = "4",
                    ["ai-model-id"] = ModelId,
                },
                await Config.O11yHeaders(cancellationToken).ConfigureAwait(false)
            );
            var body = new JsonObject
            {
                ["values"] = new JsonArray(options.Values.Select(v => (JsonNode?)v).ToArray()),
            };
            if (options.Dimensions is { } dimensions)
            {
                body["dimensions"] = dimensions;
            }

            if (options.ProviderOptions is { } providerOptions)
            {
                body["providerOptions"] = JsonSerializer.SerializeToNode(providerOptions);
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    Config.BaseUrl + "/embedding-model",
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

    // gatewayEmbeddingResponseSchema.
    private static EmbeddingModelV4Result ReadResponse(JsonElement root, ApiResponse response)
    {
        const string path = "response";
        Zod.Object(root, path);
        var embeddings = new List<IReadOnlyList<double>>();
        var index = 0;
        foreach (
            var embedding in Zod.Array(Zod.Property(root, "embeddings", path), path + ".embeddings").EnumerateArray()
        )
        {
            var values = new List<double>();
            foreach (var number in Zod.Array(embedding, path + ".embeddings[" + index + "]").EnumerateArray())
            {
                values.Add(
                    number.ValueKind == JsonValueKind.Number
                        ? number.GetDouble()
                        : throw Zod.Invalid(path + ".embeddings[" + index + "]", "expected number")
                );
            }

            embeddings.Add(values);
            index++;
        }

        EmbeddingModelV4Usage? usage = null;
        if (root.TryGetProperty("usage", out var rawUsage) && rawUsage.ValueKind != JsonValueKind.Null)
        {
            usage = new EmbeddingModelV4Usage(
                Zod.Number(Zod.Object(rawUsage, path + ".usage"), "tokens", path + ".usage")
            );
        }

        return new EmbeddingModelV4Result
        {
            Embeddings = embeddings,
            Usage = usage,
            ProviderMetadata = Zod.OptionalProviderMetadata(root, path),
            Response = new EmbeddingModelV4Response(response.ResponseHeaders, response.Value),
            Warnings = GatewayWarningSchema.ParseOptionalArray(root, "warnings", path),
        };
    }
}
