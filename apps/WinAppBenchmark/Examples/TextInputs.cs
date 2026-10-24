// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>Secure, numeric, and multiline inputs gating a submit button.</summary>
public sealed class TextInputs : Component
{
    private const int MinNotesLength = 10;

    public override Element Render()
    {
        var (username, setUsername) = UseState("");
        var (pin, setPin) = UseState("");
        var (notes, setNotes) = UseState("");
        var (submitted, setSubmitted) = UseState(false);
        var isValid = username.Length >= 3 && pin.Length == 4 && notes.Length >= MinNotesLength;

        if (submitted)
        {
            return VStack(Success("Form submitted")).Padding(24);
        }

        return ScrollView(VStack(8,
            Caption("Username (min 3 chars)"),
            TextBox(username, setUsername).AutomationName("Username").AutomationId("username-input"),
            Caption("PIN (exactly 4 digits, secure)"),
            PasswordBox(pin, text => setPin(new string([.. text.Where(char.IsDigit)]))).MaxLength(4).AutomationName("PIN").AutomationId("pin-input"),
            Caption($"Notes (multiline, min {MinNotesLength} chars) — {notes.Length}/{MinNotesLength}"),
            TextBox(notes, setNotes).AcceptsReturn().TextWrapping().MinHeight(96).AutomationName("Notes").AutomationId("notes-input"),
            Button("Submit", () => setSubmitted(true)).IsEnabled(isValid).AutomationId("submit-button"))
            .Padding(24));
    }
}
