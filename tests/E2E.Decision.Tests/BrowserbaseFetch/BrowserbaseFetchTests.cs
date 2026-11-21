// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.BrowserbaseFetch;

/// <summary>Upstream's <c>describe('browserbaseFetch')</c> of <c>tool/browserbase-fetch.test.ts</c>.</summary>
public sealed class BrowserbaseFetchTests
{
    [Fact]
    public void Creates_a_provider_executed_Browserbase_Fetch_tool()
    {
        var tool = BrowserbaseFetchTool.BrowserbaseFetch(
            new BrowserbaseFetchConfig
            {
                AllowRedirects = true,
                Format = BrowserbaseFetchFormat.Markdown,
                Proxies = true,
            }
        );

        AssertProviderExecutedTool(
            tool,
            "gateway.browserbase_fetch",
            """{"allowRedirects":true,"format":"markdown","proxies":true}"""
        );
    }

    [Fact]
    public void Describes_all_Browserbase_Fetch_API_inputs()
    {
        var inputSchema = JsonSchemaOf(BrowserbaseFetchTool.BrowserbaseFetch().InputSchema);

        MatchObject(
            JsonNode.Parse(
                """
                {
                  "required": ["url"],
                  "properties": {
                    "url": { "type": "string", "description": "URL of the page to fetch." },
                    "allow_redirects": {
                      "type": "boolean",
                      "description": "Whether to follow HTTP redirects (default: false)."
                    },
                    "allow_insecure_ssl": { "type": "boolean" },
                    "proxies": { "type": "boolean" },
                    "format": { "enum": ["raw", "json", "markdown"] },
                    "schema": { "type": "object" }
                  }
                }
                """
            ),
            inputSchema
        );
    }

    [Fact]
    public void Validates_a_Browserbase_Fetch_API_success_response()
    {
        var result = Validate(
            """
            {
              "id": "fetch_9f8e7d6c5b4a",
              "statusCode": 200,
              "headers": {
                "content-type": "text/html; charset=utf-8",
                "cache-control": "max-age=0"
              },
              "content": "<!DOCTYPE html><html><body>Example</body></html>",
              "contentType": "text/html; charset=utf-8",
              "encoding": "utf-8"
            }
            """,
            BrowserbaseFetchTool.BrowserbaseFetch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Validates_a_JSON_extraction_success_response()
    {
        var result = Validate(
            """
            {
              "id": "fetch_1a2b3c4d5e6f",
              "statusCode": 200,
              "headers": { "content-type": "text/html" },
              "content": { "title": "Example Domain", "price": 42 },
              "contentType": "text/html",
              "encoding": "utf-8"
            }
            """,
            BrowserbaseFetchTool.BrowserbaseFetch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Validates_a_Browserbase_Fetch_API_error_response()
    {
        var result = Validate(
            """{"error":"rate_limit","statusCode":429,"message":"Rate limit exceeded"}""",
            BrowserbaseFetchTool.BrowserbaseFetch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_a_success_response_missing_id()
    {
        var result = Validate(
            """
            {
              "statusCode": 200,
              "headers": { "content-type": "text/html" },
              "content": "<!DOCTYPE html><html><body>Example</body></html>",
              "contentType": "text/html; charset=utf-8",
              "encoding": "utf-8"
            }
            """,
            BrowserbaseFetchTool.BrowserbaseFetch().OutputSchema
        );

        Assert.False(result.Success);
    }
}
