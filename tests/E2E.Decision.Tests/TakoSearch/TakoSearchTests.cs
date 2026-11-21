// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.TakoSearch;

/// <summary>Upstream's <c>describe('takoSearch')</c> of <c>tool/tako-search.test.ts</c>.</summary>
public sealed class TakoSearchTests
{
    private static ValidationResult ValidateOutput(JsonNode value) =>
        SchemaUtils.SafeValidateTypes(value, TakoSearchTool.TakoSearch().OutputSchema);

    /// <summary>Upstream's <c>variableCostResponse()</c>.</summary>
    private static JsonNode VariableCostResponse() =>
        JsonNode.Parse(
            """
            {
              "request_id": "req_123",
              "cards": [
                {
                  "card_id": "card_traffic",
                  "title": "vercel.com Traffic Summary",
                  "description": "Monthly visits to vercel.com",
                  "relevance": "High",
                  "relevance_score": 4.2,
                  "source_indexes": ["data"],
                  "content": {
                    "content_format": "json_records",
                    "records": [{ "month": "2026-09", "visits": 1000 }],
                    "total_rows": 1,
                    "truncated": false,
                    "premium_rows": { "company_record": 1 },
                    "redacted_cells": { "email": [0] },
                    "total_matching": 12,
                    "export_pricing": {
                      "baseline_usd": 0.01,
                      "max_rows_ceiling": 1000,
                      "row_cpm_usd": 7,
                      "premium_row_cpm_usd": { "company_record": 20 },
                      "variable_cost_max_usd": 0.05
                    }
                  }
                },
                {
                  "card_id": "card_over_cap",
                  "title": "vercel.com Backlinks",
                  "description": "Withheld",
                  "placeholder": { "withheld_source": "Semrush", "reason": "over_cap" }
                },
                {
                  "card_id": "card_not_served",
                  "title": "vercel.com Keywords",
                  "placeholder": { "withheld_source": "Semrush" }
                }
              ],
              "usage": {
                "total_cost_usd": 0.08,
                "compute": { "cost_usd": 0.01 },
                "data": { "cost_usd": 0.02, "datasets": 1, "data_points": 2 },
                "variable_cost": { "cost_usd": 0.05 }
              }
            }
            """
        )!;

    [Fact]
    public void Passes_developer_only_configuration_through_as_camelCase_args()
    {
        var tool = GatewayTools.TakoSearch(
            new TakoSearchConfig
            {
                VariableCost = false,
                MaxVariableCostUsd = Undefinable<double>.Null,
                ExtraBody = new Dictionary<string, JsonNode?>
                {
                    ["output_settings"] = JsonNode.Parse("""{"flat_results":{"max_results":3}}"""),
                },
            }
        );

        AssertProviderExecutedTool(
            tool,
            "gateway.tako_search",
            """
            {
              "variableCost": false,
              "maxVariableCostUsd": null,
              "extraBody": { "output_settings": { "flat_results": { "max_results": 3 } } }
            }
            """
        );
        Assert.True(tool.Args.ContainsKey("maxVariableCostUsd"));
        Assert.Null(tool.Args["maxVariableCostUsd"]);
    }

    [Fact]
    public void Keeps_developer_only_fields_out_of_the_input_schema()
    {
        var serializedSchema = JsonSchemaOf(TakoSearchTool.TakoSearch().InputSchema).ToJsonString();

        foreach (
            var field in new[]
            {
                "variable_cost",
                "max_variable_cost_usd",
                "extra_body",
                "variableCost",
                "maxVariableCostUsd",
                "extraBody",
            }
        )
        {
            Assert.DoesNotContain(field, serializedSchema);
        }
    }

    [Fact]
    public void Describes_data_surcharge_controls_in_the_input_schema()
    {
        var inputSchema = JsonSchemaOf(TakoSearchTool.TakoSearch().InputSchema);

        var data = inputSchema["properties"]!["sources"]!["properties"]!["data"]!["properties"]!;
        Assert.Contains("data surcharge", data["count"]!["description"]!.GetValue<string>());
        Assert.Contains("cards.content.export_pricing", data["include_contents"]!["description"]!.GetValue<string>());
        Assert.Contains("per 1,000 exported rows", data["max_rows"]!["description"]!.GetValue<string>());
    }

    [Fact]
    public void Accepts_a_variable_cost_response_with_placeholder_cards()
    {
        var value = VariableCostResponse();

        var result = ValidateOutput(value);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(value, result.Value));
    }

    [Fact]
    public void Preserves_additive_fields_on_usage_pricing_and_placeholders()
    {
        var value = VariableCostResponse();
        value["usage"]!["future_field"] = 1;
        value["usage"]!["data"]!["future_field"] = 2;
        value["usage"]!["variable_cost"]!["future_field"] = 3;
        value["cards"]![0]!["content"]!["export_pricing"]!["future_field"] = 4;
        value["cards"]![1]!["placeholder"]!["future_field"] = 5;

        var result = ValidateOutput(value);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(value, result.Value));
    }

    [Fact]
    public void Still_accepts_and_preserves_the_deprecated_free_rows_field()
    {
        var value = VariableCostResponse();
        value["cards"]![0]!["content"]!["export_pricing"]!["free_rows"] = 0;

        var result = ValidateOutput(value);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(value, result.Value));
    }

    [Fact]
    public void Exports_the_result_content_schema_for_reuse()
    {
        var content = VariableCostResponse()["cards"]![0]!["content"]!.DeepClone();
        content["future_field"] = true;

        var result = TakoSearchTool.TakoResultContentSchema().SafeParse(content);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(content, result.Data));
    }

    [Fact]
    public void Declares_relevance_score_but_not_citation_number_in_the_output_schema()
    {
        var serializedSchema = JsonSchemaOf(TakoSearchTool.TakoSearch().OutputSchema).ToJsonString();

        Assert.Contains("relevance_score", serializedSchema);
        Assert.DoesNotContain("citation_number", serializedSchema);
    }
}
