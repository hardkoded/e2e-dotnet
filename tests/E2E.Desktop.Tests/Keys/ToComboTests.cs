// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;

namespace E2E.Desktop.Tests.Keys;

/// <summary>Ported from <c>describe('toCombo')</c> in upstream's <c>packages/desktop/tests/unit/keys.test.ts</c>.</summary>
public sealed class ToComboTests
{
    [Fact]
    public void Names_the_contract_keys_the_way_the_driver_takes_them()
    {
        Assert.Equal("return", DesktopKeys.ToCombo("Enter"));
        Assert.Equal("escape", DesktopKeys.ToCombo("Escape"));
        Assert.Equal("backspace", DesktopKeys.ToCombo("Backspace"));
        Assert.Equal("forwarddelete", DesktopKeys.ToCombo("Delete"));
        Assert.Equal("down", DesktopKeys.ToCombo("ArrowDown"));
        Assert.Equal("pageup", DesktopKeys.ToCombo("PageUp"));
        Assert.Equal("f5", DesktopKeys.ToCombo("F5"));
        Assert.Equal("space", DesktopKeys.ToCombo("Space"));
        Assert.Equal("space", DesktopKeys.ToCombo(" "));
    }

    [Fact]
    public void Maps_modifiers_ControlOrMeta_to_Command()
    {
        Assert.Equal("shift+tab", DesktopKeys.ToCombo("Shift+Tab"));
        Assert.Equal("cmd+a", DesktopKeys.ToCombo("ControlOrMeta+a"));
        Assert.Equal("cmd+alt+tab", DesktopKeys.ToCombo("Meta+Alt+Tab"));
        Assert.Equal("ctrl+c", DesktopKeys.ToCombo("Control+c"));
    }

    [Fact]
    public void Keeps_a_capital_letter_capital_with_Shift_beside_other_modifiers()
    {
        Assert.Equal("A", DesktopKeys.ToCombo("A"));
        Assert.Equal("ctrl+shift+A", DesktopKeys.ToCombo("Control+A"));
    }

    [Fact]
    public void Refuses_what_the_driver_cannot_press()
    {
        var insert = Assert.Throws<EngineException>(() => DesktopKeys.ToCombo("Insert"));
        Assert.Equal("UNSUPPORTED_CAPABILITY", insert.Code);
        Assert.Contains("the desktop engine cannot press \"Insert\"", insert.Message);
        Assert.Equal("UNSUPPORTED_CAPABILITY", Assert.Throws<EngineException>(() => DesktopKeys.ToCombo("+")).Code);
        Assert.Equal(
            "UNSUPPORTED_CAPABILITY",
            Assert.Throws<EngineException>(() => DesktopKeys.ToCombo("Hyper+a")).Code
        );
        // No key of their own in the driver's macOS key map.
        foreach (var key in new[] { "!", "é", "?", "ControlOrMeta+é" })
        {
            Assert.Equal("UNSUPPORTED_CAPABILITY", Assert.Throws<EngineException>(() => DesktopKeys.ToCombo(key)).Code);
        }
    }

    [Fact]
    public void Refuses_the_shortcuts_that_quit_log_out_force_quit_or_lock_with_every_modifier_added()
    {
        foreach (
            var key in new[]
            {
                "Meta+q",
                "ControlOrMeta+Q",
                "Meta+Shift+q",
                "Control+Meta+q",
                "Meta+Alt+Escape",
                "Meta+Shift+Backspace",
                "Meta+Alt+Shift+Escape",
            }
        )
        {
            var refused = Assert.Throws<EngineException>(() => DesktopKeys.ToCombo(key));
            Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);
            Assert.Matches(@"the desktop engine refuses to press.*desktop\.closeApp\(\)", refused.Message);
        }

        Assert.Equal("ctrl+q", DesktopKeys.ToCombo("Control+q"));
        Assert.Equal("cmd+escape", DesktopKeys.ToCombo("Meta+Escape"));
        Assert.Equal("cmd+backspace", DesktopKeys.ToCombo("Meta+Backspace"));
    }

    [Fact]
    public void Presses_the_punctuation_the_driver_has_keys_for()
    {
        foreach (var key in new[] { ",", ".", "/", ";", "'", "[", "]", "\\", "-", "=", "`" })
        {
            Assert.Equal(key, DesktopKeys.ToCombo(key));
        }

        Assert.Equal("cmd+,", DesktopKeys.ToCombo("ControlOrMeta+,"));
        Assert.Equal("7", DesktopKeys.ToCombo("7"));
    }
}
