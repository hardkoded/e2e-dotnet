// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('Provider Options')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class ProviderOptionsTests
{
    private static async Task<TestServerCall> GenerateAsync(string providerOptions)
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Test response"}""");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options(providerOptions: providerOptions));

        return server.Calls[0];
    }

    private static async Task<TestServerCall> StreamAsync(string providerOptions)
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Hello", " world"]);
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(providerOptions: providerOptions));

        await ConvertReadableStreamToArrayAsync(result.Stream);

        return server.Calls[0];
    }

    [Fact]
    public async Task Should_pass_provider_routing_order_for_doGenerate()
    {
        var call = await GenerateAsync("""{"gateway":{"order":["bedrock","anthropic"]}}""");

        AssertJsonEqual("""{"gateway":{"order":["bedrock","anthropic"]}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_pass_single_provider_in_order_array()
    {
        var call = await GenerateAsync("""{"gateway":{"order":["openai"]}}""");

        AssertJsonEqual("""{"gateway":{"order":["openai"]}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_work_without_provider_options()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Test response"}""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Options());

        var requestBody = server.Calls[0].RequestBodyJson!.AsObject();
        Assert.False(requestBody.ContainsKey("providerOptions"));
        AssertJsonEqual(
            """{"type":"text","text":"Test response"}""",
            JsonNode.Parse(result.Content!.Value.GetRawText())
        );
    }

    [Fact]
    public async Task Should_pass_provider_routing_order_for_doStream()
    {
        var call = await StreamAsync("""{"gateway":{"order":["groq","openai"]}}""");

        AssertJsonEqual("""{"gateway":{"order":["groq","openai"]}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_validate_provider_options_against_schema()
    {
        var call = await GenerateAsync("""{"gateway":{"order":["anthropic","bedrock","openai"]}}""");

        AssertJsonEqual(
            """{"gateway":{"order":["anthropic","bedrock","openai"]}}""",
            call.RequestBodyJson!["providerOptions"]
        );
    }

    [Fact]
    public async Task Should_pass_providerTimeouts_for_doGenerate()
    {
        var call = await GenerateAsync(
            """{"gateway":{"providerTimeouts":{"byok":{"openai":5000,"anthropic":2000}}}}"""
        );

        AssertJsonEqual(
            """{"gateway":{"providerTimeouts":{"byok":{"openai":5000,"anthropic":2000}}}}""",
            call.RequestBodyJson!["providerOptions"]
        );
    }

    [Fact]
    public async Task Should_pass_providerTimeouts_for_doStream()
    {
        var call = await StreamAsync("""{"gateway":{"providerTimeouts":{"byok":{"anthropic":3000}}}}""");

        AssertJsonEqual(
            """{"gateway":{"providerTimeouts":{"byok":{"anthropic":3000}}}}""",
            call.RequestBodyJson!["providerOptions"]
        );
    }

    [Fact]
    public async Task Should_pass_zeroDataRetention_option()
    {
        var call = await GenerateAsync("""{"gateway":{"zeroDataRetention":true}}""");

        AssertJsonEqual("""{"gateway":{"zeroDataRetention":true}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_pass_disallowPromptTraining_option()
    {
        var call = await GenerateAsync("""{"gateway":{"disallowPromptTraining":true}}""");

        AssertJsonEqual("""{"gateway":{"disallowPromptTraining":true}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_pass_quotaEntityId_option()
    {
        var call = await GenerateAsync("""{"gateway":{"quotaEntityId":"entity-123"}}""");

        AssertJsonEqual("""{"gateway":{"quotaEntityId":"entity-123"}}""", call.RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_pass_quotaEntityId_with_other_options()
    {
        var call = await GenerateAsync("""{"gateway":{"quotaEntityId":"entity-123","user":"user-456"}}""");

        AssertJsonEqual(
            """{"gateway":{"quotaEntityId":"entity-123","user":"user-456"}}""",
            call.RequestBodyJson!["providerOptions"]
        );
    }
}
