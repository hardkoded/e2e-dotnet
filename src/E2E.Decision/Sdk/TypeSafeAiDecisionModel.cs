// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk;

/// <summary>What <see cref="DecisionTypeSafeAiModel"/> needs (the AI SDK's <c>TypeSafeAiDecisionModelConfig</c>).</summary>
public sealed class TypeSafeAiDecisionModelConfig
{
    public required string Provider { get; init; }

    public required string BaseUrl { get; init; }

    /// <summary>Resolves the request headers on each call. Without it the model authenticates with <c>TYPESAFE_AI_API_KEY</c>.</summary>
    public Func<IReadOnlyDictionary<string, string>>? Headers { get; init; }

    /// <summary>The client the requests go through. Without it the model makes its own.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>
/// Answers questions through TypeSafe AI, <c>POST /systemone</c> (the AI SDK's <c>DecisionTypeSafeAiModel</c> in <c>@ai-sdk/typesafe-ai</c>).
/// It answers <c>choice</c>, <c>score</c>, <c>boolean</c>, and <c>box</c> questions.
/// </summary>
public sealed class DecisionTypeSafeAiModel : IDecisionModel, IEvaluationModel, IDisposable
{
    internal const string ImagesHeader = "TypeSafe-Danger-Zone-Images";
    internal const string LongContextHeader = "TypeSafe-Danger-Zone-Long-Context";

