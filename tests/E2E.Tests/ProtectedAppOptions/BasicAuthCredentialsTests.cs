// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Engine;
using Microsoft.Playwright;
using EngineProtectedApp = E2E.Engine.ProtectedApp;

namespace E2E.Tests.ProtectedAppOptions;

/// <summary>
/// <c>describe('basic-auth credentials')</c>: the credentials a context answers a challenge with, one scoped entry per origin.
/// Not ported: "declares a secrets.get() password as the engine secret it resolves per attempt". The port has no engine secrets
/// to declare; the engine resolves the password through the attempt when it starts.
/// </summary>
public sealed class BasicAuthCredentialsTests
{
    private const string App = "http://127.0.0.1:3000";

    private static Task<string> NotASecret(Secret secret, ResolveSecretOptions? options) => throw new InvalidOperationException("not a secret");

    private static bool SameCredential(HttpCredentials actual, string username, string password, string? origin) =>
        actual.Username == username && actual.Password == password && actual.Origin == origin;

    [Fact]
    public async Task Scopes_one_entry_to_each_origin_the_apps_then_the_extras_normalized()
    {
        var origins = EngineProtectedApp.CredentialOrigins(App, ["https://API.example.com:443/", "http://127.0.0.1:3000"]);
        var credentials = await WebEngine.HttpCredentialsAsync(new WebBasicAuth("ada", "plain-value"), origins, BrowserName.Chromium, NotASecret);

        Assert.Collection(
            credentials,
            first => Assert.True(SameCredential(first, "ada", "plain-value", App)),
            second => Assert.True(SameCredential(second, "ada", "plain-value", "https://api.example.com")));
    }

    [Fact]
    public async Task Goes_unscoped_on_WebKit_when_an_origin_is_on_a_default_port_or_an_IPv6_host_which_WebKit_cannot_match()
    {
        var plain = new WebBasicAuth("ada", "plain-value");
        foreach (var extra in new[] { "https://api.example.com", "http://[::1]:3000" })
        {
            var unscoped = await WebEngine.HttpCredentialsAsync(plain, EngineProtectedApp.CredentialOrigins(App, [extra]), BrowserName.Webkit, NotASecret);
            Assert.True(SameCredential(Assert.Single(unscoped), "ada", "plain-value", null));
        }

        var scoped = await WebEngine.HttpCredentialsAsync(plain, EngineProtectedApp.CredentialOrigins(App, ["http://localhost:4000"]), BrowserName.Webkit, NotASecret);
        Assert.Collection(
            scoped,
            first => Assert.True(SameCredential(first, "ada", "plain-value", App)),
            second => Assert.True(SameCredential(second, "ada", "plain-value", "http://localhost:4000")));

        var firefox = await WebEngine.HttpCredentialsAsync(plain, EngineProtectedApp.CredentialOrigins("https://example.com", null), BrowserName.Firefox, NotASecret);
        Assert.True(SameCredential(Assert.Single(firefox), "ada", "plain-value", "https://example.com"));
    }

    [Fact]
    public async Task Registers_the_Authorization_credential_a_secret_password_becomes_padded_and_not_for_redaction()
    {
        var asked = new List<(string Name, IReadOnlyList<string> Derived)>();
        var credentials = await WebEngine.HttpCredentialsAsync(
            new WebBasicAuth("ada", Secret.Create("stagingPassword", "pa55word!")),
            EngineProtectedApp.CredentialOrigins(App, null),
            BrowserName.Chromium,
            (secret, options) =>
            {
                asked.Add((secret.Name, options?.Derived?.Invoke("pa55word!") ?? []));
                return Task.FromResult("pa55word!");
            });

        Assert.True(SameCredential(Assert.Single(credentials), "ada", "pa55word!", App));
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:pa55word!"));
        Assert.EndsWith("=", encoded, StringComparison.Ordinal);
        var (name, derived) = Assert.Single(asked);
        Assert.Equal("stagingPassword", name);
        Assert.Equal([encoded, encoded.TrimEnd('=')], derived);
    }
}
