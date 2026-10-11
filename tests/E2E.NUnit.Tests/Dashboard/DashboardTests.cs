// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.Dashboard;

/// <summary>
/// The playground's sign-in and dashboard pages, ported from upstream's
/// <c>apps/testbed/tests/dashboard.e2e.ts</c>. Not ported: "authenticated session reaches the dashboard directly",
/// "a session saved from a cookie reaches the dashboard directly" and "signing out invalidates the session". Each
/// opens its test with <c>{ session: 'admin' }</c>, and the port has no saved sessions.
/// </summary>
public sealed class DashboardTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

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
