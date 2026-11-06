// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.NUnit.Tests.AuthSetup;

/// <summary>
/// The setup tests of upstream's <c>apps/testbed/tests/auth.setup.e2e.ts</c> (both <c>test.setup</c> calls, outside any <c>describe</c>). They save
/// the sessions <c>admin</c> and <c>admin-cookie</c>, which the <c>Dashboard</c> tests restore. NUnit runs the <c>AuthSetup</c> namespace
/// before <c>Dashboard</c> (by name), and a run of the <c>Dashboard</c> tests alone fails with <c>SESSION_UNAVAILABLE</c>, because the port does not
/// run a session's setup test for its dependents (#1244; see the Session store note in COMPATIBILITY.md). The config holds the <c>admin</c> credential of
/// upstream's <c>apps/testbed/e2e.config.ts</c>.
/// </summary>
public sealed class AuthSetupTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" }, "credentials": { "admin": { "username": "admin", "password": "admin-pass" } } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [E2ESetup("authenticate as admin", Saves = ["admin"])]
    public async Task Authenticate_as_admin()
    {
        var admin = Credentials.User("admin");

        await App.OpenAsync("/login");
        await Screen.GetByLabel("Username").FillAsync(admin.Username);
        await Screen.GetByLabel("Password").FillAsync(admin.Password);
        await Screen.GetByRole("button", "Sign in").TapAsync();

        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await Expect.That(Screen.GetByRole("status", "Greeting")).ToContainTextAsync("admin");

        await SaveSessionAsync("admin");
    }

    [E2ESetup("authenticate as admin with a cookie", Saves = ["admin-cookie"])]
    public async Task Authenticate_as_admin_with_a_cookie()
    {
        await App.OpenAsync("/");
        await Browser.SetCookiesAsync([new BrowserCookie { Name = "session", Value = "admin", Url = await Browser.UrlAsync(), HttpOnly = true }]);
        await App.OpenAsync("/dashboard");

        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await Expect.That(Screen.GetByRole("status", "Greeting")).ToContainTextAsync("admin");

        await SaveSessionAsync("admin-cookie");
    }
}
