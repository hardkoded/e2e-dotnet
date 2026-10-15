// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.CliLogin;
using E2E.OAuth;

namespace E2E.Cli.Tests.OauthCli;

/// <summary>
/// <c>e2e login</c> and <c>e2e logout</c> with no terminal. The tests name their logins through the environment store, never
/// the developer's own credential file. The command raises its error and <c>Main</c> prints it as one line and exits 1, so
/// these tests are partial: they read the raised error, not the printed line or the exit code.
/// </summary>
[Collection(E2eLoginFlagsTests.Name)]
public sealed class E2eLoginLogoutTests : IDisposable
{
    private readonly string? _credentials = Environment.GetEnvironmentVariable(CredentialStores.EnvironmentVariable);

    public E2eLoginLogoutTests()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "{}");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, _credentials);
    }

    [Fact]
    public async Task Needs_a_named_provider_naming_the_providers_when_there_is_no_terminal_to_pick_in()
    {
        var error = await Assert.ThrowsAsync<OAuthException>(() => Run("login"));

        Assert.Equal(OAuthException.Misconfigured, error.Code);
        Assert.Equal("name a provider: openai, github-copilot, opencode-console, spacexai", error.Message);
    }

    [Fact]
    public async Task Turns_an_unknown_provider_into_one_error_naming_the_providers_not_a_stack_trace()
    {
        var error = await Assert.ThrowsAsync<OAuthException>(() => Run("logout", "nope"));

        Assert.Equal(OAuthException.Misconfigured, error.Code);
        Assert.Contains("openai, github-copilot, opencode-console, spacexai", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Fact]
    public async Task Prints_the_sign_in_URL_on_one_unbordered_line_in_a_terminal_so_it_copies_whole()
    {
        var url = "https://auth.example.test/oauth/authorize?" + string.Concat(Enumerable.Repeat("scope=openid+profile+email&", 8)) + "state=xyz";
        OAuthProviders.Replacements["openai"] = new AuthDouble(new OAuthAuthInfo(url, "Sign in in the browser.", "ABCD-1234"));
        var previous = Program.OpenUrl;
        var configHome = Directory.CreateTempSubdirectory("e2e-login-").FullName;
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "");
        Program.OpenUrl = _ => { };
        var stdout = new StringWriter();
        try
        {
            await Assert.ThrowsAsync<OAuthException>(() =>
                Program.RunAsync(["login", "openai"], stdout, new StringWriter(), TextReader.Null, interactive: true, CancellationToken.None));
        }
        finally
        {
            Program.OpenUrl = previous;
            OAuthProviders.Replacements.Clear();
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdg);
            Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "{}");
            Directory.Delete(configHome, recursive: true);
        }

        Assert.Contains(url, stdout.ToString().Split('\n'));
        Assert.Contains("ABCD-1234", stdout.ToString(), StringComparison.Ordinal);
    }

    private sealed class AuthDouble(OAuthAuthInfo info) : IOAuthProvider
    {
        public string Id => "openai";

        public string Name => "double";

        public Task<OAuthCredentials> LoginAsync(OAuthLoginCallbacks callbacks, OAuthLoginOptions options, CancellationToken cancellationToken)
        {
            callbacks.OnAuth(info);
            throw new OAuthException(OAuthException.Cancelled, "the login was cancelled");
        }

        public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("a login is never refreshed here");

        public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("models are never listed here");
    }

    private static Task<int> Run(params string[] args) =>
        Program.RunAsync(args, new StringWriter(), new StringWriter(), TextReader.Null, interactive: false, CancellationToken.None);
}
