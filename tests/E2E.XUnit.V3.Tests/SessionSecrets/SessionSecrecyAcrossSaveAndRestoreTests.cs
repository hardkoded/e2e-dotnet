// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.SessionSecrets;

/// <summary>
/// <c>describe('session secrecy across save and restore')</c> of upstream's <c>session-secrets.test.ts</c>: a restored session redacts what the
/// session that saved it learned. Both <c>it</c>s are partial: they run through xUnit with the web engine and a cookie that stores the secret, and
/// look at the failure message only; there is no report, failure screen, or probing executor to read. The upstream <c>provider</c> case is a
/// <c>Secret.Create</c> secret (the port has no provider-resolved secrets) and <c>static</c> is a config secret. Each test runs its fixture in a
/// test process of its own.
/// </summary>
public sealed class SessionSecrecyAcrossSaveAndRestoreTests
{
    internal const string Sentinel = "synthetic-session-token-3141";

    [Fact]
    public async Task Partial_Redacts_a_provider_secret_the_restored_state_echoes_back_in_the_failure()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(CreatedSecretFixture));

        var failure = results["Meets_the_stored_token_again"];
        Assert.Equal("Fail", failure.Result);
        Assert.Contains("<secret:token>", failure.Message);
        Assert.DoesNotContain(Sentinel, failure.Message);
    }

    [Fact]
    public async Task Partial_Redacts_a_static_secret_the_restored_state_echoes_back_in_the_failure()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(ConfigSecretFixture));

        var failure = results["Meets_the_stored_token_again"];
        Assert.Equal("Fail", failure.Result);
        Assert.Contains("<secret:token>", failure.Message);
        Assert.DoesNotContain(Sentinel, failure.Message);
    }
}

/// <summary>Stores a secret filled through a locator in a cookie, and meets it again after the session restores.</summary>
public abstract class StoredSecretFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    protected async Task StoreAsync(Secret secret)
    {
        await App.OpenAsync("/browser");
        await Browser.EvaluateAsync<string?>("() => { document.body.innerHTML = '<input type=text aria-label=Token>'; return null; }");
        await Screen.GetByLabel("Token").FillAsync(secret);
        await Browser.SetCookiesAsync([new BrowserCookie { Name = "stored", Value = SessionSecrecyAcrossSaveAndRestoreTests.Sentinel, Url = await Browser.UrlAsync() }]);
        await SaveSessionAsync("acct");
    }

    protected async Task MeetAgainAsync()
    {
        await App.OpenAsync("/browser");
        var stored = (await Browser.CookiesAsync()).First(cookie => cookie.Name == "stored").Value;
        await Browser.EvaluateAsync<string?>("v => { document.body.innerHTML = '<p aria-label=Echo></p>'; document.querySelector('p').textContent = v; return null; }", stored);
        await Expect.That(Screen.GetByLabel("Echo")).ToHaveTextAsync("nothing stored", timeout: TimeSpan.FromMilliseconds(200));
    }
}

public sealed class CreatedSecretFixture : StoredSecretFixture
{
    [E2ESetup("stores the created token", Saves = ["acct"], Explicit = true)]
    public Task Store() => StoreAsync(Secret.Create("token", SessionSecrecyAcrossSaveAndRestoreTests.Sentinel));

    [Fact(Explicit = true)]
    [E2ESession("acct")]
    public Task Meets_the_stored_token_again() => MeetAgainAsync();
}

public sealed class ConfigSecretFixture : StoredSecretFixture
{
    private static readonly E2EConfig WithSecret = E2EConfig.Parse(
        $$"""{ "cache": { "mode": "off" }, "secrets": { "token": "{{SessionSecrecyAcrossSaveAndRestoreTests.Sentinel}}" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => WithSecret;

    [E2ESetup("stores the config token", Saves = ["acct"], Explicit = true)]
    public Task Store() => StoreAsync(Secrets.Get("token"));

    [Fact(Explicit = true)]
    [E2ESession("acct")]
    public Task Meets_the_stored_token_again() => MeetAgainAsync();
}
