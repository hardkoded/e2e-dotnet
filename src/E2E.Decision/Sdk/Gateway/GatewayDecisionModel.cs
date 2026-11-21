// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayConfig</c> (<c>gateway-config.ts</c>): where and how a Gateway model sends its requests.</summary>
public class GatewayConfig
{
    /// <summary>The base URL prefix for API calls.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Resolves the headers sent with every request, such as the <c>Authorization</c> header. A null value drops the header.</summary>
    public Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>? Headers { get; init; }

    /// <summary>The client that sends the requests. Defaults to a client of the model's own, with a 120 second timeout.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>The config of a <see cref="GatewayDecisionModel"/> (upstream's <c>GatewayConfig &amp; { provider, o11yHeaders }</c>).</summary>
public sealed class GatewayDecisionModelConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>
/// Answers questions through the Vercel AI Gateway, <c>POST /decision-model</c> (the AI SDK's <c>GatewayDecisionModel</c> in
/// <c>@ai-sdk/gateway</c>). It answers <c>choice</c>, <c>score</c>, and <c>boolean</c> questions; the Gateway routes the call to the model
/// named by the id, such as <c>typesafe-ai/jev</c>, and applies the <c>gateway</c> provider options (fallbacks, routing).
/// </summary>
public sealed class GatewayDecisionModel : IDecisionModel, IEvaluationModel, IDisposable
{
    private readonly GatewayDecisionModelConfig _config;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayDecisionModel(string modelId, GatewayDecisionModelConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        _config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    public IReadOnlyList<string> SupportedQuestionTypes { get; } = ["choice", "score", "boolean"];

    public string ModelId { get; }

    public string Provider => _config.Provider;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>Deprecated: use <see cref="DoDecideAsync"/>.</summary>
    public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
        DoDecideAsync(call, cancellationToken);

    /// <summary>Sends every question in one request.</summary>
    public async Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var stateParts = ToGatewayStateParts.Convert(call.State, "decision");
        var providerOptions = ValidateProviderOptions(call.ProviderOptions);
        var configured = _config.Headers is null
            ? null
            : await _config.Headers(cancellationToken).ConfigureAwait(false);
        try
        {
            var o11y = await _config.O11yHeaders(cancellationToken).ConfigureAwait(false);
            var headers = ProviderUtils.CombineHeaders(
                configured,
                call.Headers.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
                new Dictionary<string, string?>
                {
                    ["ai-decision-model-specification-version"] = "4",
                    ["ai-model-id"] = ModelId,
                },
                o11y
            );
            var body = new JsonObject
            {
                ["stateParts"] = stateParts,
                ["questions"] = DecisionQuestionJson.ToJson(call.Questions),
            };
            if (call.MaxOutputTokens is { } maxOutputTokens)
            {
                body["maxOutputTokens"] = maxOutputTokens;
            }

            if (call.Reasoning is not null)
            {
                body["reasoning"] = call.Reasoning;
            }

            if (providerOptions is not null)
            {
                body["providerOptions"] = providerOptions;
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    _config.BaseUrl.TrimEnd('/') + "/decision-model",
                    headers,
                    body.ToJsonString(),
                    (_, text) => ErrorMessage(text),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, value => ReadResponse(value, response));
        }
        catch (Exception error) when (error is ApiCallException or GatewayException)
        {
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(configured));
        }
    }

