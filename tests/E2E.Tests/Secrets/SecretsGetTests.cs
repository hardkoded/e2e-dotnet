// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.Secrets;

/// <summary>
/// Upstream's <c>secrets.get</c> describe (<c>tests/unit/secrets.test.ts</c>). The handle test is partial: a .NET handle is not frozen, so it
/// checks that the value is never in its text or JSON. The two "before any run exists" tests have no port: the port has no config-evaluation
/// registry slot, so <c>Secrets.Get</c> never returns a deferred reference.
/// </summary>
public sealed class SecretsGetTests
{
    private const string AdminPassword = "super-secret-password";

    private static readonly E2EConfig Config = E2EConfig.Parse(
        """{ "credentials": { "admin": { "username": "admin@example.com", "password": "super-secret-password" } }, "secrets": { "api-key": "sk_live_1_value" } }""",
        Path.GetTempPath(),
        _ => null);

    [Fact]
    public void Throws_SECRET_UNAVAILABLE_for_a_name_the_running_config_does_not_declare()
    {
        var error = Assert.Throws<TestException>(() => Config.Secrets.Get("missing"));
        Assert.Equal("SECRET_UNAVAILABLE", error.Code);
        Assert.Matches("E2E_SECRET_MISSING", error.Message);
        Assert.Equal("secret \"missing\" is not configured; add it to config.secrets or set E2E_SECRET_MISSING", error.Message);
    }

    [Fact]
    public void Partial_Returns_a_frozen_generic_handle_that_never_carries_the_value()
    {
        var handle = Config.Secrets.Get("api-key");

        Assert.Equal("api-key", handle.Name);
        Assert.Equal("generic-secret", handle.Purpose);
        Assert.Equal("<secret:api-key>", handle.ToString());
        Assert.DoesNotContain("sk_live_1_value", JsonSerializer.Serialize(handle), StringComparison.Ordinal);
    }

    [Fact]
    public void Never_hands_out_a_credential_password_that_is_credentials_user_name_password()
    {
        foreach (var name in new[] { "admin", "admin.password" })
        {
            var error = Assert.Throws<TestException>(() => Config.Secrets.Get(name));
            Assert.Equal("SECRET_UNAVAILABLE", error.Code);
            Assert.Contains("credential \"admin\"'s password is credentials.user(\"admin\").password", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(AdminPassword, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Keeps_a_secret_and_a_credential_of_one_name_apart()
    {
        var config = E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "admin@example.com", "password": "super-secret-password" } }, "secrets": { "admin": "admin-api-key" } }""",
            Path.GetTempPath(),
            _ => null);

        var secret = config.Secrets.Get("admin");
        Assert.Equal("admin", secret.Name);
        Assert.Equal("generic-secret", secret.Purpose);
        var password = config.Credentials["admin"].Password;
        Assert.Equal("admin.password", password.Name);
        Assert.Equal("password", password.Purpose);
    }
}
