// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('valid authentication methods')</c> in <c>describe('parseAuthMethod')</c> of <c>parse-auth-method.test.ts</c>.</summary>
public sealed class ValidAuthenticationMethodsTests
{
    private static string? Parse(string? value) =>
        GatewayErrors.ParseAuthMethod(
            new Dictionary<string, string?> { [GatewayHeaders.GatewayAuthMethodHeader] = value }
        );

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
}
