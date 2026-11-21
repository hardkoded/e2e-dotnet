// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>GatewayChatConfig &amp; { maxLineBytes?: number }</c>, the config of a <see cref="GatewayBatch"/>.</summary>
public sealed class GatewayBatchConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }

    /// <summary>The maximum UTF-8 bytes of a result row, excluding the line feed. Defaults to 64 MiB.</summary>
    public int? MaxLineBytes { get; init; }
}

/// <summary>The AI SDK's <c>GatewayBatch</c> (<c>gateway-batch.ts</c>): the Gateway's async batch surface of text-generation requests.</summary>
public sealed class GatewayBatch : IBatchV4, IDisposable
{
    private readonly GatewayBatchConfig _config;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayBatch(GatewayBatchConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        Provider = config.Provider + ".batch";
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    public string Provider { get; }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>
    /// Starts a durable batch of text-generation requests through the Gateway's async batch surface (<c>POST {baseURL}/batch/start</c>). The
    /// returned <c>BatchId</c> is the Gateway job id: provider-native batch ids stay server-side, so status and results always route back
    /// through the Gateway job.
    /// </summary>
    public async Task<BatchV4StartResult> DoStartBatchAsync(
        BatchV4StartOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        AssertTextBatchRequests(options.Requests);
        var modelId = ValidateSingleModel(options.Requests);

        var resolvedHeaders = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
        var idempotencyKey = GetGatewayBatchIdempotencyKey(options.ProviderOptions);
        var forwardedProviderOptions = OmitGatewayIdempotencyKey(options.ProviderOptions);

        try
        {
            var headers = ProviderUtils.CombineHeaders(
                resolvedHeaders,
                options.Headers,
                new Dictionary<string, string?> { ["ai-model-id"] = modelId },
                await _config.O11yHeaders(cancellationToken).ConfigureAwait(false),
                idempotencyKey is not null
                    ? new Dictionary<string, string?> { ["idempotency-key"] = idempotencyKey }
                    : null
            );
            var body = new JsonObject();
            if (options.WebhookUrl is not null)
            {
                body["callbackUrl"] = options.WebhookUrl;
            }

            var requests = new JsonArray();
            foreach (var request in options.Requests)
            {
                requests.Add(
                    new JsonObject
                    {
                        ["id"] = request.Id,
                        ["type"] = request.Type,
                        ["modelId"] = request.ModelId,
                        ["options"] = MaybeEncodeBatchFileParts(request.Options),
                    }
                );
            }

            body["requests"] = requests;
            if (forwardedProviderOptions is not null)
            {
                body["providerOptions"] = forwardedProviderOptions;
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    GetBatchUrl("start"),
                    headers,
                    body.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, ReadStartResponse);
        }
        catch (Exception error)
        {
            // Preserve cancellation: an aborted batch start may still have been accepted server-side, so it must not surface as a retryable 500.
            if (IsAbortOrTimeoutError(error))
            {
                throw;
            }

            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    /// <summary>Retrieves the lifecycle status of a Gateway batch job (<c>POST {baseURL}/batch/status</c>).</summary>
    public async Task<BatchV4Status> DoGetBatchStatusAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedHeaders = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await PostBatchAsync("status", options, resolvedHeaders, cancellationToken)
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, root =>
                ConvertGatewayBatchStatus(ReadStatusFields(root))
            );
        }
        catch (Exception error)
        {
            if (IsAbortOrTimeoutError(error))
            {
                throw;
            }

            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    /// <summary>
    /// Streams the per-request results of a terminal Gateway batch job (<c>POST {baseURL}/batch/results</c>, <c>application/x-ndjson</c>: one
    /// item result JSON object per line). Items are validated minimally (type, id, and status) and passed through: the Gateway sanitizes them
    /// server-side. The route responds 400 while the batch is non-terminal.
    /// </summary>
    public async Task<IAsyncEnumerable<BatchV4ItemResult>> DoGetBatchResultsAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedHeaders = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lines = await ProviderUtils
                .PostJsonToApiForJsonLinesAsync(
                    _http,
                    GetBatchUrl("results"),
                    await BatchHeadersAsync(options, resolvedHeaders, cancellationToken).ConfigureAwait(false),
                    new JsonObject { ["batchId"] = options.BatchId }.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    ValidateItemResultLine,
                    _config.MaxLineBytes,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ConvertGatewayBatchResultLines(lines);
        }
        catch (Exception error)
        {
            if (IsAbortOrTimeoutError(error))
            {
                throw;
            }

            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    /// <summary>Requests cancellation; status and partial results remain separate reads.</summary>
    public async Task<BatchV4CancelResult> DoCancelBatchAsync(
        BatchV4OperationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedHeaders = await ResolveHeadersAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await PostBatchAsync("cancel", options, resolvedHeaders, cancellationToken)
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, root => new BatchV4CancelResult
            {
                ProviderMetadata = ReadStatusFields(root).ProviderMetadata,
            });
        }
        catch (Exception error)
        {
            if (IsAbortOrTimeoutError(error))
            {
                throw;
            }

            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    private string GetBatchUrl(string path) => _config.BaseUrl.TrimEnd('/') + "/batch/" + path;

    private async Task<IReadOnlyDictionary<string, string?>?> ResolveHeadersAsync(
        CancellationToken cancellationToken
    ) => _config.Headers is null ? null : await _config.Headers(cancellationToken).ConfigureAwait(false);

    private async Task<Dictionary<string, string>> BatchHeadersAsync(
        BatchV4OperationOptions options,
        IReadOnlyDictionary<string, string?>? resolvedHeaders,
        CancellationToken cancellationToken
    ) =>
        ProviderUtils.CombineHeaders(
            resolvedHeaders,
            options.Headers,
            await _config.O11yHeaders(cancellationToken).ConfigureAwait(false)
        );

    private async Task<ApiResponse> PostBatchAsync(
        string path,
        BatchV4OperationOptions options,
        IReadOnlyDictionary<string, string?>? resolvedHeaders,
        CancellationToken cancellationToken
    ) =>
        await ProviderUtils
            .PostJsonToApiAsync(
                _http,
                GetBatchUrl(path),
                await BatchHeadersAsync(options, resolvedHeaders, cancellationToken).ConfigureAwait(false),
                new JsonObject { ["batchId"] = options.BatchId }.ToJsonString(),
                GatewayHttp.ErrorMessage,
                cancellationToken
            )
            .ConfigureAwait(false);

    // maybeEncodeBatchFileParts: upstream rewrites the caller's options in place; the port encodes a copy, which sends the same body.
    private static JsonObject MaybeEncodeBatchFileParts(JsonObject options)
    {
        var copy = (JsonObject)options.DeepClone();
        if (copy["prompt"] is not JsonArray prompt)
        {
            return copy;
        }

        foreach (var message in prompt.OfType<JsonObject>())
        {
            if (message["content"] is not JsonArray content)
            {
                continue;
            }

            foreach (var part in content.OfType<JsonObject>())
            {
                var type = part["type"]?.GetValue<string>();
                if (type is "file" or "reasoning-file")
                {
                    MaybeBase64EncodeFileData(part);
                }
                else if (
                    type == "tool-result"
                    && part["output"] is JsonObject { } output
                    && output["type"]?.GetValue<string>() == "content"
                    && output["value"] is JsonArray value
                )
                {
                    foreach (var contentPart in value.OfType<JsonObject>())
                    {
                        if (contentPart["type"]?.GetValue<string>() == "file")
                        {
                            MaybeBase64EncodeFileData(contentPart);
                        }
                    }
                }
            }
        }

        return copy;
    }

    // maybeBase64EncodeFileData: { type: 'data', data: Uint8Array } becomes { type: 'data', data: base64 }.
    private static void MaybeBase64EncodeFileData(JsonObject part)
    {
        if (
            part["data"] is JsonObject data
            && data["type"]?.GetValue<string>() == "data"
            && data["data"] is JsonValue bytes
            && bytes.TryGetValue<byte[]>(out var raw)
        )
        {
            data["data"] = Convert.ToBase64String(raw);
        }
    }

    private static string ValidateSingleModel(IReadOnlyList<BatchV4Request> requests)
    {
        var modelId =
            requests.Count > 0
                ? requests[0].ModelId
                : throw new InvalidArgumentException(
                    "requests",
                    "The AI Gateway Batch API requires at least one request."
                );
        foreach (var request in requests)
        {
            if (request.ModelId != modelId)
            {
                throw new InvalidArgumentException(
                    "requests",
                    "The AI Gateway Batch API requires all requests in a batch to use the same model. Found \""
                        + modelId
                        + "\" and \""
                        + request.ModelId
                        + "\"."
                );
            }
        }

        return modelId;
    }

    private static void AssertTextBatchRequests(IReadOnlyList<BatchV4Request> requests)
    {
        foreach (var request in requests)
        {
            if (request.Type != "text")
            {
                throw new UnsupportedFunctionalityException(
                    "batch request type: " + request.Type,
                    "The AI Gateway Batch API does not support batch requests with type \"" + request.Type + "\"."
                );
            }
        }
    }

    // getGatewayBatchIdempotencyKey: providerOptions.gateway.idempotencyKey, sent as the idempotency-key header.
    private static string? GetGatewayBatchIdempotencyKey(JsonObject? providerOptions) =>
        providerOptions?["gateway"] is JsonObject gateway
        && gateway["idempotencyKey"] is JsonValue key
        && key.TryGetValue<string>(out var text)
        && text.Length > 0
            ? text
            : null;

    // omitGatewayIdempotencyKey: the key is transport metadata; keeping it out of the body keeps equivalent retries' digests equal.
    private static JsonObject? OmitGatewayIdempotencyKey(JsonObject? providerOptions)
    {
        if (providerOptions?["gateway"] is not JsonObject gateway || !gateway.ContainsKey("idempotencyKey"))
        {
            return providerOptions;
        }

        var rest = (JsonObject)providerOptions.DeepClone();
        var restGateway = (JsonObject)rest["gateway"]!;
        restGateway.Remove("idempotencyKey");
        if (restGateway.Count == 0)
        {
            rest.Remove("gateway");
        }

        return rest.Count == 0 ? null : rest;
    }

    // isAbortOrTimeoutError: cancellation, which AsGatewayError would otherwise wrap into a retryable Gateway 500.
    private static bool IsAbortOrTimeoutError(Exception error) =>
        error is OperationCanceledException or TimeoutException
        || (error is ApiCallException { InnerException: TimeoutException });

    private sealed record StatusFields(
        string Status,
        string? RawStatus,
        (double? Total, double? Pending, double? Completed, double? Failed)? RequestCounts,
        BatchV4Error? Error,
        string? CreatedAt,
        string? ExpiresAt,
        JsonObject? ProviderMetadata
    );

    private static InvalidResponseDataException Malformed(string reason) => new("Type validation failed: " + reason);

    // gatewayBatchStatusFieldsSchema
    private static StatusFields ReadStatusFields(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("the response must be an object.");
        }

        var status = OptionalString(root, "status") ?? throw Malformed("status is required.");
        if (status is not ("completed" or "failed" or "pending"))
        {
            throw Malformed("status must be completed, failed, or pending.");
        }

        (double?, double?, double?, double?)? counts = null;
        if (Present(root, "requestCounts", out var rawCounts))
        {
            if (rawCounts.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("requestCounts must be an object.");
            }

            counts = (
                OptionalNumber(rawCounts, "total"),
                OptionalNumber(rawCounts, "pending"),
                OptionalNumber(rawCounts, "completed"),
                OptionalNumber(rawCounts, "failed")
            );
        }

        BatchV4Error? error = null;
        if (Present(root, "error", out var rawError))
        {
            error = ReadError(rawError);
        }

        JsonObject? metadata = null;
        if (Present(root, "providerMetadata", out var rawMetadata))
        {
            metadata = ReadProviderMetadata(rawMetadata);
        }

        return new StatusFields(
            status,
            OptionalString(root, "rawStatus"),
            counts,
            error,
            OptionalString(root, "createdAt"),
            OptionalString(root, "expiresAt"),
            metadata
        );
    }

    private static BatchV4Status ConvertGatewayBatchStatus(StatusFields body)
    {
        var requestCounts = body.RequestCounts is { } c
            ? NormalizeBatchRequestCounts.Normalize(c.Total, c.Pending, c.Completed, c.Failed)
            : null;
        return new BatchV4Status
        {
            Status = body.Status,
            RawStatus = body.RawStatus,
            RequestCounts = requestCounts,
            Error = body.Error,
            CreatedAt = body.CreatedAt,
            ExpiresAt = body.ExpiresAt,
            ProviderMetadata = body.ProviderMetadata,
        };
    }

    // gatewayBatchStartResponseSchema
    private static BatchV4StartResult ReadStartResponse(JsonElement root)
    {
        var fields = ReadStatusFields(root);
        var batchId = OptionalString(root, "batchId") ?? throw Malformed("batchId is required.");
        var warnings = new List<BatchV4Warning>();
        if (Present(root, "warnings", out var rawWarnings))
        {
            if (rawWarnings.ValueKind != JsonValueKind.Array)
            {
                throw Malformed("warnings must be an array.");
            }

            foreach (var warning in rawWarnings.EnumerateArray())
            {
                if (warning.ValueKind != JsonValueKind.Object)
                {
                    throw Malformed("a warning must be an object.");
                }

                warnings.Add(
                    new BatchV4Warning(
                        OptionalString(warning, "requestId"),
                        warning.TryGetProperty("warning", out var inner) ? JsonNode.Parse(inner.GetRawText()) : null
                    )
                );
            }
        }

        var status = ConvertGatewayBatchStatus(fields);
        return new BatchV4StartResult
        {
            BatchId = batchId,
            Warnings = warnings,
            Status = status.Status,
            RawStatus = status.RawStatus,
            RequestCounts = status.RequestCounts,
            Error = status.Error,
            CreatedAt = status.CreatedAt,
            ExpiresAt = status.ExpiresAt,
            ProviderMetadata = status.ProviderMetadata,
        };
    }

    private static BatchV4Error ReadError(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("error must be an object.");
        }

        return new BatchV4Error
        {
            Message = OptionalString(element, "message") ?? throw Malformed("error.message is required."),
            Type = OptionalString(element, "type"),
            Code = OptionalString(element, "code"),
            StatusCode = OptionalNumber(element, "statusCode") is { } code ? (int)code : null,
        };
    }

    // z.record(z.string(), z.record(z.string(), z.unknown()))
    private static JsonObject ReadProviderMetadata(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("providerMetadata must be an object.");
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("providerMetadata." + property.Name + " must be an object.");
            }
        }

