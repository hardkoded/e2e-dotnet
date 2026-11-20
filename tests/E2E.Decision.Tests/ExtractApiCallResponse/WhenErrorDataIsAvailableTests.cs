// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ExtractApiCallResponse;

/// <summary>Upstream's <c>describe('when error.data is available')</c> in <c>describe('extractResponseFromAPICallError')</c> of <c>extract-api-call-response.test.ts</c>.</summary>
public sealed class WhenErrorDataIsAvailableTests
{
    private static ApiCallException Error(int status, string? body, JsonElement? data = null) =>
        new("Request failed", status, new Dictionary<string, string>()) { ResponseBody = body, ParsedData = data };

    private static JsonElement Extract(ApiCallException error) => GatewayErrors.ExtractApiCallResponse(error)!.Value;

    // when error.data is available

    [Fact]
    public void Should_return_error_data_when_successfully_parsed_by_AI_SDK()
    {
        var parsed = GatewayJson.Parse("""{"error":{"message":"Parsed error","type":"authentication_error"}}""");

        var result = Extract(Error(401, """{"different":"data"}""", parsed));

        Assert.Equal(parsed.GetRawText(), result.GetRawText()); // Should prefer parsed data over responseBody
    }

    [Fact]
    public void Should_return_error_data_even_when_it_is_null()
    {
        var result = Extract(Error(500, """{"fallback": "data"}""", GatewayJson.Parse("null")));

        Assert.Equal(JsonValueKind.Null, result.ValueKind); // Should return null, not fallback to responseBody
    }

    [Fact]
    public void Should_return_error_data_even_when_it_is_an_empty_object()
    {
        var result = Extract(Error(400, """{"fallback": "data"}""", GatewayJson.Parse("{}")));

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Empty(result.EnumerateObject()); // Should return empty object, not fallback
    }
}
