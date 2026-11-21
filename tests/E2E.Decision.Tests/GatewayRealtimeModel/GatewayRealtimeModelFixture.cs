// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using GatewayModel = E2E.Decision.Sdk.Gateway.GatewayRealtimeModel;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>The helpers at the top of upstream's <c>gateway-realtime-model.test.ts</c>.</summary>
internal static class GatewayRealtimeModelFixture
{
    public static GatewayModel CreateTestModel(
        string modelId = "openai/gpt-realtime",
        string baseUrl = "https://ai-gateway.vercel.sh/v4/ai",
        string token = "vcst_test-secret",
        string? teamIdOrSlug = null,
        Func<GatewayClientSecretRequest, CancellationToken, Task<GatewayClientSecret>>? createClientSecret = null
    ) =>
        new(
            modelId,
            new GatewayRealtimeModelConfig
            {
                Provider = "gateway.realtime",
                BaseUrl = baseUrl,
                TeamIdOrSlug = teamIdOrSlug,
                CreateClientSecret = createClientSecret ?? ((_, _) => Task.FromResult(new GatewayClientSecret(token))),
                GetHeaders = _ =>
                {
                    var headers = new Dictionary<string, string?> { ["Authorization"] = "Bearer " + token };
                    if (teamIdOrSlug is not null)
                    {
                        headers["x-vercel-ai-gateway-team"] = teamIdOrSlug;
                    }

                    return Task.FromResult<IReadOnlyDictionary<string, string?>>(headers);
                },
            }
        );

    public const string RealtimeUrl =
        "wss://ai-gateway.vercel.sh/v4/ai/realtime-model?ai-model-id=openai%2Fgpt-realtime";

    /// <summary>The provider on a test server, with an API key and no environment.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayProvider ProviderOn(
        TestServer? server = null,
        string? teamIdOrSlug = null
    ) =>
        new(
            new GatewayProviderSettings
            {
                ApiKey = "vck_test-token",
                TeamIdOrSlug = teamIdOrSlug,
                HttpClient = server?.Client,
            }
        );
}
