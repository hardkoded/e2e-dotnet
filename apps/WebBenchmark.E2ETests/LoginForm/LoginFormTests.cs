// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.LoginForm;

/// <summary>Ported from <c>describe('login form')</c> in upstream's <c>apps/web-benchmark/tests/login-form.e2e.ts</c>.</summary>
public sealed class LoginFormTests : BenchmarkTest
{
    // The scenario's hardcoded account, declared once in the base class (upstream: e2e.config.ts). Every "valid"
    // value below comes from here; only the invalid ones are literals.
    private UserCredential _account = null!;

    [SetUp]
    public async Task OpenTheLoginForm()
    {
        _account = Credentials.User("benchmark");
        await App.OpenAsync("/e/login-form");
    }

    [Test]
    public async Task Rejects_a_malformed_email()
    {
        await Screen.GetByPlaceholder("Email").FillAsync("not-an-email");
        await Screen.GetByPlaceholder("Password").FillAsync(_account.Password);
        await Screen.GetByRole("button", "Log in").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Enter a valid email address");
    }

    [Test]
    public async Task Rejects_a_short_password()
    {
        await Screen.GetByPlaceholder("Email").FillAsync(_account.Username);
        await Screen.GetByPlaceholder("Password").FillAsync("short");
        await Screen.GetByRole("button", "Log in").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Password must be at least 8 characters");
    }

    [Test]
    public async Task Rejects_an_unknown_account()
    {
        await Screen.GetByPlaceholder("Email").FillAsync("someone@example.com");
        await Screen.GetByPlaceholder("Password").FillAsync(_account.Password);
        await Screen.GetByRole("button", "Log in").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Invalid credentials");
    }

    [Test]
    public async Task Signs_in_and_logs_out()
    {
        await Screen.GetByPlaceholder("Email").FillAsync(_account.Username);
        await Screen.GetByPlaceholder("Password").FillAsync(_account.Password);
        await Screen.GetByRole("button", "Log in").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Logged in successfully");

        await Screen.GetByRole("button", "Log out").TapAsync();
        await Expect.That(Screen.GetByPlaceholder("Email")).ToHaveValueAsync("");
        await Expect.That(Screen.GetByRole("button", "Log in")).ToBeVisibleAsync();
    }
}
