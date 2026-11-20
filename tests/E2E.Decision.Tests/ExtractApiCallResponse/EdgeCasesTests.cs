// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ExtractApiCallResponse;

/// <summary>Upstream's <c>describe('edge cases')</c> in <c>describe('extractResponseFromAPICallError')</c> of <c>extract-api-call-response.test.ts</c>.</summary>
public sealed class EdgeCasesTests
{
    private static ApiCallException Error(int status, string? body, JsonElement? data = null) =>
        new("Request failed", status, new Dictionary<string, string>()) { ResponseBody = body, ParsedData = data };

    private static JsonElement Extract(ApiCallException error) => GatewayErrors.ExtractApiCallResponse(error)!.Value;

    // when error.data is available

    [Fact]
    public void Should_handle_numeric_responseBody() => Assert.Equal(404, Extract(Error(500, "404")).GetInt32()); // Should parse as number

    [Fact]
    public void Should_handle_boolean_responseBody() => Assert.True(Extract(Error(500, "true")).GetBoolean()); // Should parse as boolean

    [Fact]
    public void Should_handle_array_responseBody()
    {
        var array = """["error1","error2","error3"]""";

        Assert.Equal(array, Extract(Error(400, array)).GetRawText());
    }
}
