// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>Form filling, validation errors, and credential handling against one hardcoded account.</summary>
public sealed class LoginForm : Component
{
    public const string ValidEmail = "tester@tester.army";
    public const string ValidPassword = "benchmark123";

    public override Element Render()
    {
        var (email, setEmail) = UseState("");
        var (password, setPassword) = UseState("");
        var (error, setError) = UseState<string?>(null);
        var (loggedIn, setLoggedIn) = UseState(false);

        // Validates the credentials against the hardcoded benchmark account and moves the screen into
        // the logged-in success state on match.
        void Submit()
        {
            if (!email.Contains('@'))
            {
                setError("Enter a valid email address");
                return;
            }

            if (password.Length < 8)
            {
                setError("Password must be at least 8 characters");
                return;
            }

            if (email != ValidEmail || password != ValidPassword)
            {
                setError("Invalid credentials");
                return;
            }

            setError(null);
            setLoggedIn(true);
        }

        if (loggedIn)
        {
            return VStack(16,
                Success("Logged in successfully"),
                Button("Log out", () =>
                {
                    setLoggedIn(false);
                    setEmail("");
                    setPassword("");
                }).AutomationId("logout-button"))
                .Padding(24);
        }

        return VStack(12,
            Hint($"Use {ValidEmail} / {ValidPassword}"),
            TextBox(email, setEmail, placeholderText: "Email").AutomationName("Email").AutomationId("email-input"),
            PasswordBox(password, setPassword, placeholderText: "Password").AutomationName("Password").AutomationId("password-input"),
            When(error is not null, () => Body(error!).Foreground("#b00020").AutomationId("error-message")),
            Button("Log in", Submit).AutomationId("login-button"))
            .Padding(24);
    }
}
