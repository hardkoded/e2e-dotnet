// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Mobile.DevFlow;

/// <summary>A simulator or emulator the tools list.</summary>
internal sealed record ListedDevice(string Id, string Name);

/// <summary>
/// The device-side verbs the agent cannot do from inside the app: put a build on the device, launch, terminate, and on
/// Android the port forwards between the host and the emulator. One launcher drives one device. The agent-device
/// daemon owns these for the other providers; here the simulator's <c>xcrun simctl</c> and Android's <c>adb</c> do.
/// </summary>
internal interface IAppLauncher
{
    MobilePlatform Platform { get; }

    /// <summary>The simulator UDID or the adb serial.</summary>
    string DeviceId { get; }

    /// <summary>Puts a build on the device. A reinstall removes <paramref name="app"/> first, so it starts with no data.</summary>
    Task InstallAsync(string appPath, string? app, bool reinstall, CancellationToken cancellationToken);

    /// <summary>Starts the app, with its launch arguments. An app that is running is brought forward on iOS and started again on Android.</summary>
    Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    /// <summary>Ends the app's process. An app that is not running is not a failure.</summary>
    Task TerminateAsync(string app, CancellationToken cancellationToken);

    /// <summary>Sends the device to its home screen.</summary>
    Task HomeAsync(CancellationToken cancellationToken);

    /// <summary>Lets the host reach the agent's port on the device. A no-op where the device shares the host's loopback.</summary>
    Task ForwardAsync(int port, CancellationToken cancellationToken);

    /// <summary>Lets the agent on the device reach the host's broker port. A no-op where the device shares the host's loopback.</summary>
    Task ReverseAsync(int port, CancellationToken cancellationToken);

    /// <summary>Undoes <see cref="ForwardAsync"/>.</summary>
    Task RemoveForwardAsync(int port, CancellationToken cancellationToken);

    /// <summary>Undoes <see cref="ReverseAsync"/>.</summary>
    Task RemoveReverseAsync(int port, CancellationToken cancellationToken);
}

/// <summary>Picks the launcher of a platform, and the device it drives.</summary>
internal static class AppLaunchers
{
    /// <summary>
    /// The launcher for <paramref name="device"/>, or for the one booted device of the platform when none is named. No
    /// booted device, or several, is a choice the options must make.
    /// </summary>
    public static async Task<IAppLauncher> ForAsync(MobilePlatform platform, string? device, IDeviceTools tools, CancellationToken cancellationToken)
    {
        var booted = platform == MobilePlatform.Ios
            ? await SimctlLauncher.BootedDevicesAsync(tools, cancellationToken).ConfigureAwait(false)
            : await AdbLauncher.DevicesAsync(tools, cancellationToken).ConfigureAwait(false);
        var chosen = device is null ? OnlyBooted(platform, booted) : Named(device, booted);
        return platform == MobilePlatform.Ios ? new SimctlLauncher(tools, chosen.Id, chosen.Name) : new AdbLauncher(tools, chosen.Id, chosen.Name);
    }

    private static ListedDevice OnlyBooted(MobilePlatform platform, IReadOnlyList<ListedDevice> booted) => booted.Count switch
    {
        1 => booted[0],
        0 => throw new AgentDeviceError("COMMAND_FAILED", $"no booted {platform.WireName()} device; boot a simulator or emulator, or set DevFlowAgentsOptions.Device"),
        _ => throw new AgentDeviceError("COMMAND_FAILED", $"{booted.Count} {platform.WireName()} devices are booted ({string.Join(", ", booted.Select(entry => $"{entry.Name} {entry.Id}"))}); set DevFlowAgentsOptions.Device to one"),
    };

