// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Desktop;

namespace E2E.Desktop.Tests.Driver;

/// <summary>Ported from <c>describe('loadNativeDriver')</c> in upstream's <c>packages/desktop/tests/unit/driver.test.ts</c>.</summary>
public sealed class LoadNativeDriverTests
{
    /// <summary>Every function of <see cref="IDesktopDriver"/>; the test fails when the interface gains or loses one. Upstream's <c>NATIVE_FUNCTIONS</c>.</summary>
    private static readonly string[] NativeFunctions =
    [
        "Status",
        "ScreenLocked",
        "ProcessStarted",
        "ProcessesCarryingAsync",
        "ForgetProcess",
        "LaunchAsync",
        "ListWindowsAsync",
        "SnapshotAsync",
        "ActAsync",
        "InputId",
        "CancelInput",
        "PressAsync",
        "CheckComboAsync",
        "FocusWindowAsync",
        "PointerAsync",
        "ScreenshotAsync",
    ];

    [Fact]
    public void Lists_every_function_of_the_driver_interface()
    {
        var declared = typeof(IDesktopDriver)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToArray();
        Assert.Equal(NativeFunctions.Order().ToArray(), declared);
    }

    [MacOsFact]
    public void Names_the_build_it_loaded_and_keeps_every_native_function_callable()
    {
        var driver = DesktopDriverCalls.LoadNativeDriver();
        Assert.False(string.IsNullOrEmpty(driver.Version));
        _ = driver.Status().Accessibility;
        // Input ids come from the driver, which every worker thread of the process shares with its cancel marks.
        var first = driver.InputId();
        Assert.True(driver.InputId() > first);
    }

    [Theory]
    [InlineData("linux")]
    [InlineData("win32")]
    public void Reports_the_OS_as_unsupported_no_driver_is_built_for_it(string platform)
    {
        var unsupported = Assert.Throws<EngineException>(() => DesktopDriverCalls.LoadNativeDriver(false, platform));
        Assert.Equal("UNSUPPORTED_CAPABILITY", unsupported.Code);
        Assert.Contains($"no native driver for {platform}", unsupported.Message);
    }
}
