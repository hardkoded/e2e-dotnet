// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Network;

/// <summary>
/// The tests of upstream's <c>apps/testbed/tests/network.e2e.ts</c> outside its <c>network</c>
/// <c>describe</c>, against the playground's <c>/network</c> page.
/// </summary>
public sealed class NetworkTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Observes_the_first_navigation_response()
    {
        var waiting = Browser.WaitForResponseAsync("**/network");
        await Browser.GotoAsync("/network");
        var response = await waiting;
        Assert.That(response.Url, Does.Match("/network$"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("text/html"));
        Assert.That(await response.TextAsync(), Does.Contain("<title>Network</title>"));
        await Expect.That(Screen.GetByRole("heading", "Network")).ToBeVisibleAsync();
    }
}
