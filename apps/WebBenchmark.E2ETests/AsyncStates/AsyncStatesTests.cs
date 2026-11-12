// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.AsyncStates;

/// <summary>Ported from <c>describe('async states')</c> in upstream's <c>apps/web-benchmark/tests/async-states.e2e.ts</c>.</summary>
public sealed class AsyncStatesTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheAsyncStates()
    {
        await App.OpenAsync("/e/async-states");
        await Expect.That(Screen.GetByTestId("initial-loading")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("initial-loading")).ToBeHiddenAsync();
    }

    [Test]
    public async Task Reads_the_code_off_the_transient_toast_and_verifies_it_once_the_button_enables()
    {
        var verify = Screen.GetByRole("button", "Verify");
        await Expect.That(verify).ToBeDisabledAsync();

        await Screen.GetByRole("button", "Send code").TapAsync();
        var toast = Screen.GetByTestId("toast");
        await Expect.That(toast).ToHaveTextAsync(new Regex(@"^Your code is \d{4}\z"));
        var code = await Support.ReadCodeAsync(toast, new Regex(@"\d{4}"));

        await Screen.GetByPlaceholder("Verification code").FillAsync(code);
        await Expect.That(verify).ToBeEnabledAsync();
        await verify.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Code verified successfully");
    }

    [Test]
    public async Task A_wrong_code_is_rejected_after_the_toast_has_gone()
    {
        await Screen.GetByRole("button", "Send code").TapAsync();
        await Expect.That(Screen.GetByTestId("toast")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("toast")).ToBeHiddenAsync();

        await Screen.GetByPlaceholder("Verification code").FillAsync("0000");
        await Screen.GetByRole("button", "Verify").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Wrong code - send it again and retry");
    }
}
