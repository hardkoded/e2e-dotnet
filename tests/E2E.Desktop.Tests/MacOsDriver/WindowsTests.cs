// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/windows.rs</c>.</summary>
public sealed class WindowsTests
{
    [Fact]
    public void Counts_an_entry_whose_role_the_app_did_not_answer_as_a_window()
    {
        Assert.True(Windows.MayBeWindow(AxResult<string?>.Ok("AXWindow")));
        Assert.False(Windows.MayBeWindow(AxResult<string?>.Ok("AXApplication")), "an app's own element in its windows");
        Assert.False(Windows.MayBeWindow(AxResult<string?>.Ok(null)));
        Assert.True(Windows.MayBeWindow(AxResult<string?>.Err(AxError.CannotComplete)), "a stall may hide a window");
        Assert.False(Windows.MayBeWindow(AxResult<string?>.Err(AxError.Failure)), "a role the element does not have");
    }

    [Fact]
    public void Refuses_a_window_list_that_left_out_every_window_it_could_not_name()
    {
        Windows.Complete(0, 0); // an app with no window
        Windows.Complete(2, 1); // another window was listed
        var stall = Assert.Throws<Failure>(() => Windows.Complete(0, 1));
        Assert.Equal(Codes.AppUnresponsive, stall.Code);
        Assert.Equal(true, stall.Details!["retryable"]);
    }

    [Fact]
    public void Tells_siri_from_an_app_dialog()
    {
        Assert.True(Windows.SystemDialog(["siri"]));
        Assert.False(Windows.SystemDialog(["confirm-delete", "ok"]));
        Assert.False(Windows.SystemDialog([]));
    }
}
