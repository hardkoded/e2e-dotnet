// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.KeyboardShadowFocus;

/// <summary>Upstream's <c>focusShadow</c> of keyboard-shadow-focus.test.ts: two nested shadow roots whose leaf takes focus and counts its keys.</summary>
internal static class ShadowFocusPage
{
    private const string Focus = """
        ({ mode, html }) => {
          document.body.innerHTML = '<div id="outer"></div>';
          const outer = document.getElementById('outer').attachShadow({ mode });
          outer.innerHTML = '<div id="inner"></div>';
          const inner = outer.getElementById('inner').attachShadow({ mode });
          inner.innerHTML = html;
          const field = inner.querySelector('input,button,[tabindex]');
          field.focus();
          field.addEventListener('keydown', () => { document.body.dataset.keys = String(Number(document.body.dataset.keys ?? 0) + 1); });
          field.addEventListener('input', () => { document.body.dataset.value = field.value; });
        }
        """;

    internal static async Task<(IEngineSession Session, IPage Page)> OpenAsync()
    {
        var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        try
        {
            return (session, WebEngine.SurfaceOf(session)!.Page());
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    internal static Task FocusShadowAsync(IPage page, string shadowMode, string markup) =>
        page.EvaluateAsync(Focus, new { mode = shadowMode, html = markup });

    internal static Task<string?> AttributeAsync(IPage page, string name) => page.GetAttributeAsync("body", name);
}
