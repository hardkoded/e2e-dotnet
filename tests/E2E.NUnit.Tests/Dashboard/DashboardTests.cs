// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.Dashboard;

/// <summary>
/// The playground's sign-in and dashboard pages, ported from upstream's <c>apps/testbed/tests/dashboard.e2e.ts</c>
/// (<c>describe('dashboard')</c>). The three tests that open with <c>{ session }</c> restore the sessions that <c>AuthSetup/AuthSetupTests</c> saves;
/// a run of these tests alone runs that setup test first (#1244). Upstream's
/// <c>tags: ['auth']</c> is <c>[Category("auth")]</c>.
/// </summary>
[Category("auth")]
public sealed class DashboardTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [OneTimeTearDown]
    public void RemoveSessions() => SessionRun.Cleanup();

    [Test]
    [E2ESession("admin")]
    public async Task Authenticated_session_reaches_the_dashboard_directly()
    {
        await App.OpenAsync("/dashboard");
        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await Expect.That(Screen.GetByRole("status", "Greeting")).ToHaveTextAsync("Welcome back, admin!");
    }

    [Test]
    [E2ESession("admin-cookie")]
    public async Task A_session_saved_from_a_cookie_reaches_the_dashboard_directly()
    {
        await App.OpenAsync("/dashboard");
        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await Expect.That(Screen.GetByRole("status", "Greeting")).ToHaveTextAsync("Welcome back, admin!");
    }

    [Test]
    [E2ESession("admin")]
    public async Task Signing_out_invalidates_the_session()
    {
        await App.OpenAsync("/dashboard");
        await Screen.GetByRole("link", "Sign out").TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/login");
        await App.OpenAsync("/dashboard");
        await Expect.That(Browser).ToHaveURLAsync("/login");
    }

    [Test]
    public async Task Anonymous_visitors_are_redirected_to_login()
    {
        await App.OpenAsync("/dashboard");
        await Expect.That(Browser).ToHaveURLAsync("/login");
        await Expect.That(Screen.GetByRole("heading", "Sign in")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Wrong_credentials_surface_an_alert()
    {
        await App.OpenAsync("/login");
        await Screen.GetByLabel("Username").FillAsync("admin");
        await Screen.GetByLabel("Password").FillAsync("wrong-password");
        await Screen.GetByRole("button", "Sign in").TapAsync();
        await Expect.That(Screen.GetByRole("alert")).ToHaveTextAsync("Invalid credentials");
    }
}
