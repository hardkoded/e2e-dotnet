// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Automation.Peers;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Distilled from a real production registration screen: every field, label, and button of the
/// scrollable form is in the UI Automation raw view only. Individual inputs are never reachable through
/// the tree and carry no automation ids: the agent must tap fields from vision and type into whatever
/// holds focus, across roughly two screens of content.
/// </summary>
public sealed class FlattenedForm : Component
{
    private enum Field { FirstName, LastName, Email, Password }

    private static readonly (Field Key, string Label, bool Secure)[] Fields =
    [
        (Field.FirstName, "First name", false),
        (Field.LastName, "Last name", false),
        (Field.Email, "Email address", false),
        (Field.Password, "Set password", true),
    ];

    private static string ErrorOf(Field key) => key switch
    {
        Field.FirstName => "Please enter your first name",
        Field.LastName => "Please enter your last name",
        Field.Email => "Please enter a valid email address",
        _ => "Password must be at least 8 characters",
    };

    // Validates one field the same way on every run so failures are deterministic: names must be
    // non-empty, the email needs an "@", and the password needs at least 8 characters.
    private static bool IsValid(Field key, string value)
    {
        var trimmed = value.Trim();
        return key switch
        {
            Field.Email => trimmed.Contains('@') && trimmed.Length >= 3,
            Field.Password => value.Length >= 8,
            _ => trimmed.Length > 0,
        };
    }

    public override Element Render()
    {
        var (values, setValues) = UseState<IReadOnlyDictionary<Field, string>>(new Dictionary<Field, string>());
        var (submitted, setSubmitted) = UseState(false);
        var (done, setDone) = UseState(false);

        string ValueOf(Field key) => values.GetValueOrDefault(key, "");

        void SetValue(Field key, string value)
        {
            var next = new Dictionary<Field, string>(values) { [key] = value };
            setValues(next);
        }

        if (done)
        {
            return VStack(TextBlock("Account created").SemiBold().FontSize(20).AccessibilityView(AccessibilityView.Raw)).Padding(24);
        }

        var filler = "Passwords must contain a minimum of 8 characters. By signing up you confirm you have read the benchmark terms. This filler paragraph exists so the form spans roughly two screens and the scroll indicator reports multiple pages, matching the production app this scenario was distilled from.";
        var trailer = "More filler content below the primary action: support links, privacy policy, and terms and conditions would normally live here. It pads the scroll extent so off-screen estimation has something to measure.";

        return ScrollView(VStack(16, [
            Subtitle("Create your benchmark account").AccessibilityView(AccessibilityView.Raw),
            .. Fields.Select(field => VStack(6,
                    Caption(field.Label).AccessibilityView(AccessibilityView.Raw),
                    field.Secure
                        ? PasswordBox(ValueOf(field.Key), value => SetValue(field.Key, value)).AccessibilityView(AccessibilityView.Raw)
                        : TextBox(ValueOf(field.Key), value => SetValue(field.Key, value)).AccessibilityView(AccessibilityView.Raw),
                    When(submitted && !IsValid(field.Key, ValueOf(field.Key)), () => Body(ErrorOf(field.Key)).Foreground("#b00020").AccessibilityView(AccessibilityView.Raw)))
                .Margin(0, 0, 0, 24)
                .WithKey(field.Key.ToString())),
            Body(filler).TextWrapping().Margin(0, 0, 0, 120).AccessibilityView(AccessibilityView.Raw),
            Button("CREATE ACCOUNT", () =>
            {
                setSubmitted(true);
                if (Fields.All(field => IsValid(field.Key, ValueOf(field.Key))))
                {
                    setDone(true);
                }
            }).HAlign(HorizontalAlignment.Stretch).AccessibilityView(AccessibilityView.Raw),
            Body(trailer).TextWrapping().Margin(0, 120, 0, 0).AccessibilityView(AccessibilityView.Raw),
        ]).Padding(24));
    }
}
