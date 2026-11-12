// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.RichTextEditor;

/// <summary>Ported from <c>describe('rich text editor')</c> in upstream's <c>apps/web-benchmark/tests/rich-text-editor.e2e.ts</c>.</summary>
public sealed class RichTextEditorTests : BenchmarkTest
{
    [Test]
    public async Task Bolding_exactly_the_required_word_passes_the_check()
    {
        await App.OpenAsync("/e/rich-text-editor");
        await TypeAndSelectTailAsync("approved".Length);
        await Screen.GetByRole("button", "B").TapAsync();
        await Screen.GetByRole("button", "Check document").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Document approved");
    }

    [Test]
    public async Task Bold_that_spills_into_the_previous_word_fails_the_check()
    {
        await App.OpenAsync("/e/rich-text-editor");
        await TypeAndSelectTailAsync("se approved".Length);
        await Screen.GetByRole("button", "B").TapAsync();
        await Screen.GetByRole("button", "Check document").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Only the word approved must be bold");
    }

    /// <summary>Types the phrase, then selects its last <paramref name="length"/> characters with the keyboard.</summary>
    private async Task TypeAndSelectTailAsync(int length)
    {
        await Screen.GetByTestId("editor").TapAsync();
        await Browser.Keyboard.TypeAsync("release approved");
        for (var i = 0; i < length; i++)
        {
            await Browser.Keyboard.PressAsync("Shift+ArrowLeft");
        }
    }
}
