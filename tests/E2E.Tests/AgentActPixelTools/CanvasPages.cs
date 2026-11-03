// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// Upstream's <c>tests/helpers/fixture-pages/canvas.ts</c>: surfaces the semantic tree cannot describe, drawn pixels and pointer
/// geometry. <c>/canvas</c> is a drawn map with two pins and a Reset button, a Note field, a Pad widget, and a Tint select under it;
/// <c>/canvas-bare</c> is the same map with nothing the tree can act on; <c>/popover</c> is a popover over a full-screen dismiss control.
/// <c>/</c> is an ordinary page with one listed control, standing in for upstream's home page.
/// </summary>
internal static class CanvasPages
{
    private const string Controls = """
          <button id="reset" style="position:fixed;left:0;top:260px;width:100px;height:30px">Reset</button>
          <input id="note" aria-label="Note" style="position:fixed;left:0;top:300px;width:200px;height:30px;margin:0;padding:0;border:1px solid #000">
          <div id="pad" role="application" aria-label="Pad" tabindex="0" style="position:fixed;left:0;top:380px;width:200px;height:30px;border:1px solid #000"></div>
          <select id="tint" aria-label="Tint" style="position:fixed;left:0;top:340px;width:200px;height:30px;margin:0">
            <option>plain</option><option>warm</option><option>cool</option>
          </select>
          <script>
            document.getElementById('reset').addEventListener('click', () => {
              document.getElementById('hit').textContent = 'reset';
            });
            document.getElementById('note').addEventListener('keydown', (event) => {
              if (event.key === 'Enter') document.getElementById('hit').textContent = 'note: ' + event.target.value;
            });
            document.getElementById('tint').addEventListener('change', (event) => {
              document.getElementById('hit').textContent = 'tint: ' + event.target.value;
            });
            // A listed but unfillable widget: focusable, with its own key handling.
            let padText = '';
            document.getElementById('pad').addEventListener('keydown', (event) => {
              event.preventDefault();
              if (event.key === 'Enter') document.getElementById('hit').textContent = 'pad: ' + padText;
              else if (event.key.length === 1) padText += event.key;
            });
            // A drawn field: the canvas takes focus on click and collects keystrokes itself, the way a canvas-rendered
            // text input does. Nothing in the tree is editable, so only the keyboard path can reach it.
            const map = document.getElementById('map');
            map.tabIndex = 0;
            map.contentEditable = 'true';
            map.style.outline = 'none';
            let drawn = '';
            map.addEventListener('keydown', (event) => {
              event.preventDefault();
              if (event.key === 'Enter') document.getElementById('hit').textContent = 'drawn: ' + drawn;
              else if (event.key.length === 1) drawn += event.key;
            });
          </script>
        """;

    private const string Map = """
          <script>
            const canvas = document.getElementById('map');
            const context = canvas.getContext('2d');
            const pins = [
              { name: 'red', x: 300, y: 60, color: '#ff0000' },
              { name: 'blue', x: 80, y: 140, color: '#0000ff' },
            ];
            // A tap count drawn on the map: every click repaints it, and nothing in the tree lists it, the way a
            // canvas-rendered widget shows its own state.
            let taps = 0;
            const paint = () => {
              context.fillStyle = '#dddddd';
              context.fillRect(0, 0, 400, 200);
              for (const pin of pins) {
                context.fillStyle = pin.color;
                context.beginPath();
                context.arc(pin.x, pin.y, 14, 0, Math.PI * 2);
                context.fill();
              }
              if (taps > 0) {
                context.fillStyle = '#000000';
                context.fillText('taps: ' + taps, 4, 194);
              }
            };
            paint();
            // A drawn drag: a pin pressed and released away from itself moves there, the way a whiteboard moves a
            // shape. The release's own click is the drag's, not a tap.
            let held;
            let dragged = false;
            const at = (event) => {
              const box = canvas.getBoundingClientRect();
              return { x: Math.round(event.clientX - box.left), y: Math.round(event.clientY - box.top) };
            };
            canvas.addEventListener('pointerdown', (event) => {
              const point = at(event);
              held = pins.find((candidate) => Math.hypot(candidate.x - point.x, candidate.y - point.y) <= 18);
            });
            canvas.addEventListener('pointerup', (event) => {
              const point = at(event);
              if (held !== undefined && Math.hypot(held.x - point.x, held.y - point.y) > 18) {
                held.x = point.x;
                held.y = point.y;
                paint();
                document.getElementById('hit').textContent = 'moved ' + held.name + ' to ' + point.x + ',' + point.y;
                dragged = true;
              }
              held = undefined;
            });
            canvas.addEventListener('click', (event) => {
              if (dragged) {
                dragged = false;
                return;
              }
              const box = canvas.getBoundingClientRect();
              const x = event.clientX - box.left;
              const y = event.clientY - box.top;
              const pin = pins.find((candidate) => Math.hypot(candidate.x - x, candidate.y - y) <= 18);
              document.getElementById('hit').textContent = pin ? pin.name : 'miss at ' + Math.round(x) + ',' + Math.round(y);
              taps += 1;
              paint();
            });
          </script>
        """;

