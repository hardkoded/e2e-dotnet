// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.WinApp.TUnit;
using TUnit.Core.Exceptions;
using TUnitContext = global::TUnit.Core.TestContext;

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

/// <summary>A Windows app fixture whose test the filters leave out. Its test never runs, and the tests call its setup by hand.</summary>
public sealed class LeftOutWinAppFixture : E2E.WinApp.TUnit.WinAppTest
{
    [Test]
    [Skip("only found by reflection")]
    public void Windows_test_is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream has no Windows adapter): <see cref="E2E.WinApp.TUnit.WinAppTest"/> derives from <c>E2E.TUnit.E2ETest</c>,
/// reads the app from the target, and skips a test the filters left out before the app opens, like the xUnit v3 and NUnit twins.
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

    [Test]
    [NotInParallel("RunSelectionTests")]
    public async Task Skips_a_windows_test_the_filters_left_out_before_the_app_opens(CancellationToken cancellationToken)
    {
        var context = TUnitContext.Current!;
        var method = context.Metadata.TestDetails.MethodName;
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutWinAppFixture).FullName}.{method}"] = "login.cs did not change" });
        try
        {
            // The assertions would take the skip for a failure, so the skip is read by hand.
            // A test that went on would fail at the engine it has no session for, not with the skip.
            string? reason = null;
            try
            {
                await new LeftOutWinAppFixture().StartE2ESessionAsync(context, cancellationToken);
            }
            catch (SkipTestException ex)
            {
                reason = ex.Message;
            }

            await Assert.That(reason).IsEqualTo("login.cs did not change");
        }
        finally
        {
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public async Task Finds_the_tests_of_a_windows_fixture()
    {
        var found = E2E.TUnit.RunSelection.Discover([typeof(LeftOutWinAppFixture)], typeof(LeftOutWinAppFixture).Assembly);

        await Assert.That(found.Select(test => test.Title).ToArray()).IsEquivalentTo([nameof(LeftOutWinAppFixture.Windows_test_is_left_out)]);
    }
}
