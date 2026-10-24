// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.CliLogin;
using E2E.Cli.Tests.Helpers;
using E2E.OAuth;

namespace E2E.Cli.Tests.OauthCli;

/// <summary>
/// <c>e2e models</c>. The providers that list models are doubles registered in place of the built-in ones, so no vendor is
/// called, and the logins come from the environment store, never the developer's own credential file.
/// </summary>
[Collection(E2eLoginFlagsTests.Name)]
public sealed class E2eModelsTests : IDisposable
{
    private const string Logins = """
        {
          "github-copilot": { "access": "gho", "refresh": "", "expires": 0 },
          "spacexai": { "access": "x", "refresh": "", "expires": 0 }
        }
        """;

    private readonly string? _credentials = Environment.GetEnvironmentVariable(CredentialStores.EnvironmentVariable);
    private readonly string? _apiKey = Environment.GetEnvironmentVariable("OPENCODE_API_KEY");

    public E2eModelsTests()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "{}");
        Environment.SetEnvironmentVariable("OPENCODE_API_KEY", null);
    }

    public void Dispose()
    {
        OAuthProviders.Replacements.Clear();
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, _credentials);
        Environment.SetEnvironmentVariable("OPENCODE_API_KEY", _apiKey);
    }

    /// <summary>Partial: upstream "lists models only for stored logins and says how to sign in when there is none" reads the printed line and the exit code. The command raises the missing-login error and <c>Main</c> prints it as one line and exits 1, so the test reads the raised error.</summary>
    [Fact]
    public async Task Raises_the_missing_login_error_naming_how_to_sign_in_when_there_is_none()
    {
        var none = await Assert.ThrowsAsync<OAuthException>(() => RunAsync("models"));
        Assert.Equal("no login is stored; sign in with e2e login <openai|github-copilot|opencode-console|spacexai>, or set OPENCODE_API_KEY", none.Message);

        var named = await Assert.ThrowsAsync<OAuthException>(() => RunAsync("models", "spacexai"));
        Assert.Equal("no SpaceXAI login is stored; run `e2e login spacexai`", named.Message);
    }

    [Fact]
    public async Task Lists_a_provider_whose_API_key_is_set_alongside_the_stored_logins()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, """{ "spacexai": { "access": "x", "refresh": "", "expires": 0 } }""");
        Environment.SetEnvironmentVariable("OPENCODE_API_KEY", "oc_sk_env");
        OAuthProviders.Replacements["opencode-console"] = new ModelsDouble("opencode-console", "OpenCode Console", () => [new SubscriptionModel("go/deepseek-v4.1-flash")]) { KeyVariable = "OPENCODE_API_KEY" };
        OAuthProviders.Replacements["spacexai"] = new ModelsDouble("spacexai", "SpaceXAI", () => [new SubscriptionModel("grok-4")]);

        var (code, stdout, _) = await RunAsync("models");

        Assert.Equal(0, code);
        Assert.Equal("OpenCode Console (opencode-console): 1 model\n  go/deepseek-v4.1-flash\n\nSpaceXAI (spacexai): 1 model\n  grok-4\n", stdout);
    }

    [Fact]
    public async Task Prints_one_aligned_line_per_model_with_vendor_text_stripped_of_terminal_controls_and_keeps_going_past_a_failing_provider()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, Logins);
        OAuthProviders.Replacements["github-copilot"] = new ModelsDouble("github-copilot", "GitHub Copilot", () => throw new OAuthException(OAuthException.FlowFailed, "GitHub Copilot did not list its models: 503: down"));
        OAuthProviders.Replacements["spacexai"] = new ModelsDouble(
            "spacexai",
            "SpaceXAI",
            () =>
            [
                new SubscriptionModel("grok-4", Detail: "vision; also grok-4-latest"),
                new SubscriptionModel("grok-4-fast\u001b[31m", "grok-4-fast\u001b[31m"),
                new SubscriptionModel("grok-3"),
            ]);

        var (code, stdout, stderr) = await RunAsync("models");

        Assert.Equal(1, code);
        Assert.Contains("GitHub Copilot (github-copilot): GitHub Copilot did not list its models: 503: down\n", stderr, StringComparison.Ordinal);
        Assert.Contains(
            string.Join('\n', "SpaceXAI (spacexai): 3 models", "  grok-4       vision; also grok-4-latest", "  grok-4-fast", "  grok-3", ""),
            stdout,
            StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', stdout);

        OAuthProviders.Replacements["spacexai"] = new ModelsDouble("spacexai", "SpaceXAI", () => []);
        var (emptyCode, emptyOut, _) = await RunAsync("models", "spacexai");
        Assert.Equal(0, emptyCode);
        Assert.Equal("SpaceXAI (spacexai): no models listed\n", emptyOut);
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunAsync(params string[] args)
    {
        var stdout = new LineFeedWriter();
        var stderr = new LineFeedWriter();
        var code = await Program.RunAsync(args, stdout, stderr, TextReader.Null, interactive: false, CancellationToken.None);
        return (code, stdout.ToString(), stderr.ToString());
    }

    /// <summary>A provider that lists what its callback says, or fails with its callback's error.</summary>
    private sealed class ModelsDouble(string id, string name, Func<IReadOnlyList<SubscriptionModel>> models) : IOAuthProvider
    {
        public string Id => id;

        public string Name => name;

        public string? KeyVariable { get; init; }

        public string? ApiKeyEnv => KeyVariable;

        public Task<OAuthCredentials> LoginAsync(OAuthLoginCallbacks callbacks, OAuthLoginOptions options, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("a login is never started here");

        public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("a login is never refreshed here");

        public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken) =>
            Task.FromResult(models());
    }
}
