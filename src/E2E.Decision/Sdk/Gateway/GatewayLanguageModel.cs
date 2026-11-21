// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayChatConfig</c> (<c>gateway-language-model.ts</c>): <c>GatewayConfig &amp; { provider, o11yHeaders }</c>.</summary>
public sealed class GatewayChatConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>
/// Calls a language model through the Vercel AI Gateway, <c>POST /language-model</c> (the AI SDK's <c>GatewayLanguageModel</c> in
/// <c>@ai-sdk/gateway</c>). The Gateway routes the call to the model named by the id, such as <c>anthropic/claude-sonnet-5</c>. The Gateway's
/// response is not validated (<c>z.any()</c> upstream): the model gives the JSON it sent. Not ported: <c>WORKFLOW_SERIALIZE</c> and
/// <c>WORKFLOW_DESERIALIZE</c>, since the port has no workflow serialization.
/// </summary>
public class GatewayLanguageModel : ILanguageModelV4, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayLanguageModel(string modelId, GatewayChatConfig config)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        Config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    public IReadOnlyDictionary<string, IReadOnlyList<Regex>> SupportedUrls { get; } =
        new Dictionary<string, IReadOnlyList<Regex>>
        {
            ["*/*"] = [new Regex(".*", RegexOptions.None, TimeSpan.FromSeconds(1))],
        };

    public string ModelId { get; }

    public string Provider => Config.Provider;

    /// <summary>Upstream's protected <c>config</c>. Internal as well, so the tests can read what the provider gave the model.</summary>
    protected internal GatewayChatConfig Config { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<LanguageModelV4GenerateResult> DoGenerateAsync(
        LanguageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var (args, warnings) = GetArgs(options);
        var resolvedHeaders = Config.Headers is null
            ? null
            : await Config.Headers(cancellationToken).ConfigureAwait(false);
        try
        {
            var headers = await RequestHeadersAsync(options, resolvedHeaders, false, cancellationToken)
                .ConfigureAwait(false);
            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    GetUrl(),
                    headers,
                    args.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, body =>
                ReadResult(body, response, args, warnings)
            );
        }
        catch (Exception error)
        {
            // A caller's cancellation is rethrown as it is.
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    public async Task<LanguageModelV4StreamResult> DoStreamAsync(
        LanguageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var (args, warnings) = GetArgs(options);
        var resolvedHeaders = Config.Headers is null
            ? null
            : await Config.Headers(cancellationToken).ConfigureAwait(false);
        try
        {
            var headers = await RequestHeadersAsync(options, resolvedHeaders, true, cancellationToken)
                .ConfigureAwait(false);
            var response = await ProviderUtils
                .PostJsonToApiStreamAsync(
                    _http,
                    GetUrl(),
                    headers,
                    args.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new LanguageModelV4StreamResult
            {
                Stream = TransformStreamAsync(response, options.IncludeRawChunks == true, warnings, cancellationToken),
                Request = new LanguageModelV4RequestMetadata { Body = args },
                Response = new LanguageModelV4ResponseMetadata { Headers = response.ResponseHeaders },
            };
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    /// <summary>Encodes inline bytes of file data to a base64 string. Upstream encodes in place; the options given are not changed here.</summary>
    /// <param name="options">The options to encode.</param>
    /// <returns>The options with the file data encoded.</returns>
    protected virtual LanguageModelV4CallOptions MaybeEncodeFileParts(LanguageModelV4CallOptions options) =>
        options with
        {
            Prompt = [.. options.Prompt.Select(message => message.MapParts(EncodePart))],
        };

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _ownsClient)
        {
            _http.Dispose();
        }
    }

    private static LanguageModelV4PromptPart EncodePart(LanguageModelV4PromptPart part) =>
        part switch
        {
            LanguageModelV4FilePart file => file with { Data = MaybeBase64EncodeFileData(file.Data) },
            LanguageModelV4ReasoningFilePart file => file with { Data = MaybeBase64EncodeFileData(file.Data) },
            LanguageModelV4ToolResultPart result => result with
            {
                Output = result.Output.MapParts(content =>
                    content is LanguageModelV4ToolResultContentFile file
                        ? file with
                        {
                            Data = MaybeBase64EncodeFileData(file.Data),
                        }
                        : content
                ),
            },
            _ => part,
        };

    private static SharedV4FileData MaybeBase64EncodeFileData(SharedV4FileData data) =>
        data is SharedV4FileDataData { Bytes: { } bytes }
            ? new SharedV4FileDataData(Convert.ToBase64String(bytes))
            : data;

    private static LanguageModelV4GenerateResult ReadResult(
        JsonElement body,
        ApiResponse response,
        System.Text.Json.Nodes.JsonObject args,
        IReadOnlyList<SharedV4Warning> warnings
    )
    {
        var additional = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var allWarnings = new List<SharedV4Warning>();
        JsonElement? content = null;
        JsonElement? finishReason = null;
        JsonElement? usage = null;
        JsonElement? providerMetadata = null;
        string? structuredOutputText = null;
        if (body.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in body.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "content":
                        content = property.Value.Clone();
                        break;
                    case "finishReason":
                        finishReason = property.Value.Clone();
                        break;
                    case "usage":
                        usage = property.Value.Clone();
                        break;
                    case "providerMetadata":
                        providerMetadata = property.Value.Clone();
                        break;
                    case "structuredOutputText":
                        structuredOutputText =
                            property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "warnings":
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            allWarnings.AddRange(property.Value.EnumerateArray().Select(SharedV4Warning.FromJson));
                        }
                        else if (property.Value.ValueKind != JsonValueKind.Null)
                        {
                            throw new InvalidResponseDataException(
                                "Type validation failed: warnings must be an array."
                            );
                        }

                        break;
                    case "request":
                    case "response":
                        // Replaced by the request and response of the call.
                        break;
                    default:
                        additional[property.Name] = property.Value.Clone();
                        break;
                }
            }
        }

        // warnings: [...(responseBody.warnings ?? []), ...warnings]
        allWarnings.AddRange(warnings);
        return new LanguageModelV4GenerateResult
        {
            Content = content,
            StructuredOutputText = structuredOutputText,
            FinishReason = finishReason,
            Usage = usage,
            ProviderMetadata = providerMetadata,
            Request = new LanguageModelV4RequestMetadata { Body = args },
            Response = new LanguageModelV4ResponseMetadata
            {
                Headers = response.ResponseHeaders,
                Body = response.Value,
            },
            Warnings = allWarnings,
            AdditionalProperties = additional,
        };
    }

    private static LanguageModelV4StreamPart ReadStreamPart(JsonElement part)
    {
        var type =
            part.ValueKind == JsonValueKind.Object
            && part.TryGetProperty("type", out var typeElement)
            && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()!
                : string.Empty;
        // response-metadata: a timestamp the Gateway sent as a string becomes a Date.
        DateTimeOffset? timestamp = null;
        if (
            type == "response-metadata"
            && part.TryGetProperty("timestamp", out var timestampElement)
            && timestampElement.ValueKind == JsonValueKind.String
        )
        {
            timestamp = LanguageModelV4Json.ParseTimestamp(timestampElement.GetString()!);
        }

        IReadOnlyList<SharedV4Warning>? warnings = null;
        if (
            type == "stream-start"
            && part.TryGetProperty("warnings", out var warningsElement)
            && warningsElement.ValueKind == JsonValueKind.Array
        )
        {
            warnings = [.. warningsElement.EnumerateArray().Select(SharedV4Warning.FromJson)];
        }

        return new LanguageModelV4StreamPart
        {
            Type = type,
            Json = part,
            Timestamp = timestamp,
            Warnings = warnings,
        };
    }

    // The TransformStream of doStream. The enumeration owns the response and releases it when the stream ends, fails, or is disposed early.
    private static async IAsyncEnumerable<LanguageModelV4StreamPart> TransformStreamAsync(
        ApiStreamResponse response,
        bool includeRawChunks,
        IReadOnlyList<SharedV4Warning> warnings,
        CancellationToken callToken,
        [EnumeratorCancellation] CancellationToken enumeratorToken = default
    )
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callToken, enumeratorToken);
        if (warnings.Count > 0)
        {
            yield return LanguageModelV4StreamPart.StreamStart(warnings);
        }

        await foreach (
            var chunk in ProviderUtils.ParseJsonEventStreamAsync(response, linked.Token).ConfigureAwait(false)
        )
        {
            if (chunk.Error is not null)
            {
                throw chunk.Error;
            }

            var streamPart = ReadStreamPart(chunk.Value!.Value);

            // Handle raw chunks: if this is a raw chunk from the gateway API, only emit it if includeRawChunks is true.
            if (streamPart.Type == "raw" && !includeRawChunks)
            {
                continue;
            }

            yield return streamPart;
        }
    }

    // getArgs: the options without the abort signal (the cancellation token here), with the file parts encoded.
    private (System.Text.Json.Nodes.JsonObject Args, IReadOnlyList<SharedV4Warning> Warnings) GetArgs(
        LanguageModelV4CallOptions options
    ) => (LanguageModelV4Json.ToJson(MaybeEncodeFileParts(options)), []);

    private async Task<Dictionary<string, string>> RequestHeadersAsync(
        LanguageModelV4CallOptions options,
        IReadOnlyDictionary<string, string?>? resolvedHeaders,
        bool streaming,
        CancellationToken cancellationToken
    )
    {
        var o11yHeaders = await Config.O11yHeaders(cancellationToken).ConfigureAwait(false);
        return ProviderUtils.CombineHeaders(
            resolvedHeaders,
            options.Headers,
            GetModelConfigHeaders(streaming),
            o11yHeaders
        );
    }

    private string GetUrl() => Config.BaseUrl.TrimEnd('/') + "/language-model";

    private Dictionary<string, string?> GetModelConfigHeaders(bool streaming) =>
        new()
        {
            ["ai-language-model-specification-version"] = "4",
            ["ai-language-model-id"] = ModelId,
            ["ai-language-model-streaming"] = streaming ? "true" : "false",
        };
}
