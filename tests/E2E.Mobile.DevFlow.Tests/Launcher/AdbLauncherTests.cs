// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.DevFlow.Tests.Launcher;

public sealed class AdbLauncherTests
{
    private const string App = FakeAgent.PackageId;

    [Fact]
    public async Task Lists_the_online_devices_by_serial_and_model()
    {
        var tools = new FakeTools();

        var devices = await AdbLauncher.DevicesAsync(tools, CancellationToken.None);

        Assert.Equal(["adb devices -l"], tools.Commands);
        Assert.Equal([new ListedDevice(FakeTools.Serial, "sdk_gphone64_arm64")], devices);
    }

    [Fact]
    public async Task Picks_the_one_device_and_targets_every_command_at_its_serial()
    {
        var tools = new FakeTools();

        var launcher = await AppLaunchers.ForAsync(MobilePlatform.Android, null, tools, CancellationToken.None);
        tools.Commands.Clear();
        await launcher.InstallAsync("/build/app-Signed.apk", App, reinstall: true, CancellationToken.None);
        await launcher.LaunchAsync(App, [], CancellationToken.None);
        await launcher.TerminateAsync(App, CancellationToken.None);
        await launcher.HomeAsync(CancellationToken.None);

        Assert.Equal(FakeTools.Serial, launcher.DeviceId);
        Assert.Equal(
            [
                $"adb -s {FakeTools.Serial} uninstall {App}",
                $"adb -s {FakeTools.Serial} install -r /build/app-Signed.apk",
                $"adb -s {FakeTools.Serial} shell monkey -p {App} -c android.intent.category.LAUNCHER 1",
                $"adb -s {FakeTools.Serial} shell am force-stop {App}",
                $"adb -s {FakeTools.Serial} shell input keyevent KEYCODE_HOME",
            ],
            tools.Commands);
    }

    [Fact]
    public async Task A_launch_with_arguments_resolves_the_launcher_activity_and_starts_it()
    {
        var tools = new FakeTools();
        var launcher = new AdbLauncher(tools, FakeTools.Serial, "emulator");

        await launcher.LaunchAsync(App, ["--es", "mode", "test"], CancellationToken.None);

        Assert.Equal(
            [
                $"adb -s {FakeTools.Serial} shell cmd package resolve-activity --brief {App}",
                $"adb -s {FakeTools.Serial} shell am start -n {App}/crc64.MainActivity --es mode test",
            ],
            tools.Commands);
    }

    [Fact]
    public async Task An_app_without_a_launcher_activity_cannot_take_arguments()
    {
        var tools = new FakeTools();
        tools.Script($"adb -s {FakeTools.Serial} shell cmd package resolve-activity", FakeTools.Ok("No activity found\n"));
        var launcher = new AdbLauncher(tools, FakeTools.Serial, "emulator");

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => launcher.LaunchAsync(App, ["--es", "a", "b"], CancellationToken.None));

        Assert.Contains("no launcher activity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forwards_the_agents_port_reverses_the_brokers_and_removes_the_forward()
    {
        var tools = new FakeTools();
        tools.Fail("adb -s emulator-5554 forward --remove", "error: listener 'tcp:10223' not found");
        var launcher = new AdbLauncher(tools, FakeTools.Serial, "emulator");

        await launcher.ForwardAsync(10223, CancellationToken.None);
        await launcher.ReverseAsync(19223, CancellationToken.None);
        await launcher.RemoveForwardAsync(10223, CancellationToken.None);
        await launcher.RemoveReverseAsync(19223, CancellationToken.None);

        Assert.Equal(
            [
                $"adb -s {FakeTools.Serial} forward tcp:10223 tcp:10223",
                $"adb -s {FakeTools.Serial} reverse tcp:19223 tcp:19223",
                $"adb -s {FakeTools.Serial} forward --remove tcp:10223",
                $"adb -s {FakeTools.Serial} reverse --remove tcp:19223",
            ],
            tools.Commands);
    }

    [Fact]
    public async Task A_device_that_is_gone_fails_the_command_with_adbs_words()
    {
        var tools = new FakeTools();
        tools.Fail("adb -s emulator-5554 shell am force-stop", "error: device 'emulator-5554' not found");
        var launcher = new AdbLauncher(tools, FakeTools.Serial, "emulator");

        var error = await Assert.ThrowsAsync<AgentDeviceError>(() => launcher.TerminateAsync(App, CancellationToken.None));

        Assert.Contains("device 'emulator-5554' not found", DeviceErrors.Message(error), StringComparison.Ordinal);
    }
}
