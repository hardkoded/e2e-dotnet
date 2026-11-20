// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('invalid authentication methods')</c> in <c>describe('parseAuthMethod')</c> of <c>parse-auth-method.test.ts</c>.</summary>
public sealed class InvalidAuthenticationMethodsTests
{
    private static string? Parse(string? value) =>
        GatewayErrors.ParseAuthMethod(
            new Dictionary<string, string?> { [GatewayHeaders.GatewayAuthMethodHeader] = value }
        );

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
}
