// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

/// <summary>
/// The credentials a <c>Secret</c> password becomes. Not ported: "declares a secrets.get() password as the engine secret
/// it resolves per attempt". The port has no engine secrets to resolve; the engine declares the secret to the attempt
/// when it starts.
/// </summary>
public sealed class BasicAuthCredentialsFromASecretTests
{
    [Fact]
    public void Registers_the_Authorization_credential_the_password_becomes_padded_and_not_for_redaction()
    {
        var auth = new WebBasicAuth("ada", Secret.Create("stagingPassword", "pa55word!"));

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:pa55word!"));
        Assert.EndsWith("=", encoded, StringComparison.Ordinal);
        var declared = auth.Redactions();
        Assert.All(declared, secret => Assert.Equal("stagingPassword", secret.Name));
        Assert.Equal(["pa55word!", encoded, encoded.TrimEnd('=')], declared.Select(secret => secret.Value));
    }

    [Fact]
    public void Resolves_nothing_for_a_plain_string_password()
    {
        Assert.Empty(new WebBasicAuth("ada", "plain-value").Redactions());
    }
}
