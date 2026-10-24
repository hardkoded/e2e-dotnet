// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Fixture;

/// <summary>.NET-only: upstream has no Windows engine. The window fixture over the driver seam.</summary>
public sealed class WindowFixtureTests
{
    private static RawElement WithDialog(params RawElement[] buttons) =>
        RawElements.Window("Settings", RawElements.Button("btn-save", "Save"), RawElements.Dialog("dlg", "Confirm action", buttons));

    [Fact]
    public async Task Accept_presses_the_primary_button_of_the_open_dialog()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(WithDialog(RawElements.Button("btn-cancel", "Cancel", "CloseButton"), RawElements.Button("btn-ok", "Confirm", "PrimaryButton")));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        harness.Settles.Clear();
        await session.Window.AlertAsync(AlertAction.Accept);
        Assert.Equal(["invoke btn-ok Invoke"], harness.Driver.Calls);
        Assert.Single(harness.Settles);
    }

    [Fact]
    public async Task Accept_falls_back_to_the_first_button_and_dismiss_to_the_close_or_secondary_one()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(WithDialog(RawElements.Button("btn-yes", "Yes"), RawElements.Button("btn-no", "No", "SecondaryButton")));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        await session.Window.AlertAsync(AlertAction.Accept);
        await session.Window.AlertAsync(AlertAction.Dismiss);
        Assert.Equal(["invoke btn-yes Invoke", "invoke btn-no Invoke"], harness.Driver.Calls);
    }

    [Fact]
    public async Task Accept_never_presses_a_cancel_or_no_button_it_cannot_tell_from_an_accept_one()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(WithDialog(RawElements.Button("btn-cancel", "Cancel"), RawElements.Button("btn-ok", "OK")));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        await session.Window.AlertAsync(AlertAction.Accept);
        Assert.Equal(["invoke btn-ok Invoke"], harness.Driver.Calls);

        await using var other = new Harness();
        var bare = await other.OpenAsync(WithDialog(RawElements.Button("btn-cancel", "Cancel"), RawElements.Button("btn-no", "No")));
        await bare.ObserveAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => bare.Window.AlertAsync(AlertAction.Accept));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
    }

    [Fact]
    public async Task Dismiss_sends_escape_when_the_dialog_has_no_close_button()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(WithDialog(RawElements.Button("btn-yes", "Yes")));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        await session.Window.AlertAsync(AlertAction.Dismiss);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} esc"], harness.Driver.Calls);
    }

    [Fact]
    public async Task Alert_without_a_dialog_is_invalid_state_and_a_dialog_without_buttons_cannot_be_accepted()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        await session.ObserveAsync(CancellationToken.None);
        var missing = await Assert.ThrowsAsync<EngineException>(() => session.Window.AlertAsync(AlertAction.Accept));
        Assert.Equal(EngineErrorCodes.InvalidState, missing.Code);

        await using var other = new Harness();
        var bare = await other.OpenAsync(WithDialog());
        await bare.ObserveAsync(CancellationToken.None);
        var empty = await Assert.ThrowsAsync<EngineException>(() => bare.Window.AlertAsync(AlertAction.Accept));
        Assert.Equal(EngineErrorCodes.NotActionable, empty.Code);
    }

    [Fact]
    public async Task A_dialog_the_newest_observation_no_longer_shows_is_not_open()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(WithDialog(RawElements.Button("btn-ok", "Confirm", "PrimaryButton")));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Trees[FakeWindowDriver.MainHandle] = RawElements.Window("Settings", RawElements.Button("btn-save", "Save"));
        await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.Window.AlertAsync(AlertAction.Dismiss));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        Assert.Empty(harness.Driver.Calls);
    }

    [Fact]
    public async Task A_button_outside_the_dialog_is_never_the_dialog_s_button()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(RawElements.Window("Settings", RawElements.Dialog("dlg", "Confirm"), RawElements.Button("btn-save", "Save", "PrimaryButton")));
        await session.ObserveAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.Window.AlertAsync(AlertAction.Accept));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
    }

    [Fact]
    public async Task Resize_maximize_restore_and_minimize_act_on_the_main_window()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Calls.Clear();
        await session.Window.ResizeAsync(640, 480);
        await session.Window.MaximizeAsync();
        await session.Window.RestoreAsync();
        await session.Window.MinimizeAsync();
        var main = FakeWindowDriver.MainHandle;
        Assert.Equal([$"resize {main} 640x480", $"show {main} Maximized", $"show {main} Normal", $"show {main} Minimized"], harness.Driver.Calls);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.Window.ResizeAsync(0, 10));
    }

    [Fact]
    public async Task Foreground_app_and_the_window_list_read_the_app_s_windows()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var app = await session.Window.ForegroundAppAsync();
        Assert.Equal(new ForegroundApp("Desk", "Settings"), app);
        var windows = await session.Window.ListWindowsAsync();
        Assert.Equal("Settings", Assert.Single(windows).Title);
    }

    [Fact]
    public async Task Open_app_without_a_name_relaunches_the_app_under_test()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var first = session.App!.ProcessId;
        await session.Window.OpenAppAsync(options: new OpenAppOptions { Relaunch = true });
        Assert.Equal([first], harness.Driver.Closed);
        await session.Window.CloseAppAsync();
        Assert.Null(session.App);
    }
}
