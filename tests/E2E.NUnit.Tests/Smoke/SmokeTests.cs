// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Smoke;

/// <summary>
/// The tests of upstream's <c>apps/testbed/tests/smoke.e2e.ts</c> (all four outside a <c>describe</c>), against the playground's landing page.
/// All four are ported and carry the <c>smoke</c> category (upstream's <c>tags: ['smoke']</c>).
/// </summary>
[Category("smoke")]
public sealed class SmokeTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Playground_renders_and_navigates()
    {
        await App.OpenAsync();
        await Expect.That(Browser).ToHaveTitleAsync("Playground");
        await Expect.That(Screen.GetByRole("heading", new RoleOptions { Name = "Playground", Level = 1 })).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("heading", new RoleOptions { Level = 2 })).ToHaveCountAsync(0);

        await Screen.GetByRole("link", "Todos").TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/todos");
        await Expect.That(Screen.GetByRole("heading", "Todos")).ToBeVisibleAsync();

        await App.BackAsync();
        await Expect.That(Browser).ToHaveURLAsync("/");
    }

    [Test]
    public async Task Screenshots_capture_evidence()
    {
        await App.OpenAsync();
        await App.ScreenshotAsync("landing");
    }

    [Test]
    public async Task Value_matchers_name_their_check_and_compare_numbers_loosely()
    {
        await App.OpenAsync();
        var links = await Screen.GetByRole("link").CountAsync();
        Expect.That(links, "the playground lists its pages").ToBeGreaterThanOrEqual(2);
        Expect.That((double)links / links, "a ratio of a number to itself").ToBeCloseTo(1);
    }

    // A fact only the running app can tell decides the skip; the step before it stays in the report.
    [Test]
    public async Task Skips_itself_when_the_playground_has_no_marketplace()
    {
        await App.OpenAsync();
        var marketplace = await Screen.GetByRole("link", "Marketplace").CountAsync();
        if (marketplace == 0)
        {
            Assert.Ignore("the playground has no marketplace");
        }

        await Screen.GetByRole("link", "Marketplace").TapAsync();
    }
}