    // getErrorMessage(data) ?? 'unknown error' over the parsed body, else the raw text.
    internal static string ErrorMessage(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var data = document.RootElement;
            if (data.ValueKind == JsonValueKind.String)
            {
                return data.GetString()!;
            }

            if (
                data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
            )
            {
                return message.GetString()!;
            }

            return data.GetRawText();
        }
        catch (JsonException)
        {
            return string.IsNullOrEmpty(text) ? "unknown error" : text;
        }
    }

    // parseProviderOptions({ provider: 'gateway', schema: gatewayDecisionProviderOptionsSchema }), then the options as sent.
    private static JsonNode? ValidateProviderOptions(IReadOnlyDictionary<string, JsonElement>? providerOptions)
    {
        if (providerOptions is null)
        {
            return null;
        }

        if (providerOptions.TryGetValue("gateway", out var gateway) && gateway.ValueKind != JsonValueKind.Null)
        {
            var problem = GatewayProviderOptions.ValidateDecisionProviderOptions(gateway);
            if (problem is not null)
            {
                throw new InvalidArgumentException("providerOptions", "invalid gateway provider options: " + problem);
            }
        }

        return JsonSerializer.SerializeToNode(providerOptions);
    }

    private DecisionModelResult ReadResponse(JsonElement root, ApiResponse response)
    {
        InvalidResponseDataException Malformed(string reason) => new("Type validation failed: " + reason);

        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("answers", out var list)
            || list.ValueKind != JsonValueKind.Object
        )
        {
            throw Malformed("answers must be an object.");
        }

        var answers = new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal);
        foreach (var property in list.EnumerateObject())
        {
            answers[property.Name] = ReadAnswer(property.Value, Malformed);
        }

        DecisionRounding? rounding = null;
        if (root.TryGetProperty("rounding", out var rawRounding) && rawRounding.ValueKind != JsonValueKind.Null)
        {
            rounding = new DecisionRounding(
                OptionalInt(rawRounding, "probabilityDecimals", Malformed),
                OptionalInt(rawRounding, "scoreDecimals", Malformed)
            );
        }

        DecisionUsage? usage = null;
        if (root.TryGetProperty("usage", out var rawUsage) && rawUsage.ValueKind != JsonValueKind.Null)
        {
            usage = new DecisionUsage(
                OptionalInt(rawUsage, "inputTokens", Malformed),
                OptionalInt(rawUsage, "outputTokens", Malformed)
            );
        }

        var warnings = new List<string>();
        if (root.TryGetProperty("warnings", out var rawWarnings) && rawWarnings.ValueKind != JsonValueKind.Null)
        {
            if (rawWarnings.ValueKind != JsonValueKind.Array)
            {
                throw Malformed("warnings must be an array.");
            }

            foreach (var warning in rawWarnings.EnumerateArray())
            {
                warnings.Add(ReadWarning(warning, Malformed));
            }
        }

        JsonElement? providerMetadata = null;
        if (root.TryGetProperty("providerMetadata", out var rawMetadata) && rawMetadata.ValueKind != JsonValueKind.Null)
        {
            providerMetadata = rawMetadata.Clone();
        }

        return new DecisionModelResult
        {
            Answers = answers,
            Rounding = rounding,
            Usage = usage,
            Warnings = warnings,
            ProviderMetadata = providerMetadata,
            ResponseModelId = ModelHttp.OptionalString(root, "model") ?? ModelId,
            ResponseHeaders = response.ResponseHeaders,
            ResponseBody = response.Value,
        };
    }

    private static DecisionAnswer ReadAnswer(JsonElement element, Func<string, InvalidResponseDataException> malformed)
    {
        var type = ModelHttp.OptionalString(element, "type") ?? throw malformed("answer type is required.");
        switch (type)
        {
            case "refusal":
                return new DecisionAnswer { Type = "refusal" };
            case "boolean":
                return new DecisionAnswer { Type = "boolean", Probability = Number(element, "probability", malformed) };
            case "choice":
                return new DecisionAnswer
                {
                    Type = "choice",
                    Choice = ModelHttp.OptionalString(element, "choice") ?? throw malformed("choice must be a string."),
                    Probabilities = Probabilities(element, malformed),
                };
            case "score":
                return new DecisionAnswer
                {
                    Type = "score",
                    Score = Number(element, "score", malformed),
                    Probabilities = Probabilities(element, malformed),
                };
            default:
                throw malformed("unknown answer type.");
        }
    }

    private static double Number(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    ) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw malformed(name + " must be a number.");

    private static Dictionary<string, double>? Probabilities(
        JsonElement element,
        Func<string, InvalidResponseDataException> malformed
    )
    {
        if (!element.TryGetProperty("probabilities", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw malformed("probabilities must be an object.");
        }

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            result[property.Name] =
                property.Value.ValueKind == JsonValueKind.Number
                    ? property.Value.GetDouble()
                    : throw malformed("probabilities must be numbers.");
        }

        return result;
    }

    private static int? OptionalInt(
        JsonElement element,
        string name,
        Func<string, InvalidResponseDataException> malformed
    )
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw malformed(name + " must be a number.");
    }

    // The Gateway's structured warnings as the text a result carries: the same "unsupported setting: <feature>" the other models write.
    private static string ReadWarning(JsonElement warning, Func<string, InvalidResponseDataException> malformed)
    {
        string Required(string name) =>
            ModelHttp.OptionalString(warning, name) ?? throw malformed("warning " + name + " must be a string.");

        string WithDetails(string text) =>
            ModelHttp.OptionalString(warning, "details") is { } details ? text + ": " + details : text;

        return Required("type") switch
        {
            "unsupported" => WithDetails("unsupported setting: " + Required("feature")),
            "compatibility" => WithDetails("compatibility mode: " + Required("feature")),
            "deprecated" => "deprecated setting: " + Required("setting") + ": " + Required("message"),
            "other" => Required("message"),
            _ => throw malformed("unknown warning type."),
        };
    }
}