        return (JsonObject)JsonNode.Parse(element.GetRawText())!;
    }

    // z.nullish: a missing or null property is absent.
    private static bool Present(JsonElement element, string name, out JsonElement value) =>
        element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    private static string? OptionalString(JsonElement element, string name)
    {
        if (!Present(element, name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw Malformed(name + " must be a string.");
    }

    private static double? OptionalNumber(JsonElement element, string name)
    {
        if (!Present(element, name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw Malformed(name + " must be a number.");
    }

    // gatewayBatchItemResultLineSchema: type 'text', a string id, and a terminal status; everything else passes through.
    private static void ValidateItemResultLine(JsonNode node)
    {
        if (node is not JsonObject line)
        {
            throw Malformed("a result line must be an object.");
        }

        if (!(line["type"] is JsonValue type && type.TryGetValue<string>(out var typeText) && typeText == "text"))
        {
            throw Malformed("type must be 'text'.");
        }

        if (!(line["id"] is JsonValue id && id.TryGetValue<string>(out _)))
        {
            throw Malformed("id must be a string.");
        }

        if (
            !(
                line["status"] is JsonValue status
                && status.TryGetValue<string>(out var statusText)
                && statusText is "cancelled" or "expired" or "failed" or "succeeded"
            )
        )
        {
            throw Malformed("status must be cancelled, expired, failed, or succeeded.");
        }
    }

    // convertGatewayBatchResultLines: the Gateway already sanitizes the complete result objects server-side.
    private static async IAsyncEnumerable<BatchV4ItemResult> ConvertGatewayBatchResultLines(
        IAsyncEnumerable<JsonNode> lines,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var node in lines.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var line = (JsonObject)node;
            var status = line["status"]!.GetValue<string>();
            var result = line["result"] as JsonObject;

            // JSON carries response.timestamp as an ISO string; core expects a date.
            if (
                status == "succeeded"
                && result?["response"] is JsonObject response
                && response["timestamp"] is JsonValue timestamp
                && timestamp.TryGetValue<string>(out var text)
                && DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed
                )
            )
            {
                response["timestamp"] = JsonValue.Create(parsed);
            }

            yield return new BatchV4ItemResult
            {
                Type = line["type"]!.GetValue<string>(),
                Id = line["id"]!.GetValue<string>(),
                Status = status,
                Result = result,
                Error = line["error"] is JsonObject error ? ReadError(JsonSerializer.SerializeToElement(error)) : null,
                ProviderMetadata = line["providerMetadata"] as JsonObject,
                Raw = line,
            };
        }
    }
}
