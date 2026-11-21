// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using LiveModel = E2E.Decision.Sdk.Gateway.GatewayRealtimeModelLive;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>The helpers at the top of upstream's <c>gateway-realtime-model-live.test.ts</c>.</summary>
internal static class GatewayRealtimeModelLiveFixture
{
    public const string LiveUrl = "wss://ai-gateway.vercel.sh/v4/ai/live-model?ai-model-id=openai%2Fgpt-live-1.1";

    public static LiveModel CreateTestModel(
        string modelId = "openai/gpt-live-1.1",
        string baseUrl = "https://ai-gateway.vercel.sh/v4/ai",
        string? teamIdOrSlug = null,
        Func<GatewayClientSecretRequest, CancellationToken, Task<GatewayClientSecret>>? createClientSecret = null
    ) =>
        new(
            modelId,
            new GatewayRealtimeModelLiveConfig
            {
                Provider = "gateway.live",
                BaseUrl = baseUrl,
                TeamIdOrSlug = teamIdOrSlug,
                CreateClientSecret = createClientSecret
                    ?? ((_, _) => Task.FromResult(new GatewayClientSecret("vcst_test-secret"))),
                GetHeaders = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer vck_test-token",
                            ["x-unset"] = null,
                        }
                    ),
            }
        );

    public static IReadOnlyDictionary<string, JsonNode?> ProviderOptions(string json) =>
        JsonNode.Parse(json)!.AsObject().ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone());

    public static JsonNode Json(string json) => JsonNode.Parse(json)!;

    public static JsonNode Message(string role, string type) =>
        Json($$"""{"type":"message","role":"{{role}}","content":[{"type":"{{type}}","text":"Hi"}]}""");
}
