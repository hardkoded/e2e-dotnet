// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentReading;

/// <summary>
/// .NET-only: no upstream <c>it</c> covers the <c>scroll_to</c> paging loop (<c>ActionDispatcher.PageUntilAsync</c>): which
/// list it pages, when it stops, and when it hands the paging to the viewport. Upstream's scroll-settle tests cover only the
/// look after the scroll.
/// </summary>
public sealed class PageUntilTests
{
    [Fact]
    public async Task Pages_inside_the_list_it_is_given_and_the_whole_screen_without_one()
    {
        static DocumentWorld World() => new DocumentWorld().Map("/ledger", page =>
        {
            page.Button("Jump to Row 333");
            var list = new DocumentElement { Role = "list", Name = "Ledger" };
            list.Children.Add(new DocumentElement { Role = "listitem", Text = "Row 1" });
            list.Children.Add(new DocumentElement { Role = "listitem", Text = "Row 2" });
            list.OnScroll = _ =>
            {
                if (list.Children.Count == 2)
                {
                    list.Children.Add(new DocumentElement { Role = "listitem", Text = "Row 333" });
                }
            };
            page.Roots.Add(list);
        });

        var inList = await ScrollToAsync(World(), new { text = "Row 333", role = "list", name = "Ledger" });
        Assert.Equal(["Row 333"], inList.ScrolledIntoView);
        Assert.True(inList.ListSwipes >= 1);

        var onScreen = await ScrollToAsync(World(), new { text = "Row 333" });
        Assert.Equal(["Jump to Row 333"], onScreen.ScrolledIntoView);
        Assert.Equal(0, onScreen.ListSwipes + onScreen.ViewportSwipes);
    }

    [Fact]
    public async Task Keeps_paging_while_a_rows_text_moves_on_and_stops_when_it_stays_the_same()
    {
        var moving = await PageAsync(textMoves: 12);
        Assert.Contains("before the screen stopped moving", moving.Result, StringComparison.Ordinal);
        Assert.True(moving.Swipes >= 15);

        var still = await PageAsync(textMoves: 0);
        Assert.Contains("before the screen stopped moving", still.Result, StringComparison.Ordinal);
        Assert.True(still.Swipes <= 4);
    }

    private static async Task<ScrollLogEngine> ScrollToAsync(DocumentWorld world, object arguments)
    {
        var engine = new ScrollLogEngine(world);
        var calls = 0;
        var model = new ScriptedModel(_ => calls++ == 0 ? ModelResponses.Call("scroll_to", arguments) : ModelResponses.Done("passed", "scrolled"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, Model = model, BaseUrl = "https://ledger.test", TestTitle = "reading" });
        await session.App.OpenAsync("/ledger");
        await session.Agent.ActAsync("reach Row 333");
        session.Complete();
        return engine;
    }

    private static async Task<(string Result, int Swipes)> PageAsync(int textMoves)
    {
        var swipes = 0;
        var world = new DocumentWorld().Map("/products", page =>
        {
            var row = new DocumentElement { Role = "listitem", Name = "Product", Text = "Product 1" };
            page.Roots.Add(row);
            page.OnScroll = _ =>
            {
                swipes++;
                if (swipes <= textMoves)
                {
                    row.Text = "Product " + (swipes + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            };
        });
        string? result = null;
        var calls = 0;
        var model = new ScriptedModel(request =>
        {
            if (calls++ == 0)
            {
                return ModelResponses.Call("scroll_to", new { text = "Golden" });
            }

            result = request.Messages.Last(message => message.Role == "tool").Content;
            return ModelResponses.Done("passed", "gave up");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = new ScrollLogEngine(world), Model = model, BaseUrl = "https://shop.test", TestTitle = "reading shape" });
        await session.App.OpenAsync("/products");
        await session.Agent.ActAsync("reach the golden product");
        session.Complete();
        return (result ?? "", swipes);
    }
}
