// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/roles.rs</c>.</summary>
public sealed class RolesTests
{
    [Fact]
    public void Reads_an_appkit_tab_as_a_tab_not_a_radio_button()
    {
        Assert.Equal("tab", Roles.Role("AXRadioButton", "AXTabButton"));
        Assert.Equal("radiobutton", Roles.Role("AXRadioButton", null));
    }

    [Fact]
    public void Reads_the_actions_of_a_label_an_image_and_a_heading()
    {
        // Any of them can be a button of its own, which needs a ref to be tapped.
        foreach (var role in new[] { "statictext", "image", "heading" })
        {
            Assert.False(Roles.Inert(role), role);
        }

        Assert.True(Roles.Inert("separator"));
    }
}
