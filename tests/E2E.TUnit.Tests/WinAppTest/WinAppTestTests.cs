// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.WinApp.TUnit;

namespace E2E.TUnit.Tests.WinAppTest;

/// <summary>A Windows app fixture over a config with a target. It has no test: the tests below read its members.</summary>
public sealed class WinAppProbe : E2E.WinApp.TUnit.WinAppTest
{
    private static readonly E2EConfig ProbeConfig = E2EConfig.Parse(
        """{ "targets": [{ "platform": "windows", "app": { "appPath": "bin/MyApp.exe", "bundleId": "MyApp" } }] }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => ProbeConfig;

    public string? ReadAppId() => AppId;

    public TimeSpan ReadStepTimeout() => StepTimeout;
}

/// <summary>
/// .NET-only (upstream has no Windows adapter): <see cref="E2E.WinApp.TUnit.WinAppTest"/> derives from <c>E2E.TUnit.E2ETest</c>
/// and reads the app from the target, like the xUnit v3 and NUnit twins. The TUnit adapter has no run selection yet,
/// so the left-out-test checks of those twins have no counterpart here.
/// </summary>
public sealed class WinAppTestTests
{
    [Test]
    public async Task Derives_from_the_e2e_test_of_tunit()
    {
        await Assert.That(typeof(E2E.WinApp.TUnit.WinAppTest).BaseType).IsEqualTo(typeof(E2E.TUnit.E2ETest));
    }

    [Test]
    public async Task Reads_the_app_from_the_target_and_waits_90_seconds_for_a_step()
    {
        var probe = new WinAppProbe();

        await Assert.That(probe.ReadAppId()).IsEqualTo("MyApp");
        await Assert.That(probe.ReadStepTimeout()).IsEqualTo(TimeSpan.FromSeconds(90));
    }

    [Test]
    public async Task Opens_the_app_in_a_before_test_hook_of_its_own()
    {
        var hook = typeof(E2E.WinApp.TUnit.WinAppTest).GetMethod("OpenAppAsync", BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(hook).IsNotNull();
        await Assert.That(hook!.GetCustomAttributes<global::TUnit.Core.BeforeAttribute>().Any()).IsTrue();
    }
}
