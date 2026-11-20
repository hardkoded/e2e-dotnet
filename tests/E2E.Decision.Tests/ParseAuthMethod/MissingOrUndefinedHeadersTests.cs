// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('missing or undefined headers')</c> in <c>describe('parseAuthMethod')</c> of <c>parse-auth-method.test.ts</c>.</summary>
public sealed class MissingOrUndefinedHeadersTests
{
    private static string? Parse(string? value) =>
        GatewayErrors.ParseAuthMethod(
            new Dictionary<string, string?> { [GatewayHeaders.GatewayAuthMethodHeader] = value }
        );

    [Fact]
    public void Should_return_undefined_when_header_is_missing() =>
        Assert.Null(
            GatewayErrors.ParseAuthMethod(new Dictionary<string, string?> { ["authorization"] = "Bearer token" })
        );

    [Fact]
    public void Should_return_undefined_when_header_is_undefined() => Assert.Null(Parse(null));

    [Fact]
    public void Should_return_undefined_when_headers_object_is_empty() =>
        Assert.Null(GatewayErrors.ParseAuthMethod(new Dictionary<string, string?>()));
}