    /// <summary>The device an option names: by id, else by name when one booted device has it. A name several share is a choice the id makes.</summary>
    private static ListedDevice Named(string device, IReadOnlyList<ListedDevice> booted)
    {
        if (booted.FirstOrDefault(entry => entry.Id == device) is { } byId)
        {
            return byId;
        }

        var byName = booted.Where(entry => string.Equals(entry.Name, device, StringComparison.OrdinalIgnoreCase)).ToList();
        return byName.Count switch
        {
            1 => byName[0],
            0 => new ListedDevice(device, device),
            _ => throw new AgentDeviceError("COMMAND_FAILED", $"{byName.Count} booted devices are named {device} ({string.Join(", ", byName.Select(entry => entry.Id))}); set DevFlowAgentsOptions.Device to the id of one"),
        };
    }

    /// <summary>The launcher a worker rebuilds from the device id its binding names.</summary>
    public static IAppLauncher For(MobilePlatform platform, string deviceId, IDeviceTools tools) =>
        platform == MobilePlatform.Ios ? new SimctlLauncher(tools, deviceId, deviceId) : new AdbLauncher(tools, deviceId, deviceId);
}

/// <summary>The iOS simulator, through <c>xcrun simctl</c>. A simulator shares the Mac's loopback, so there is nothing to forward.</summary>
internal sealed partial class SimctlLauncher(IDeviceTools tools, string udid, string name) : IAppLauncher
{
    internal static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public MobilePlatform Platform => MobilePlatform.Ios;

    public string DeviceId => udid;

    public string Name => name;

