// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayImageModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-image-model.test.ts</c>.</summary>
internal static class GatewayImageModelFixture
{
    public const string ImageUrl = "https://api.test.com/image-model";

    public const string TestModelId = "openai/gpt-image-1";

    public static GatewayImageConfig Config(
        TestServer? server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null,
        string provider = "gateway",
        bool withAuthorization = true
    ) =>
        new()
        {
            Provider = provider,
            BaseUrl = "https://api.test.com",
            Headers = _ =>
                Task.FromResult<IReadOnlyDictionary<string, string?>>(
                    withAuthorization
                        ? new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                        : new Dictionary<string, string?>()
                ),
            HttpClient = server?.Client,
            O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
        };

    /// <summary>Upstream's <c>createTestModel</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayImageModel CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) => new(TestModelId, Config(server, o11yHeaders));

    /// <summary>Upstream's <c>prepareJsonResponse</c>.</summary>
    public static void PrepareJsonResponse(
        TestServer server,
        string images = """["base64-image-1"]""",
        bool? isRetryable = null,
        string? warnings = null,
        string? providerMetadata = null
    )
    {
        var body = new JsonObject { ["images"] = JsonNode.Parse(images) };
        if (isRetryable is not null)
        {
            body["isRetryable"] = isRetryable;
        }

        if (warnings is not null)
        {
            body["warnings"] = JsonNode.Parse(warnings);
        }

        if (providerMetadata is not null)
        {
            body["providerMetadata"] = JsonNode.Parse(providerMetadata);
        }

        server.RespondJson(ImageUrl, body.ToJsonString());
    }

    /// <summary>The call of the tests: a prompt and the options a test sets, with the rest unset.</summary>
    public static ImageModelV4CallOptions Call(
        string prompt = "Test prompt",
        int n = 1,
        string? size = null,
        string? aspectRatio = null,
        long? seed = null,
        IReadOnlyList<ImageModelV4File>? files = null,
        ImageModelV4File? mask = null,
        string providerOptions = "{}",
        IReadOnlyDictionary<string, string?>? headers = null
    ) =>
        new()
        {
            Prompt = prompt,
            N = n,
            Size = size,
            AspectRatio = aspectRatio,
            Seed = seed,
            Files = files,
            Mask = mask,
            ProviderOptions = Options(providerOptions),
            Headers = headers,
        };

    public static IReadOnlyDictionary<string, JsonElement> Options(string json) =>
        GatewayDecisionModel.GatewayDecisionModelFixture.Options(json);

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        GatewayDecisionModel.GatewayDecisionModelFixture.AssertJsonEqual(expected, actual);

    public static JsonNode? Json(string json) => JsonNode.Parse(json);
}
