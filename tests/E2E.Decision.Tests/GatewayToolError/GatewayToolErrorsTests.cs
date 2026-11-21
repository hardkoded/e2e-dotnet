// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.ProviderToolAssertions;

namespace E2E.Decision.Tests.GatewayToolError;

/// <summary>Upstream's <c>describe('Gateway tool errors')</c> of <c>tool/gateway-tool-error.test.ts</c>.</summary>
public sealed class GatewayToolErrorsTests
{
    /// <summary>Upstream's <c>gatewayTools[name]().outputSchema</c>.</summary>
    public static TheoryData<string> ToolNames => ["deepseekFetch", "deepseekSearch", "parallelSearch", "takoSearch"];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Preserves_structured_errors_and_additive_fields(string name)
    {
        const string value = """
            {
              "error": "gateway_rate_limit",
              "message": "Retry later",
              "statusCode": 429,
              "retryAfter": "60",
              "statusText": "Too Many Requests",
              "future_field": true
            }
            """;
        FlexibleSchema outputSchema = name switch
        {
            "deepseekFetch" => GatewayTools.DeepseekFetch().OutputSchema,
            "deepseekSearch" => GatewayTools.DeepseekSearch().OutputSchema,
            "parallelSearch" => GatewayTools.ParallelSearch().OutputSchema,
            "takoSearch" => GatewayTools.TakoSearch().OutputSchema,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        var result = Validate(value, outputSchema);

        Assert.True(result.Success);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(value), result.Value));
    }
}
