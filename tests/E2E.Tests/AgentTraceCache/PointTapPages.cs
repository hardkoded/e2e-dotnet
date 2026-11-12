// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentActPixelTools;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// The pages of the point-tap trace cache tests: upstream's <c>/canvas</c> and its <c>/repeats</c>, a control repeated per row, as a
/// listing repeats one reservation button. Every query derived from any row matches all of them. The <c>b</c> variant serves the same
/// three offers in the opposite order: a warm run replays an entry recorded against a page whose rows have since reordered. <c>?still=1</c>
/// leaves out the widget that upstream appends at body level, so no row moves after load.
/// </summary>
internal sealed class PointTapPages : IDisposable
{
    private readonly TinySite _site;

    private PointTapPages(TinySite site) => _site = site;

    /// <summary>The variant every page renders as from now on, or null for the plain one (upstream's <c>setVariant</c>).</summary>
    public string? Variant { get; set; }

    public string Url => _site.Url;

    public static async Task<PointTapPages> StartAsync()
    {
        PointTapPages? pages = null;
        var site = await TinySite.StartAsync(context => TinySite.RespondAsync(
            context,
            context.Request.Url!.AbsolutePath switch
            {
                "/canvas" => CanvasPages.Canvas,
                "/repeats" => Repeats(pages!.Variant == "b"),
                _ => CanvasPages.Home,
            }));
        pages = new PointTapPages(site);
        return pages;
    }

    private static string Repeats(bool reverse)
    {
        var rows = string.Join(
            "\n",
            (reverse ? new[] { "C", "B", "A" } : ["A", "B", "C"]).Select(offer =>
                $"    <li><span>Offer {offer}</span> <button name=\"reserve-{offer.ToLowerInvariant()}\" onclick=\"pick('{offer}')\">Reserve now</button></li>"));
        return $$"""
            <!doctype html>
            <html>
            <head><title>Repeats</title></head>
            <body style="margin:0">
              <output id="picked" role="status" aria-label="Picked">none</output>
              <ul style="list-style:none;padding:0">
            {{rows}}
              </ul>
              <script>
                function pick(offer) {
                  document.getElementById('picked').textContent = offer;
                }
              </script>
            </body>
            </html>
            """;
    }

    public void Dispose() => _site.Dispose();
}
