// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace E2E.Playground;

/// <summary>
/// The routes of the tests ported from Playwright, a port of upstream's <c>apps/testbed/app/playwright/routes.mjs</c>:
/// its test assets (copied from microsoft/playwright <c>tests/assets</c> at v1.64.1, Apache-2.0, under
/// <c>Testbed/playwright/assets</c> next to the test assembly) and the dynamic routes its test server sets per test.
/// The playground serves them for any path it has no page for. This port has <c>/content</c>, <c>/never</c> and the assets so far.
/// </summary>
internal static class PlaywrightRoutes
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Testbed", "playwright", "assets");

    /// <summary>Cancelled when the process exits, which releases the requests a route holds.</summary>
    private static readonly CancellationTokenSource Shutdown = CreateShutdown();

    private static readonly Dictionary<string, string> Types = new(StringComparer.Ordinal)
    {
        [".html"] = "text/html",
        [".js"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".pdf"] = "application/pdf",
        [".svg"] = "image/svg+xml",
        [".txt"] = "text/plain",
    };

    /// <summary>Serves one request: a dynamic route, else an asset, else a 404.</summary>
    public static async Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;
        var path = context.Request.Url!.AbsolutePath;
        if (path == "/content")
        {
            var html = context.Request.QueryString["html"] ?? "";
            response.ContentType = "text/html; charset=utf-8";
            await WriteAsync(response, FromBase64Url(html)).ConfigureAwait(false);
            return;
        }

        if (path == "/never")
        {
            // Holds the response and never answers, as upstream's /never. It ends when the run ends (the testbed never stops before that)
            // or after upstream's longest delay, so a held request cannot outlive the run.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), Shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run is ending.
            }

            response.Abort();
            return;
        }

        var file = Path.GetFullPath(Path.Combine(Root, Uri.UnescapeDataString(path).TrimStart('/')));
        if (!file.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
        {
            response.StatusCode = 404;
            response.ContentType = "text/plain";
            await WriteAsync(response, Encoding.UTF8.GetBytes("Not found")).ConfigureAwait(false);
            return;
        }

        response.ContentType = Types.GetValueOrDefault(Path.GetExtension(file), "application/octet-stream");
        response.AddHeader("Cache-Control", "no-store");
        await WriteAsync(response, await File.ReadAllBytesAsync(file).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static CancellationTokenSource CreateShutdown()
    {
        var source = new CancellationTokenSource();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => source.Cancel();
        return source;
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        var bytes = new byte[padded.Length];
        return Convert.TryFromBase64String(padded, bytes, out var written) ? bytes[..written] : [];
    }

    private static async Task WriteAsync(HttpListenerResponse response, byte[] bytes)
    {
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}
