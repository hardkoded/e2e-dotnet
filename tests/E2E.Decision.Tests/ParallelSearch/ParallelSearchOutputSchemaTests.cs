// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.ParallelSearch;

/// <summary>Upstream's <c>describe('output schema')</c> of <c>tool/parallel-search.test.ts</c>.</summary>
public sealed class ParallelSearchOutputSchemaTests
{
    [Fact]
    public void Validates_a_Beta_response()
    {
        var result = Validate(
            """
            {
              "search_id": "search_beta_1",
              "results": [
                {
                  "url": "https://example.com/docs",
                  "title": "Example Docs",
                  "excerpts": ["# Example Docs\nGetting started."]
                }
              ],
              "usage": [{ "name": "sku_search", "count": 1 }]
            }
            """,
            ParallelSearchTool.ParallelSearch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Validates_a_GA_response_with_null_fields()
    {
        var result = Validate(
            """
            {
              "search_id": "search_ga_1",
              "results": [
                {
                  "url": "https://example.com/docs",
                  "title": null,
                  "publish_date": null,
                  "excerpts": ["First excerpt", "Second excerpt"]
                },
                {
                  "url": "https://example.com/blog",
                  "title": "Example Blog",
                  "publish_date": "2026-01-15",
                  "excerpts": []
                }
              ],
              "warnings": null,
              "usage": [{ "name": "sku_search", "count": 1 }],
              "session_id": "session_ga_1",
              "metadata": null
            }
            """,
            ParallelSearchTool.ParallelSearch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Fact]
    public void Validates_a_GA_response_with_warnings()
    {
        var result = Validate(
            """
            {
              "search_id": "search_ga_2",
              "results": [],
              "warnings": [
                {
                  "type": "input_validation_warning",
                  "message": "Invalid location was ignored.",
                  "detail": { "field": "location" }
                }
              ],
              "session_id": "session_ga_2"
            }
            """,
            ParallelSearchTool.ParallelSearch().OutputSchema
        );

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("""{"error":"execution_error","message":"Search failed"}""")]
    [InlineData("""{"error":"timeout","message":"Search timed out","statusCode":504}""")]
    [InlineData("""{"error":"gateway_rate_limit","message":"Too many requests"}""")]
    public void Validates_a_gateway_error(string value)
    {
        var result = Validate(value, ParallelSearchTool.ParallelSearch().OutputSchema);

        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_a_response_without_search_id()
    {
        var result = Validate(
            """{"results":[{"url":"https://example.com","excerpts":["Excerpt"]}]}""",
            ParallelSearchTool.ParallelSearch().OutputSchema
        );

        Assert.False(result.Success);
    }

    [Fact]
    public void Rejects_the_previously_declared_camelCase_shape()
    {
        var result = Validate(
            """
            {
              "searchId": "search_old_1",
              "results": [{ "url": "https://example.com", "title": "Example", "excerpt": "Excerpt" }]
            }
            """,
            ParallelSearchTool.ParallelSearch().OutputSchema
        );

        Assert.False(result.Success);
    }

    [Fact]
    public void Does_not_declare_fields_the_gateway_never_returns()
    {
        var serializedSchema = JsonSchemaOf(ParallelSearchTool.ParallelSearch().OutputSchema).ToJsonString();

        Assert.DoesNotContain("relevanceScore", serializedSchema);
        Assert.DoesNotContain("searchId", serializedSchema);
        Assert.DoesNotContain("publishDate", serializedSchema);
        Assert.DoesNotContain("\"excerpt\"", serializedSchema);
    }
}
