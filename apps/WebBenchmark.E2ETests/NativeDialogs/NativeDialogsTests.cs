// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Engine;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.NativeDialogs;

/// <summary>Ported from <c>describe('native dialogs')</c> in upstream's <c>apps/web-benchmark/tests/native-dialogs.e2e.ts</c>.</summary>
public sealed class NativeDialogsTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheDialogs() => await App.OpenAsync("/e/native-dialogs");

    [Test]
    public async Task Answers_the_confirm_prompt_and_alert_chain()
    {
        var seen = new ConcurrentQueue<string>();
        var dispose = await Browser.OnDialogAsync(async dialog =>
        {
            seen.Enqueue(dialog.Message);
            if (dialog.Message == "Enter your gift code")
            {
                await dialog.AcceptAsync("GIFT-7");
                return;
            }

            await dialog.AcceptAsync();
        });
        await Screen.GetByRole("button", "Place order").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Order placed with code GIFT-7");
        Assert.That(seen, Is.EqualTo(new[] { "Place this order?", "Enter your gift code", "Order placed. Thank you!" }));
        await dispose();
    }

    [Test]
    public async Task Dismissing_the_confirm_cancels_the_order()
    {
        var dispose = await Browser.OnDialogAsync(DialogHandler.Dismiss);
        await Screen.GetByRole("button", "Place order").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Order was cancelled");
        await dispose();
    }

    [Test]
    public async Task A_wrong_gift_code_is_rejected()
    {
        var dispose = await Browser.OnDialogAsync(async dialog =>
        {
            await dialog.AcceptAsync(dialog.Message == "Enter your gift code" ? "GIFT-9" : null);
        });
        await Screen.GetByRole("button", "Place order").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Invalid gift code");
        await dispose();
    }
}
