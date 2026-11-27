// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using E2E.Desktop;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: quitting')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceQuittingTests : SurfaceTestBase
{
    [Fact]
    public async Task Quits_the_process_it_launched_at_the_end_of_the_attempt_and_only_that_one()
    {
        var theirs = StandIn();
        var (surface, fake, _) = await Launched();
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.True(await Gone(fake.Pid));
        Assert.False(await Gone(theirs, 300));
        Assert.Equal([(object)fake.Pid], fake.Of("forgetProcess").Select(call => call.Args[0]!).ToArray());
    }

    [Fact]
    public async Task Closes_the_app_on_desktop_closeApp_and_opens_it_fresh_again()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        var first = fake.Pid;
        await surface.CloseAppAsync(Operation(timeoutMs: 5_000));
        Assert.True(await Gone(first));
        await surface.RestartAsync(Operation());
        Assert.NotEqual(first, fake.Pid);
    }

    [Fact]
    public async Task Kills_nothing_on_a_desktop_closeApp_cancelled_before_the_kill_and_quits_the_app_on_the_next_try()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Rejects(surface.CloseAppAsync(Operation(timeoutMs: 5_000, signal: cancelled.Token)), "CANCELLED");
        Assert.False(await Gone(fake.Pid, 300));
        await surface.CloseAppAsync(Operation(timeoutMs: 5_000));
        Assert.True(await Gone(fake.Pid));
    }

    [Fact]
    public async Task Presses_a_key_into_the_apps_window_through_desktop_press_for_an_app_with_no_control_that_takes_focus()
    {
        var (surface, fake, _) = await Launched();
        await new Desktop(surface, () => null, TimeSpan.FromSeconds(10)).PressAsync("ControlOrMeta+2");
        var press = Last<PressRequest>(fake, "press")!;
        Assert.Equal(("w-mine", "cmd+2", false), (press.WindowId, press.Combo, press.Physical));
    }

    [Fact]
    public async Task Kills_nothing_when_the_app_already_quit_on_its_own()
    {
        var (surface, fake, _) = await Launched([], new LaunchOptions { Observe = false });
        Process.GetProcessById(fake.Pid).Kill();
        Assert.True(await Gone(fake.Pid));
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
    }

    [Fact]
    public async Task Removes_the_launch_record_once_the_app_quit_and_the_state_directory_at_dispose()
    {
        var (surface, _, _) = await Launched([], new LaunchOptions { Observe = false });
        var home = surface.Home!;
        var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(home, "app.json"))).RootElement;
        Assert.Equal(JsonValueKind.String, record.GetProperty("token").ValueKind);
        Assert.Equal(JsonValueKind.Number, record.GetProperty("pid").ValueKind);
        Assert.Equal(JsonValueKind.Number, record.GetProperty("started").ValueKind);
        await surface.EndAttemptAsync(CancellationToken.None, 5_000);
        Assert.False(File.Exists(Path.Combine(home, "app.json")));
        await surface.DisposeAsync(CancellationToken.None, 5_000);
        Assert.False(Directory.Exists(home));
    }
}
