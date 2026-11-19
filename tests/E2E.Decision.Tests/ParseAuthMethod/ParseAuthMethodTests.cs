// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('parseAuthMethod')</c> of <c>parse-auth-method.test.ts</c>: every nested describe is a group of tests here.</summary>
public sealed class ParseAuthMethodTests
{
    private static string? Parse(string? value) =>
        GatewayErrors.ParseAuthMethod(
            new Dictionary<string, string?> { [GatewayHeaders.GatewayAuthMethodHeader] = value }
        );

    // valid authentication methods
    [Fact]
    public void Should_parse_api_key_auth_method() => Assert.Equal("api-key", Parse("api-key"));

    [Fact]
    public void Should_parse_oidc_auth_method() => Assert.Equal("oidc", Parse("oidc"));

    [Fact]
    public void Should_handle_headers_with_other_fields_present() =>
        Assert.Equal(
            "api-key",
            GatewayErrors.ParseAuthMethod(
                new Dictionary<string, string?>
                {
                    ["authorization"] = "Bearer token",
                    ["content-type"] = "application/json",
                    [GatewayHeaders.GatewayAuthMethodHeader] = "api-key",
                    ["user-agent"] = "test-agent",
                }
            )
        );

    // invalid authentication methods
    [Fact]
    public void Should_return_undefined_for_invalid_auth_method_string() => Assert.Null(Parse("invalid-method"));

    [Fact]
    public void Should_return_undefined_for_empty_string() => Assert.Null(Parse(""));

    [Fact]
    public void Should_return_undefined_for_numeric_value() => Assert.Null(Parse("123"));

    [Fact]
    public void Should_return_undefined_for_boolean_like_strings() => Assert.Null(Parse("true"));

    [Fact]
    public void Should_return_undefined_for_case_sensitive_variations() => Assert.Null(Parse("API-KEY"));

    [Fact]
    public void Should_return_undefined_for_OIDC_case_variations() => Assert.Null(Parse("OIDC"));

    // missing or undefined headers
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

    // edge cases
    [Fact]
    public void Should_return_undefined_for_whitespace_only_strings() => Assert.Null(Parse("   "));

    [Fact]
    public void Should_return_undefined_for_auth_methods_with_extra_whitespace() => Assert.Null(Parse(" api-key "));

    [Fact]
    public void Should_handle_null_values() => Assert.Null(Parse(null));
}
