// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayRerankingModel.GatewayRerankingModelFixture;

namespace E2E.Decision.Tests.GatewayRerankingModel;

/// <summary>Upstream's <c>describe('URL construction')</c> of <c>gateway-reranking-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class UrlConstructionTests
{
    [Fact]
    public async Task Should_post_to_reranking_model_endpoint()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoRerankAsync(Call());

        Assert.Equal("https://api.test.com/reranking-model", server.Calls[0].RequestUrl);
    }
}
