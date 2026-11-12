// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Visibility;

[Collection(BrowserCollection.Name)]
public sealed class TheTreeWalkThroughABoxWithNoSizeTests
{
    [Fact]
    public async Task Lists_the_fixed_controls_and_the_empty_wrapper_under_a_zero_height_page_and_marks_neither_the_wrapper_nor_the_root_as_hidden()
    {
        var nodes = await VisibilitySupport.ObserveAsync("""
            <nav aria-label="Floating" data-testid="wrapper">
              <button style="position:fixed;left:0;top:0;width:100px;height:30px">Reset</button>
            </nav>
            """);
        Assert.False(nodes[0].States.Hidden);
        Assert.Equal("Reset", nodes.First(node => node.Role == "button").Name);
        // Upstream leaves the empty wrapper out. A box with no size is not hidden here, so it is listed and shown.
        Assert.False(nodes.Single(node => node.TestId == "wrapper").States.Hidden);
    }

    // Partial: upstream lists neither the document of a frame with no box nor the options of a select with none.
    // A box with no size is not hidden here (COMPATIBILITY.md), so the port lists both, and this test checks only
    // that the frame and the select that have a box are listed.
    [Fact]
    public async Task Partial_Lists_neither_the_document_of_a_frame_with_no_box_nor_the_options_of_a_select_with_none()
    {
        var nodes = await VisibilitySupport.ObserveAsync("""
            <iframe style="width:0;height:0;border:0" srcdoc="<button>Pay</button>"></iframe>
            <iframe style="width:200px;height:100px" srcdoc="<button>Shown pay</button>"></iframe>
            <select aria-label="Folded" style="width:0;height:0;padding:0;border:0"><option>plain</option><option>warm</option></select>
            <select aria-label="Shown"><option>cool</option></select>
            """);
        Assert.Contains("Shown pay", nodes.Where(node => node.Role == "button").Select(node => node.Name));
        Assert.Contains("cool", nodes.Where(node => node.Role == "option").Select(node => node.Name));
        Assert.Contains("Shown", nodes.Where(node => node.Role == "combobox").Select(node => node.Name));
    }
}
