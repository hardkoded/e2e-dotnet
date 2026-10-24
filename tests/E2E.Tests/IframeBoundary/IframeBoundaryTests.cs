// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.IframeBoundary;

// .NET-only: upstream has no test for an iframe with a presentational role.
[Collection(BrowserCollection.Name)]
public sealed class IframeBoundaryTests
{
    [Fact]
    public async Task Lists_an_iframe_with_a_presentational_role_as_a_boundary_node_with_its_document_under_it()
    {
        using var site = await TinySite.StartAsync("""
            <!DOCTYPE html><html><body>
            <iframe role="none" title="Payment" srcdoc="<button>Pay</button>"></iframe>
            <iframe role="presentation" title="Refunds" srcdoc="<button>Refund</button>"></iframe>
            <iframe title="Plain" srcdoc="<button>Plain</button>"></iframe>
            </body></html>
            """);
        var session = await WebSemanticsTests.StartSessionAsync(site.Url);
        await WebSemanticsTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            var nodes = WebSemanticsTests.Flatten((await session.Engine.ObserveAsync(CancellationToken.None)).Roots).ToList();
            var frames = nodes.Where(node => node.Role is "none" or "presentation" or "iframe").ToDictionary(node => node.Name!);
            Assert.Equal("none", frames["Payment"].Role);
            Assert.Equal("presentation", frames["Refunds"].Role);
            Assert.Equal("iframe", frames["Plain"].Role);
            Assert.Equal(new[] { "Pay", "Refund", "Plain" }, nodes.Where(node => node.Role == "button").Select(node => node.Name!).ToArray());
        });
    }
}
