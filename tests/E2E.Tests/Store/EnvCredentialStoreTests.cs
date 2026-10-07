// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;

namespace E2E.Tests.Store;

public sealed class EnvCredentialStoreTests
{
    [Fact]
    public void Rejects_malformed_JSON_naming_the_variable()
    {
        var error = Assert.Throws<OAuthException>(() => new EnvironmentCredentialStore("nope"));
        Assert.Contains(CredentialStores.EnvironmentVariable + " is not valid JSON", error.Message, StringComparison.Ordinal);
    }
}
