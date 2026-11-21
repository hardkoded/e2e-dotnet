// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.DeepseekFetch;

/// <summary>Upstream's <c>describe('deepseekFetch')</c> of <c>tool/deepseek-fetch.test.ts</c>.</summary>
public sealed class DeepseekFetchTests
{
    [Fact]
    public void Registers_a_provider_executed_tool_with_an_optional_pinned_URL()
    {
        AssertProviderExecutedTool(
            GatewayTools.DeepseekFetch(new DeepSeekFetchConfig { Url = "https://example.com" }),
            "gateway.deepseek_fetch",
            """{"url":"https://example.com"}"""
        );
    }

    [Fact]
    public void Describes_the_required_fetch_URL()
    {
        MatchObject(
            JsonNode.Parse("""{"required":["url"],"properties":{"url":{"type":"string"}}}"""),
            JsonSchemaOf(DeepseekFetchTool.DeepseekFetch().InputSchema)
        );
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@example.com")]
    [InlineData("not a URL")]
    public void Leaves_URL_validation_to_the_gateway(string url)
    {
        var value = new JsonObject { ["url"] = url }.ToJsonString();

        Assert.True(Validate(value, DeepseekFetchTool.DeepseekFetch().InputSchema).Success);
    }

    [Theory]
    [InlineData("""{"title":"","content":"# Markdown"}""")]
    [InlineData("""{"content":"# Markdown"}""")]
    [InlineData("""{"title":"Title","content":""}""")]
    [InlineData("""{"error":"timeout","message":"Timed out"}""")]
    public void Accepts_response(string value)
    {
        Assert.True(Validate(value, DeepseekFetchTool.DeepseekFetch().OutputSchema).Success);
    }

    [Fact]
    public void Rejects_a_response_without_content()
    {
        Assert.False(Validate("""{"title":"Title"}""", DeepseekFetchTool.DeepseekFetch().OutputSchema).Success);
    }
}
