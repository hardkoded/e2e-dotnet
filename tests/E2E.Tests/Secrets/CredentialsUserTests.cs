// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.Store;

namespace E2E.Tests.Secrets;

/// <summary>
/// The port reads the process environment, so these tests set variables one at a time. "throws AUTH_CREDENTIAL_UNAVAILABLE
/// when the runner is not active" is not ported: outside a session the port reads the <c>E2E_USER_*</c> variables. "replacing the
/// registry swaps the visible credential set" is partial: a hold stands in for <c>setSecretRegistry</c>. The handle test is partial: a .NET handle is not frozen, so it checks that the password is
/// never in the handle's text or JSON.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CredentialsUserTests
{
    [Fact]
    public void Throws_AUTH_CREDENTIAL_UNAVAILABLE_for_an_unconfigured_name()
    {
        var error = Assert.Throws<TestException>(() => Credentials.User("secrets-test-missing"));
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
        Assert.Contains("'secrets-test-missing' is not configured", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_a_handle_exposing_username_but_never_the_password_value()
    {
        using var username = ProcessEnvironment.Set("E2E_USER_SECRETS_TEST_ADMIN_USERNAME", "admin@example.com");
        using var password = ProcessEnvironment.Set("E2E_USER_SECRETS_TEST_ADMIN_PASSWORD", "super-secret-password");

        var handle = Credentials.User("secrets-test-admin");

        Assert.Equal("admin@example.com", handle.Username);
        Assert.DoesNotContain("super-secret-password", handle.Password.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", JsonSerializer.Serialize(handle), StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", JsonSerializer.Serialize(handle.Password), StringComparison.Ordinal);
    }

    // .NET-only: upstream's credentials.user reads no variable outside a session; the port's fallback maps a name as upstream's envName does.
    [Fact]
    public void Reads_the_variables_envName_maps_a_name_with_punctuation_to()
    {
        using var username = ProcessEnvironment.Set("E2E_USER_SECRETS_TEST_SVC_A_B_USERNAME", "svc@example.com");
        using var password = ProcessEnvironment.Set("E2E_USER_SECRETS_TEST_SVC_A_B_PASSWORD", "svc-secret-password");

        var handle = Credentials.User("secrets-test.svc a-b");

        Assert.Equal("svc@example.com", handle.Username);
        Assert.Equal("secrets-test.svc a-b.password", handle.Password.Name);
    }

    // .NET-only: a session holds the config's accounts for its lifetime, where upstream's runner installs the registry.
    [Fact]
    public async Task A_session_resolves_the_accounts_it_was_given_until_it_is_disposed()
    {
        var admin = new ResolvedCredential("secrets-test-session", "session@example.com", Secret.Create("secrets-test-session.password", "session-pass", "password"));
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            BaseUrl = "https://billing.test",
            TestTitle = "billing > credentials",
            Credentials = new Dictionary<string, ResolvedCredential> { [admin.Name] = admin },
        });

        Assert.Equal("session@example.com", Credentials.User("secrets-test-session").Username);
        await session.DisposeAsync();
        Assert.Throws<TestException>(() => Credentials.User("secrets-test-session"));
    }

    [Fact]
    public void Partial_Replacing_the_registry_swaps_the_visible_credential_set()
    {
        var admin = new ResolvedCredential("secrets-test-admin", "admin@example.com", Secret.Create("secrets-test-admin.password", "super-secret-password", "password"));
        var viewer = new ResolvedCredential("secrets-test-viewer", "viewer@example.com", Secret.Create("secrets-test-viewer.password", "viewer-pass", "password"));
        using (SecretRegistry.Hold(new Dictionary<string, ResolvedCredential> { [admin.Name] = admin }))
        {
            Assert.Equal("admin@example.com", Credentials.User("secrets-test-admin").Username);
        }

        using (SecretRegistry.Hold(new Dictionary<string, ResolvedCredential> { [viewer.Name] = viewer }))
        {
            Assert.Throws<TestException>(() => Credentials.User("secrets-test-admin"));
            var handle = Credentials.User("secrets-test-viewer");
            Assert.Equal("viewer@example.com", handle.Username);
            Assert.Equal("secrets-test-viewer.password", handle.Password.Name);
        }
    }
}