    private const string ReferenceCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly JsonSerializerOptions Text = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TypeSafeAiDecisionModelConfig _config;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public DecisionTypeSafeAiModel(string modelId, TypeSafeAiDecisionModelConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        _config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    /// <summary>What the AI SDK appends to the user agent: <c>ai-sdk-typesafe-ai/&lt;version&gt;</c>, after this client's own.</summary>
    internal static string UserAgent { get; } =
        ModelHttp.UserAgent + " ai-sdk-typesafe-ai/" + ModelHttp.PackageVersion();

    public string ModelId { get; }

    public string Provider => _config.Provider;

    public string SpecificationVersion => "v4";

    public IReadOnlyList<string> SupportedQuestionTypes => TypeSafeAiModelCatalog.TypeSafeAiSupportedQuestionTypes;

    /// <summary>The AI SDK's <c>loadApiKey</c> for TypeSafe: <paramref name="apiKey"/>, else <c>TYPESAFE_AI_API_KEY</c>.</summary>
    internal static string LoadApiKey(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : ModelHttp.Environment("TYPESAFE_AI_API_KEY")
                ?? throw new LoadApiKeyException(
                    "TypeSafe API key is missing. Pass it using the 'apiKey' parameter or the TYPESAFE_AI_API_KEY environment variable."
                );

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>The AI SDK's deprecated <c>doEvaluate</c>: <see cref="DoDecideAsync"/> under its old name.</summary>
    [Obsolete("Use DoDecideAsync instead.")]
    public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
        DoDecideAsync(call, cancellationToken);

    /// <summary>Sends every question in one request.</summary>
    public async Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        foreach (var (id, question) in call.Questions)
        {
            if (question.Type == "choice" && question.Choices!.Count is 0 or > 255)
            {
                throw new InvalidArgumentException(
                    "questions." + id + ".criteria",
                    "TypeSafe Choice questions require 1 to 255 options."
                );
            }

            if (
                question.Type == "score"
                && (question.Levels!.Count is 0 or > 10 || question.Levels.Any(level => level is null))
            )
            {
                throw new InvalidArgumentException(
                    "questions." + id + ".criteria",
                    "TypeSafe Score questions require 1 to 10 non-null levels."
                );
            }
        }

        var longContext = ReadLongContext(call.ProviderOptions, out var warnings);
        if (call.MaxOutputTokens is not null)
        {
            warnings.Add("unsupported setting: maxOutputTokens");
        }

        if (call.Reasoning is not null)
        {
            warnings.Add("unsupported setting: reasoning");
        }

        var assets = new JsonObject();
        var images = new List<(string Name, byte[] Data, string MediaType, string Filename)>();
        var stateStrings = call.State.SelectMany(StateStrings).ToList();
        var stateParts = new List<DecisionStatePart>();
        for (var index = 0; index < call.State.Count; index++)
        {
            var part = call.State[index];
            if (part is not DecisionFilePart file)
            {
                stateParts.Add(part);
                continue;
            }

            if (file.MediaType is not ("image/png" or "image/jpeg"))
            {
                throw new NotSupportedException(
                    "'TypeSafe AI decision image media type: " + file.MediaType + "' functionality not supported."
                );
            }

            var (fileReference, insertOption) = ReadFileOptions(file.ProviderOptions);
            if (insertOption == false && fileReference is null)
            {
                throw new InvalidArgumentException(
                    "providerOptions.typesafe.reference",
                    "TypeSafe images with insertReference: false need an explicit reference."
                );
            }

            // Random IDs avoid accidental text matches, as TypeSafe recommends as the safest option.
            var reference = fileReference ?? RandomNumberGenerator.GetString(ReferenceCharacters, 16);
            var insertReference = insertOption
                ?? !stateStrings.Any(text => text.Contains(reference, StringComparison.Ordinal));
            if (assets.ContainsKey(reference))
            {
                throw new InvalidArgumentException(
                    "providerOptions.typesafe.reference",
                    "Duplicate TypeSafe image reference: " + reference
                );
            }

            var partName = "image-" + index.ToString(CultureInfo.InvariantCulture);
            assets[reference] = new JsonObject { ["type"] = "image", ["part"] = partName };
            images.Add(
                (
                    partName,
                    file.Data,
                    file.MediaType,
                    file.Filename ?? partName + (file.MediaType == "image/png" ? ".png" : ".jpeg")
                )
            );
            if (insertReference)
            {
                stateParts.Add(new DecisionTextPart(reference));
            }
        }

        var hasImages = assets.Count > 0;
        var body = new JsonObject { ["model"] = ModelId, ["state"] = RequestState(stateParts) };
        if (hasImages)
        {
            body["assets"] = assets;
        }

        var questions = new JsonObject();
        foreach (var (id, question) in call.Questions)
        {
            questions[id] = BuildQuestion(question);
        }

        body["questions"] = questions;

        using var message = new HttpRequestMessage(HttpMethod.Post, _config.BaseUrl + "/systemone");
        var modelHeaders = _config.Headers?.Invoke()
            ?? new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + LoadApiKey(null),
                ["User-Agent"] = UserAgent,
            };
        ModelEndpoint.AddHeaders(message, modelHeaders);
        if (hasImages || call.Questions.Values.Any(question => question.Type == "box"))
        {
            message.Headers.TryAddWithoutValidation(ImagesHeader, "true");
        }

        if (longContext)
        {
            message.Headers.TryAddWithoutValidation(LongContextHeader, "true");
        }

        ModelEndpoint.AddHeaders(message, call.Headers);
        message.Content = hasImages ? BuildForm(body, images) : JsonContent(body);

        // The retries belong to ExperimentalDecide, as the AI SDK retries around doDecide: one attempt here.
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's timeout, not the caller: a timeout is no cancellation.
            throw new ApiCallException("The model request got no response within 120 seconds.", isRetryable: false);
        }
        catch (HttpRequestException error)
        {
            // A request that got no answer is retried, as the AI SDK retries a fetch that failed before an answer.
            throw new ApiCallException("The model provider could not be reached.", innerException: error);
        }

        using var _ = response;
        var responseHeaders = Headers(response);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiCallException(FailureMessage(response, text), (int)response.StatusCode, responseHeaders);
        }

