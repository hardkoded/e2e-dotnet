// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.DragAndDrop;

/// <summary>Ported from <c>describe('drag and drop')</c> in upstream's <c>apps/web-benchmark/tests/drag-and-drop.e2e.ts</c>.</summary>
public sealed class DragAndDropTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheDragAndDrop() => await App.OpenAsync("/e/drag-and-drop");

    [Test]
    public async Task An_out_of_order_item_resets_the_dropzone()
    {
        var dropzone = Screen.GetByTestId("dropzone");
        await Screen.GetByTestId("drag-banana").DragToAsync(dropzone);
        await Expect.That(dropzone).ToContainTextAsync("Banana");
        await Screen.GetByTestId("drag-apple").DragToAsync(dropzone);
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Wrong item order, starting over");
        await Expect.That(dropzone).ToHaveTextAsync("Drop items here");
    }

    [Test]
    public async Task Dropping_Banana_then_Cherry_passes()
    {
        var dropzone = Screen.GetByTestId("dropzone");
        await Screen.GetByTestId("drag-banana").DragToAsync(dropzone);
        await Expect.That(dropzone).ToContainTextAsync("Banana");
        await Screen.GetByTestId("drag-cherry").DragToAsync(dropzone);
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Items dropped in the right order");
    }
}
