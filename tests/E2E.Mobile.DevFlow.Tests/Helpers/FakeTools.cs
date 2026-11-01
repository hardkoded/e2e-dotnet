// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.DevFlow.Tests;

/// <summary>
/// The device tools as a script: it records every command line and answers a plausible machine (one booted iPhone, one
/// emulator, every command succeeding), else what a test scripts for a command prefix.
/// </summary>
internal sealed class FakeTools : IDeviceTools
{
    public const string Udid = "0A1B2C3D-0000-4000-8000-000000000001";

    public const string Serial = "emulator-5554";

    private readonly List<(string Prefix, Func<IReadOnlyList<string>, ToolResult> Respond)> _scripted = [];

    /// <summary>Every command run, as one line: <c>xcrun simctl launch &lt;udid&gt; com.example</c>.</summary>
    public List<string> Commands { get; } = [];

    /// <summary>Answers every command whose line starts with <paramref name="prefix"/> from now on.</summary>
    public void Script(string prefix, Func<IReadOnlyList<string>, ToolResult> respond) => _scripted.Insert(0, (prefix, respond));

    public void Script(string prefix, ToolResult result) => Script(prefix, _ => result);

    public void Fail(string prefix, string stderr, int exitCode = 1) => Script(prefix, new ToolResult(exitCode, "", stderr));

    public static ToolResult Ok(string output = "") => new(0, output, "");

    public static string BootedSimulators(params (string Udid, string Name)[] devices)
    {
        var entries = devices
            .Select(device => $$"""{ "udid": "{{device.Udid}}", "name": "{{device.Name}}", "state": "Booted", "isAvailable": true }""")
            .Append("""{ "udid": "SHUTDOWN-0000-4000-8000-000000000002", "name": "iPad Pro", "state": "Shutdown", "isAvailable": true }""");
        return $$"""
        {
          "devices": {
            "com.apple.CoreSimulator.SimRuntime.iOS-26-0": [ {{string.Join(", ", entries)}} ],
            "com.apple.CoreSimulator.SimRuntime.watchOS-26-0": []
          }
        }
        """;
    }

    public static string AdbDevices(params (string Serial, string Model)[] devices) =>
        "List of devices attached\n" + string.Join("", devices.Select(device => $"{device.Serial}          device product:sdk_gphone64 model:{device.Model} device:emu64a transport_id:1\n")) + "offline-1 offline\n";

    public Task<ToolResult> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var line = string.Join(' ', [program, .. arguments]);
        Commands.Add(line);
        foreach (var (prefix, respond) in _scripted)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return Task.FromResult(respond(arguments));
            }
        }

        return Task.FromResult(line switch
        {
            "xcrun simctl list devices booted -j" => Ok(BootedSimulators((Udid, "iPhone 17"))),
            "adb devices -l" => Ok(AdbDevices((Serial, "sdk_gphone64_arm64"))),
            var launch when launch.StartsWith($"adb -s {Serial} shell cmd package resolve-activity", StringComparison.Ordinal) => Ok("priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true\ndev.hardkoded.e2e.mobileapp/crc64.MainActivity\n"),
            _ => Ok(),
        });
    }
}
