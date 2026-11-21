// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayRerankingModel.GatewayRerankingModelFixture;

namespace E2E.Decision.Tests.GatewayRerankingModel;

/// <summary>Upstream's <c>describe('error handling')</c> of <c>gateway-reranking-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task Should_throw_GatewayInvalidRequestError_on_400()
    {
        var server = new TestServer();
        server.RespondError(
            RerankingUrl,
            400,
            """{"error":{"message":"Invalid documents format","type":"invalid_request_error"}}"""
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() => model.DoRerankAsync(Call()));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Should_throw_GatewayInternalServerError_on_500()
    {
        var server = new TestServer();
        server.RespondError(
            RerankingUrl,
            500,
            """{"error":{"message":"Internal server error","type":"internal_server_error"}}"""
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() => model.DoRerankAsync(Call()));

        Assert.Equal(500, error.StatusCode);
    }
}
