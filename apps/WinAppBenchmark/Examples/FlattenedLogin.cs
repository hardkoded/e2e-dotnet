// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Automation.Peers;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Credential-filling variant of the hidden-tree drill: both inputs and the button are in the UI
/// Automation raw view only, with no automation ids. The agent cannot target the email or password
/// fields through the tree: it must tap each field from vision and fill the credential into whatever
/// holds focus, then verify success from pixels alone.
/// </summary>
public sealed class FlattenedLogin : Component
{
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

            if (email != LoginForm.ValidEmail || password != LoginForm.ValidPassword)
            {
                setError("Invalid credentials");
                return;
            }

            setError(null);
            setLoggedIn(true);
        }

        if (loggedIn)
        {
            return VStack(TextBlock("Logged in successfully").SemiBold().FontSize(20).AccessibilityView(AccessibilityView.Raw)).Padding(24);
        }

        return VStack(12,
            Hint($"Use {LoginForm.ValidEmail} / {LoginForm.ValidPassword}").AccessibilityView(AccessibilityView.Raw),
            Caption("Email").AccessibilityView(AccessibilityView.Raw),
            TextBox(email, setEmail).AccessibilityView(AccessibilityView.Raw),
            Caption("Password").AccessibilityView(AccessibilityView.Raw),
            PasswordBox(password, setPassword).AccessibilityView(AccessibilityView.Raw),
            When(error is not null, () => Body(error!).Foreground("#b00020").AccessibilityView(AccessibilityView.Raw)),
            Button("SIGN IN", Submit).AccessibilityView(AccessibilityView.Raw))
            .Padding(24);
    }
}
