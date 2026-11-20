// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayFetchMetadata;

/// <summary>The helpers at the top of upstream's <c>gateway-fetch-metadata.test.ts</c>.</summary>
internal static class GatewayFetchMetadataFixture
{
    public const string BaseUrl = "https://api.example.com";

    public const string Wildcard = BaseUrl + "/*";

    /// <summary>Upstream's <c>mockModelEntry</c>.</summary>
    public const string MockModelEntry = """
        {"id":"model-1","name":"Model One","description":"A test model","pricing":{"input":"0.000001","output":"0.000002"},"specification":{"specificationVersion":"v4","provider":"test-provider","modelId":"model-1"}}
        """;

    /// <summary>Upstream's <c>mockModelEntryWithoutPricing</c>.</summary>
    public const string MockModelEntryWithoutPricing = """
        {"id":"model-2","name":"Model Two","specification":{"specificationVersion":"v4","provider":"test-provider","modelId":"model-2"}}
        """;

    /// <summary>Upstream's <c>createTestServer({ 'https://api.example.com/*': ... })</c> with a JSON body.</summary>
    public static TestServer ServerWith(string body)
    {
        var server = new TestServer();
        server.RespondJson(Wildcard, body);
        return server;
    }

    public static TestServer ServerWithModels(params string[] models) =>
        ServerWith("""{"models":[""" + string.Join(",", models) + "]}");

    public static TestServer ServerWithError(int status, string body)
    {
        var server = new TestServer();
        server.RespondError(Wildcard, status, body);
        return server;
    }

    /// <summary>Upstream's <c>createBasicMetadataFetcher</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayFetchMetadata CreateBasicMetadataFetcher(
        TestServer server,
        Func<IReadOnlyDictionary<string, string?>>? headers = null
    ) =>
        new(
            new GatewayConfig
            {
                BaseUrl = BaseUrl,
                Headers = _ =>
                    Task.FromResult(
                        headers?.Invoke() ?? new Dictionary<string, string?> { ["Authorization"] = "Bearer test-token" }
                    ),
                HttpClient = server.Client,
            }
        );
}
