// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.TypeSafeAi;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>The constants and helpers at the top of upstream's <c>typesafe-ai-decision-model.test.ts</c>.</summary>
internal static class TypeSafeAiDecisionModelFixture
{
    public const string Url = "https://api.typesafe.ai/v1/systemone";

    public static readonly JsonNode NativeRequest = Fixture("decision-request.json");

    public static readonly JsonNode NativeResponse = Fixture("decision-response.json");

    /// <summary>The questions of the native request, with the refund question written as a <c>boolean</c> (upstream's <c>questions</c>).</summary>
    public static readonly IReadOnlyDictionary<string, DecisionQuestion> NativeQuestions = new Dictionary<
        string,
        DecisionQuestion
    >
    {
        ["department"] = DecisionQuestion.Choice(
            Json("""{"question":"Which team should handle this?"}"""),
            [
                new DecisionChoice("billing", Json("""{"includes":["Charges","Invoices","Refunds"]}""")),
                new DecisionChoice("technical", Json("""["Bugs","Outages"]""")),
                new DecisionChoice("other"),
            ]
        ),
        ["severity"] = DecisionQuestion.Score(
            Json("""["How severe is the issue?"]"""),
            [
                Json("""{"meaning":"Cosmetic; functionality works"}"""),
                "Functionality impaired; workaround exists",
                "Blocking; no workaround",
            ]
        ),
        ["requestsRefund"] = DecisionQuestion.Boolean(
            "Is the customer requesting a refund?",
            trueCriteria: Json("""{"meaning":"Explicit request for money back"}"""),
            falseCriteria: DecisionInput.Null
        ),
    };

    /// <summary>Upstream's <c>expectedRequest</c>: the native request the model must send.</summary>
    public static JsonNode ExpectedRequest => NativeRequest.DeepClone();

    /// <summary>Upstream's <c>options</c>: the call every test sends unless it changes a part.</summary>
    public static DecisionModelCall Options(
        IReadOnlyList<DecisionStatePart>? state = null,
        IReadOnlyDictionary<string, DecisionQuestion>? questions = null,
        int? maxOutputTokens = null,
        string? reasoning = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? providerOptions = null
    ) =>
        new()
        {
            State = state
                ?? [new DecisionJsonPart(JsonDocument.Parse(NativeRequest["state"]!.ToJsonString()).RootElement)],
            Questions = questions ?? NativeQuestions,
            MaxOutputTokens = maxOutputTokens,
            Reasoning = reasoning,
            Headers = headers ?? new Dictionary<string, string>(),
            ProviderOptions = providerOptions is null ? null : ProviderOptions(providerOptions),
        };

    public static IReadOnlyDictionary<string, JsonElement> ProviderOptions(string json) =>
        JsonDocument.Parse(json).RootElement.Clone().EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    /// <summary>A file part of the given media type and base64 data, with the <c>typesafe</c> options of the part.</summary>
    public static DecisionFilePart FilePart(string mediaType, string base64, string? typesafeOptions = null) =>
        new(mediaType, [])
        {
            Base64 = base64,
            ProviderOptions = typesafeOptions is null
                ? null
                : ProviderOptions("""{"typesafe":""" + typesafeOptions + "}"),
        };

    public static DecisionInput Json(string json) => DecisionInput.FromJson(JsonDocument.Parse(json).RootElement);

    /// <summary>A server that answers the native response, and a provider over it (upstream's <c>server</c>, <c>provider</c>).</summary>
    public static (TestServer Server, DecisionTypeSafeAiModel Model) Create(string modelId = "jev-latest")
    {
        var server = new TestServer();
        server.RespondJson(Url, NativeResponse.ToJsonString());
        var provider = new E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider(
            new TypeSafeAiProviderSettings { ApiKey = "test-api-key", HttpClient = server.Client }
        );
        return (server, provider.DecisionModel(modelId));
    }

    /// <summary>The request body of a call: its JSON, or the <c>request</c> part of a multipart one.</summary>
    public static JsonNode RequestJson(TestServerCall call) =>
        call.RequestHeaders["content-type"].StartsWith("multipart/form-data", StringComparison.Ordinal)
            ? JsonNode.Parse(call.RequestBodyMultipart.Single(part => part.Name == "request").Text)!
            : call.RequestBodyJson!;

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "Expected: " + expected?.ToJsonString() + "\nActual:   " + actual?.ToJsonString()
        );

    private static JsonNode Fixture(string name) =>
        JsonNode.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TypeSafeAiDecisionModel", "Fixtures", name))
        )!;
}
