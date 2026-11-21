// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayExtractionModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-extraction-model.test.ts</c>.</summary>
internal static class GatewayExtractionModelFixture
{
    public const string ExtractionUrl = "https://api.test.com/extraction-model";

    public static readonly IReadOnlyList<DecisionStatePart> TestState =
    [
        new DecisionTextPart("Invoice INV-2041 from Acme. Total due $1,250.00."),
    ];

    public static readonly JsonNode TestStateJson = JsonNode.Parse(
        """[{"type":"text","text":"Invoice INV-2041 from Acme. Total due $1,250.00."}]"""
    )!;

    public static readonly IReadOnlyDictionary<string, ExtractionField> TestFields = new Dictionary<
        string,
        ExtractionField
    >
    {
        ["invoiceNumber"] = ExtractionField.Text("The invoice number"),
        ["total"] = ExtractionField.Number("Total amount due", decimals: 2),
        ["contacts"] = ExtractionField.List("text"),
    };

    public static readonly JsonNode TestFieldsJson = JsonNode.Parse(
        """
        {
          "invoiceNumber": { "type": "text", "instructions": "The invoice number" },
          "total": { "type": "number", "instructions": "Total amount due", "decimals": 2 },
          "contacts": { "type": "list", "items": "text" }
        }
        """
    )!;

    public const string DummyAnswers = """
        {
          "invoiceNumber": {
            "type": "text",
            "value": "INV-2041",
            "confidence": 0.98,
            "pNull": 0.01,
            "source": { "type": "text", "part": 0, "start": 8, "end": 16 }
          },
          "total": { "type": "number", "value": 1250, "guess": 1250 },
          "contacts": { "type": "list", "value": null }
        }
        """;

    /// <summary>Upstream's <c>createTestModel</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayExtractionModel CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            "scaledown/extract-1",
            new GatewayExtractionModelConfig
            {
                Provider = "gateway",
                BaseUrl = "https://api.test.com",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                    ),
                HttpClient = server.Client,
                O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
            }
        );

    /// <summary>Upstream's <c>prepareJsonResponse</c>: the body is <c>{ answers: dummyAnswers, ...body }</c>.</summary>
    public static void PrepareJsonResponse(
        TestServer server,
        string? body = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        var response = new JsonObject { ["answers"] = JsonNode.Parse(DummyAnswers) };
        foreach (var (name, value) in (JsonObject)JsonNode.Parse(body ?? "{}")!)
        {
            response[name] = value?.DeepClone();
        }

        server.RespondJson(ExtractionUrl, response.ToJsonString(), headers);
    }

    /// <summary>Upstream's <c>prepareErrorResponse</c>: the status and <c>{ error }</c> the server gives the next call.</summary>
    public static void PrepareErrorResponse(TestServer server, int status, string error) =>
        server.RespondError(ExtractionUrl, status, """{"error":""" + error + "}");

    /// <summary>The extraction call of the tests: the test state and fields, unless a test replaces them.</summary>
    public static ExtractionModelCall Call(
        IReadOnlyList<DecisionStatePart>? state = null,
        string? referenceDate = null,
        int? maxOutputTokens = null,
        string? reasoning = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? providerOptions = null
    ) =>
        new()
        {
            State = state ?? TestState,
            Fields = TestFields,
            ReferenceDate = referenceDate,
            MaxOutputTokens = maxOutputTokens,
            Reasoning = reasoning,
            Headers = headers ?? new Dictionary<string, string>(),
            ProviderOptions = providerOptions is null
                ? null
                : JsonDocument
                    .Parse(providerOptions)
                    .RootElement.Clone()
                    .EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.Clone()),
        };

    /// <summary>State parts from the JSON a test case writes: <c>text</c> and <c>json</c> parts.</summary>
    public static IReadOnlyList<DecisionStatePart> State(string json) =>
        JsonDocument
            .Parse(json)
            .RootElement.EnumerateArray()
            .Select<JsonElement, DecisionStatePart>(part =>
                part.GetProperty("type").GetString() == "text"
                    ? new DecisionTextPart(part.GetProperty("text").GetString()!)
                    : new DecisionJsonPart(part.GetProperty("value").Clone())
            )
            .ToList();

    /// <summary>
    /// The answers as upstream's tests compare them (<c>toStrictEqual</c>): a value is always written, and an optional field only when the
    /// answer has it.
    /// </summary>
    public static JsonNode AnswersJson(IReadOnlyDictionary<string, ExtractionAnswer> answers)
    {
        var result = new JsonObject();
        foreach (var (id, answer) in answers)
        {
            var json = new JsonObject { ["type"] = answer.Type };
            if (answer.Type != "refusal")
            {
                json["value"] =
                    answer.Type == "list"
                        ? answer.StringListValue is { } strings
                            ? new JsonArray(strings.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray())
                            : answer.NumberListValue is { } numbers
                                ? new JsonArray(numbers.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray())
                                : null
                        : answer.StringValue is not null
                            ? JsonValue.Create(answer.StringValue)
                            : answer.NumberValue is { } number
                                ? JsonValue.Create(number)
                                : null;
            }

            if (answer.StringGuess is not null)
            {
                json["guess"] = answer.StringGuess;
            }

            if (answer.NumberGuess is { } guess)
            {
                json["guess"] = guess;
            }

            if (answer.PNull is { } pNull)
            {
                json["pNull"] = pNull;
            }

            if (answer.Confidence is { } confidence)
            {
                json["confidence"] = confidence;
            }

            if (answer.Source is { } source)
            {
                json["source"] = SourceJson(source);
            }

            if (answer.Items is { } items)
            {
                var array = new JsonArray();
                foreach (var item in items)
                {
                    var entry = new JsonObject();
                    if (item.Confidence is { } itemConfidence)
                    {
                        entry["confidence"] = itemConfidence;
                    }

                    if (item.Source is { } itemSource)
                    {
                        entry["source"] = SourceJson(itemSource);
                    }

                    array.Add(entry);
                }

                json["items"] = array;
            }

            result[id] = json;
        }

        return result;
    }

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "Expected: " + expected?.ToJsonString() + "\nActual:   " + actual?.ToJsonString()
        );

    private static JsonObject SourceJson(ExtractionSource source)
    {
        var json = new JsonObject { ["type"] = source.Type, ["part"] = source.Part };
        if (source.Start is { } start)
        {
            json["start"] = start;
        }

        if (source.End is { } end)
        {
            json["end"] = end;
        }

        return json;
    }
}
