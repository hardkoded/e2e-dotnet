// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ExtractApiCallResponse;

/// <summary>Upstream's <c>describe('when responseBody is not available')</c> in <c>describe('extractResponseFromAPICallError')</c> of <c>extract-api-call-response.test.ts</c>.</summary>
public sealed class WhenResponseBodyIsNotAvailableTests
{
    private static ApiCallException Error(int status, string? body, JsonElement? data = null) =>
        new("Request failed", status, new Dictionary<string, string>()) { ResponseBody = body, ParsedData = data };

    private static JsonElement Extract(ApiCallException error) => GatewayErrors.ExtractApiCallResponse(error)!.Value;

    // when error.data is available

    [Fact]
    public void Should_return_empty_object_when_both_data_and_responseBody_are_undefined()
    {
        var result = Extract(Error(500, null));

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Empty(result.EnumerateObject());
    }

    [Fact]
    public void Should_return_empty_object_when_responseBody_is_null()
    {
        var result = Extract(Error(500, null));

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Empty(result.EnumerateObject());
    }
}
