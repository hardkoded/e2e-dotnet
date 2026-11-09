// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheMintedTestId;

/// <summary>
/// Upstream's <c>/graph</c> page of <c>tests/helpers/fixture-pages/home.ts</c>: a canvas whose nodes have only a random test id;
/// <c>?seeded</c> starts with one.
/// </summary>
internal static class GraphPage
{
    public const string Html = """
        <!doctype html>
        <html>
        <head><title>Graph</title></head>
        <body>
          <h1>Graph</h1>
          <button id="add">Add node</button>
          <p role="status" id="status">Empty canvas</p>
          <div id="canvas"></div>
          <script>
            const status = document.getElementById('status');
            function addNode() {
              const node = document.createElement('div');
              node.dataset.testid = 'rf__node-' + crypto.randomUUID();
              node.style.cssText = 'display:inline-block;width:60px;height:40px;margin:8px;background:#8ad';
              node.addEventListener('click', () => { status.textContent = 'Node selected'; });
              document.getElementById('canvas').append(node);
            }
            document.getElementById('add').addEventListener('click', () => { addNode(); status.textContent = 'Node added'; });
            if (new URLSearchParams(location.search).has('seeded')) { addNode(); status.textContent = 'One node'; }
          </script>
        </body>
        </html>
        """;

    /// <summary>Serves the page at <c>/graph</c>, whatever the query.</summary>
    public static Task<TinySite> StartAsync() =>
        TinySite.StartAsync(context => context.Request.Url!.AbsolutePath == "/graph"
            ? TinySite.RespondAsync(context, Html)
            : NotFoundAsync(context));

    private static Task NotFoundAsync(System.Net.HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        context.Response.Close();
        return Task.CompletedTask;
    }
}