    /// <summary>Every booted simulator, as <c>simctl list devices booted -j</c> lists them.</summary>
    public static async Task<IReadOnlyList<ListedDevice>> BootedDevicesAsync(IDeviceTools tools, CancellationToken cancellationToken)
    {
        var result = (await tools.RunAsync("xcrun", ["simctl", "list", "devices", "booted", "-j"], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect("listing the booted simulators");
        var devices = new List<ListedDevice>();
        try
        {
            if (JsonNode.Parse(result.Output)?["devices"] is JsonObject runtimes)
            {
                foreach (var (_, list) in runtimes)
                {
                    foreach (var device in (list as JsonArray)?.OfType<JsonObject>() ?? [])
                    {
                        if (Text(device["udid"]) is { } id && string.Equals(Text(device["state"]), "Booted", StringComparison.OrdinalIgnoreCase))
                        {
                            devices.Add(new ListedDevice(id, Text(device["name"]) ?? id));
                        }
                    }
                }
            }
        }
        catch (JsonException cause)
        {
            throw new AgentDeviceError("COMMAND_FAILED", $"simctl listed the devices in a form this launcher cannot read: {cause.Message}", inner: cause);
        }

        return devices;
    }

    public async Task InstallAsync(string appPath, string? app, bool reinstall, CancellationToken cancellationToken)
    {
        if (reinstall && app is not null)
        {
            // Not installed is not a failure: the point is a device without the app's data.
            await tools.RunAsync("xcrun", ["simctl", "uninstall", udid, app], CommandTimeout, cancellationToken).ConfigureAwait(false);
        }

        (await tools.RunAsync("xcrun", ["simctl", "install", udid, appPath], InstallTimeout, cancellationToken).ConfigureAwait(false)).Expect($"installing {appPath} on {name}");
    }

    public async Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        (await tools.RunAsync("xcrun", ["simctl", "launch", udid, app, .. arguments], LaunchTimeout, cancellationToken).ConfigureAwait(false)).Expect($"launching {app} on {name}");

    public async Task TerminateAsync(string app, CancellationToken cancellationToken)
    {
        var result = await tools.RunAsync("xcrun", ["simctl", "terminate", udid, app], CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && !NotRunning().IsMatch(result.Error))
        {
            throw ToolFailures.Failure(result, $"terminating {app} on {name}");
        }
    }

    public Task HomeAsync(CancellationToken cancellationToken) =>
        Task.FromException(DevFlowErrors.Unsupported("simctl has no home button; close the app or open another one instead"));

    public Task ForwardAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ReverseAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveForwardAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveReverseAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>What simctl says when the app has no process to end.</summary>
    [GeneratedRegex(@"found nothing to terminate|not running|No such process", RegexOptions.IgnoreCase)]
    private static partial Regex NotRunning();
}

/// <summary>An Android emulator or device, through <c>adb</c>. The emulator has its own loopback, so the agent's port is forwarded and the broker's reversed.</summary>
internal sealed class AdbLauncher(IDeviceTools tools, string serial, string name) : IAppLauncher
{
    internal static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public MobilePlatform Platform => MobilePlatform.Android;

    public string DeviceId => serial;

    public string Name => name;

    /// <summary>Every device adb lists as online.</summary>
    public static async Task<IReadOnlyList<ListedDevice>> DevicesAsync(IDeviceTools tools, CancellationToken cancellationToken)
    {
        var result = (await tools.RunAsync("adb", ["devices", "-l"], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect("listing the Android devices");
        var devices = new List<ListedDevice>();
        foreach (var raw in result.Output.Split('\n'))
        {
            var fields = raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 2 && fields[1] == "device")
            {
                var model = fields.Skip(2).FirstOrDefault(field => field.StartsWith("model:", StringComparison.Ordinal))?["model:".Length..];
                devices.Add(new ListedDevice(fields[0], model ?? fields[0]));
            }
        }

        return devices;
    }

    public async Task InstallAsync(string appPath, string? app, bool reinstall, CancellationToken cancellationToken)
    {
        if (reinstall && app is not null)
        {
            await Adb(["uninstall", app], CommandTimeout, cancellationToken).ConfigureAwait(false);
        }

        (await Adb(["install", "-r", appPath], InstallTimeout, cancellationToken).ConfigureAwait(false)).Expect($"installing {appPath} on {name}");
    }

    /// <summary>
    /// Starts the launcher activity. Without arguments the <c>monkey</c> shortcut needs no activity name; with them
    /// <c>am start</c> does, so the package's launcher activity is resolved first and the arguments follow it.
    /// </summary>
    public async Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            (await Adb(["shell", "monkey", "-p", app, "-c", "android.intent.category.LAUNCHER", "1"], LaunchTimeout, cancellationToken).ConfigureAwait(false)).Expect($"launching {app} on {name}");
            return;
        }

        var resolved = (await Adb(["shell", "cmd", "package", "resolve-activity", "--brief", app], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect($"finding the launcher activity of {app}");
        var component = resolved.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault(line => line.Contains('/'))
            ?? throw new AgentDeviceError("COMMAND_FAILED", $"{app} has no launcher activity on {name}, so it cannot be started with arguments");
        (await Adb(["shell", "am", "start", "-n", component, .. arguments], LaunchTimeout, cancellationToken).ConfigureAwait(false)).Expect($"launching {app} on {name}");
    }

    public async Task TerminateAsync(string app, CancellationToken cancellationToken) =>
        (await Adb(["shell", "am", "force-stop", app], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect($"terminating {app} on {name}");

    public async Task HomeAsync(CancellationToken cancellationToken) =>
        (await Adb(["shell", "input", "keyevent", "KEYCODE_HOME"], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect($"pressing home on {name}");

    public async Task ForwardAsync(int port, CancellationToken cancellationToken) =>
        (await Adb(["forward", $"tcp:{port}", $"tcp:{port}"], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect($"forwarding port {port} from {name}");

    public async Task ReverseAsync(int port, CancellationToken cancellationToken) =>
        (await Adb(["reverse", $"tcp:{port}", $"tcp:{port}"], CommandTimeout, cancellationToken).ConfigureAwait(false)).Expect($"reversing port {port} to {name}");

    public async Task RemoveForwardAsync(int port, CancellationToken cancellationToken)
    {
        // Best effort: a forward that is gone already, or a device that is, leaves nothing to do.
        await Adb(["forward", "--remove", $"tcp:{port}"], CommandTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveReverseAsync(int port, CancellationToken cancellationToken)
    {
        await Adb(["reverse", "--remove", $"tcp:{port}"], CommandTimeout, cancellationToken).ConfigureAwait(false);
    }

    private Task<ToolResult> Adb(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
        tools.RunAsync("adb", ["-s", serial, .. arguments], timeout, cancellationToken);
}
