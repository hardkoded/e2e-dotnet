// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewaySpendReport;

/// <summary>The helpers at the top of upstream's <c>gateway-spend-report.test.ts</c>.</summary>
internal static class GatewaySpendReportFixture
{
    public const string Wildcard = "https://api.example.com/*";

    /// <summary>Upstream's <c>createTestServer</c>, answering every URL of the host with <paramref name="body"/>.</summary>
    public static TestServer ServerWith(string body = """{"results":[]}""")
    {
        var server = new TestServer();
        server.RespondJson(Wildcard, body);
        return server;
    }

    public static TestServer ServerWithError(int status, string body)
    {
        var server = new TestServer();
        server.RespondError(Wildcard, status, body);
        return server;
    }

    /// <summary>Upstream's <c>createSpendReport</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewaySpendReport CreateSpendReport(
        TestServer server,
        Func<IReadOnlyDictionary<string, string?>>? headers = null
    ) =>
        new(
            new GatewayConfig
            {
                BaseUrl = "https://api.example.com",
                Headers = _ =>
                    Task.FromResult(
                        headers?.Invoke() ?? new Dictionary<string, string?> { ["Authorization"] = "Bearer test-token" }
                    ),
                HttpClient = server.Client,
            }
        );
}
