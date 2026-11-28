// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;
using MobileBenchmark.E2ETests.Fixtures;

namespace MobileBenchmark.E2ETests.Controls;

/// <summary>
/// Upstream's <c>test.describe('control inventory')</c> of <c>controls.e2e.ts</c>, the Fields section's name field and the
/// Gestures section: the app is a MAUI app with those sections so far.
/// </summary>
public sealed class ControlInventoryTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheInventory() => await OpenScenarioAsync("Control Inventory");

    /// <summary>Shows one of the scenario's sections; every control of it fits on screen.</summary>
    private async Task ShowSectionAsync(string name) =>
        await Screen.GetByTestId($"tab-{name.ToLowerInvariant()}").TapAsync();

    [Test]
    public async Task GetAttribute_reads_the_label_resource_id_and_class_an_Appium_suite_asks_for()
    {
        var field = Screen.GetByTestId("name-input");
        Assert.That(await field.GetAttributeAsync("resource-id"), Is.EqualTo("name-input"));
        await Expect.That(field).ToHaveAttributeAsync("label", "Name field");
        if (Platform == MobilePlatform.Ios)
        {
            await Expect.That(field).ToHaveAttributeAsync("class", "TextField");
            Assert.That(await field.GetAttributeAsync("content-desc"), Is.Null);
        }
        else
        {
            await Expect.That(field).ToHaveAttributeAsync("class", "android.widget.EditText");
            await Expect.That(field).ToHaveAttributeAsync("content-desc", "Name field");
        }
    }

    [Test]
    public async Task A_long_press_lands_as_one()
    {
        await ShowSectionAsync("Gestures");
        await Screen
            .GetByRole("button", "Hold me")
            .LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(800) });
        await Expect.That(Screen.GetByTestId("gesture-status")).ToHaveTextAsync("gesture: long press");
    }

    // The engine sends a double tap as two presses, since agent-device's own double-tap gesture reaches a Pressable as
    // one press. The app prints how many taps arrived and how far apart, so a pair that missed the window shows what
    // the device delivered. agent-device's iOS runner lands two presses 284 to 285 ms apart, so on iOS the scenario's
    // 300 ms window has no margin: this test runs on Android only, as upstream's does.
    [Test]
    public async Task A_double_tap_lands_inside_the_300_ms_window()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Android), "iOS lands the pair about 285 ms apart.");
        await ShowSectionAsync("Gestures");
        await Screen.GetByRole("button", "Double-tap me").DoubleTapAsync();
        await Expect.That(Screen.GetByTestId("tap-count")).ToContainTextAsync("2 taps");
        await Expect.That(Screen.GetByTestId("gesture-status")).ToHaveTextAsync("gesture: double tap");
    }

    // tap({ count: 3 }) is agent-device's press with count 3: three presses at the double-tap cadence. The scenario
    // counts what arrives; the 300 ms window is left alone, since iOS lands presses about 285 ms apart.
    [Test]
    public async Task Tap_count_3_lands_three_presses()
    {
        await ShowSectionAsync("Gestures");
        await Screen.GetByRole("button", "Double-tap me").TapAsync(new TapOptions { Count = 3 });
        await Expect.That(Screen.GetByTestId("tap-count")).ToContainTextAsync("3 taps");
    }
}
