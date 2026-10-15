// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;

namespace E2E.Cli.Tests.CliLogin;

/// <summary>
/// No upstream <c>it</c>: the device-code response is vendor text. It is cleaned before it is printed,
/// and only an http or https URL is ever handed to the operating system.
/// </summary>
[Collection(E2eLoginFlagsTests.Name)]
public sealed class E2eLoginVendorTextTests : IDisposable
{
    private readonly string _configHome = Directory.CreateTempSubdirectory("e2e-login-").FullName;
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    private readonly string? _credentials = Environment.GetEnvironmentVariable(CredentialStores.EnvironmentVariable);

    public E2eLoginVendorTextTests()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configHome);
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "");
    }

    public void Dispose()
    {
        OAuthProviders.Replacements.Clear();
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _xdg);
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, _credentials);
        Directory.Delete(_configHome, recursive: true);
    }

    [Fact]
    public async Task Strips_terminal_escape_sequences_from_the_URL_instructions_and_user_code_it_prints()
    {
        OAuthProviders.Replacements["github-copilot"] = new AuthDouble(new OAuthAuthInfo("https://vendor.test/\u001b[31mdevice", "Enter the code\u001b]0;pwned\u0007 now", "AB\u001b[2JCD"));
        var stdout = new StringWriter();

        var code = await Program.RunAsync(["login", "github-copilot"], stdout, new StringWriter(), TextReader.Null, interactive: false, CancellationToken.None);

        Assert.Equal(0, code);
        var printed = stdout.ToString();
        Assert.DoesNotContain('\u001b', printed);
        Assert.Contains("https://vendor.test/device\nCode: ABCD\nEnter the code now\n", printed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://vendor.test/device")]
    [InlineData("http://localhost:8080/cb?x=1&y=2")]
    public void Opens_an_http_or_https_URL(string url)
    {
        Assert.NotNull(Program.BrowserStart(url));
    }

    [Theory]
    [InlineData("-a /Applications/Calculator.app x")]
    [InlineData("--help")]
    [InlineData("file:///etc/passwd")]
    [InlineData("calc.exe")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void Opens_nothing_else(string url)
    {
        Assert.Null(Program.BrowserStart(url));
    }

    private sealed class AuthDouble(OAuthAuthInfo info) : IOAuthProvider
    {
        public string Id => "github-copilot";

        public string Name => "double";

        public Task<OAuthCredentials> LoginAsync(OAuthLoginCallbacks callbacks, OAuthLoginOptions options, CancellationToken cancellationToken)
        {
            callbacks.OnAuth(info);
            return Task.FromResult(new OAuthCredentials { Access = "token", Refresh = "", Expires = 0 });
        }

        public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("a login is never refreshed here");

        public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("models are never listed here");
    }
}
