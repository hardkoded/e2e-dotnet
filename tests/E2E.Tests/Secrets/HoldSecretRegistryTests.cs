// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Store;

namespace E2E.Tests.Secrets;

/// <summary>
/// The port's registry holds only the config's credentials, so each test checks a hold through <c>Credentials.User</c> where upstream
/// checks it through <c>secrets.get</c>. "leaves a registry a run installed since in place" is not ported: the port has no
/// <c>setSecretRegistry</c>. <c>Credentials.User</c> falls back to the <c>E2E_USER_*</c> variables, which these tests leave unset.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class HoldSecretRegistryTests
{
    private static Dictionary<string, ResolvedCredential> Registry(string name, string username) =>
        new(StringComparer.Ordinal) { [name] = new ResolvedCredential(name, username, Secret.Create(name + ".password", "registry-pass-" + name, "password")) };

    private static bool Resolves(string name)
    {
        try
        {
            Credentials.User(name);
            return true;
        }
        catch (TestException)
        {
            return false;
        }
    }

    [Fact]
    public void Partial_Installs_the_newest_registry_still_held_when_a_hold_is_released()
    {
        var releaseKey = SecretRegistry.Hold(Registry("hold-key", "key@example.com"));
        var releaseAdmin = SecretRegistry.Hold(Registry("hold-admin", "admin@example.com"));
        Assert.True(Resolves("hold-admin"));
        releaseAdmin.Dispose();
        Assert.False(Resolves("hold-admin"));
        Assert.True(Resolves("hold-key"));
        releaseAdmin.Dispose();
        Assert.True(Resolves("hold-key"));
        releaseKey.Dispose();
        Assert.False(Resolves("hold-key"));
    }

    [Fact]
    public void Partial_Keeps_a_newer_registry_installed_when_an_older_hold_is_released()
    {
        var releaseKey = SecretRegistry.Hold(Registry("hold-key", "key@example.com"));
        var releaseAdmin = SecretRegistry.Hold(Registry("hold-admin", "admin@example.com"));
        releaseKey.Dispose();
        Assert.True(Resolves("hold-admin"));
        releaseAdmin.Dispose();
        Assert.False(Resolves("hold-admin"));
    }
}
