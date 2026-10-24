// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.Device;

/// <summary>
/// Scenarios that need the engine's fixture on top of locators. A desktop window has no soft keyboard, so the
/// wizard's steps need no <c>dismissKeyboard</c> between a field and Continue.
/// </summary>
public sealed class DeviceTests : BenchmarkTest
{
    // Every step focuses its first field. The success screen echoes the values, so a stray key press fails it.
    [Test]
    public async Task Sequential_onboarding_echoes_the_exact_values()
    {
        await OpenScenarioAsync("Sequential Onboarding");
        await Screen.GetByTestId("email-input").FillAsync("jane@example.com");
        await Screen.GetByTestId("continue-button").TapAsync();

        await Screen.GetByTestId("first-name-input").FillAsync("Jane");
        await Screen.GetByTestId("last-name-input").FillAsync("Merchant");
        await Screen.GetByTestId("continue-button").TapAsync();

        await Screen.GetByTestId("phone-input").FillAsync("0612435678");
        await Screen.GetByTestId("continue-button").TapAsync();

        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Onboarding complete");
        await Expect.That(Screen.GetByTestId("echo-email")).ToHaveTextAsync("jane@example.com");
        await Expect.That(Screen.GetByTestId("echo-name")).ToHaveTextAsync("Jane Merchant");
        await Expect.That(Screen.GetByTestId("echo-phone")).ToHaveTextAsync("0612435678");
    }

    [Test]
    public async Task Onboarding_rejects_a_malformed_email_before_advancing()
    {
        await OpenScenarioAsync("Sequential Onboarding");
        await Screen.GetByTestId("email-input").FillAsync("jane");
        await Screen.GetByTestId("continue-button").TapAsync();
        await Expect.That(Screen.GetByTestId("step-error")).ToHaveTextAsync("Enter a valid email address");
        await Expect.That(Screen.GetByTestId("onboarding-step-email")).ToBeVisibleAsync();
    }

    // Not ported from upstream's device.e2e.ts:
    //  - `describe('permission prompt')` (two tests): the app has no Permission Prompt scenario.
    //  - 'dismissing the keyboard over choice controls leaves them as they were': a desktop window has no soft keyboard to dismiss.
}
