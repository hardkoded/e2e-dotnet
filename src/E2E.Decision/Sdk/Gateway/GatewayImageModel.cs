// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The config of a <see cref="GatewayImageModel"/> (upstream's <c>GatewayImageConfig</c>: <c>GatewayConfig &amp; { provider, o11yHeaders }</c>).</summary>
public sealed class GatewayImageConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>Generates images through the Vercel AI Gateway, <c>POST /image-model</c> (the AI SDK's <c>GatewayImageModel</c>).</summary>
public sealed class GatewayImageModel : IImageModelV4, IDisposable
{
    // DEFAULT_BINARY_FIELD_MIN_BYTES (json-binary-field-scanner.ts): a base64 string that decodes to at least this many bytes is decoded.
    private const int BinaryFieldMinBytes = 64 * 1024;

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayImageModel(string modelId, GatewayImageConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        Config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    // Set a very large number to prevent client-side splitting of requests (Number.MAX_SAFE_INTEGER).
    public long? MaxImagesPerCall => 9007199254740991;

    public string ModelId { get; }

    public string Provider => Config.Provider;

    /// <summary>Upstream's private <c>config</c>.</summary>
    internal GatewayImageConfig Config { get; }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    public async Task<ImageModelV4Result> DoGenerateAsync(
        ImageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var (requestProviderOptions, streamingImageDecoding) = RequestProviderOptions(options.ProviderOptions);
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
                    ["ai-image-model-specification-version"] = "4",
                    ["ai-model-id"] = ModelId,
                },
                await Config.O11yHeaders(cancellationToken).ConfigureAwait(false)
            );
            var body = new JsonObject();
            if (options.Prompt is not null)
            {
                body["prompt"] = options.Prompt;
            }

            body["n"] = options.N;
            if (!string.IsNullOrEmpty(options.Size))
            {
                body["size"] = options.Size;
            }

            if (!string.IsNullOrEmpty(options.AspectRatio))
            {
                body["aspectRatio"] = options.AspectRatio;
            }

            if (options.Seed is { } seed)
            {
                body["seed"] = seed;
            }

            if (requestProviderOptions is not null)
            {
                body["providerOptions"] = requestProviderOptions;
            }

            if (options.Files is { } files)
            {
                body["files"] = new JsonArray(files.Select(MaybeEncodeImageFile).ToArray());
            }

            if (options.Mask is { } mask)
            {
                body["mask"] = MaybeEncodeImageFile(mask);
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    Config.BaseUrl + "/image-model",
                    headers,
                    body.ToJsonString(),
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, value =>
                ReadResponse(value, response, streamingImageDecoding)
            );
        }
        catch (Exception error) when (error is ApiCallException or GatewayException)
        {
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    // The client-only gateway option experimental_streamingImageDecoding is read, and removed from the options sent to the service.
    private static (JsonObject? RequestProviderOptions, bool StreamingImageDecoding) RequestProviderOptions(
        IReadOnlyDictionary<string, JsonElement> providerOptions
    )
    {
        const string option = "experimental_streamingImageDecoding";
        var requestProviderOptions = (JsonObject?)JsonSerializer.SerializeToNode(providerOptions);
        var streamingImageDecoding = false;
        if (
            providerOptions.TryGetValue("gateway", out var gatewayOptions)
            && gatewayOptions.ValueKind == JsonValueKind.Object
            && gatewayOptions.TryGetProperty(option, out var enabled)
        )
        {
            streamingImageDecoding = enabled.ValueKind == JsonValueKind.True;
            var serviceOptions = new JsonObject();
            foreach (var property in gatewayOptions.EnumerateObject().Where(p => p.Name != option))
            {
                serviceOptions[property.Name] = JsonSerializer.SerializeToNode(property.Value);
            }

            if (serviceOptions.Count > 0)
            {
                requestProviderOptions!["gateway"] = serviceOptions;
            }
            else
            {
                requestProviderOptions!.Remove("gateway");
            }

            if (requestProviderOptions.Count == 0)
            {
                requestProviderOptions = null;
            }
        }

        return (requestProviderOptions, streamingImageDecoding);
    }

    private static JsonNode MaybeEncodeImageFile(ImageModelV4File file)
    {
        var json = new JsonObject();
        switch (file)
        {
            case ImageModelV4FileData data:
                json["type"] = "file";
                json["mediaType"] = data.MediaType;
                json["data"] = data.Data is { } bytes ? Convert.ToBase64String(bytes) : data.Base64Data;
                break;
            case ImageModelV4FileUrl url:
                json["type"] = "url";
                json["url"] = url.Url;
                break;
            default:
                throw new ArgumentException("Unknown image file type.", nameof(file));
        }

        if (file.ProviderOptions is { } providerOptions)
        {
            json["providerOptions"] = JsonSerializer.SerializeToNode(providerOptions);
        }

        return json;
    }

    // gatewayImageResponseSchema. Images are base64 strings over the wire. With the streaming option, a canonical base64 string that decodes
    // to at least 64 KiB is decoded to bytes (upstream's experimental_binaryFields: ['images[*]'] does it while the body is read).
    private ImageModelV4Result ReadResponse(JsonElement root, ApiResponse response, bool streamingImageDecoding)
    {
        const string path = "response";
        Zod.Object(root, path);
        var images = new List<(string? Text, byte[]? Bytes)>();
        var index = 0;
        foreach (var image in Zod.Array(Zod.Property(root, "images", path), path + ".images").EnumerateArray())
        {
            if (image.ValueKind != JsonValueKind.String)
            {
                // The error does not carry the image.
                throw Zod.Invalid(path + ".images[" + index + "]", "expected string");
            }

            var text = image.GetString()!;
            images.Add(
                streamingImageDecoding && TryDecodeCanonicalBase64(text, out var bytes) ? (null, bytes) : (text, null)
            );
            index++;
        }

        var isRetryable = root.TryGetProperty("isRetryable", out _)
            ? (bool?)Zod.Boolean(root, "isRetryable", path)
            : null;
        var providerMetadata = Zod.OptionalProviderMetadata(root, path, (entry, entryPath) =>
        {
            if (entry.TryGetProperty("images", out var entryImages))
            {
                Zod.Array(entryImages, entryPath + ".images");
            }
        });
        ImageModelV4Usage? usage = null;
        if (root.TryGetProperty("usage", out var rawUsage))
        {
            Zod.Object(rawUsage, path + ".usage");
            usage = new ImageModelV4Usage(
                Zod.NullishNumber(rawUsage, "inputTokens", path + ".usage"),
                Zod.NullishNumber(rawUsage, "outputTokens", path + ".usage"),
                Zod.NullishNumber(rawUsage, "totalTokens", path + ".usage")
            );
        }

        var warnings = GatewayWarningSchema.ParseOptionalArray(root, "warnings", path);
        var allText = images.All(image => image.Text is not null);
        return new ImageModelV4Result
        {
            Images = allText ? images.Select(image => image.Text!).ToList() : null,
            ImageBytes = allText ? null : images.Select(image => image.Bytes ?? DecodeBase64(image.Text!)).ToList(),
            IsRetryable = isRetryable,
            Warnings = warnings,
            ProviderMetadata = providerMetadata,
            Response = new ImageModelV4ResultResponse(DateTimeOffset.UtcNow, ModelId, response.ResponseHeaders),
            Usage = usage,
        };
    }

    // A string is decoded only if it is canonical standard-alphabet base64 (padded, no whitespace) and decodes to at least 64 KiB.
    private static bool TryDecodeCanonicalBase64(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length % 4 != 0 || text.Length / 4 * 3 - 2 < BinaryFieldMinBytes)
        {
            return false;
        }

        var padding =
            text.EndsWith("==", StringComparison.Ordinal) ? 2
            : text.EndsWith('=') ? 1
            : 0;
        for (var i = 0; i < text.Length - padding; i++)
        {
            var c = text[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c is '+' or '/'))
            {
                return false;
            }
        }

        if (text.Length / 4 * 3 - padding < BinaryFieldMinBytes)
        {
            return false;
        }

        bytes = Convert.FromBase64String(text);
        return true;
    }

    // convertBase64ToUint8Array: base64url is accepted, and the padding is optional.
    private static byte[] DecodeBase64(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
