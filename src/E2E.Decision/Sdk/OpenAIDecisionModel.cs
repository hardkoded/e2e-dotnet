// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk;

/// <summary>Settings of an <see cref="OpenAIDecisionModel"/> (the AI SDK's <c>createOpenAI</c> settings it reads).</summary>
public sealed class OpenAIDecisionModelOptions
{
    /// <summary>The API key. Falls back to <see cref="ApiKeyEnv"/> when empty.</summary>
    public string? ApiKey { get; init; }

    /// <summary>The environment variable read when <see cref="ApiKey"/> is empty. Defaults to <c>OPENAI_API_KEY</c>.</summary>
    public string ApiKeyEnv { get; init; } = "OPENAI_API_KEY";

    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    /// <summary>Headers added to every request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Answers questions through the OpenAI Decisions API, <c>POST /decisions</c> (the AI SDK's <c>OpenAIDecisionModel</c> in
/// <c>@ai-sdk/openai</c>). It answers <c>choice</c>, <c>score</c>, and <c>boolean</c> questions, and the answers carry the model's probabilities.
/// </summary>
public sealed class OpenAIDecisionModel : IDecisionModel, IDisposable
{
    private const int MaxSafetyIdentifierLength = 128;
    private const int MaxPromptCacheKeyLength = 64;
    private const int InputJsonMaxDepth = 32;
    private const int InputJsonMaxValues = 10_000;
    private const int InputJsonMaxBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Text = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] ImageMediaTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    private readonly OpenAIDecisionModelOptions _options;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public OpenAIDecisionModel(
        string modelId,
        OpenAIDecisionModelOptions? options = null,
        HttpClient? httpClient = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ModelId = modelId;
        _options = options ?? new OpenAIDecisionModelOptions();
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string Provider => "openai.decision";

    public string ModelId { get; }

    public string SpecificationVersion => "v4";

    public IReadOnlyList<string> SupportedQuestionTypes { get; } = ["choice", "score", "boolean"];

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>Sends every question in one Decisions request.</summary>
    public async Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var apiKey = ResolveApiKey();
        var warnings = new List<string>();
        if (call.MaxOutputTokens is not null)
        {
            warnings.Add("unsupported setting: maxOutputTokens");
        }

        if (call.Reasoning is not null)
        {
            warnings.Add("unsupported setting: reasoning");
        }

        var (safetyIdentifier, promptCacheKey) = ReadProviderOptions(call.ProviderOptions, warnings);
        var body = new JsonObject { ["model"] = ModelId };
        if (safetyIdentifier is not null)
        {
            body["safety_identifier"] = safetyIdentifier;
        }

        if (promptCacheKey is not null)
        {
            body["prompt_cache_key"] = promptCacheKey;
        }

        body["input"] = BuildInput(call.State);
        var questions = new JsonArray();
        foreach (var (id, question) in call.Questions)
        {
            questions.Add(BuildQuestion(id, question));
        }

        body["questions"] = questions;
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            ModelEndpoint.Build(_options.BaseUrl, "/decisions", null)
        );
        ModelEndpoint.Authorize(message, apiKey, null);
        ModelEndpoint.AddHeaders(message, _options.Headers);
        ModelEndpoint.AddHeaders(message, call.Headers);
        message.Content = new StringContent(body.ToJsonString(Text), System.Text.Encoding.UTF8, "application/json");
        string text;
        try
        {
            // The retries belong to ExperimentalDecide, as the AI SDK retries around doDecide: one attempt here.
            text = await ModelHttp
                .SendAsync(_http, message, Redactor.None, cancellationToken, maxRetries: 0)
                .ConfigureAwait(false);
        }
        catch (AgentException error) when (error.Code == "MODEL_PROVIDER_FAILED" && !error.Blocked)
        {
            throw new ApiCallException(
                error.Message,
                error.ProviderStatus,
                error.ProviderRetryHeaders,
                // A request that got no answer is retried; so is a retryable status; a timeout is neither.
                error.ProviderStatus
                    is { } status
                    ? null
                    : error.NoProviderAnswer,
                error
            );
        }

        using var document = ParseDocument(text);
        var root = document.RootElement;
        var parsed = ParseAnswers(root);
        var sent = call.Questions.Keys.ToHashSet(StringComparer.Ordinal);
        var names = parsed.Select(answer => answer.Name).ToList();
        if (
            names.Count != call.Questions.Count
            || names.Distinct(StringComparer.Ordinal).Count() != names.Count
            || names.Any(name => name is null || !sent.Contains(name))
        )
        {
            throw new InvalidResponseDataException(
                "Decisions must return exactly one answer for every question.",
                root.Clone()
            );
        }

