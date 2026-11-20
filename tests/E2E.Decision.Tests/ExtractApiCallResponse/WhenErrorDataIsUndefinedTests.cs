// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ExtractApiCallResponse;

/// <summary>Upstream's <c>describe('when error.data is undefined')</c> in <c>describe('extractResponseFromAPICallError')</c> of <c>extract-api-call-response.test.ts</c>.</summary>
public sealed class WhenErrorDataIsUndefinedTests
{
    private static ApiCallException Error(int status, string? body, JsonElement? data = null) =>
        new("Request failed", status, new Dictionary<string, string>()) { ResponseBody = body, ParsedData = data };

    private static JsonElement Extract(ApiCallException error) => GatewayErrors.ExtractApiCallResponse(error)!.Value;

    // when error.data is available

    [Fact]
    public void Should_parse_and_return_responseBody_as_JSON_when_valid()
    {
        var responseData = """{"ferror":{"message":"Malformed error","type":"model_not_found"}}""";

        var result = Extract(Error(404, responseData));

        Assert.Equal(responseData, result.GetRawText());
    }

    [Fact]
    public void Should_return_raw_responseBody_when_JSON_parsing_fails() =>
        Assert.Equal("This is not valid JSON", Extract(Error(500, "This is not valid JSON")).GetString());

    [Fact]
    public void Should_handle_HTML_error_responses()
    {
        var html = "<html><body><h1>500 Internal Server Error</h1></body></html>";

        Assert.Equal(html, Extract(Error(500, html)).GetString());
    }

    [Fact]
    public void Should_handle_empty_string_responseBody() => Assert.Equal("", Extract(Error(502, "")).GetString());

    [Fact]
    public void Should_handle_malformed_JSON_gracefully()
    {
        var malformed = """{"incomplete": json""";

        Assert.Equal(malformed, Extract(Error(500, malformed)).GetString()); // Should return raw string, not throw
    }

    [Fact]
    public void Should_return_raw_responseBody_when_JSON_contains_forbidden_prototype_keys()
    {
        var malicious = """{"__proto__": {"polluted": true}}""";

        Assert.Equal(malicious, Extract(Error(500, malicious)).GetString());
    }

    [Fact]
    public void Should_parse_complex_nested_JSON_structures()
    {
        var complex =
            """{"error":{"message":"Complex error","type":"validation_error","details":{"field":"prompt","issues":[{"code":"too_long","message":"Prompt exceeds maximum length"},{"code":"invalid_format","message":"Contains invalid characters"}]}},"metadata":{"requestId":"12345","timestamp":"2024-01-01T00:00:00Z"}}""";

        Assert.Equal(complex, Extract(Error(400, complex)).GetRawText());
    }
}
