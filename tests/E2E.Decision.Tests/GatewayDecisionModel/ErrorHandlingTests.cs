// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;

namespace E2E.Decision.Tests.GatewayDecisionModel;

/// <summary>Upstream's <c>describe('error handling')</c> of <c>gateway-decision-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task Should_throw_GatewayInvalidRequestError_on_400()
    {
        var server = new TestServer();
        server.RespondError(
            DecisionUrl,
            400,
            """{"error":{"message":"Invalid questions format","type":"invalid_request_error"}}"""
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoDecideAsync(Call(), CancellationToken.None)
        );

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Should_throw_GatewayInternalServerError_on_500()
    {
        var server = new TestServer();
        server.RespondError(
            DecisionUrl,
            500,
            """{"error":{"message":"Internal server error","type":"internal_server_error"}}"""
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            model.DoDecideAsync(Call(), CancellationToken.None)
        );

        Assert.Equal(500, error.StatusCode);
    }
}
