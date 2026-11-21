// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The config of a <see cref="GatewayExtractionModel"/> (upstream's <c>GatewayConfig &amp; { provider, o11yHeaders }</c>).</summary>
public sealed class GatewayExtractionModelConfig : GatewayConfig
{
    public required string Provider { get; init; }

    /// <summary>Resolves the observability headers (<c>ai-o11y-*</c>) sent with every request.</summary>
    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
}

/// <summary>
/// Extracts typed field values from shared state through the Vercel AI Gateway, <c>POST /extraction-model</c> (the AI SDK's
/// <c>GatewayExtractionModel</c> in <c>@ai-sdk/gateway</c>). The Gateway routes the call to the model named by the id, such as
/// <c>scaledown/extract-1</c>, and rejects the field types that model does not support.
/// </summary>
public sealed class GatewayExtractionModel : IExtractionModel, IDisposable
{
    // JSON.stringify leaves non-ASCII and HTML characters unescaped.
    private static readonly JsonSerializerOptions s_compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly GatewayExtractionModelConfig _config;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public GatewayExtractionModel(string modelId, GatewayExtractionModelConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        _config = config;
        _ownsClient = config.HttpClient is null;
        _http = config.HttpClient ?? new HttpClient { Timeout = ModelHttp.Timeout };
    }

    public string SpecificationVersion => "v4";

    /// <summary>Support varies by model, so the Gateway rejects unsupported fields.</summary>
    public IReadOnlyList<string> SupportedFieldTypes { get; } =
    ["text", "number", "date", "time", "datetime", "duration", "list"];

    public string ModelId { get; }

