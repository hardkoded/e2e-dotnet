// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.BrowserbaseSearch;

/// <summary>Upstream's <c>describe('browserbaseSearch')</c> of <c>tool/browserbase-search.test.ts</c>.</summary>
public sealed class BrowserbaseSearchTests
{
    [Fact]
    public void Creates_a_provider_executed_Browserbase_Search_tool()
    {
        var tool = BrowserbaseSearchTool.BrowserbaseSearch(new BrowserbaseSearchConfig { NumResults = 5 });

        AssertProviderExecutedTool(tool, "gateway.browserbase_search", """{"numResults":5}""");
    }

    [Fact]
    public void Describes_the_Browserbase_Search_API_input_constraints()
    {
        var inputSchema = JsonSchemaOf(BrowserbaseSearchTool.BrowserbaseSearch().InputSchema);

        MatchObject(
            JsonNode.Parse(
                """
                {
                  "required": ["query"],
                  "properties": {
                    "query": { "minLength": 1, "maxLength": 200 },
                    "num_results": { "minimum": 1, "maximum": 25 }
                  }
                }
                """
            ),
            inputSchema
        );
    }

    [Fact]
    public void Validates_a_Browserbase_Search_API_success_response()
    {
        var result = Validate(
            """
            {
              "requestId": "req_9f8e7d6c5b4a",
              "query": "half dome permit",
              "results": [
                {
                  "id": "result_1",
                  "url": "https://www.nps.gov/yose/planyourvisit/halfdome.htm",
                  "title": "Half Dome - Yosemite National Park",
                  "author": "National Park Service",
                  "favicon": "https://www.nps.gov/favicon.ico",
                  "image": "https://www.nps.gov/halfdome.jpg",
                  "publishedDate": "2026-01-15T00:00:00Z"
                },
                {
                  "id": "result_2",
                  "url": "https://www.recreation.gov/permits/234652",
                  "title": "Half Dome Permits"
                }
              ]
            }
            """,
            BrowserbaseSearchTool.BrowserbaseSearch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Validates_a_Browserbase_Search_API_error_response()
    {
        var result = Validate(
            """{"error":"timeout","statusCode":504,"message":"Search request timed out"}""",
            BrowserbaseSearchTool.BrowserbaseSearch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_a_success_response_missing_requestId()
    {
        var result = Validate(
            """{"query":"half dome permit","results":[]}""",
            BrowserbaseSearchTool.BrowserbaseSearch().OutputSchema
        );

        Assert.False(result.Success);
    }
}