    private static string CanvasMap(bool reset) =>
        $$"""
        <!doctype html>
        <html>
        <head><title>{{(reset ? "Canvas map" : "Bare canvas map")}}</title></head>
        <body style="margin:0">
          <canvas id="map" width="400" height="200" style="position:fixed;left:0;top:0"></canvas>
          <output id="hit" role="status" aria-label="Hit" style="position:fixed;left:0;top:220px">none</output>
        {{(reset ? Controls : "")}}{{Map}}
        </body>
        </html>
        """;

    /// <summary>A map whose pins exist only as pixels, with a Reset button, a Note field, a Pad, and a Tint select under it.</summary>
    public static string Canvas { get; } = CanvasMap(reset: true);

    /// <summary>The same map with nothing the tree can act on: a screen like this is "thin" to the agent, which then opens with a screenshot.</summary>
    public static string CanvasBare { get; } = CanvasMap(reset: false);

    /// <summary>
    /// A popover over a full-screen dismiss control, the way iOS presents one: every point outside the popover is inside the dismiss
    /// control, whose center, (640, 360) in the runner's 1280 by 720 viewport, is under the popover's own button.
    /// </summary>
    public const string Popover = """
        <!doctype html>
        <html>
        <head><title>Popover</title></head>
        <body style="margin:0">
          <output id="hit" role="status" aria-label="Hit">open</output>
          <button id="dismiss" aria-label="Dismiss popover" style="position:fixed;inset:0;width:100%;height:100%;z-index:1"></button>
          <div role="dialog" aria-label="Calendar" style="position:fixed;left:540px;top:260px;width:200px;height:200px;z-index:2">
            <button id="day" style="width:200px;height:200px">15</button>
          </div>
          <script>
            document.getElementById('dismiss').addEventListener('click', () => {
              document.getElementById('hit').textContent = 'dismissed';
            });
            document.getElementById('day').addEventListener('click', () => {
              document.getElementById('hit').textContent = 'picked 15';
            });
          </script>
        </body>
        </html>
        """;

    /// <summary>An ordinary page with one listed control, so the opening look is tree-only.</summary>
    public const string Home = """
        <!doctype html>
        <html>
        <head><title>Home</title></head>
        <body>
          <h1>Home</h1>
          <button>Continue</button>
          <label>Password <input type="password" name="password"></label>
        </body>
        </html>
        """;

    /// <summary>Serves the pages by path.</summary>
    public static Task<TinySite> StartAsync() =>
        TinySite.StartAsync(context => TinySite.RespondAsync(
            context,
            context.Request.Url!.AbsolutePath switch
            {
                "/canvas" => Canvas,
                "/canvas-bare" => CanvasBare,
                "/popover" => Popover,
                _ => Home,
            }));
}
