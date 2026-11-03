// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Doctor;

/// <summary>
/// <c>describe('doctor')</c> of upstream <c>packages/mobile/tests/unit/doctor.test.ts</c>: <c>e2e doctor</c> for a mobile target over a
/// scripted client. agent-device's doctor per configured device, mapped to e2e checks, and the build check.
/// </summary>
public sealed class DoctorTests : IDisposable
{
    private static readonly JsonArray Machine =
    [
        Check("agent-device", "pass", "agent-device 0.21.35"),
        Check("toolchain", "pass", "Apple toolchain: Xcode 27.0."),
        Check("session", "info", "No active session."),
        Check("metro", "fail", "React Native dev server is not reachable."),
    ];

    private string? _dir;

    public void Dispose()
    {
        if (_dir is not null)
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static JsonObject Check(string id, string status, string summary, string? hint = null)
    {
        var check = new JsonObject { ["id"] = id, ["status"] = status, ["summary"] = summary };
        if (hint is not null)
        {
            check["hint"] = hint;
        }

        return check;
    }

    /// <summary>A harness whose doctor answers per selected device: found when its name is in <paramref name="known"/>.</summary>
    private static Harness DoctorHarness(Func<MobileOptions, MobileOptions>? configure = null, Func<MobileApp, MobileApp>? app = null, params string[] known)
    {
        var harness = Harness.Create(configure, true, app);
        harness.Fake.Respond(DeviceMethods.CommandDoctor, args =>
        {
            var device = args?["device"]?.GetValue<string>();
            var targetApp = args?["targetApp"]?.GetValue<string>();
            var found = device is null || known.Contains(device);
            var checks = new JsonArray([.. Machine.Select(item => item!.DeepClone())]);
            if (found)
            {
                checks.Add(Check("ios-runner-cache", "pass", "iOS runner artifact cached."));
                checks.Add(Check("device", "pass", "1 iOS device available; 1 booted."));
            }
            else
            {
                checks.Add(Check("device", "fail", "No iOS devices found.", "Start or create a simulator."));
            }

            if (targetApp is not null && found)
            {
                checks.Add(device is null
                    ? Check("target-app-device", "fail", "Target app check needs one booted device; 4 matched.")
                    : Check("target-app", "pass", $"Target app is launchable: {targetApp}"));
            }

            return new JsonObject { ["status"] = found ? "pass" : "fail", ["checks"] = checks };
        });
        return harness;
    }

    private static EngineDoctorInfo Info(Harness harness, string? projectRoot = null) => new()
    {
        TargetName = "ios-simulator",
        ProjectRoot = projectRoot ?? Harness.ProjectRoot,
        App = new EngineAppInfo { BundleId = harness.App.BundleId, AppPath = harness.App.AppPath },
        Env = new Dictionary<string, string?>(),
        Headed = false,
        Log = _ => { },
    };

    [Fact]
    public async Task Maps_agent_device_findings_dropping_evidence_Metro_and_an_app_check_with_no_single_booted_device()
    {
        var harness = DoctorHarness();
        Assert.Equal(
            [
                new EngineDoctorCheck("agent-device", EngineDoctorStatus.Pass, "agent-device 0.21.35"),
                new EngineDoctorCheck("toolchain", EngineDoctorStatus.Pass, "Apple toolchain: Xcode 27.0."),
                new EngineDoctorCheck("ios-runner-cache", EngineDoctorStatus.Pass, "iOS runner artifact cached."),
                new EngineDoctorCheck("device", EngineDoctorStatus.Pass, "1 iOS device available; 1 booted."),
            ],
            await harness.Engine.DoctorAsync(Info(harness), CancellationToken.None));
        JsonAssert.Args("""{"platform":"ios","targetApp":"Settings"}""", harness.Fake.LastArgs(DeviceMethods.CommandDoctor));
        Assert.Equal(["e2e-ios-simulator-0"], harness.Sessions);
    }

    [Fact]
    public async Task Checks_each_configured_device_machine_checks_once_and_names_the_device_it_found_missing()
    {
        var harness = DoctorHarness(options => options with { Devices = ["Gone", "iPhone 17"] }, known: "iPhone 17");
        Assert.Equal(
            [
                new EngineDoctorCheck("agent-device", EngineDoctorStatus.Pass, "agent-device 0.21.35"),
                new EngineDoctorCheck("toolchain", EngineDoctorStatus.Pass, "Apple toolchain: Xcode 27.0."),
                new EngineDoctorCheck("device", EngineDoctorStatus.Fail, "\"Gone\": No iOS devices found. Start or create a simulator."),
                new EngineDoctorCheck("ios-runner-cache", EngineDoctorStatus.Pass, "iOS runner artifact cached."),
                new EngineDoctorCheck("device", EngineDoctorStatus.Pass, "\"iPhone 17\": 1 iOS device available; 1 booted."),
                new EngineDoctorCheck("target-app", EngineDoctorStatus.Pass, "\"iPhone 17\": Target app is launchable: Settings"),
            ],
            await harness.Engine.DoctorAsync(Info(harness), CancellationToken.None));
    }

    [Fact]
    public async Task Selects_a_device_by_UDID()
    {
        const string Udid = "78D5CEB4-D3A1-48E2-9AFD-6F8A99A824DA";
        var harness = DoctorHarness(options => options with { Device = Udid });
        await harness.Engine.DoctorAsync(Info(harness), CancellationToken.None);
        JsonAssert.Args($$"""{"platform":"ios","udid":"{{Udid}}","targetApp":"Settings"}""", harness.Fake.LastArgs(DeviceMethods.CommandDoctor));
    }

    [Fact]
    public async Task Checks_the_build_appPath_names_instead_of_an_installed_app()
    {
        _dir = Directory.CreateTempSubdirectory("e2e-mobile-doctor-").FullName;
        File.WriteAllText(Path.Combine(_dir, "App.app"), "");
        var present = DoctorHarness(app: _ => new MobileApp { AppPath = "App.app", BundleId = "com.example.app" });
        var checks = await present.Engine.DoctorAsync(Info(present, _dir), CancellationToken.None);
        Assert.Equal(new EngineDoctorCheck("app-path", EngineDoctorStatus.Pass, Path.Combine(_dir, "App.app")), checks[0]);
        Assert.False(present.Fake.LastArgs(DeviceMethods.CommandDoctor)!.ContainsKey("targetApp"));

        var missing = DoctorHarness(app: _ => new MobileApp { AppPath = "Missing.app" });
        var failed = (await missing.Engine.DoctorAsync(Info(missing, _dir), CancellationToken.None))[0];
        Assert.Equal(("app-path", EngineDoctorStatus.Fail), (failed.Id, failed.Status));
    }

    [Fact]
    public async Task Skips_the_device_and_the_build_for_a_providers_leases_without_calling_agent_device()
    {
        var harness = DoctorHarness(options => options with { Provider = new CloudProvider() }, app: _ => new MobileApp { AppPath = "hosted.app" });
        Assert.Equal(
            [new EngineDoctorCheck("device", EngineDoctorStatus.Skip, "devices and the build come from the \"cloud\" provider")],
            await harness.Engine.DoctorAsync(Info(harness), CancellationToken.None));
        Assert.Empty(harness.Fake.Methods());
    }

    private sealed class CloudProvider : IDeviceProvider
    {
        public string Name => "cloud";

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("unused");

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
