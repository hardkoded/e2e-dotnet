// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.OtpAutoAdvance;

/// <summary>Ported from <c>describe('otp auto-advance')</c> in upstream's <c>apps/web-benchmark/tests/otp-auto-advance.e2e.ts</c>.</summary>
public sealed class OtpAutoAdvanceTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheOtpAutoAdvance() => await App.OpenAsync("/e/otp-auto-advance");

    [Test]
    public async Task Focus_advances_with_each_digit_and_the_full_code_verifies()
    {
        await Box(0).PressSequentiallyAsync("4");
        await Expect.That(Box(0)).ToHaveValueAsync("4");
        await Expect.That(Box(1)).ToBeFocusedAsync();

        await Box(1).PressSequentiallyAsync("9302");
        await Expect.That(Box(4)).ToHaveValueAsync("2");
        await Expect.That(Box(5)).ToBeFocusedAsync();

        await Box(5).PressSequentiallyAsync("7");
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Code verified");
    }

    [Test]
    public async Task Backspace_on_an_empty_box_clears_and_refocuses_the_previous_one()
    {
        await Box(0).PressSequentiallyAsync("49");
        await Expect.That(Box(2)).ToBeFocusedAsync();
        await Box(2).PressAsync("Backspace");
        await Expect.That(Box(1)).ToHaveValueAsync("");
        await Expect.That(Box(1)).ToBeFocusedAsync();
        await Expect.That(Box(0)).ToHaveValueAsync("4");
    }

    [Test]
    public async Task A_wrong_code_clears_every_box_and_starts_over()
    {
        await Box(0).PressSequentiallyAsync("000000");
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Incorrect code");
        await Expect.That(Box(0)).ToBeFocusedAsync();
        for (var index = 0; index < 6; index += 1)
        {
            await Expect.That(Box(index)).ToHaveValueAsync("");
        }

        await Box(0).PressSequentiallyAsync("493027");
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Code verified");
    }

    /// <summary>The segmented input at <paramref name="index"/>, zero-based.</summary>
    private E2E.Locator Box(int index) => Screen.GetByTestId($"otp-input-{index}");
}