    public string Provider => _config.Provider;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>Extracts every field from the same state in one request.</summary>
    public async Task<ExtractionModelResult> DoExtractAsync(
        ExtractionModelCall call,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(call);
        var stateParts = ToGatewayStateParts.Convert(call.State, "extraction", allowReferences: true);
        var resolvedHeaders = _config.Headers is null
            ? null
            : await _config.Headers(cancellationToken).ConfigureAwait(false);
        try
        {
            var o11y = await _config.O11yHeaders(cancellationToken).ConfigureAwait(false);
            var headers = ProviderUtils.CombineHeaders(
                resolvedHeaders,
                call.Headers.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
                new Dictionary<string, string?>
                {
                    ["ai-extraction-model-specification-version"] = "4",
                    ["ai-model-id"] = ModelId,
                },
                o11y
            );
            var body = new JsonObject
            {
                ["stateParts"] = stateParts,
                ["fields"] = ExtractionFieldJson.ToJson(call.Fields),
            };
            if (call.ReferenceDate is not null)
            {
                body["referenceDate"] = call.ReferenceDate;
            }

            if (call.MaxOutputTokens is { } maxOutputTokens)
            {
                body["maxOutputTokens"] = maxOutputTokens;
            }

            if (call.Reasoning is not null)
            {
                body["reasoning"] = call.Reasoning;
            }

            if (call.ProviderOptions is not null)
            {
                body["providerOptions"] = JsonSerializer.SerializeToNode(call.ProviderOptions);
            }

            var response = await ProviderUtils
                .PostJsonToApiAsync(
                    _http,
                    _config.BaseUrl.TrimEnd('/') + "/extraction-model",
                    headers,
                    body.ToJsonString(),
                    (_, text) => GatewayDecisionModel.ErrorMessage(text),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, value => ReadResponse(value, response));
        }
        catch (Exception error)
        {
            if (GetUnsupportedField(error) is var (fieldId, fieldType))
            {
                throw new ExtractionUnsupportedFieldTypeException(fieldId, fieldType, Provider, ModelId);
            }

            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }

    // getUnsupportedField: reads the field from the Gateway's unsupported field type response (gatewayUnsupportedFieldErrorSchema).
    private static (string FieldId, string FieldType)? GetUnsupportedField(Exception error)
    {
        if (
            error is not ApiCallException api
            || GatewayErrors.ExtractApiCallResponse(api) is not { ValueKind: JsonValueKind.Object } body
            || !body.TryGetProperty("error", out var gatewayError)
            || gatewayError.ValueKind != JsonValueKind.Object
            || !gatewayError.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String
            || type.GetString() != "invalid_request_error"
            || !gatewayError.TryGetProperty("param", out var param)
            || param.ValueKind != JsonValueKind.Object
            || !param.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String
            || name.GetString() != "AI_ExtractionUnsupportedFieldTypeError"
            || !param.TryGetProperty("fieldId", out var fieldId)
            || fieldId.ValueKind != JsonValueKind.String
            || !param.TryGetProperty("fieldType", out var fieldType)
            || fieldType.ValueKind != JsonValueKind.String
        )
        {
            return null;
        }

        return (fieldId.GetString()!, fieldType.GetString()!);
    }

    // gatewayExtractionResponseSchema, then omitNulls: a null optional field reads as absent, and a null value stays null.
    private ExtractionModelResult ReadResponse(JsonElement root, ApiResponse response)
    {
        Zod.Object(root, "$");
        var list = Zod.Object(Zod.Property(root, "answers", "$"), "$.answers");
        var answers = new Dictionary<string, ExtractionAnswer>(StringComparer.Ordinal);
        foreach (var property in list.EnumerateObject())
        {
            answers[property.Name] = ReadAnswer(property.Value, "$.answers." + property.Name);
        }

        DecisionUsage? usage = null;
        if (root.TryGetProperty("usage", out var rawUsage) && rawUsage.ValueKind != JsonValueKind.Null)
        {
            Zod.Object(rawUsage, "$.usage");
            usage = new DecisionUsage(
                (int?)Zod.NullishNumber(rawUsage, "inputTokens", "$.usage"),
                (int?)Zod.NullishNumber(rawUsage, "outputTokens", "$.usage")
            );
        }

        var warnings = new List<SharedV4Warning>();
        if (root.TryGetProperty("warnings", out var rawWarnings) && rawWarnings.ValueKind != JsonValueKind.Null)
        {
            foreach (var warning in Zod.Array(rawWarnings, "$.warnings").EnumerateArray())
            {
                warnings.Add(ReadWarning(warning));
            }
        }

        JsonElement? providerMetadata = null;
        if (root.TryGetProperty("providerMetadata", out var rawMetadata) && rawMetadata.ValueKind != JsonValueKind.Null)
        {
            foreach (var provider in Zod.Object(rawMetadata, "$.providerMetadata").EnumerateObject())
            {
                Zod.Object(provider.Value, "$.providerMetadata." + provider.Name);
            }

            providerMetadata = rawMetadata.Clone();
        }

        return new ExtractionModelResult
        {
            Answers = answers,
            Usage = usage,
            Warnings = warnings,
            ProviderMetadata = providerMetadata,
            ResponseModelId = Zod.NullishString(root, "model", "$") ?? ModelId,
            ResponseHeaders = response.ResponseHeaders,
            ResponseBody = response.Value,
        };
    }

    // gatewayExtractionWarningSchema: a warning this client does not know is kept as `other` instead of failing a successful response.
    private static SharedV4Warning ReadWarning(JsonElement warning)
    {
        try
        {
            return GatewayWarningSchema.Parse(warning, "$.warnings[]");
        }
        catch (InvalidResponseDataException)
        {
            return new SharedV4Warning
            {
                Type = "other",
                Message = "Unknown warning: " + JsonSerializer.Serialize(warning, s_compact),
            };
        }
    }

    // gatewayExtractionAnswerSchema.
    private static ExtractionAnswer ReadAnswer(JsonElement element, string path)
    {
        Zod.Object(element, path);
        var type = Zod.String(element, "type", path);
        switch (type)
        {
            case "text":
            case "date":
            case "time":
            case "datetime":
                return new ExtractionAnswer
                {
                    Type = type,
                    StringValue = NullableString(element, path),
                    StringGuess = Zod.NullishString(element, "guess", path),
                    PNull = Zod.NullishNumber(element, "pNull", path),
                    Confidence = Zod.NullishNumber(element, "confidence", path),
                    Source = NullishSource(element, "source", path),
                };
            case "number":
            case "duration":
                return new ExtractionAnswer
                {
                    Type = type,
                    NumberValue = NullableNumber(element, path),
                    NumberGuess = Zod.NullishNumber(element, "guess", path),
                    PNull = Zod.NullishNumber(element, "pNull", path),
                    Confidence = Zod.NullishNumber(element, "confidence", path),
                    Source = NullishSource(element, "source", path),
                };
            case "list":
                return ReadListAnswer(element, path);
            case "refusal":
                return new ExtractionAnswer { Type = type };
            default:
                throw Zod.Invalid(path + ".type", "invalid discriminator value");
        }
    }

    private static ExtractionAnswer ReadListAnswer(JsonElement element, string path)
    {
        var value = Zod.Property(element, "value", path);
        IReadOnlyList<string>? strings = null;
        IReadOnlyList<double>? numbers = null;
        if (value.ValueKind != JsonValueKind.Null)
        {
            var entries = Zod.Array(value, path + ".value").EnumerateArray().ToList();
            if (entries.All(entry => entry.ValueKind == JsonValueKind.String))
            {
                strings = entries.Select(entry => entry.GetString()!).ToList();
            }
            else if (entries.All(entry => entry.ValueKind == JsonValueKind.Number))
            {
                numbers = entries.Select(entry => entry.GetDouble()).ToList();
            }
            else
            {
                throw Zod.Invalid(path + ".value", "expected an array of strings or an array of numbers");
            }
        }

        List<ExtractionListItem>? items = null;
        if (element.TryGetProperty("items", out var rawItems) && rawItems.ValueKind != JsonValueKind.Null)
        {
            items = [];
            foreach (var item in Zod.Array(rawItems, path + ".items").EnumerateArray())
            {
                var itemPath = path + ".items[]";
                Zod.Object(item, itemPath);
                items.Add(
                    new ExtractionListItem(
                        Zod.NullishNumber(item, "confidence", itemPath),
                        NullishSource(item, "source", itemPath)
                    )
                );
            }
        }

        return new ExtractionAnswer
        {
            Type = "list",
            StringListValue = strings,
            NumberListValue = numbers,
            PNull = Zod.NullishNumber(element, "pNull", path),
            Confidence = Zod.NullishNumber(element, "confidence", path),
            Items = items,
        };
    }

    // z.nullable(z.string()): the value is required, and null when the state has none.
    private static string? NullableString(JsonElement element, string path)
    {
        var value = Zod.Property(element, "value", path);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw Zod.Invalid(path + ".value", "expected string"),
        };
    }

    // z.nullable(z.number()).
    private static double? NullableNumber(JsonElement element, string path)
    {
        var value = Zod.Property(element, "value", path);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number => value.GetDouble(),
            _ => throw Zod.Invalid(path + ".value", "expected number"),
        };
    }

    // z.nullish(gatewayExtractionSourceSchema()).
    private static ExtractionSource? NullishSource(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var source) || source.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var sourcePath = path + "." + name;
        Zod.Object(source, sourcePath);
        var type = Zod.String(source, "type", sourcePath);
        return type switch
        {
            "text" => new ExtractionSource(
                type,
                Index(source, "part", sourcePath),
                Index(source, "start", sourcePath),
                Index(source, "end", sourcePath)
            ),
            "file" => new ExtractionSource(type, Index(source, "part", sourcePath)),
            _ => throw Zod.Invalid(sourcePath + ".type", "invalid discriminator value"),
        };
    }

    // The offsets and part indexes are z.number() upstream; here they are integers.
    private static int Index(JsonElement source, string name, string path)
    {
        var number = Zod.Number(source, name, path);
        return number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue
            ? (int)number
            : throw Zod.Invalid(path + "." + name, "expected an integer");
    }
}