        var answers = new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal);
        var confidence = new JsonObject();
        foreach (var answer in parsed)
        {
            answers[answer.Name!] = ToAnswer(answer, root);
            if (answer.Type is "choice" or "score" && answer.Confidence is { } value)
            {
                confidence[answer.Name!] = value;
            }
        }

        DecisionUsage? usage = null;
        var metadata = new JsonObject();
        if (root.TryGetProperty("usage", out var rawUsage) && rawUsage.ValueKind == JsonValueKind.Object)
        {
            usage = new DecisionUsage(Count(rawUsage, "input_tokens"), Count(rawUsage, "output_tokens"));
            metadata["usage"] = JsonNode.Parse(rawUsage.GetRawText());
        }

        metadata["confidence"] = confidence;
        var providerMetadata = JsonSerializer.SerializeToElement(new JsonObject { ["openai"] = metadata });
        return new DecisionModelResult
        {
            Answers = answers,
            Warnings = warnings,
            Usage = usage,
            // The Decisions API reports probabilities and scores to two decimal places.
            Rounding = new DecisionRounding(2, 2),
            ProviderMetadata = providerMetadata,
            ResponseModelId = ModelHttp.OptionalString(root, "model") ?? ModelId,
        };
    }

    private static DecisionAnswer ToAnswer(ParsedAnswer answer, JsonElement root)
    {
        switch (answer.Type)
        {
            case "refusal":
                return new DecisionAnswer { Type = "refusal" };
            case "predicate":
                return new DecisionAnswer { Type = "boolean", Probability = answer.Probability };
        }

        var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, probability) in answer.Probabilities!)
        {
            if (!probabilities.TryAdd(key, probability))
            {
                throw new InvalidResponseDataException(
                    "Decisions returned duplicate probability values.",
                    root.Clone()
                );
            }
        }

        return answer.Type == "choice"
            ? new DecisionAnswer
            {
                Type = "choice",
                Choice = answer.Choice,
                Probabilities = probabilities,
            }
            : new DecisionAnswer
            {
                Type = "score",
                Score = answer.Score,
                Probabilities = probabilities,
            };
    }

    private string ResolveApiKey()
    {
        var key = !string.IsNullOrWhiteSpace(_options.ApiKey)
            ? _options.ApiKey
            : ModelHttp.Environment(_options.ApiKeyEnv);
        return key
            ?? throw new LoadApiKeyException(
                "OpenAI API key is missing. Pass it using the 'apiKey' parameter or the "
                    + _options.ApiKeyEnv
                    + " environment variable."
            );
    }

    private static (string? SafetyIdentifier, string? PromptCacheKey) ReadProviderOptions(
        IReadOnlyDictionary<string, JsonElement>? providerOptions,
        List<string> warnings
    )
    {
        string? safetyIdentifier = null;
        string? promptCacheKey = null;
        if (
            providerOptions is null
            || !providerOptions.TryGetValue("openai", out var openai)
            || openai.ValueKind != JsonValueKind.Object
        )
        {
            return (null, null);
        }

        foreach (var option in openai.EnumerateObject())
        {
            switch (option.Name)
            {
                case "safetyIdentifier":
                    if (
                        option.Value.ValueKind != JsonValueKind.String
                        || option.Value.GetString()!.Length > MaxSafetyIdentifierLength
                    )
                    {
                        throw new InvalidArgumentException(
                            "providerOptions",
                            "invalid openai provider options: safetyIdentifier must be a string of at most "
                                + MaxSafetyIdentifierLength.ToString(CultureInfo.InvariantCulture)
                                + " characters"
                        );
                    }

                    safetyIdentifier = option.Value.GetString();
                    break;
                case "promptCacheKey":
                    if (
                        option.Value.ValueKind != JsonValueKind.String
                        || option.Value.GetString()!.Length > MaxPromptCacheKeyLength
                    )
                    {
                        throw new InvalidArgumentException(
                            "providerOptions",
                            "invalid openai provider options: promptCacheKey must be a string of at most "
                                + MaxPromptCacheKeyLength.ToString(CultureInfo.InvariantCulture)
                                + " characters"
                        );
                    }

                    promptCacheKey = option.Value.GetString();
                    break;
                default:
                    warnings.Add("unsupported setting: providerOptions.openai." + option.Name);
                    break;
            }
        }

        return (safetyIdentifier, promptCacheKey);
    }

    private static int? Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var count)
            ? count
            : null;

    private static JsonDocument ParseDocument(string body)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (JsonException)
        {
            throw new InvalidResponseDataException("Invalid JSON response");
        }
    }

    private static JsonArray BuildInput(IReadOnlyList<DecisionStatePart> state)
    {
        var content = new JsonArray();
        foreach (var part in state)
        {
            switch (part)
            {
                case DecisionTextPart text:
                    content.Add(new JsonObject { ["type"] = "input_text", ["text"] = text.Text });
                    break;
                case DecisionJsonPart json:
                    var serialized = JsonSerializer.Serialize(json.Value, Text);
                    content.Add(
                        IsInputJsonData(json.Value, serialized)
                            ? new JsonObject { ["type"] = "input_json", ["data"] = JsonNode.Parse(serialized) }
                            : new JsonObject { ["type"] = "input_text", ["text"] = serialized }
                    );
                    break;
                case DecisionFilePart file:
                    // Direct DoDecideAsync calls can bypass the media type detection of the decide call.
                    var mediaType = IsFullMediaType(file.MediaType) ? file.MediaType : DetectImageMediaType(file.Data);
                    if (mediaType is null || !ImageMediaTypes.Contains(mediaType, StringComparer.Ordinal))
                    {
                        throw new NotSupportedException(
                            "'OpenAI decision image media type: " + file.MediaType + "' functionality not supported."
                        );
                    }

                    content.Add(
                        new JsonObject
                        {
                            ["type"] = "input_image",
                            ["image_url"] = "data:" + mediaType + ";base64," + Convert.ToBase64String(file.Data),
                        }
                    );
                    break;
                default:
                    throw new NotSupportedException(
                        "'OpenAI decision state part: " + part.GetType().Name + "' functionality not supported."
                    );
            }
        }

        return [new JsonObject { ["role"] = "user", ["content"] = content }];
    }

    // detect-media-type.ts isFullMediaType: a type/subtype whose subtype is neither empty nor a wildcard.
    private static bool IsFullMediaType(string mediaType)
    {
        var slash = mediaType.IndexOf('/', StringComparison.Ordinal);
        return slash != -1 && slash < mediaType.Length - 1 && mediaType.Substring(slash + 1) != "*";
    }

    // detectMediaType({ topLevelType: 'image' }) for the signatures of the image types the Decisions API accepts.
    private static string? DetectImageMediaType(byte[] data)
    {
        static bool StartsWith(byte[] data, params int[] prefix)
        {
            if (data.Length < prefix.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                // -1 matches any byte.
                if (prefix[i] != -1 && data[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        if (
            StartsWith(data, 0x47, 0x49, 0x46, 0x38, 0x37, 0x61) || StartsWith(data, 0x47, 0x49, 0x46, 0x38, 0x39, 0x61)
        )
        {
            return "image/gif";
        }

        if (StartsWith(data, 0x89, 0x50, 0x4e, 0x47))
        {
            return "image/png";
        }

        if (StartsWith(data, 0xff, 0xd8))
        {
            return "image/jpeg";
        }

        return StartsWith(data, 0x52, 0x49, 0x46, 0x46, -1, -1, -1, -1, 0x57, 0x45, 0x42, 0x50) ? "image/webp" : null;
    }

    private static bool IsInputJsonData(JsonElement value, string serialized)
    {
        var shaped =
            value.ValueKind == JsonValueKind.Array
                ? value.GetArrayLength() > 0
                    && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Object)
                : value.ValueKind == JsonValueKind.Object;
        return shaped && FitsInputJsonLimits(value, serialized);
    }

    private static bool FitsInputJsonLimits(JsonElement value, string serialized)
    {
        var count = 0;
        bool Visit(JsonElement node, int depth)
        {
            if (++count > InputJsonMaxValues)
            {
                return false;
            }

            if (node.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                return true;
            }

            if (depth > InputJsonMaxDepth)
            {
                return false;
            }

            return node.ValueKind == JsonValueKind.Array
                ? node.EnumerateArray().All(child => Visit(child, depth + 1))
                : node.EnumerateObject().All(property => Visit(property.Value, depth + 1));
        }

        return Visit(value, 1) && System.Text.Encoding.UTF8.GetByteCount(serialized) <= InputJsonMaxBytes;
    }

    private static JsonObject BuildQuestion(string name, DecisionQuestion question)
    {
        switch (question.Type)
        {
            case "boolean":
                List<string> parts = [];
                if (question.Instructions is not null)
                {
                    parts.Add(question.Instructions.ToText());
                }

                if (question.TrueCriteria is not null)
                {
                    parts.Add("Criteria for true:\n" + question.TrueCriteria.ToText());
                }

                if (question.FalseCriteria is not null)
                {
                    parts.Add("Criteria for false:\n" + question.FalseCriteria.ToText());
                }

                return new JsonObject
                {
                    ["type"] = "predicate",
                    ["name"] = name,
                    ["instructions"] = string.Join("\n\n", parts),
                };
            case "choice":
                if (question.Choices!.Count is < 2 or > 255)
                {
                    throw new InvalidArgumentException(
                        "questions." + name,
                        "the OpenAI Decisions API takes 2 to 255 choices"
                    );
                }

                var choices = new JsonArray();
                foreach (var option in question.Choices!)
                {
                    var choice = new JsonObject { ["value"] = option.Value };
                    if (option.Description is not null)
                    {
                        choice["description"] = option.Description.ToText();
                    }

                    choices.Add(choice);
                }

                return WithOptions(
                    new JsonObject { ["type"] = "choice", ["name"] = name },
                    question.Instructions?.ToText(),
                    "choices",
                    choices
                );
            case "score":
                if (question.Levels!.Count is < 2 or > 10)
                {
                    throw new InvalidArgumentException(
                        "questions." + name,
                        "the OpenAI Decisions API takes 2 to 10 score levels"
                    );
                }

                var levels = new JsonArray();
                var index = 0;
                foreach (var description in question.Levels!)
                {
                    // Score criteria have no separate labels; indices identify each level.
                    var level = new JsonObject { ["label"] = index.ToString(CultureInfo.InvariantCulture) };
                    if (description is not null)
                    {
                        level["description"] = description.ToText();
                    }

                    levels.Add(level);
                    index++;
                }

                return WithOptions(
                    new JsonObject { ["type"] = "score", ["name"] = name },
                    question.Instructions?.ToText(),
                    "levels",
                    levels
                );
            default:
                throw new DecisionUnsupportedQuestionTypeException(name, question.Type, "openai.decision", "openai");
        }
    }

    // Instructions are left out when absent, so the wire order stays type, name, instructions, then the options.
    private static JsonObject WithOptions(
        JsonObject question,
        string? instructions,
        string optionsName,
        JsonArray options
    )
    {
        if (instructions is not null)
        {
            question["instructions"] = instructions;
        }

        question[optionsName] = options;
        return question;
    }

    private static List<ParsedAnswer> ParseAnswers(JsonElement root)
    {
        static InvalidResponseDataException Malformed(string reason) => new("Type validation failed: " + reason);

        static double Probability(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.GetDouble() is >= 0 and <= 1
                ? value.GetDouble()
                : throw Malformed(property + " must be a number between 0 and 1.");

        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("answers", out var list)
            || list.ValueKind != JsonValueKind.Array
        )
        {
            throw Malformed("answers must be an array.");
        }

        var result = new List<ParsedAnswer>();
        foreach (var element in list.EnumerateArray())
        {
            if (
                element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String
            )
            {
                throw Malformed("answer type is required.");
            }

            var type = typeElement.GetString()!;
            var name = ModelHttp.OptionalString(element, "name");
            if (
                name is null
                && (
                    type != "refusal"
                    || !element.TryGetProperty("name", out var nameElement)
                    || nameElement.ValueKind != JsonValueKind.Null
                )
            )
            {
                throw Malformed("answer name must be a string.");
            }

            var answer = new ParsedAnswer { Type = type, Name = name };
            switch (type)
            {
                case "refusal":
                    if (name is null)
                    {
                        throw new InvalidResponseDataException(
                            "OpenAI Decisions refused an unnamed question.",
                            root.Clone()
                        );
                    }

                    break;
                case "predicate":
                    answer.Probability = Probability(element, "probability");
                    break;
                case "choice":
                case "score":
                    ReadDistribution(element, type, answer);
                    break;
                default:
                    throw Malformed("unknown answer type.");
            }

            result.Add(answer);
        }

        return result;

        void ReadDistribution(JsonElement element, string type, ParsedAnswer answer)
        {
            if (type == "choice")
            {
                answer.Choice = ModelHttp.OptionalString(element, "choice")
                    ?? throw Malformed("choice must be a string.");
            }
            else
            {
                answer.Score =
                    element.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number
                        ? score.GetDouble()
                        : throw Malformed("score must be a number.");
            }

            if (element.TryGetProperty("confidence", out var confidence) && confidence.ValueKind != JsonValueKind.Null)
            {
                answer.Confidence = Probability(element, "confidence");
            }

            if (!element.TryGetProperty("probabilities", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                throw Malformed("probabilities must be an array.");
            }

            answer.Probabilities = [];
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("value", out var value))
                {
                    throw Malformed("probability value is required.");
                }

                string key;
                if (type == "choice" && value.ValueKind == JsonValueKind.String)
                {
                    key = value.GetString()!;
                }
                else if (
                    type == "score"
                    && value.ValueKind == JsonValueKind.Number
                    && value.TryGetInt32(out var level)
                    && level >= 0
                )
                {
                    key = level.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    throw Malformed("probability value has the wrong type.");
                }

                answer.Probabilities.Add(new KeyValuePair<string, double>(key, Probability(item, "probability")));
            }
        }
    }

    private sealed class ParsedAnswer
    {
        public string Type { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? Choice { get; set; }

        public double? Score { get; set; }

        public double? Probability { get; set; }

        public double? Confidence { get; set; }

        public List<KeyValuePair<string, double>>? Probabilities { get; set; }
    }
}
