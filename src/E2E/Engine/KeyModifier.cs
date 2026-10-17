// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// A key held for a click, as a Shift-click extends a selection. Upstream's <c>KeyModifier</c> (<c>engine/contract.ts</c>).
/// <see cref="ControlOrMeta"/> is Control, or Meta on macOS.
/// </summary>
public enum KeyModifier
{
    Shift,
    Control,
    Alt,
    Meta,
    ControlOrMeta,
}

/// <summary>The names of <see cref="KeyModifier"/>, which a recorded action and an MCP argument spell. Upstream's <c>KEY_MODIFIERS</c> and <c>isKeyModifier</c>.</summary>
public static class KeyModifiers
{
    /// <summary>The names in declaration order: <c>Shift</c>, <c>Control</c>, <c>Alt</c>, <c>Meta</c>, <c>ControlOrMeta</c>.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Enum.GetNames<KeyModifier>()];

    /// <summary>Reads a name exactly as spelled: no other case, no number.</summary>
    public static bool TryParse(string? name, out KeyModifier modifier)
    {
        var index = name is null ? -1 : Array.IndexOf(Enum.GetNames<KeyModifier>(), name);
        modifier = index < 0 ? default : Enum.GetValues<KeyModifier>()[index];
        return index >= 0;
    }
}
