// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.DeferredDialog;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/deferred-dialog.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class DeferredDialogTests : BenchmarkTest
{
    [Test]
    public async Task One_submit_opens_one_confirm_even_when_double_clicked()
    {
        await App.OpenAsync("/e/deferred-dialog");
        var dialogs = 0;
        var dispose = await Browser.OnDialogAsync(async dialog =>
        {
            Interlocked.Increment(ref dialogs);
            await dialog.AcceptAsync();
        });
        try
        {
            await Screen.GetByRole("button", "Submit report").DoubleTapAsync();
            await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Report submitted after deferred confirmation");
            Assert.That(Volatile.Read(ref dialogs), Is.EqualTo(1));
        }
        finally
        {
            await dispose();
        }
    }
}
