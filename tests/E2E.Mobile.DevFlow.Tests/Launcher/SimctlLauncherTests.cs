// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.DevFlow.Tests.Launcher;

public sealed class SimctlLauncherTests
{
    private const string App = FakeAgent.PackageId;

    [Fact]
    public async Task Lists_the_booted_simulators_only()
    {
        var tools = new FakeTools();

        var booted = await SimctlLauncher.BootedDevicesAsync(tools, CancellationToken.None);

        Assert.Equal(["xcrun simctl list devices booted -j"], tools.Commands);
        Assert.Equal([new ListedDevice(FakeTools.Udid, "iPhone 17")], booted);
    }

    [Fact]
    public async Task Picks_the_one_booted_simulator_or_the_one_named()
    {
        var tools = new FakeTools();

        var booted = await AppLaunchers.ForAsync(MobilePlatform.Ios, null, tools, CancellationToken.None);
        var byName = await AppLaunchers.ForAsync(MobilePlatform.Ios, "iphone 17", tools, CancellationToken.None);
        var byId = await AppLaunchers.ForAsync(MobilePlatform.Ios, "OTHER-UDID", tools, CancellationToken.None);

        Assert.Equal(FakeTools.Udid, booted.DeviceId);
        Assert.Equal(FakeTools.Udid, byName.DeviceId);
        Assert.Equal("OTHER-UDID", byId.DeviceId);
        Assert.Equal(MobilePlatform.Ios, booted.Platform);
    }

    [Fact]
    public async Task No_booted_simulator_or_several_is_a_choice_the_options_make()
    {
        var none = new FakeTools();
        none.Script("xcrun simctl list", FakeTools.Ok(FakeTools.BootedSimulators()));
        var two = new FakeTools();
        two.Script("xcrun simctl list", FakeTools.Ok(FakeTools.BootedSimulators((FakeTools.Udid, "iPhone 17"), ("B", "iPhone 17 Pro"))));

        var missing = await Assert.ThrowsAsync<AgentDeviceError>(() => AppLaunchers.ForAsync(MobilePlatform.Ios, null, none, CancellationToken.None));
        var ambiguous = await Assert.ThrowsAsync<AgentDeviceError>(() => AppLaunchers.ForAsync(MobilePlatform.Ios, null, two, CancellationToken.None));

        Assert.Contains("no booted ios device", missing.Message, StringComparison.Ordinal);
        Assert.Contains("2 ios devices are booted", ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains("iPhone 17 Pro B", ambiguous.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_name_two_booted_simulators_share_needs_the_id()
    {
        var tools = new FakeTools();
        tools.Script("xcrun simctl list", FakeTools.Ok(FakeTools.BootedSimulators((FakeTools.Udid, "iPhone 17"), ("B", "iPhone 17"))));

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => AppLaunchers.ForAsync(MobilePlatform.Ios, "iPhone 17", tools, CancellationToken.None));
        var picked = await AppLaunchers.ForAsync(MobilePlatform.Ios, "B", tools, CancellationToken.None);

        Assert.Contains("2 booted devices are named iPhone 17", error.Message, StringComparison.Ordinal);
        Assert.Equal("B", picked.DeviceId);
    }

    [Fact]
    public async Task Installs_launches_with_arguments_and_terminates()
    {
        var tools = new FakeTools();
        var launcher = new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17");

        await launcher.InstallAsync("/build/MobileApp.app", App, reinstall: false, CancellationToken.None);
        await launcher.LaunchAsync(App, ["-reset", "1"], CancellationToken.None);
        await launcher.TerminateAsync(App, CancellationToken.None);

        Assert.Equal(
            [
                $"xcrun simctl install {FakeTools.Udid} /build/MobileApp.app",
                $"xcrun simctl launch {FakeTools.Udid} {App} -reset 1",
                $"xcrun simctl terminate {FakeTools.Udid} {App}",
            ],
            tools.Commands);
    }

    [Fact]
    public async Task A_reinstall_removes_the_app_first_and_tolerates_one_that_is_not_there()
    {
        var tools = new FakeTools();
        tools.Fail("xcrun simctl uninstall", "An error was encountered processing the command (domain=NSPOSIXErrorDomain, code=2): No such file or directory");
        var launcher = new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17");

        await launcher.InstallAsync("/build/MobileApp.app", App, reinstall: true, CancellationToken.None);

        Assert.Equal([$"xcrun simctl uninstall {FakeTools.Udid} {App}", $"xcrun simctl install {FakeTools.Udid} /build/MobileApp.app"], tools.Commands);
    }

    [Fact]
    public async Task Terminating_an_app_that_is_not_running_is_not_a_failure()
    {
        var tools = new FakeTools();
        tools.Fail("xcrun simctl terminate", $"An error was encountered processing the command (domain=NSPOSIXErrorDomain, code=3): found nothing to terminate", 3);
        var launcher = new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17");

        await launcher.TerminateAsync(App, CancellationToken.None);
    }

    [Fact]
    public async Task A_tool_that_fails_reports_its_stderr_where_the_engine_reads_it()
    {
        var tools = new FakeTools();
        tools.Fail("xcrun simctl launch", "An error was encountered processing the command (domain=FBSOpenApplicationServiceErrorDomain, code=4):\nThe request to open \"dev.hardkoded.e2e.mobileapp\" failed.", 4);
        var launcher = new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17");

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => launcher.LaunchAsync(App, [], CancellationToken.None));

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Equal(true, error.Details!["processExitError"]);
        Assert.Contains("failed with exit code 4", error.Message, StringComparison.Ordinal);
        Assert.Contains("The request to open", DeviceErrors.Message(error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_simulator_shares_the_hosts_loopback_so_the_forwards_do_nothing_and_home_is_unsupported()
    {
        var tools = new FakeTools();
        var launcher = new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17");

        await launcher.ForwardAsync(10223, CancellationToken.None);
        await launcher.ReverseAsync(19223, CancellationToken.None);
        await launcher.RemoveForwardAsync(10223, CancellationToken.None);
        var home = await Assert.ThrowsAsync<AgentDeviceError>(() => launcher.HomeAsync(CancellationToken.None));

        Assert.Empty(tools.Commands);
        Assert.Equal("UNSUPPORTED_OPERATION", home.Code);
    }
}
