// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ScreenshotCaret;

/// <summary>The pages of upstream's fixture app (<c>tests/helpers/fixture-app.ts</c>) the screenshot caret tests open.</summary>
internal static class FixtureApp
{
    /// <summary>
    /// A form for the screenshot caret test: light-DOM editables, a secure field, an editable inside a closed shadow
    /// root whose own stylesheet asks for a red caret, and an iframe holding one more input.
    /// </summary>
    private const string Caret = """
        <!doctype html>
        <html>
        <head><title>Fixture Caret</title></head>
        <body style="margin:0;background:#fff">
        <input id="plain" value="plain" style="width:120px;height:20px">
        <textarea id="notes" style="width:120px;height:40px">notes</textarea>
        <input id="secret" type="password" value="secret" style="width:120px;height:20px">
        <x-caret id="caret-host"></x-caret>
        <iframe id="framed-page" srcdoc='<input id="framed" value="framed" style="width:120px;height:20px;background:#fff">'></iframe>
        <script>
          const root = document.getElementById('caret-host').attachShadow({ mode: 'closed' });
          root.innerHTML = '<style>input { caret-color: rgb(255, 0, 0); }</style><input id="shadowed" value="shadow">';
        </script>
        </body>
        </html>
        """;

    /// <summary>A page whose policy blocks <c>&lt;style&gt;</c> elements, for the caret stylesheet test.</summary>
    private const string CaretCsp = """
        <!doctype html>
        <html>
        <head><title>Fixture Caret CSP</title><meta http-equiv="Content-Security-Policy" content="style-src 'none'"></head>
        <body><input id="plain" value="csp"></body>
        </html>
        """;

    /// <summary>Starts the app: <c>/caret</c> and <c>/caret-csp</c>.</summary>
    public static Task<TinySite> StartAsync()
    {
        return TinySite.StartAsync(context => TinySite.RespondAsync(context, context.Request.Url!.AbsolutePath == "/caret-csp" ? CaretCsp : Caret));
    }
}
