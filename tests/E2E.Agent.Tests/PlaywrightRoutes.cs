// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.Playground;

/// <summary>
/// The routes of the tests ported from Playwright, a port of upstream's <c>apps/testbed/app/playwright/routes.mjs</c>:
/// its test assets (copied from microsoft/playwright <c>tests/assets</c> at v1.64.1, Apache-2.0, under
/// <c>Testbed/playwright/assets</c> next to the test assembly) and the dynamic routes its test server sets per test.
/// The playground serves them for any path it has no page for. This port has <c>/content</c>, <c>/never</c>, <c>/download</c>, <c>/respond</c>, <c>/binary</c>, <c>/upload</c> and the assets so far.
/// </summary>
internal static class PlaywrightRoutes
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Testbed", "playwright", "assets"));

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

        // Download routes (group: dialog and download).
        var query = context.Request.QueryString;
        if (path == "/download")
        {
            response.ContentType = "application/octet-stream";
            response.AddHeader("Content-Disposition", "attachment" + (query["name"] is { } name ? "; filename=\"" + name.Replace("\"", "", StringComparison.Ordinal).ReplaceLineEndings("") + "\"" : ""));
            await WriteAsync(response, Encoding.UTF8.GetBytes(query["body"] ?? "Hello world")).ConfigureAwait(false);
            return;
        }

        if (path == "/respond")
        {
            try
            {
                response.StatusCode = int.TryParse(query["status"], CultureInfo.InvariantCulture, out var status) ? status : 200;
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(query["headers"] ?? "{}") ?? [];
                foreach (var (header, value) in headers)
                {
                    if (header.Equals("content-type", StringComparison.OrdinalIgnoreCase))
                    {
                        response.ContentType = value;
                    }
                    else
                    {
                        response.AddHeader(header, value);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or ProtocolViolationException)
            {
                response.StatusCode = 400;
                response.ContentType = "text/plain";
                await WriteAsync(response, Encoding.UTF8.GetBytes("Bad headers: " + ex.Message)).ConfigureAwait(false);
                return;
            }

            await WriteAsync(response, Encoding.UTF8.GetBytes(query["body"] ?? "")).ConfigureAwait(false);
            return;
        }

        if (path == "/binary")
        {
            var length = Math.Clamp(int.TryParse(query["bytes"], CultureInfo.InvariantCulture, out var bytes) ? bytes : 1 << 20, 0, 16 << 20);
            var data = new byte[length];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(((uint)(i + 1) * 2654435761u) >> 24);
            }

            response.ContentType = "application/zip";
            await WriteAsync(response, data).ConfigureAwait(false);
            return;
        }

        if (path == "/upload")
        {
            await UploadAsync(context).ConfigureAwait(false);
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

    /// <summary>Answers a multipart form with the files it carried as JSON, as upstream's <c>/upload</c>: name, filename, mimetype, size, and the bytes (base64) of a file up to 64 KB.</summary>
    private static async Task UploadAsync(HttpListenerContext context)
    {
        using var received = new MemoryStream();
        await context.Request.InputStream.CopyToAsync(received).ConfigureAwait(false);
        var data = received.GetBuffer().AsMemory(0, (int)received.Length);
        var type = context.Request.ContentType ?? "";
        var marker = type.IndexOf("boundary=", StringComparison.Ordinal);
        var boundary = Encoding.UTF8.GetBytes("--" + (marker < 0 ? "" : type[(marker + "boundary=".Length)..]));
        var files = new List<Dictionary<string, object?>>();
        var start = data.Span.IndexOf(boundary);
        while (start >= 0)
        {
            var afterStart = start + boundary.Length;
            var relative = data.Span[afterStart..].IndexOf(boundary);
            if (relative < 0)
            {
                break;
            }

            var next = afterStart + relative;
            var part = data[(afterStart + 2)..(next - 2)];
            var split = part.Span.IndexOf("\r\n\r\n"u8);
            if (split < 0)
            {
                start = next;
                continue;
            }

            var head = Encoding.UTF8.GetString(part.Span[..split]);
            var body = part[(split + 4)..];
            files.Add(new Dictionary<string, object?>
            {
                ["name"] = Capture(head, "name=\"([^\"]*)\""),
                ["filename"] = Capture(head, "filename=\"([^\"]*)\""),
                ["mimetype"] = Capture(head, "content-type: ([^\r\n]+)"),
                ["size"] = body.Length,
                ["base64"] = body.Length <= 65536 ? Convert.ToBase64String(body.Span) : null,
            });
            start = next;
        }

        context.Response.ContentType = "text/plain";
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");
        await WriteAsync(context.Response, JsonSerializer.SerializeToUtf8Bytes(files)).ConfigureAwait(false);
    }

    private static string? Capture(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.IgnoreCase) is { Success: true } match ? match.Groups[1].Value : null;

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
