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

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>The config of a <see cref="DecisionTypeSafeAiModel"/> (upstream's <c>TypeSafeAiDecisionModelConfig</c>).</summary>
public sealed class TypeSafeAiDecisionModelConfig
{
    public required string Provider { get; init; }

    public required string BaseUrl { get; init; }

    /// <summary>
    /// Resolves the headers sent with every request, such as <c>Authorization</c>. Without it, the model sends the <c>TYPESAFE_AI_API_KEY</c>
    /// environment variable as the bearer, read at each call.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>? Headers { get; init; }

    /// <summary>The client that sends the requests. Defaults to a client of the model's own, with a 120 second timeout.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>What crosses a workflow boundary for a model (the object upstream's <c>serializeModelOptions</c> returns).</summary>
/// <param name="ModelId">The model id.</param>
/// <param name="Config">The JSON-serializable config: <c>provider</c>, <c>baseURL</c>, and the resolved <c>headers</c> when there are any.</param>
public sealed record SerializedModelOptions(string ModelId, JsonObject Config);

/// <summary>
/// Answers questions through TypeSafe's SystemOne API, <c>POST /systemone</c> (the AI SDK's <c>DecisionTypeSafeAiModel</c> in
/// <c>@ai-sdk/typesafe-ai</c>). It answers <c>choice</c>, <c>score</c>, <c>boolean</c>, and <c>box</c> questions, takes PNG and JPEG images, and
/// reports each answer's confidence as <c>typesafe</c> provider metadata.
/// </summary>
public sealed class DecisionTypeSafeAiModel : IDecisionModel, IEvaluationModel, IDisposable
{
    private const int MaxChoices = 255;
    private const int MaxScoreLevels = 10;
    private const string ApiKeyEnvironmentVariable = "TYPESAFE_AI_API_KEY";
    private const string IdAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly JsonSerializerOptions Compact = new()
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

    public string SpecificationVersion => "v4";

    public IReadOnlyList<string> SupportedQuestionTypes => TypeSafeAiModelCatalog.TypeSafeAiSupportedQuestionTypes;

    public string ModelId { get; }

    public string Provider => _config.Provider;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>
    /// Serializes a model for a workflow step boundary (upstream's <c>[WORKFLOW_SERIALIZE]</c>). The client is not serializable, so it is omitted.
    /// The headers function is resolved now and kept when it completes synchronously.
    /// </summary>
    public static SerializedModelOptions WorkflowSerialize(DecisionTypeSafeAiModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var config = new JsonObject { ["provider"] = model._config.Provider, ["baseURL"] = model._config.BaseUrl };
        if (model._config.Headers is not null)
        {
            var pending = model._config.Headers(CancellationToken.None);
            if (!pending.IsCompleted)
            {
                throw new InvalidOperationException("Cannot serialize asynchronous model options.");
            }

            var headers = new JsonObject();
            foreach (var (name, value) in pending.GetAwaiter().GetResult())
            {
                headers[name] = value;
            }

            config["headers"] = headers;
        }

        return new SerializedModelOptions(model.ModelId, config);
    }

    /// <summary>
    /// Restores a model from <see cref="WorkflowSerialize"/> (upstream's <c>[WORKFLOW_DESERIALIZE]</c>). Without serialized headers the model reads
    /// <c>TYPESAFE_AI_API_KEY</c> at each call. <paramref name="httpClient"/> stands in for the <c>fetch</c> a config can carry upstream.
    /// </summary>
    public static DecisionTypeSafeAiModel WorkflowDeserialize(
        SerializedModelOptions options,
        HttpClient? httpClient = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var config = options.Config;
        Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>? headers = null;
        if (config["headers"] is JsonObject serialized)
        {
            IReadOnlyDictionary<string, string?> resolved = serialized.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.GetValue<string>()
            );
            headers = _ => Task.FromResult(resolved);
        }

        return new DecisionTypeSafeAiModel(
            options.ModelId,
            new TypeSafeAiDecisionModelConfig
            {
                Provider = config["provider"]!.GetValue<string>(),
                BaseUrl = config["baseURL"]!.GetValue<string>(),
                Headers = headers,
                HttpClient = httpClient,
            }
        );
    }

    /// <summary>Deprecated: use <see cref="DoDecideAsync"/>.</summary>
    public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
        DoDecideAsync(call, cancellationToken);

    /// <summary>Sends every question in one request.</summary>
    public async Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        foreach (var (id, question) in call.Questions)
        {
            ValidateLimits(id, question);
        }

        var typesafeOptions = TypeSafeAiDecisionModelOptions.Parse(call.ProviderOptions);
        var warnings = new List<string>();
        if (call.ProviderOptions is not null && call.ProviderOptions.TryGetValue("typesafe", out var typesafe))
        {
            if (typesafe.ValueKind == JsonValueKind.Object)
            {
                warnings.AddRange(
                    typesafe
                        .EnumerateObject()
                        .Where(option => option.Name != "longContext")
                        .Select(option => "unsupported setting: providerOptions.typesafe." + option.Name)
                );
            }
        }

        if (call.MaxOutputTokens is not null)
        {
            warnings.Add("unsupported setting: maxOutputTokens");
        }

        if (call.Reasoning is not null)
        {
            warnings.Add("unsupported setting: reasoning");
        }

        var assets = new JsonObject();
        var images = new List<(string Name, byte[] Data, string MediaType, string FileName)>();
        var stateStrings = call.State.SelectMany(StateStrings).ToList();
        var placed = new List<DecisionStatePart>();
        var index = 0;
        foreach (var part in call.State)
        {
            if (part is not DecisionFilePart file)
            {
                placed.Add(part);
                index++;
                continue;
            }

            if (file.DataType != "data")
            {
                throw new UnsupportedFunctionalityException(
                    "TypeSafe AI decision file input: " + file.MediaType + " (" + file.DataType + ")"
                );
            }

            if (file.MediaType is not ("image/png" or "image/jpeg"))
            {
                throw new UnsupportedFunctionalityException("TypeSafe AI decision image media type: " + file.MediaType);
            }

            var fileOptions = TypeSafeAiFilePartProviderOptions.Parse(file.ProviderOptions);
            if (fileOptions is { InsertReference: false, Reference: null })
            {
                throw new InvalidArgumentException(
                    "providerOptions.typesafe.reference",
                    "TypeSafe images with insertReference: false need an explicit reference."
                );
            }

            // Default to random IDs to avoid accidental text matches, as TypeSafe recommends as the safest option.
            var reference = fileOptions?.Reference ?? GenerateId();
            var insertReference = fileOptions?.InsertReference
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
                    file.GetBytes(),
                    file.MediaType,
                    file.Filename ?? partName + (file.MediaType == "image/png" ? ".png" : ".jpeg")
                )
            );
            if (insertReference)
            {
                placed.Add(new DecisionTextPart(reference));
            }

            index++;
        }

        var requestState = RequestState(placed);
        var hasImages = assets.Count > 0;
        var modelHeaders = _config.Headers is null
            ? new Dictionary<string, string?>
            {
                ["Authorization"] = "Bearer " + LoadApiKey(),
                ["User-Agent"] = ModelHttp.RequestHeaders["User-Agent"],
            }
            : await _config.Headers(cancellationToken).ConfigureAwait(false);
        var body = new JsonObject { ["model"] = ModelId, ["state"] = requestState };
        if (hasImages)
        {
            body["assets"] = assets;
        }

        body["questions"] = Questions(call.Questions);
        var headers = ProviderUtils.CombineHeaders(
            modelHeaders,
            hasImages || call.Questions.Values.Any(question => question.Type == "box")
                ? new Dictionary<string, string?> { ["TypeSafe-Danger-Zone-Images"] = "true" }
                : null,
            typesafeOptions?.LongContext == true
                ? new Dictionary<string, string?> { ["TypeSafe-Danger-Zone-Long-Context"] = "true" }
                : null,
            call.Headers.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)
        );
        var url = _config.BaseUrl.TrimEnd('/') + "/systemone";
        ApiResponse response;
        if (hasImages)
        {
            var form = new MultipartFormDataContent();
            var request = new StringContent(body.ToJsonString(Compact), System.Text.Encoding.UTF8);
            request.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            form.Add(request, "request", "request.json");
            foreach (var (name, data, mediaType, fileName) in images)
            {
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
                form.Add(content, name, fileName);
            }

            response = await ProviderUtils
                .PostFormDataToApiAsync(
                    _http,
                    url,
                    headers,
                    form,
                    (_, text) => TypeSafeAiDecisionApi.ErrorMessage(text),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    url,
                    headers,
                    body.ToJsonString(Compact),
                    (_, text) => TypeSafeAiDecisionApi.ErrorMessage(text),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var parsed = ProviderUtils.ProcessSuccessfulResponse(response, TypeSafeAiDecisionApi.ReadResponse);
        var confidence = new JsonObject();
        var legend = new JsonObject();
        var answers = new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal);
        foreach (var (id, answer) in parsed.Answers)
        {
            answers[id] = answer.Answer;
            if (answer.Type != "noul" && answer.Confidence is { } value)
            {
                confidence[id] = value;
            }

            if (answer.Type == "score" && answer.Legend is { } scoreLegend)
            {
                legend[id] = JsonNode.Parse(scoreLegend.GetRawText());
            }
        }

        return new DecisionModelResult
        {
            Answers = answers,
            Usage = new DecisionUsage(parsed.InputTokens, parsed.OutputTokens),
            // The API rounds displayed probabilities and scores to two decimal places.
            Rounding = new DecisionRounding(2, 2),
            Warnings = warnings,
            ProviderMetadata = JsonSerializer.SerializeToElement(
                new JsonObject
                {
                    ["typesafe"] = new JsonObject { ["confidence"] = confidence, ["legend"] = legend },
                }
            ),
            ResponseModelId = parsed.Model ?? ModelId,
            ResponseHeaders = response.ResponseHeaders,
            ResponseBody = response.Value,
        };
    }

    // The questions as TypeSafe takes them: a boolean question is a noul, and the rest are the AI SDK's shape.
    private static JsonObject Questions(IReadOnlyDictionary<string, DecisionQuestion> questions)
    {
        var json = DecisionQuestionJson.ToJson(questions);
        foreach (var (_, question) in json)
        {
            if (question is JsonObject { } entry && (string?)entry["type"] == "boolean")
            {
                entry["type"] = "noul";
            }
        }

        return json;
    }

    private static void ValidateLimits(string id, DecisionQuestion question)
    {
        if (question.Type == "choice" && question.Choices is not { Count: >= 1 and <= MaxChoices })
        {
            throw new InvalidArgumentException(
                "questions." + id + ".criteria",
                "TypeSafe Choice questions require 1 to 255 options."
            );
        }

        if (
            question.Type == "score"
            && (
                question.Levels is not { Count: >= 1 and <= MaxScoreLevels }
                || question.Levels.Any(level => level is null)
            )
        )
        {
            throw new InvalidArgumentException(
                "questions." + id + ".criteria",
                "TypeSafe Score questions require 1 to 10 non-null levels."
            );
        }
    }

    // loadApiKey({ environmentVariableName: 'TYPESAFE_AI_API_KEY', description: 'TypeSafe' }).
    private static string LoadApiKey() =>
        ModelHttp.Environment(ApiKeyEnvironmentVariable)
        ?? throw new LoadApiKeyException(
            "TypeSafe API key is missing. Pass it using the 'apiKey' parameter or the "
                + ApiKeyEnvironmentVariable
                + " environment variable."
        );

    // The state as TypeSafe takes it: a lone JSON object, array, or string as is; anything else as text lines.
    private static JsonNode RequestState(IReadOnlyList<DecisionStatePart> parts)
    {
        if (
            parts
            is [
                DecisionJsonPart
                {
                    Value.ValueKind: not (
                        JsonValueKind.Null
                        or JsonValueKind.True
                        or JsonValueKind.False
                        or JsonValueKind.Number
                    )
                } only,
            ]
        )
        {
            return JsonNode.Parse(only.Value.GetRawText())!;
        }

        return string.Join(
            "\n",
            parts.Select(part =>
                part switch
                {
                    DecisionTextPart text => text.Text,
                    DecisionJsonPart json => JsonSerializer.Serialize(json.Value, Compact),
                    _ => string.Empty,
                }
            )
        );
    }

    // jsonStrings: every string value of a JSON value, not its object keys.
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

    // generateId(): a random 16 character alphanumeric ID.
    private static string GenerateId()
    {
        var id = new char[16];
        for (var i = 0; i < id.Length; i++)
        {
            id[i] = IdAlphabet[RandomNumberGenerator.GetInt32(IdAlphabet.Length)];
        }

        return new string(id);
    }
}
