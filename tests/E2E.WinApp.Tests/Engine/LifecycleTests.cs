// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Engine;

/// <summary>.NET-only: upstream has no Windows engine. The session's launch, relaunch, attach, and close, scripted on the driver seam.</summary>
public sealed class LifecycleTests
{
    [Fact]
    public async Task Declares_the_capabilities_of_a_pinned_app_engine_and_no_location()
    {
        await using var harness = new Harness();
        Assert.Equal("windows", harness.Engine.Platform);
        Assert.Equal(WinAppEngine.EngineVersion, harness.Engine.Version);
        Assert.True(harness.Engine.Capabilities.HasFlag(EngineCapabilities.PinnedApp));
        Assert.True(harness.Engine.Capabilities.HasFlag(EngineCapabilities.Screenshot));
        Assert.False(harness.Engine.Capabilities.HasFlag(EngineCapabilities.Location));
        Assert.False(harness.Engine.Capabilities.HasFlag(EngineCapabilities.History));
        Assert.False(harness.Engine.Capabilities.HasFlag(EngineCapabilities.Video));
        var session = await harness.StartAsync();
        Assert.False(((IEngineSession)session).CanLocate);
    }

    [Fact]
    public async Task Refuses_an_observation_before_the_app_is_open()
    {
        await using var harness = new Harness();
        var session = await harness.StartAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        Assert.Contains("call app.open()", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_launches_the_executable_of_the_target_with_its_arguments_and_brings_it_forward()
    {
        await using var harness = new Harness(Harness.Options(launchArguments: ["--benchmark"]));
        var session = await harness.StartAsync();
        await session.OpenAsync("", CancellationToken.None);
        Assert.Equal("launch Desk.exe --benchmark", harness.Driver.Calls[0]);
        Assert.Contains($"foreground {FakeWindowDriver.MainHandle}", harness.Driver.Calls);
        Assert.Single(harness.Settles);
        Assert.NotNull(session.App);
        Assert.True(session.App!.Owned);
        Assert.Equal(Path.GetFullPath("bin/Desk.exe", Harness.Root), session.AppPath);
        Assert.Contains(harness.AppLog, entry => entry.Text == "Desk.exe started" && entry.Source == AppLogSource.Console);
    }

    [Fact]
    public async Task Registers_the_session_while_it_runs_and_tells_the_trace_the_platform()
    {
        await using var harness = new Harness();
        var session = await harness.StartAsync();
        Assert.Same(session, Assert.Single(harness.Engine.ActiveSessions));
        var facts = Assert.Single(harness.Environment);
        Assert.Equal("windows", facts["platform"]);
        Assert.Equal(WinAppEngine.LibraryVersion, facts["uiAutomation"]);
        await session.DisposeAsync();
        Assert.Empty(harness.Engine.ActiveSessions);
    }

    [Fact]
    public async Task A_relaunch_ends_the_app_it_started_before_launching_again()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var first = session.App!.ProcessId;
        await session.RestartAsync(CancellationToken.None);
        Assert.Equal([first], harness.Driver.Closed);
        Assert.NotEqual(first, session.App!.ProcessId);
        Assert.Equal(2, harness.Driver.Calls.Count(call => call.StartsWith("launch", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Clear_state_is_a_relaunch()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var first = session.App!.ProcessId;
        await session.ClearStateAsync(CancellationToken.None);
        Assert.Equal([first], harness.Driver.Closed);
    }

    [Fact]
    public async Task An_attach_target_attaches_to_the_running_app_and_never_ends_it()
    {
        await using var harness = new Harness(Harness.Options(appPath: null, bundleId: "Notepad"));
        harness.Driver.Attachable["Notepad"] = new LaunchedApp(777, FakeWindowDriver.MainHandle, "Notepad", Owned: false);
        harness.Driver.Script(777, FakeWindowDriver.MainHandle, "Untitled - Notepad", RawElements.Window("Untitled - Notepad"));
        var session = await harness.StartAsync();
        await session.OpenAsync("", CancellationToken.None);
        Assert.Equal("attach Notepad", harness.Driver.Calls[0]);
        Assert.False(session.App!.Owned);
        await session.RestartAsync(CancellationToken.None);
        await session.DisposeAsync();
        Assert.Empty(harness.Driver.Closed);
        Assert.True(harness.Driver.Disposed);
    }

    [Fact]
    public async Task An_attach_that_finds_no_app_says_so_as_invalid_state()
    {
        await using var harness = new Harness(Harness.Options(appPath: null, bundleId: "Ghost"));
        var session = await harness.StartAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.OpenAsync("", CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        Assert.Contains("call app.open()", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_launch_that_fails_is_the_environment_being_unavailable()
    {
        await using var harness = new Harness();
        harness.Driver.Launch = LaunchBehavior.Fail;
        var session = await harness.StartAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.OpenAsync("", CancellationToken.None));
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", error.Code);
        Assert.Contains("exited with code 1", error.Message, StringComparison.Ordinal);
        Assert.Null(session.App);
    }

    [Fact]
    public async Task A_cancelled_launch_surfaces_as_cancelled()
    {
        await using var harness = new Harness();
        harness.Driver.Launch = LaunchBehavior.Hang;
        var session = await harness.StartAsync();
        using var cancel = new CancellationTokenSource();
        var opening = session.OpenAsync("", cancel.Token);
        await cancel.CancelAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => opening);
        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
    }

    [Fact]
    public async Task Open_refuses_a_url_since_a_desktop_app_has_none()
    {
        await using var harness = new Harness();
        var session = await harness.StartAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.OpenAsync("https://example.test/", CancellationToken.None));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Empty(harness.Driver.Calls);
    }

    [Fact]
    public async Task A_target_without_an_app_cannot_open()
    {
        var engine = new WinAppEngine(Harness.Options(appPath: null, bundleId: null), () => new FakeWindowDriver());
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => engine.StartAsync(new EngineStartOptions(), CancellationToken.None));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("needs app.appPath or app.bundleId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_base_url_on_a_windows_target_is_a_config_error()
    {
        var engine = new WinAppEngine(Harness.Options(), () => new FakeWindowDriver());
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => engine.StartAsync(new EngineStartOptions { BaseUrl = "https://x.test" }, CancellationToken.None));
        Assert.Contains("app.url", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_ends_the_app_it_started_and_the_driver()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var pid = session.App!.ProcessId;
        await session.DisposeAsync();
        Assert.Equal([pid], harness.Driver.Closed);
        Assert.True(harness.Driver.Disposed);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
    }

    [Fact]
    public async Task Dispose_disposes_the_driver_even_when_the_close_fails()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Faults["close"] = new WindowDriverException(DriverFault.Other, "boom");
        await session.DisposeAsync();
        Assert.True(harness.Driver.Disposed);
    }

    [Fact]
    public async Task Close_app_forgets_the_app_so_the_next_observation_asks_for_an_open()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var pid = session.App!.ProcessId;
        await session.CloseAppAsync();
        Assert.Equal([pid], harness.Driver.Closed);
        Assert.Null(session.App);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
        await session.OpenAsync("", CancellationToken.None);
        Assert.NotNull(session.App);
    }

    [Fact]
    public async Task Open_app_of_another_name_attaches_and_brings_it_forward_without_adopting_it()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var own = session.App;
        harness.Driver.Attachable["Calculator"] = new LaunchedApp(900, 0x2000, "Calculator", Owned: false);
        await session.OpenAppAsync("Calculator");
        Assert.Contains("attach Calculator", harness.Driver.Calls);
        Assert.Contains("foreground 8192", harness.Driver.Calls);
        Assert.Same(own, session.App);
    }

    [Fact]
    public async Task Open_app_of_the_pinned_app_without_relaunch_only_brings_it_forward()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Calls.Clear();
        await session.OpenAppAsync("Desk");
        Assert.DoesNotContain(harness.Driver.Calls, call => call.StartsWith("launch", StringComparison.Ordinal));
        Assert.Contains($"foreground {FakeWindowDriver.MainHandle}", harness.Driver.Calls);
        Assert.Empty(harness.Driver.Closed);
    }

    [Fact]
    public void Refuses_options_that_cannot_work()
    {
        Assert.Throws<ConfigurationException>(() => new WinAppEngine(Harness.Options() with { SettleMilliseconds = -1 }, () => new FakeWindowDriver()));
        Assert.Throws<ConfigurationException>(() => new WinAppEngine(Harness.Options() with { LaunchTimeout = TimeSpan.Zero }, () => new FakeWindowDriver()));
    }
}
