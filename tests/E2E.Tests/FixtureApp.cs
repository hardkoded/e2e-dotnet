// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Web;

namespace E2E.Tests;

/// <summary>The pages of upstream's <c>tests/helpers/fixture-app.ts</c> that the CDP, media, and scroll tests use.</summary>
internal static class FixtureApp
{
    private const string Login = """
        <!doctype html>
        <html>
        <head><title>Fixture Login</title></head>
        <body style="margin:0;background:#fff">
        <h1>Login</h1>
        <label>User <input name="user" value="ada" style="width:200px;height:40px;background:#fff;border:1px solid #fff"></label>
        <label>Password <input type="password" name="password" style="width:200px;height:40px;background:#fff;border:1px solid #fff"></label>
        </body>
        </html>
        """;

    private const string Form = """
        <!doctype html>
        <html>
        <head><title>Fixture Form</title></head>
        <body>
        <h1>Form</h1>
        <label>First <input name="first" value="alpha"></label>
        <label>Second <input name="second" value="beta"></label>
        <label>Third <input name="third" value="alpha"></label>
        </body>
        </html>
        """;

    // Styled by the media features the browser reports, and recording what a script in the head read before
    // first paint, so a test can tell an emulation the page loaded with from one applied after.
    private const string Media = """
        <!doctype html>
        <html>
        <head>
        <title>Fixture Media</title>
        <style>
          p { color: rgb(0, 0, 0); transition-property: opacity; transition-duration: 0.3s; }
          @media (prefers-color-scheme: dark) { p { color: rgb(255, 255, 255); } }
          @media (prefers-reduced-motion: reduce) { p { transition-duration: 0s; } }
        </style>
        <script>
          window.atLoad = { dark: matchMedia('(prefers-color-scheme: dark)').matches, reduce: matchMedia('(prefers-reduced-motion: reduce)').matches };
        </script>
        </head>
        <body><p>media</p></body>
        </html>
        """;

    private const int ScrollRows = 300;

    private const int ScrollRowHeight = 60;

    /// <summary>
    /// Upstream's <c>SCROLL_PAGES['/smooth-scroll']</c> (<c>fixture-pages/scroll.ts</c>): a windowed list whose scrolling animates after
    /// the wheel event, as a smooth-scroll library does. <c>?smooth=js</c> eases the document to the wheel's target over <c>?ms=</c> (450
    /// by default) with <c>requestAnimationFrame</c>, the way Lenis-style libraries do; <c>?smooth=css</c> hands the target to
    /// <c>scrollTo</c> under <c>scroll-behavior: smooth</c>. Without either the wheel scrolls natively. Rows draw their text only once
    /// scrolled into view, as a windowed list does, so a row far down is not in the tree until the page reaches it.
    /// </summary>
    private static string SmoothScroll(Uri url)
    {
        var query = HttpUtility.ParseQueryString(url.Query);
        var smooth = query["smooth"] ?? "none";
        var duration = double.Parse(query["ms"] ?? "450", CultureInfo.InvariantCulture);
        var rows = string.Concat(Enumerable.Range(0, ScrollRows).Select(at => $"<li data-row=\"Row {at}\"></li>"));
        return $$"""
            <!doctype html>
            <html>
            <head><title>Smooth scroll</title>
            <style>
              html { {{(smooth == "css" ? "scroll-behavior: smooth;" : "")}} }
              body { margin: 0; }
              ul { margin: 0; padding: 0; list-style: none; }
              li { height: {{ScrollRowHeight}}px; box-sizing: border-box; border-bottom: 1px solid #ddd; }
            </style>
            </head>
            <body>
            <h1>Smooth scroll</h1>
            <ul aria-label="Rows">{{rows}}</ul>
            <script>
              const observer = new IntersectionObserver((entries) => {
                for (const entry of entries) {
                  if (!entry.isIntersecting) continue;
                  entry.target.textContent = entry.target.dataset.row;
                  observer.unobserve(entry.target);
                }
              });
              for (const row of document.querySelectorAll('li')) observer.observe(row);
              const mode = {{JsonSerializer.Serialize(smooth)}};
              if (mode !== 'none') {
                let target = 0;
                let frame = 0;
                const limit = () => document.documentElement.scrollHeight - innerHeight;
                addEventListener('wheel', (event) => {
                  event.preventDefault();
                  target = Math.min(limit(), Math.max(0, target + event.deltaY));
                  if (mode === 'css') {
                    scrollTo({ top: target });
                    return;
                  }
                  const from = scrollY;
                  const started = performance.now();
                  cancelAnimationFrame(frame);
                  const step = (now) => {
                    const done = Math.min(1, (now - started) / {{duration.ToString(CultureInfo.InvariantCulture)}});
                    scrollTo(0, from + (target - from) * (1 - (1 - done) ** 3));
                    if (done < 1) frame = requestAnimationFrame(step);
                  };
                  frame = requestAnimationFrame(step);
                }, { passive: false });
              }
            </script>
            </body>
            </html>
            """;
    }

    /// <summary>Starts the fixture app: <c>/login</c>, <c>/form</c>, <c>/media</c>, and <c>/smooth-scroll</c>.</summary>
    public static Task<TinySite> StartAsync()
    {
        return TinySite.StartAsync(context => TinySite.RespondAsync(context, context.Request.Url!.AbsolutePath switch
        {
            "/form" => Form,
            "/media" => Media,
            "/smooth-scroll" => SmoothScroll(context.Request.Url!),
            _ => Login,
        }));
    }
}
