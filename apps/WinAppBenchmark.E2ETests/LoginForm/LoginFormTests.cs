// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.LoginForm;

public sealed class LoginFormTests : BenchmarkTest
{
    // The scenario's hardcoded account, declared once in the base class. Every "valid" value below comes from
    // here; only the invalid ones are literals.
    private UserCredential _account = null!;

    [SetUp]
    public async Task OpenTheLoginForm()
    {
        _account = Credentials.User("benchmark");
        await OpenScenarioAsync("Login Form");
    }

    [Test]
    public async Task Rejects_a_malformed_email()
    {
        await Screen.GetByTestId("email-input").FillAsync("not-an-email");
        await Screen.GetByTestId("password-input").FillAsync(_account.Password);
        await Screen.GetByTestId("login-button").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Enter a valid email address");
    }

    [Test]
    public async Task Rejects_an_unknown_account()
    {
        await Screen.GetByTestId("email-input").FillAsync("someone@example.com");
        await Screen.GetByTestId("password-input").FillAsync(_account.Password);
        await Screen.GetByTestId("login-button").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Invalid credentials");
    }

    [Test]
    public async Task Signs_in_and_logs_out()
    {
        await Screen.GetByTestId("email-input").FillAsync(_account.Username);
        await Screen.GetByTestId("password-input").FillAsync(_account.Password);
        await Screen.GetByTestId("login-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Logged in successfully");

        await Screen.GetByTestId("logout-button").TapAsync();
        await Expect.That(Screen.GetByTestId("login-button")).ToBeVisibleAsync();
    }
}
