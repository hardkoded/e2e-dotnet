// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.ParallelSearch;

/// <summary>Upstream's <c>describe('input schema')</c> of <c>tool/parallel-search.test.ts</c>.</summary>
public sealed class ParallelSearchInputSchemaTests
{
    public static TheoryData<string> Modes => ["turbo", "fast", "basic", "advanced", "one-shot", "agentic"];

    [Theory]
    [MemberData(nameof(Modes))]
    public void Accepts_mode(string mode)
    {
        var value = new JsonObject
        {
            ["objective"] = "Find the latest TypeScript release",
            ["mode"] = mode,
        }.ToJsonString();

        var result = Validate(value, ParallelSearchTool.ParallelSearch().InputSchema);

        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_an_unknown_mode()
    {
        var result = Validate(
            """{"objective":"Find the latest TypeScript release","mode":"deep"}""",
            ParallelSearchTool.ParallelSearch().InputSchema
        );

        Assert.False(result.Success);
    }

    [Fact]
    public void Keeps_the_GA_input_fields()
    {
        const string input = """
            {
              "objective": "Find the Python 3 asyncio docs",
              "search_queries": ["python asyncio docs"],
              "location": "us",
              "max_chars_total": 6000,
              "fetch_policy": {
                "max_age_seconds": 600,
                "timeout_seconds": 10,
                "disable_cache_fallback": true
              },
              "source_policy": { "include_domains": ["docs.python.org/3"] }
            }
            """;

        var result = Validate(input, ParallelSearchTool.ParallelSearch().InputSchema);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(input), result.Value));
    }

    [Fact]
    public void Describes_modes_and_domain_path_prefixes_in_the_JSON_schema()
    {
        var inputSchema = JsonSchemaOf(ParallelSearchTool.ParallelSearch().InputSchema);

        MatchObject(
            JsonNode.Parse(
                """
                {
                  "required": ["objective"],
                  "properties": {
                    "mode": { "enum": ["turbo", "fast", "basic", "advanced", "one-shot", "agentic"] }
                  }
                }
                """
            ),
            inputSchema
        );
        var includeDomains = inputSchema["properties"]!["source_policy"]!["properties"]!["include_domains"]!;
        Assert.Contains("domain/path", includeDomains["description"]!.GetValue<string>());
    }
}
