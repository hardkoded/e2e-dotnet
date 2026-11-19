// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.CliLogin;
using E2E.Cli.Tests.Helpers;
using E2E.OAuth;

namespace E2E.Cli.Tests.Cli;

/// <summary>
/// <c>describe('e2e logout argument parsing')</c>. Not ported: "rejects an unknown provider with exit code 2, naming the ids
/// and their aliases". The port has no argument-choice check in its CLI: an unknown provider raises an
/// <see cref="OAuthException"/> that <c>Main</c> prints as one line and exits 1 (covered by
/// <c>E2eLoginLogoutTests</c>).
/// </summary>
[Collection(E2eLoginFlagsTests.Name)]
public sealed class E2eLogoutArgumentParsingTests : IDisposable
{
    private readonly string? _credentials = Environment.GetEnvironmentVariable(CredentialStores.EnvironmentVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, _credentials);
    }

    [Theory]
    [InlineData("spacexsi")]
    [InlineData("spacexai")]
    [InlineData("grok")]
    public async Task Takes_provider_for_the_SpaceXSI_login(string provider)
    {
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "{}");
        var stdout = new LineFeedWriter();

        var code = await Program.RunAsync(
            ["logout", provider],
            stdout,
            new StringWriter(),
            TextReader.Null,
            interactive: false,
            CancellationToken.None
        );

        Assert.Equal(0, code);
        Assert.Contains("No SpaceXSI login was stored.", stdout.ToString(), StringComparison.Ordinal);
    }
}
