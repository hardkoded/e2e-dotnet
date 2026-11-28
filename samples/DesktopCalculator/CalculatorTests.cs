// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace DesktopCalculator;

public sealed class CalculatorTests : CalculatorTest
{
    [Test]
    public async Task Lists_the_window_the_app_opened()
    {
        var windows = await Desktop.WindowsAsync();

        Assert.That(windows, Is.Not.Empty);
        await Expect.That(Screen.GetByRole("button", new() { Name = "5" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Taps_the_number_keys_through_the_accessibility_tree()
    {
        // No pointer moves and no focus changes hands: the engine presses each button through the Accessibility API.
        await Screen.GetByRole("button", new() { Name = "2" }).TapAsync();
        await Screen.GetByRole("button", new() { Name = "3" }).TapAsync();

        await Expect
            .That(
                Screen
                    .GetByRole("button", new() { Name = "All Clear" })
                    .Or(Screen.GetByRole("button", new() { Name = "Clear" }))
            )
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task Presses_keys_into_the_apps_window()
    {
        await Desktop.PressAsync("2");
        await Desktop.PressAsync("Shift+=");
        await Desktop.PressAsync("3");
        await Desktop.PressAsync("Enter");

        Assert.That(await Desktop.WindowsAsync(), Is.Not.Empty);
    }

    [Test]
    [Category("RealModel")]
    public async Task Adds_two_numbers()
    {
        await Agent.ActAsync("add 2 and 3 with the calculator, and check that the display reads 5");

        await Expect.That(Screen.GetByText("5").First()).ToBeVisibleAsync();
    }

    [Test]
    public async Task Quits_the_app_when_asked()
    {
        await Desktop.CloseAppAsync();

        Assert.That(await Desktop.WindowsAsync(), Is.Empty);
    }
}
