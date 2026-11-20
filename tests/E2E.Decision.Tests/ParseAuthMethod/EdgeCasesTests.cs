// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('edge cases')</c> in <c>describe('parseAuthMethod')</c> of <c>parse-auth-method.test.ts</c>.</summary>
public sealed class EdgeCasesTests
{
    private static string? Parse(string? value) =>
        GatewayErrors.ParseAuthMethod(
            new Dictionary<string, string?> { [GatewayHeaders.GatewayAuthMethodHeader] = value }
        );

    [Fact]
    public void Should_return_undefined_for_whitespace_only_strings() => Assert.Null(Parse("   "));

    [Fact]
    public void Should_return_undefined_for_auth_methods_with_extra_whitespace() => Assert.Null(Parse(" api-key "));

    [Fact]
    public void Should_handle_null_values() => Assert.Null(Parse(null));
}