        using var document = ParseDocument(text, (int)response.StatusCode, responseHeaders);
        var root = document.RootElement;
        var parsed = ParseResponse(root, (int)response.StatusCode, responseHeaders);
        var answers = new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal);
        var confidence = new JsonObject();
        var legend = new JsonObject();
        foreach (var (id, answer) in parsed)
        {
            answers[id] = answer.ToAnswer();
            if (answer.Type != "noul" && answer.Confidence is { } value)
            {
                confidence[id] = value;
            }

            if (answer.Type == "score" && answer.Legend is { } scale)
            {
                legend[id] = JsonNode.Parse(scale.GetRawText());
            }
        }

        var usage =
            root.TryGetProperty("usage", out var rawUsage) && rawUsage.ValueKind == JsonValueKind.Object
                ? new DecisionUsage(Count(rawUsage, "input_tokens"), Count(rawUsage, "output_tokens"))
                : new DecisionUsage(null, null);
        return new DecisionModelResult
        {
            Answers = answers,
            Usage = usage,
            // The API rounds displayed probabilities and scores to two decimal places.
            Rounding = new DecisionRounding(2, 2),
            Warnings = warnings,
            ProviderMetadata = JsonSerializer.SerializeToElement(
                new JsonObject
                {
                    ["typesafe"] = new JsonObject { ["confidence"] = confidence, ["legend"] = legend },
                }
            ),
            ResponseModelId = ModelHttp.OptionalString(root, "model") ?? ModelId,
            ResponseHeaders = responseHeaders,
            ResponseBody = root.Clone(),
        };
    }

    private static bool ReadLongContext(
        IReadOnlyDictionary<string, JsonElement>? providerOptions,
        out List<string> warnings
    )
    {
        warnings = [];
        if (
            providerOptions is null
            || !providerOptions.TryGetValue("typesafe", out var options)
            || options.ValueKind != JsonValueKind.Object
        )
        {
            return false;
        }

        var longContext = false;
        foreach (var option in options.EnumerateObject())
        {
            if (option.Name != "longContext")
            {
                warnings.Add("unsupported setting: providerOptions.typesafe." + option.Name);
            }
            else if (option.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                longContext = option.Value.GetBoolean();
            }
            else
            {
                throw new InvalidArgumentException(
                    "providerOptions",
                    "invalid typesafe provider options: longContext must be a boolean"
                );
            }
        }

        return longContext;
    }

    // The AI SDK's typesafeAiFilePartProviderOptions: a nonblank reference and a boolean insertReference.
    private static (string? Reference, bool? InsertReference) ReadFileOptions(
        IReadOnlyDictionary<string, JsonElement>? providerOptions
    )
    {
        if (
            providerOptions is null
            || !providerOptions.TryGetValue("typesafe", out var options)
            || options.ValueKind != JsonValueKind.Object
        )
        {
            return (null, null);
        }

        string? reference = null;
        bool? insertReference = null;
        foreach (var option in options.EnumerateObject())
        {
            switch (option.Name)
            {
                case "reference":
                    if (
                        option.Value.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(option.Value.GetString())
                    )
                    {
                        throw new InvalidArgumentException(
                            "providerOptions",
                            "invalid typesafe provider options: reference must be a nonempty string"
                        );
                    }

                    reference = option.Value.GetString();
                    break;
                case "insertReference":
                    if (option.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        throw new InvalidArgumentException(
                            "providerOptions",
                            "invalid typesafe provider options: insertReference must be a boolean"
                        );
                    }

                    insertReference = option.Value.GetBoolean();
                    break;
            }
        }

        return (reference, insertReference);
    }

    // The strings a reference can occur in: text parts and the string values of JSON parts, never object keys.
    private static IEnumerable<string> StateStrings(DecisionStatePart part) =>
        part switch
        {
            DecisionTextPart text => [text.Text],
            DecisionJsonPart json => JsonStrings(json.Value),
            _ => [],
        };

    private static IEnumerable<string> JsonStrings(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!],
            JsonValueKind.Array => value.EnumerateArray().SelectMany(JsonStrings),
            JsonValueKind.Object => value.EnumerateObject().SelectMany(property => JsonStrings(property.Value)),
            _ => [],
        };

    // One JSON object, array, or string part goes native; anything else is text joined by newlines.
    private static JsonNode? RequestState(List<DecisionStatePart> parts)
    {
        if (
            parts
            is [
                DecisionJsonPart
                {
                    Value.ValueKind: JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String
                } single,
            ]
        )
        {
            return JsonNode.Parse(single.Value.GetRawText());
        }

        return string.Join(
            "\n",
            parts.Select(part =>
                part is DecisionTextPart text ? text.Text
                : part is DecisionJsonPart json ? JsonSerializer.Serialize(json.Value, Text)
                : string.Empty
            )
        );
    }

    private static JsonNode? Input(DecisionInput? input) =>
        input is null ? null
        : input.Text is not null ? JsonValue.Create(input.Text)
        : JsonNode.Parse(input.Json!.Value.GetRawText());

    private static JsonObject BuildQuestion(DecisionQuestion question)
    {
        var result = new JsonObject();
        switch (question.Type)
        {
            case "box":
                result["type"] = "box";
                if (question.Instructions is not null)
                {
                    result["instructions"] = Input(question.Instructions);
                }

                return result;
            case "boolean":
                result["type"] = "noul";
                break;
            default:
                result["type"] = question.Type;
                break;
        }

        if (question.Instructions is not null)
        {
            result["instructions"] = Input(question.Instructions);
        }

        switch (question.Type)
        {
            case "choice":
                var options = new JsonObject();
                foreach (var choice in question.Choices!)
                {
                    options[choice.Value] = Input(choice.Description);
                }

                result["criteria"] = options;
                break;
            case "score":
                result["criteria"] = new JsonArray([.. question.Levels!.Select(Input)]);
                break;
            case "boolean" when question.TrueCriteria is not null || question.FalseCriteria is not null:
                result["criteria"] = new JsonObject
                {
                    ["true"] = Input(question.TrueCriteria),
                    ["false"] = Input(question.FalseCriteria),
                };
                break;
        }

        return result;
    }

    // application/json without a charset, as the request Blob and postJsonToApi send it.
    private static ByteArrayContent JsonContent(JsonObject body)
    {
        var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body.ToJsonString(Text)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static MultipartFormDataContent BuildForm(
        JsonObject body,
        List<(string Name, byte[] Data, string MediaType, string Filename)> images
    )
    {
        var form = new MultipartFormDataContent();
        form.Add(JsonContent(body), "request", "request.json");
        foreach (var (name, data, mediaType, filename) in images)
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            form.Add(content, name, filename);
        }

        return form;
    }

    private static Dictionary<string, string> Headers(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
        }

        return headers;
    }

    // typesafeFailedResponseHandler's errorToMessage: message, then error (a string or its message), then detail, then error_type.
    private static string FailureMessage(HttpResponseMessage response, string text)
    {
        const string fallback = "TypeSafe request failed";
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return string.IsNullOrEmpty(response.ReasonPhrase) ? fallback : response.ReasonPhrase;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return string.IsNullOrEmpty(response.ReasonPhrase) ? fallback : response.ReasonPhrase;
        }

        if (ModelHttp.OptionalString(root, "message") is { } message)
        {
            return message;
        }

        if (root.TryGetProperty("error", out var error))
        {
            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString()!;
            }

            if (ModelHttp.OptionalString(error, "message") is { } nested)
            {
                return nested;
            }
        }

        if (root.TryGetProperty("detail", out var detail))
        {
            return detail.ValueKind == JsonValueKind.String ? detail.GetString()! : detail.GetRawText();
        }

        return ModelHttp.OptionalString(root, "error_type") ?? fallback;
    }

    private static JsonDocument ParseDocument(string body, int status, IReadOnlyDictionary<string, string> headers)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (JsonException)
        {
            throw new ApiCallException("Invalid JSON response", status, headers, isRetryable: false);
        }
    }

    private static int? Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var count)
            ? count
            : null;

    // typesafeDecisionResponseSchema: a map of answers, each a choice, a score, a box, or a noul.
    private static List<KeyValuePair<string, ParsedAnswer>> ParseResponse(
        JsonElement root,
        int status,
        IReadOnlyDictionary<string, string> headers
    )
    {
        ApiCallException Malformed(string reason) =>
            new("Type validation failed: " + reason, status, headers, isRetryable: false);

        double Number(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : throw Malformed(property + " must be a number.");

        double? OptionalNumber(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
                ? Number(element, property)
                : null;

        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("answers", out var list)
            || list.ValueKind != JsonValueKind.Object
        )
        {
            throw Malformed("answers must be an object.");
        }

        var result = new List<KeyValuePair<string, ParsedAnswer>>();
        foreach (var entry in list.EnumerateObject())
        {
            var element = entry.Value;
            var type = ModelHttp.OptionalString(element, "type") ?? throw Malformed("answer type is required.");
            var answer = new ParsedAnswer { Type = type };
            switch (type)
            {
                case "choice":
                case "score":
                    if (type == "choice")
                    {
                        answer.Choice = ModelHttp.OptionalString(element, "choice")
                            ?? throw Malformed("choice must be a string.");
                    }
                    else
                    {
                        answer.Score = Number(element, "score");
                        if (
                            element.TryGetProperty("legend", out var legend)
                            && legend.ValueKind == JsonValueKind.Object
                        )
                        {
                            answer.Legend = legend.Clone();
                        }
                    }

                    if (
                        !element.TryGetProperty("probabilities", out var items)
                        || items.ValueKind != JsonValueKind.Object
                    )
                    {
                        throw Malformed("probabilities must be an object.");
                    }

                    answer.Probabilities = items
                        .EnumerateObject()
                        .ToDictionary(
                            item => item.Name,
                            item =>
                                item.Value.ValueKind == JsonValueKind.Number
                                    ? item.Value.GetDouble()
                                    : throw Malformed("probability must be a number."),
                            StringComparer.Ordinal
                        );
                    answer.Confidence = OptionalNumber(element, "confidence");
                    break;
                case "box":
                    if (!element.TryGetProperty("box", out var box) || box.ValueKind != JsonValueKind.Object)
                    {
                        throw Malformed("box must be an object.");
                    }

                    answer.Box = new DecisionBox(
                        Number(box, "top"),
                        Number(box, "bottom"),
                        Number(box, "left"),
                        Number(box, "right")
                    );
                    answer.Confidence = OptionalNumber(element, "confidence");
                    break;
                case "noul":
                    answer.Noul = Number(element, "noul");
                    break;
                default:
                    throw Malformed("unknown answer type.");
            }

            result.Add(new KeyValuePair<string, ParsedAnswer>(entry.Name, answer));
        }

        return result;
    }

    private sealed class ParsedAnswer
    {
        public string Type { get; set; } = string.Empty;

        public string? Choice { get; set; }

        public double? Score { get; set; }

        public double? Noul { get; set; }

        public double? Confidence { get; set; }

        public JsonElement? Legend { get; set; }

        public DecisionBox? Box { get; set; }

        public Dictionary<string, double>? Probabilities { get; set; }

        public DecisionAnswer ToAnswer() =>
            Type switch
            {
                "box" => new DecisionAnswer { Type = "box", Box = Box },
                "noul" => new DecisionAnswer { Type = "boolean", Probability = Noul },
                "choice" => new DecisionAnswer
                {
                    Type = "choice",
                    Choice = Choice,
                    Probabilities = Probabilities,
                },
                _ => new DecisionAnswer
                {
                    Type = "score",
                    Score = Score,
                    Probabilities = Probabilities,
                },
            };
    }
}
