// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('getSpendReport method')</c> of <c>gateway-provider.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GetSpendReportMethodTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    private static GatewaySpendReportParams Report() => new() { StartDate = "2026-03-01", EndDate = "2026-03-25" };

    [Fact]
    public Task Should_fetch_spend_report_successfully() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(
                DefaultBaseUrl,
                """{"results":[{"day":"2026-03-01","total_cost":10.5,"request_count":25}]}"""
            );
            var provider = ProviderOn(server, apiKey: "test-key");

            var report = await provider.GetSpendReportAsync(Report());

            Assert.Equal(
                new GatewaySpendReportRow
                {
                    Day = "2026-03-01",
                    TotalCost = 10.5,
                    RequestCount = 25,
                },
                Assert.Single(report.Results)
            );
            // The report fetcher gets the default baseURL and the provider's headers.
            Assert.StartsWith(
                "https://ai-gateway.vercel.sh/v1/report?",
                server.Calls[0].RequestUrl,
                StringComparison.Ordinal
            );
            Assert.Equal("Bearer test-key", server.Calls[0].RequestHeaders["authorization"]);
        });

    [Fact]
    public Task Should_pass_params_through_to_GatewaySpendReport() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, SpendReportBody);
            var provider = ProviderOn(server, apiKey: "test-key");

            await provider.GetSpendReportAsync(
                new GatewaySpendReportParams
                {
                    StartDate = "2026-03-01",
                    EndDate = "2026-03-25",
                    GroupBy = "model",
                    DatePart = "day",
                    UserId = "user-123",
                    Model = "anthropic/claude-sonnet-4.6",
                    Tags = ["production", "api"],
                }
            );

            Assert.Equal(
                "https://ai-gateway.vercel.sh/v1/report?start_date=2026-03-01&end_date=2026-03-25&group_by=model&date_part=day&user_id=user-123&model=anthropic%2Fclaude-sonnet-4.6&tags=production%2Capi",
                server.Calls[0].RequestUrl
            );
        });

    [Fact]
    public Task Should_work_with_custom_baseURL() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            const string customBaseUrl = "https://custom-gateway.example.com/v4/ai";
            var server = ServerFor(customBaseUrl, SpendReportBody);
            var provider = ProviderOn(server, baseUrl: customBaseUrl, apiKey: "test-key");

            await provider.GetSpendReportAsync(Report());

            Assert.StartsWith(
                "https://custom-gateway.example.com/v1/report?",
                server.Calls[0].RequestUrl,
                StringComparison.Ordinal
            );
        });

    // Upstream passes a custom fetch; the port's counterpart is a custom HttpClient.
    [Fact]
    public Task Should_work_with_custom_fetch_function() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, SpendReportBody);
            var provider = ProviderOn(server, apiKey: "test-key");

            await provider.GetSpendReportAsync(Report());

            Assert.Single(server.Calls);
        });

    [Fact]
    public Task Should_handle_errors_from_the_spend_report_endpoint() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = new TestServer();
            server.RespondError(
                "https://ai-gateway.vercel.sh/*",
                500,
                """{"error":{"message":"Reporting service unavailable","type":"internal_server_error"}}"""
            );
            var provider = ProviderOn(server, apiKey: "test-key");

            var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
                provider.GetSpendReportAsync(Report())
            );

            Assert.Contains("Reporting service unavailable", error.Message, StringComparison.Ordinal);
        });

    [Fact]
    public Task Should_work_with_OIDC_authentication_in_spend_reports() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, SpendReportBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("oidc-token");
            });

            var report = await provider.GetSpendReportAsync(Report());

            Assert.NotNull(report);
            Assert.True(oidcCalls > 0);
        });

    [Fact]
    public Task Should_be_available_on_the_provider_interface() => WithEnvironmentAsync(NoEnvironment, () =>
        {
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { ApiKey = "test-key" }
            );

            Func<GatewaySpendReportParams, CancellationToken, Task<GatewaySpendReportResponse>> getSpendReport =
                provider.GetSpendReportAsync;

            Assert.NotNull(getSpendReport);
            return Task.CompletedTask;
        });

    [Fact]
    public void Should_be_available_on_the_default_gateway_export()
    {
        Func<GatewaySpendReportParams, CancellationToken, Task<GatewaySpendReportResponse>> getSpendReport =
            E2E.Decision.Sdk.Gateway.GatewayProvider.Gateway.GetSpendReportAsync;

        Assert.NotNull(getSpendReport);
    }
}
