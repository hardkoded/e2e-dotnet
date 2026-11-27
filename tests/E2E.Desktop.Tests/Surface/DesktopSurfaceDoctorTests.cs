// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: doctor')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceDoctorTests : SurfaceTestBase
{
    private static EngineDoctorInfo Info(EngineAppInfo app, string? projectRoot = null) =>
        new()
        {
            TargetName = "mac",
            ProjectRoot = projectRoot ?? Directory.GetCurrentDirectory(),
            App = app,
            Env = new Dictionary<string, string?>(),
            Headed = false,
            Log = _ => { },
        };

    private static DesktopSurface SurfaceOver(Func<IDesktopDriver> load) =>
        new(DesktopOptionsResolver.Resolve(new DesktopOptions()), "macos", load);

    [Fact]
    public async Task Reports_the_driver_and_the_permissions_it_holds()
    {
        var fake = NewFake();
        fake.ScreenRecording = false;
        var checks = await SurfaceOver(() => fake)
            .DoctorAsync(Info(new EngineAppInfo { BundleId = "com.example.app" }));
        Assert.Equal(3, checks.Count);
        Assert.Equal(
            new EngineDoctorCheck("desktop-driver", EngineDoctorStatus.Pass, "desktop driver 0.0.0-test"),
            checks[0]
        );
        Assert.Equal(("accessibility", EngineDoctorStatus.Pass), (checks[1].Id, checks[1].Status));
        Assert.NotNull(checks[1].Message);
        Assert.Equal(("screen-recording", EngineDoctorStatus.Warn), (checks[2].Id, checks[2].Status));
        Assert.Contains("Screen Recording", checks[2].Message);
    }

    [Fact]
    public async Task Fails_doctor_when_the_native_driver_cannot_be_loaded()
    {
        var surface = SurfaceOver(() => throw new InvalidOperationException("no native driver for this machine"));
        var checks = await surface.DoctorAsync(Info(new EngineAppInfo { BundleId = "com.example.app" }));
        Assert.Equal(
            [new EngineDoctorCheck("desktop-driver", EngineDoctorStatus.Fail, "no native driver for this machine")],
            checks
        );
    }

    [Fact]
    public async Task Fails_doctor_on_an_appPath_that_is_no_app_bundle_or_does_not_exist()
    {
        var directory = Artifacts();
        Directory.CreateDirectory(Path.Combine(directory, "Empty.app"));
        var surface = SurfaceOver(() => NewFake());
        async Task<EngineDoctorCheck?> AppPath(string appPath) =>
            (await surface.DoctorAsync(Info(new EngineAppInfo { AppPath = appPath }, directory))).FirstOrDefault(
                check => check.Id == "app-path"
            );
        var empty = await AppPath("Empty.app");
        Assert.Equal(EngineDoctorStatus.Fail, empty?.Status);
        Assert.Contains("no Contents/Info.plist", empty?.Message);
        var missing = await AppPath("Missing.app");
        Assert.Equal(EngineDoctorStatus.Fail, missing?.Status);
        Assert.Contains("does not exist", missing?.Message);
    }

    [MacOsFact]
    public async Task Fails_doctor_on_a_bundle_init_would_refuse_no_bundle_id_or_not_the_targets()
    {
        var directory = Artifacts();
        static string Plist(string body) =>
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>{body}</dict></plist>";
        foreach (
            var (name, body) in new[]
            {
                ("Anonymous.app", ""),
                ("Built.app", "<key>CFBundleIdentifier</key><string>dev.e2e.built</string>"),
            }
        )
        {
            Directory.CreateDirectory(Path.Combine(directory, name, "Contents"));
            File.WriteAllText(Path.Combine(directory, name, "Contents", "Info.plist"), Plist(body));
        }

        var surface = SurfaceOver(() => NewFake());
        async Task<EngineDoctorCheck?> AppPath(string appPath, string? bundleId = null) =>
            (
                await surface.DoctorAsync(Info(new EngineAppInfo { AppPath = appPath, BundleId = bundleId }, directory))
            ).FirstOrDefault(check => check.Id == "app-path");
        var anonymous = await AppPath("Anonymous.app");
        Assert.Equal(EngineDoctorStatus.Fail, anonymous?.Status);
        Assert.Contains("declares no CFBundleIdentifier", anonymous?.Message);
        var other = await AppPath("Built.app", "com.example.other");
        Assert.Equal(EngineDoctorStatus.Fail, other?.Status);
        Assert.Contains("is not the CFBundleIdentifier \"dev.e2e.built\"", other?.Message);
        Assert.Equal(EngineDoctorStatus.Pass, (await AppPath("Built.app", "dev.e2e.built"))?.Status);
    }
}
