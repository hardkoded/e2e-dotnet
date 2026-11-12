// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Network;

/// <summary>
/// <c>test.describe('network')</c> of upstream's <c>apps/testbed/tests/network.e2e.ts</c>, against the playground's
/// <c>/network</c> page. The class carries the <c>network</c> category (upstream's <c>tags: ['network']</c>).
/// </summary>
[Category("network")]
public sealed class NetworkNetworkTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // Upstream's beforeEach. NUnit runs the base class's SetUp (which starts the session) before this one.
    [SetUp]
    public async Task OpenNetwork()
    {
        await App.OpenAsync("/network");
    }

    private sealed record User(string Name);

    [Test]
    public async Task Loads_users_from_the_real_API()
    {
        var response = await AllAsync(Browser.WaitForResponseAsync("**/api/users"), Screen.GetByRole("button", "Load users").TapAsync());
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(await response.JsonAsync<User[]>(), Is.EqualTo(new User[] { new("Ada"), new("Grace"), new("Margaret") }));
        await Expect.That(Screen.GetByRole("status", "Network state")).ToHaveTextAsync("loaded 3");
        await Expect.That(Screen.GetByText("Grace")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Evaluate_inspects_live_page_state()
    {
        await Screen.GetByRole("button", "Load users").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Network state")).ToHaveTextAsync("loaded 3");
        var names = await Browser.EvaluateAsync<string[]>(
            "() => Array.from(document.querySelectorAll('#users li')).map((item) => item.textContent)");
        Assert.That(names, Is.EqualTo(new[] { "Ada", "Grace", "Margaret" }));
    }
}
