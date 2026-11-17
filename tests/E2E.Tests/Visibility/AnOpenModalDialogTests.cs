// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Visibility;

[Collection(BrowserCollection.Name)]
public sealed class AnOpenModalDialogTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    private async Task<List<string?>> ButtonNamesAsync() =>
        (await chromium.CaptureAsync()).Where(node => node.Role == "button").Select(node => node.Name).ToList();

    [Fact]
    public async Task Leaves_the_page_behind_it_out_of_the_tree_while_Playwright_still_calls_that_page_visible()
    {
        await chromium.Page.SetContentAsync(
            """
            <main><h2>Store</h2><button id="behind">Add to cart</button></main>
            <dialog id="consent"><p>We use cookies.</p><button>Accept</button></dialog>
            <script>document.getElementById('consent').showModal();</script>
            """
        );
        var names = (await chromium.CaptureAsync()).Select(node => node.Name).OfType<string>().ToList();
        Assert.Contains("Accept", names);
        Assert.DoesNotContain("Add to cart", names);
        Assert.DoesNotContain("Store", names);
        var behind = chromium.Page.Locator("#behind");
        // Upstream's hiddenOf reads the element in node mode, which reads it on its own, not through the walk that leaves it out.
        var reader = await behind.EvaluateAsync<bool>(
            $$"""
            (el) => {
            {{ReadSemantics.Prelude}}
            {{ReadSemantics.NodeMode}}
              return nodeOf(el, "data-testid").hidden;
            }
            """
        );
        Assert.False(reader);
        Assert.False(await behind.IsHiddenAsync());
    }

    [Fact]
    public async Task Keeps_the_dialog_holding_focus_when_two_were_shown_out_of_tree_order_with_no_show_order_recorded()
    {
        await chromium.Page.SetContentAsync(
            """
            <dialog id="first"><button>First dialog</button></dialog>
            <dialog id="second"><button>Second dialog</button></dialog>
            <script>document.getElementById('second').showModal(); document.getElementById('first').showModal();</script>
            """
        );
        Assert.Equal(["First dialog"], await ButtonNamesAsync());
    }

    [Fact]
    public async Task Leaves_the_page_alone_for_a_dialog_shown_without_a_modal_and_once_a_modal_dialog_closes()
    {
        await chromium.Page.SetContentAsync(
            """
            <button>Page button</button>
            <dialog id="plain"><button>Plain dialog</button></dialog>
            <dialog id="modal"><button>Modal dialog</button></dialog>
            <script>
              document.getElementById('plain').show();
              document.getElementById('modal').showModal();
              document.getElementById('modal').close();
            </script>
            """
        );
        // The port lists a closed dialog's content as hidden nodes, where upstream's tree omits it.
        var shown = (await chromium.CaptureAsync())
            .Where(node => node.Role == "button" && !node.Hidden)
            .Select(node => node.Name);
        Assert.Equal(["Page button", "Plain dialog"], shown);
    }
}
