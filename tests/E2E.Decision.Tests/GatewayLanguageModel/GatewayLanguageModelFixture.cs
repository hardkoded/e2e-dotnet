// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using GatewayLanguageModelType = E2E.Decision.Sdk.Gateway.GatewayLanguageModel;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-language-model.test.ts</c>.</summary>
internal static class GatewayLanguageModelFixture
{
    public const string LanguageModelUrl = "https://api.test.com/language-model";

    /// <summary>Upstream's <c>TEST_PROMPT</c>.</summary>
    public static readonly IReadOnlyList<LanguageModelV4Message> TestPrompt =
    [
        new LanguageModelV4UserMessage([new LanguageModelV4TextPart("Hello")]),
    ];

    public const string TestPromptJson = """[{"role":"user","content":[{"type":"text","text":"Hello"}]}]""";

    /// <summary>Upstream's <c>createTestModel</c>: a model whose requests go to <paramref name="client"/>.</summary>
    public static GatewayLanguageModelType CreateTestModel(
        HttpClient client,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            "test-model",
            new GatewayChatConfig
            {
                Provider = "test-provider",
                BaseUrl = "https://api.test.com",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                    ),
                HttpClient = client,
                O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
            }
        );

    public static GatewayLanguageModelType CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) => CreateTestModel(server.Client, o11yHeaders);

    /// <summary>The call options of a test: the prompt, unless a test replaces it.</summary>
    public static LanguageModelV4CallOptions Options(
        IReadOnlyList<LanguageModelV4Message>? prompt = null,
        IReadOnlyDictionary<string, string?>? headers = null,
        bool? includeRawChunks = null,
        string? providerOptions = null
    ) =>
        new()
        {
            Prompt = prompt ?? TestPrompt,
            Headers = headers,
            IncludeRawChunks = includeRawChunks,
            ProviderOptions = providerOptions is null
                ? null
                : JsonDocument
                    .Parse(providerOptions)
                    .RootElement.Clone()
                    .EnumerateObject()
                    .ToDictionary(property => property.Name, property => property.Value.Clone()),
        };

    /// <summary>Upstream's <c>prepareJsonResponse</c> of the <c>doGenerate</c> tests: the response the server gives the next call.</summary>
    public static void PrepareJsonResponse(
        TestServer server,
        string content = """{"type":"text","text":""}""",
        string usage = """{"prompt_tokens":4,"completion_tokens":30}""",
        string finishReason = "stop",
        string id = "test-id",
        long created = 1711115037,
        string model = "test-model",
        string? warnings = null
    )
    {
        var body = new JsonObject
        {
            ["id"] = id,
            ["created"] = created,
            ["model"] = model,
            ["content"] = JsonNode.Parse(content),
            ["finish_reason"] = finishReason,
            ["usage"] = JsonNode.Parse(usage),
        };
        if (warnings is not null)
        {
            body["warnings"] = JsonNode.Parse(warnings);
        }

        server.RespondJson(LanguageModelUrl, body.ToJsonString());
    }

    /// <summary>Upstream's <c>prepareStreamResponse</c> of the <c>doStream</c> tests: a stream of text deltas and a finish part.</summary>
    public static void PrepareStreamResponse(TestServer server, string[] content, string finishReason = "stop") =>
        server.RespondStreamChunks(
            LanguageModelUrl,
            [
                .. content.Select(text =>
                    "data: " + new JsonObject { ["type"] = "text-delta", ["textDelta"] = text }.ToJsonString() + "\n\n"
                ),
                "data: "
                    + new JsonObject
                    {
                        ["type"] = "finish",
                        ["finishReason"] = finishReason,
                        ["usage"] = new JsonObject { ["prompt_tokens"] = 10, ["completion_tokens"] = 20 },
                    }.ToJsonString()
                    + "\n\n",
            ]
        );

    /// <summary>Upstream's <c>convertReadableStreamToArray</c> (<c>@ai-sdk/provider-utils/test</c>).</summary>
    public static async Task<List<T>> ConvertReadableStreamToArrayAsync<T>(IAsyncEnumerable<T> stream)
    {
        var result = new List<T>();
        await foreach (var item in stream)
        {
            result.Add(item);
        }

        return result;
    }

    /// <summary>The parts as the JSON array vitest's snapshot prints.</summary>
    public static JsonNode PartsJson(IEnumerable<LanguageModelV4StreamPart> parts) =>
        new JsonArray([.. parts.Select(part => JsonNode.Parse(part.Json.GetRawText()))]);

    public static void AssertJsonEqual(string expected, JsonNode? actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), actual),
            "Expected: " + expected + "\nActual:   " + actual?.ToJsonString()
        );

    /// <summary>
    /// Upstream's <c>vi.fn().mockRejectedValue(error)</c> fetch: a handler that throws <paramref name="error"/>. It stands in for the
    /// <c>fetch</c> option of the config.
    /// </summary>
    public sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw error;
    }

    /// <summary>
    /// Upstream's <c>vi.fn().mockImplementation(globalThis.fetch)</c> fetch: it hands the token of each request to <paramref name="probe"/>
    /// and passes the request on to the server.
    /// </summary>
    public sealed class TokenProbeHandler(HttpMessageHandler inner, Action<CancellationToken> probe)
        : DelegatingHandler(inner)
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            probe(cancellationToken);
            return base.SendAsync(request, CancellationToken.None);
        }
    }
}
