// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Engine;

/// <summary>Ported from <c>describe('desktop()')</c> in upstream's <c>packages/desktop/tests/unit/engine.test.ts</c>.</summary>
public sealed class DesktopTests : SurfaceTestBase
{
    [Fact]
    public void Offers_keys_without_headed_and_the_pointer_only_actions_with_headed_alone()
    {
        var plain = new DesktopEngine();
        var headed = new DesktopEngine(new DesktopOptions { Headed = true });
        Assert.DoesNotContain("doubleTap", plain.ActionKinds);
        Assert.DoesNotContain("hover", plain.ActionKinds);
        // A key goes into the app's own event queue, which reaches its key window while it stays in the background.
        Assert.Contains("press", plain.ActionKinds);
        Assert.True(plain.Capabilities.HasFlag(EngineCapabilities.Keyboard));
        Assert.Empty(plain.PointerActionKinds);
        Assert.Subset(headed.ActionKinds.ToHashSet(), new HashSet<string> { "doubleTap", "hover", "press" });
        Assert.Subset(
            headed.PointerActionKinds.ToHashSet(),
            new HashSet<string> { "tap", "doubleTap", "secondaryTap", "hover" }
        );
        Assert.True(headed.Capabilities.HasFlag(EngineCapabilities.Keyboard));
        Assert.Equal(1, new DesktopEngine(null, () => NewFake()).NewSurface().Prepare());
        Assert.True(((IEngine)plain).LaunchArgumentValues);
    }

    [Fact]
    public void Refuses_options_it_does_not_take()
    {
        static void Invalid(DesktopOptions options) =>
            Assert.Equal(
                "INVALID_CONFIG",
                Assert.Throws<ConfigurationException>(() => DesktopOptionsResolver.Resolve(options)).Code
            );
        Invalid(new DesktopOptions { SettleMilliseconds = -1 });
        // Upstream's `snapshot: 'all'`: an enum value no member names.
        Invalid(new DesktopOptions { Snapshot = (DesktopSnapshotMode)7 });
        var withoutSettle = DesktopOptionsResolver.Resolve(new DesktopOptions { Settle = false });
        Assert.Equal((0, false, false), (withoutSettle.SettleMs, withoutSettle.Headed, withoutSettle.InteractiveOnly));
        Assert.Equal(24, DesktopOptionsResolver.Resolve(new DesktopOptions()).MaxDepth);
        Assert.Equal(50, DesktopOptionsResolver.Resolve(new DesktopOptions { MaxDepth = 50 }).MaxDepth);
        foreach (var maxDepth in new[] { 0, 51 })
        {
            Invalid(new DesktopOptions { MaxDepth = maxDepth });
        }
    }

    [Fact]
    public void Refuses_an_app_it_cannot_launch()
    {
        IEngine engine = new DesktopEngine();
        Action Check(EngineAppDeclaration app) =>
            () => engine.ValidateApp(app, new EngineAppCheckInfo { TargetName = "mac" });
        Assert.Equal(
            "INVALID_CONFIG",
            Assert
                .Throws<ConfigurationException>(Check(new EngineAppDeclaration { Url = "http://localhost:3000" }))
                .Code
        );
        Assert.Equal("INVALID_CONFIG", Assert.Throws<ConfigurationException>(Check(new EngineAppDeclaration())).Code);
        Assert.Equal(
            "INVALID_CONFIG",
            Assert
                .Throws<ConfigurationException>(
                    Check(
                        new EngineAppDeclaration
                        {
                            BundleId = "com.example.app",
                            Permissions = new Dictionary<string, AppPermissionState>
                            {
                                ["camera"] = AppPermissionState.Grant,
                            },
                        }
                    )
                )
                .Code
        );
        Check(new EngineAppDeclaration { BundleId = "com.example.app" })();
        Assert.Equal(
            "INVALID_CONFIG",
            Assert.Throws<ConfigurationException>(Check(new EngineAppDeclaration { AppPath = "build/App.zip" })).Code
        );
        Check(new EngineAppDeclaration { AppPath = "build/App.app" })();
    }

    [Theory]
    [InlineData("linux")]
    [InlineData("win32")]
    public async Task Loads_on_the_OS_and_reports_a_target_there_as_unsupported_before_it_starts(string platform)
    {
        var engine = new DesktopEngine { Host = new DesktopHost(false, platform) };
        Assert.Equal("macos", engine.Platform);
        var message = $"desktop() is not supported on {platform}: it drives macOS apps only";
        var prepare = await Assert.ThrowsAsync<ConfigurationException>(() =>
            engine.PrepareAsync(
                new EnginePrepareInfo
                {
                    RunId = "run",
                    TargetName = "mac",
                    Slots = 1,
                    Env = new Dictionary<string, string?>(),
                    Log = _ => { },
                },
                CancellationToken.None
            )
        );
        Assert.Equal(("UNSUPPORTED_CAPABILITY", message), (prepare.Code, prepare.Message));
        var start = await Assert.ThrowsAsync<ConfigurationException>(() =>
            engine.StartAsync(new EngineStartOptions(), CancellationToken.None)
        );
        Assert.Equal(("UNSUPPORTED_CAPABILITY", message), (start.Code, start.Message));
        var doctor = await engine.DoctorAsync(
            new EngineDoctorInfo
            {
                TargetName = "mac",
                ProjectRoot = ".",
                App = new EngineAppInfo(),
                Env = new Dictionary<string, string?>(),
                Headed = false,
                Log = _ => { },
            },
            CancellationToken.None
        );
        Assert.Equal([new EngineDoctorCheck("platform", EngineDoctorStatus.Fail, message)], doctor);
    }
}
