// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Sequential Onboarding, distilled from a real production signup wizard: a multi-step signup wizard
/// where every step auto-focuses its first field, and the Continue button is pinned at the bottom of
/// the step. The success screen echoes the exact entered values, so a single stray character fails the
/// run. The mobile app's soft keyboard covering the Continue button has no desktop counterpart.
/// </summary>
public sealed partial class SequentialOnboarding : Component
{
    private enum Step { Email, Name, Phone, Done }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+\z")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^0\d{9}\z")]
    private static partial Regex PhonePattern();

    public override Element Render()
    {
        var (step, setStep) = UseState(Step.Email);
        var (email, setEmail) = UseState("");
        var (firstName, setFirstName) = UseState("");
        var (lastName, setLastName) = UseState("");
        var (phone, setPhone) = UseState("");
        var (error, setError) = UseState<string?>(null);
        var (focusRef, requestFocus) = this.UseElementFocus();

        // Every step focuses its first field the moment it appears.
        UseEffect(() => requestFocus(), step);

        void Advance(bool isValid, string message, Step next)
        {
            if (!isValid)
            {
                setError(message);
                return;
            }

            setError(null);
            setStep(next);
        }

        if (step == Step.Done)
        {
            return ScrollView(VStack(12,
                Success("Onboarding complete"),
                Body(email).AutomationId("echo-email"),
                Body($"{firstName} {lastName}").AutomationId("echo-name"),
                Body(phone).AutomationId("echo-phone"))
                .Padding(24));
        }

        Element fields = step switch
        {
            Step.Email => VStack(12,
                Caption("Step 1 of 3"),
                Subtitle("Let's start with your email"),
                TextBox(email, setEmail, placeholderText: "e.g. jane@example.com").EmailInput().Ref(focusRef).AutomationName("Email").AutomationId("email-input").WithKey("email")),
            Step.Name => VStack(12,
                Caption("Step 2 of 3"),
                Subtitle("Tell us more about you"),
                Caption("First name"),
                TextBox(firstName, setFirstName, placeholderText: "e.g. Jane").Ref(focusRef).AutomationName("First name").AutomationId("first-name-input").WithKey("first-name"),
                Caption("Last name").Margin(0, 160, 0, 0),
                TextBox(lastName, setLastName, placeholderText: "e.g. Merchant").AutomationName("Last name").AutomationId("last-name-input").WithKey("last-name")),
            _ => VStack(12,
                Caption("Step 3 of 3"),
                Subtitle("Your phone number"),
                TextBox(phone, setPhone, placeholderText: "e.g. 0612435678").Ref(focusRef).AutomationName("Phone number").AutomationId("phone-input").WithKey("phone")),
        };

        Action advance = step switch
        {
            Step.Email => () => Advance(EmailPattern().IsMatch(email), "Enter a valid email address", Step.Name),
            Step.Name => () => Advance(firstName.Trim().Length > 0 && lastName.Trim().Length > 0, "Enter your first and last name", Step.Phone),
            _ => () => Advance(PhonePattern().IsMatch(phone), "Enter a 10-digit phone number starting with 0", Step.Done),
        };

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star(), GridSize.Auto],
            ScrollView(fields.Padding(24)).AutomationName($"Onboarding step {step}").AutomationId($"onboarding-step-{step.ToString().ToLowerInvariant()}"),
            VStack(8,
                When(error is not null, () => Body(error!).Foreground("#b00020").AutomationId("step-error")),
                Button("Continue", advance).HAlign(HorizontalAlignment.Stretch).AutomationId("continue-button"))
                .Padding(24, 16)
                .Grid(row: 1));
    }
}
