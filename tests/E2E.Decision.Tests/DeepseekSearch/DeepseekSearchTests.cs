// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.DeepseekSearch;

/// <summary>Upstream's <c>describe('deepseekSearch')</c> of <c>tool/deepseek-search.test.ts</c>.</summary>
public sealed class DeepseekSearchTests
{
    [Fact]
    public void Registers_a_provider_executed_tool_with_camelCase_developer_configuration()
    {
        var tool = GatewayTools.DeepseekSearch(
            new DeepSeekSearchConfig
            {
                Count = 5,
                NeedContent = true,
                OriQuestion = "Question",
                Localization = "8.8.8.8",
            }
        );

        AssertProviderExecutedTool(
            tool,
            "gateway.deepseek_search",
            """{"count":5,"needContent":true,"oriQuestion":"Question","localization":"8.8.8.8"}"""
        );
    }

    [Fact]
    public void Exposes_wire_input_names_but_keeps_localization_developer_only()
    {
        var schema = JsonSchemaOf(DeepseekSearchTool.DeepseekSearch().InputSchema);

        MatchObject(
            JsonNode.Parse(
                """
                {
                  "required": ["query"],
                  "properties": {
                    "query": { "type": "string" },
                    "count": { "type": "integer" },
                    "need_content": { "type": "boolean" }
                  }
                }
                """
            ),
            schema
        );
        Assert.False(schema["properties"]!.AsObject().ContainsKey("localization"));
    }

    public static TheoryData<string> Queries =>
        ["", " ", new string('x', 129), string.Concat(Enumerable.Repeat("😀", 129))];

    [Theory]
    [MemberData(nameof(Queries))]
    public void Leaves_query_validation_to_the_gateway(string query)
    {
        var value = new JsonObject { ["query"] = query }.ToJsonString();

        Assert.True(Validate(value, DeepseekSearchTool.DeepseekSearch().InputSchema).Success);
    }

    [Fact]
    public void Accepts_nullable_result_metadata_and_preserves_additive_response_fields()
    {
        const string value = """
            {
              "results": [
                {
                  "authority": -1,
                  "content": "",
                  "date": null,
                  "rerank_score": 2.4,
                  "site_icon": null,
                  "site_name": null,
                  "snippet": "Snippet",
                  "summary": "Summary",
                  "title": "Title",
                  "url": "https://example.com",
                  "extra": true
                }
              ]
            }
            """;

        var result = Validate(value, DeepseekSearchTool.DeepseekSearch().OutputSchema);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(value), result.Value));
    }

    [Theory]
    [InlineData("""{"results":[]}""")]
    [InlineData("""{"error":"gateway_rate_limit","message":"Retry later","statusCode":429,"retryAfter":"2"}""")]
    public void Accepts_result(string value)
    {
        Assert.True(Validate(value, DeepseekSearchTool.DeepseekSearch().OutputSchema).Success);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"url":"https://example.com"}""")]
    [InlineData("""{"authority":null,"summary":null,"future_field":true}""")]
    public void Accepts_partial_and_evolving_result_metadata(string item)
    {
        var value = $$"""{"results":[{{item}}]}""";

        Assert.True(Validate(value, DeepseekSearchTool.DeepseekSearch().OutputSchema).Success);
    }

    [Fact]
    public void Rejects_malformed_result_bodies()
    {
        Assert.False(Validate("""{"results":null}""", DeepseekSearchTool.DeepseekSearch().OutputSchema).Success);
    }
}
