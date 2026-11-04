// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.Playground;

/// <summary>
/// The routes of the tests ported from Playwright, a port of upstream's <c>apps/testbed/app/playwright/routes.mjs</c>:
/// its test assets (copied from microsoft/playwright <c>tests/assets</c> at v1.64.1, Apache-2.0, under
/// <c>Testbed/playwright/assets</c> next to the test assembly) and the dynamic routes its test server sets per test.
/// The playground serves them for any path it has no page for. <c>127.0.0.1</c> and <c>localhost</c> on the playground's
/// port are the two origins of the cross-origin cases, and the only ones trusted.
/// </summary>
internal static partial class PlaywrightRoutes
{
    private const int MaxDelayMs = 30_000;
    private const int MaxBytes = 16 << 20;

    // .NET-only: HttpListener has no close event for a request, so a held request is released after this long.
    private static readonly TimeSpan HeldRequestLimit = TimeSpan.FromMinutes(2);

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Testbed", "playwright", "assets");

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

    private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, List<string>> Logs = new(StringComparer.Ordinal);

    /// <summary>Serves one request: a dynamic route, else an asset, else a 404.</summary>
    public static async Task HandleAsync(HttpListenerContext context)
    {
        if (context.Request.IsWebSocketRequest)
        {
            await UpgradeAsync(context).ConfigureAwait(false);
            return;
        }

        var request = context.Request;
        var response = context.Response;
        var port = request.LocalEndPoint.Port;
        var path = request.Url!.AbsolutePath;
        var query = ParseQuery(request.Url.Query);
        switch (path)
        {
            case "/content":
                response.ContentType = "text/html; charset=utf-8";
                await WriteAsync(response, FromBase64Url(query.GetValueOrDefault("html") ?? "")).ConfigureAwait(false);
                return;
            case "/echo":
                {
                    var received = await BodyAsync(request).ConfigureAwait(false);
                    response.ContentType = "application/json";
                    response.AddHeader("access-control-allow-origin", "*");
                    await WriteAsync(response, Echo(request, received)).ConfigureAwait(false);
                    return;
                }

            case "/status":
                response.StatusCode = Number(query.GetValueOrDefault("code"), 200);
                response.ContentType = "text/plain";
                await WriteAsync(response, Encoding.UTF8.GetBytes(query.GetValueOrDefault("body") ?? "")).ConfigureAwait(false);
                return;
            case "/slow":
                await Task.Delay(Bounded(query.GetValueOrDefault("ms"), 1000, MaxDelayMs)).ConfigureAwait(false);
                response.ContentType = query.GetValueOrDefault("type") ?? "text/plain";
                response.AddHeader("access-control-allow-origin", "*");
                await WriteAsync(response, Encoding.UTF8.GetBytes(query.GetValueOrDefault("body") ?? "slow")).ConfigureAwait(false);
                return;
            case "/never":
                await HoldAsync().ConfigureAwait(false);
                response.Abort();
                return;
            case "/redirect":
                response.StatusCode = 302;
                response.RedirectLocation = SameServer(query.GetValueOrDefault("to"), port);
                return;
            case "/download":
                response.ContentType = "application/octet-stream";
                response.AddHeader("content-disposition", $"attachment{(query.TryGetValue("name", out var name) ? $"; filename={name}" : "")}");
                await WriteAsync(response, Encoding.UTF8.GetBytes(query.GetValueOrDefault("body") ?? "Hello world")).ConfigureAwait(false);
                return;
            case "/set-cookie":
                response.AddHeader("set-cookie", query.GetValueOrDefault("cookie") ?? "name=value");
                response.ContentType = "text/html";
                await WriteAsync(response, Encoding.UTF8.GetBytes("<title>cookie</title>")).ConfigureAwait(false);
                return;
            case "/respond":
                response.StatusCode = Number(query.GetValueOrDefault("status"), 200);
                AddHeaders(response, query.GetValueOrDefault("headers") ?? "{}");
                await WriteAsync(response, Encoding.UTF8.GetBytes(query.GetValueOrDefault("body") ?? "")).ConfigureAwait(false);
                return;
            case "/cors-echo":
                {
                    response.AddHeader("access-control-allow-origin", TrustedOrigin(request, port));
                    response.AddHeader("access-control-allow-credentials", "true");
                    response.AddHeader("access-control-allow-methods", "POST, GET, OPTIONS, DELETE");
                    response.AddHeader("access-control-allow-headers", request.Headers["access-control-request-headers"] ?? "*");
                    if (request.HttpMethod == "OPTIONS" && query.ContainsKey("preflight"))
                    {
                        response.StatusCode = 204;
                        return;
                    }

                    var received = await BodyAsync(request).ConfigureAwait(false);
                    response.ContentType = "text/plain";
                    await WriteAsync(response, Echo(request, received)).ConfigureAwait(false);
                    return;
                }

            case "/bounce":
                {
                    var options = request.HttpMethod == "OPTIONS";
                    if (!options)
                    {
                        response.RedirectLocation = SameServer(query.GetValueOrDefault("to"), port);
                    }

                    response.AddHeader("access-control-allow-origin", TrustedOrigin(request, port));
                    response.AddHeader("access-control-allow-credentials", "true");
                    response.AddHeader("access-control-allow-headers", request.Headers["access-control-request-headers"] ?? "*");
                    if (query.TryGetValue("cookie", out var cookie))
                    {
                        response.AddHeader("set-cookie", cookie);
                    }

                    response.StatusCode = options ? 204 : Number(query.GetValueOrDefault("status"), 302);
                    return;
                }

            case "/count":
                {
                    var count = Counts.AddOrUpdate(query.GetValueOrDefault("key") ?? "", 1, (_, current) => current + 1);
                    response.ContentType = "text/plain";
                    await WriteAsync(response, Encoding.UTF8.GetBytes((count * 11).ToString(System.Globalization.CultureInfo.InvariantCulture))).ConfigureAwait(false);
                    return;
                }

            case "/options":
                {
                    var log = Logs.GetOrAdd(query.GetValueOrDefault("key") ?? "", _ => []);
                    lock (log)
                    {
                        log.Add($"{request.HttpMethod}:{path}");
                    }

                    var options = request.HttpMethod == "OPTIONS";
                    response.AddHeader("access-control-allow-origin", "*");
                    if (options)
                    {
                        response.AddHeader("access-control-allow-methods", "POST, GET, OPTIONS, DELETE");
                        response.AddHeader("access-control-allow-headers", "*");
                        response.AddHeader("cache-control", "no-cache");
                    }

                    await WriteAsync(response, Encoding.UTF8.GetBytes(options ? "Hello" : "World")).ConfigureAwait(false);
                    return;
                }

            case "/options-log":
                {
                    response.ContentType = "application/json";
                    var log = Logs.GetValueOrDefault(query.GetValueOrDefault("key") ?? "") ?? [];
                    string[] entries;
                    lock (log)
                    {
                        entries = [.. log];
                    }

                    await WriteAsync(response, JsonSerializer.SerializeToUtf8Bytes(entries)).ConfigureAwait(false);
                    return;
                }

            case "/upload":
                {
                    var received = await BodyAsync(request).ConfigureAwait(false);
                    response.ContentType = "text/plain";
                    response.AddHeader("access-control-allow-origin", "*");
                    await WriteAsync(response, JsonSerializer.SerializeToUtf8Bytes(Files(received, request.ContentType ?? ""))).ConfigureAwait(false);
                    return;
                }

            case "/binary":
                {
                    var data = new byte[Bounded(query.GetValueOrDefault("bytes"), 1 << 20, MaxBytes)];
                    for (var i = 0; i < data.Length; i++)
                    {
                        data[i] = (byte)(unchecked((uint)((i + 1) * 2654435761L)) >> 24);
                    }

                    response.ContentType = "application/zip";
                    await WriteAsync(response, data).ConfigureAwait(false);
                    return;
                }

            case "/download-stall":
                {
                    response.ContentType = "application/octet-stream";
                    response.AddHeader("content-disposition", "attachment; filename=file.txt");
                    response.SendChunked = true;
                    await response.OutputStream.WriteAsync(Encoding.ASCII.GetBytes(new string('a', 4096) + "foo")).ConfigureAwait(false);
                    await response.OutputStream.FlushAsync().ConfigureAwait(false);
                    await HoldAsync().ConfigureAwait(false);
                    response.Abort();
                    return;
                }

            case "/sse":
                {
                    response.ContentType = "text/event-stream";
                    response.AddHeader("cache-control", "no-cache");
                    response.SendChunked = true;
                    await response.OutputStream.WriteAsync(Encoding.ASCII.GetBytes("data: hello\n\n")).ConfigureAwait(false);
                    await response.OutputStream.FlushAsync().ConfigureAwait(false);
                    await HoldAsync().ConfigureAwait(false);
                    response.Abort();
                    return;
                }
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

    /// <summary>
    /// <c>/ws</c> accepts the handshake and holds the socket open, as Playwright's server does, so an open WebSocket is in the
    /// page during network-idle tests. Any other path is refused.
    /// </summary>
    private static async Task UpgradeAsync(HttpListenerContext context)
    {
        if (context.Request.Url!.AbsolutePath != "/ws")
        {
            context.Response.Abort();
            return;
        }

        var socket = (await context.AcceptWebSocketAsync(null).ConfigureAwait(false)).WebSocket;
        using var limit = new CancellationTokenSource(HeldRequestLimit);
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var received = await socket.ReceiveAsync(buffer, limit.Token).ConfigureAwait(false);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
        }
        finally
        {
            socket.Abort();
            socket.Dispose();
        }
    }

    /// <summary>A redirect target on this server (a path, or either origin), else the empty page.</summary>
    private static string SameServer(string? target, int port)
    {
        if (target is null)
        {
            return "/empty.html";
        }

        var origin = new Uri($"http://127.0.0.1:{port}");
        if (!Uri.TryCreate(origin, target, out var resolved) || resolved.Scheme != "http" || resolved.Port != port || resolved.Host is not ("127.0.0.1" or "localhost"))
        {
            return "/empty.html";
        }

        return target.StartsWith('/') ? resolved.PathAndQuery + resolved.Fragment : resolved.AbsoluteUri;
    }

    /// <summary>
    /// The request's origin when it is one of the server's own, the only ones granted credentials, or <c>null</c>,
    /// the origin a cross-origin redirect leaves.
    /// </summary>
    private static string TrustedOrigin(HttpListenerRequest request, int port)
    {
        var origin = request.Headers["origin"];
        if (origin == "null")
        {
            return "null";
        }

        return origin is not null && (origin == $"http://127.0.0.1:{port}" || origin == $"http://localhost:{port}") ? origin : $"http://127.0.0.1:{port}";
    }

    /// <summary>The request as JSON: <c>{ method, headers, body, url }</c>.</summary>
    private static byte[] Echo(HttpListenerRequest request, byte[] body)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in request.Headers.AllKeys)
        {
            if (name is not null)
            {
                headers[name.ToLowerInvariant()] = request.Headers[name]!;
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(new { method = request.HttpMethod, headers, body = Encoding.UTF8.GetString(body), url = request.RawUrl });
    }

    /// <summary>The headers of a JSON object, a header with an array value sent once for each item.</summary>
    private static void AddHeaders(HttpListenerResponse response, string json)
    {
        using var headers = JsonDocument.Parse(json);
        foreach (var header in headers.RootElement.EnumerateObject())
        {
            JsonElement[] values = header.Value.ValueKind == JsonValueKind.Array ? [.. header.Value.EnumerateArray()] : [header.Value];
            foreach (var value in values)
            {
                var text = value.ToString();
                switch (header.Name.ToLowerInvariant())
                {
                    case "content-type":
                        response.ContentType = text;
                        break;
                    case "location":
                        response.RedirectLocation = text;
                        break;
                    default:
                        response.AppendHeader(header.Name, text);
                        break;
                }
            }
        }
    }

    /// <summary>The parts of a multipart body, as <c>/upload</c> answers with them.</summary>
    private static List<object> Files(byte[] received, string type)
    {
        var files = new List<object>();
        var at = type.IndexOf("boundary=", StringComparison.Ordinal);
        var boundary = Encoding.UTF8.GetBytes("--" + (at >= 0 ? type[(at + "boundary=".Length)..] : ""));
        for (var start = IndexOf(received, boundary, 0); start != -1;)
        {
            var next = IndexOf(received, boundary, start + boundary.Length);
            if (next == -1)
            {
                break;
            }

            var part = received.AsSpan(start + boundary.Length + 2, next - 2 - (start + boundary.Length + 2));
            var split = part.IndexOf("\r\n\r\n"u8);
            var head = Encoding.UTF8.GetString(part[..split]);
            var data = part[(split + 4)..];
            files.Add(new
            {
                name = PartName().Match(head) is { Success: true } named ? named.Groups[1].Value : null,
                filename = PartFilename().Match(head) is { Success: true } filename ? filename.Groups[1].Value : null,
                mimetype = PartMimetype().Match(head) is { Success: true } mimetype ? mimetype.Groups[1].Value.Trim() : null,
                size = data.Length,
                base64 = data.Length <= 65536 ? Convert.ToBase64String(data) : null,
            });
            start = next;
        }

        return files;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from) =>
        from > haystack.Length ? -1 : haystack.AsSpan(from).IndexOf(needle) is var at and >= 0 ? from + at : -1;

    [GeneratedRegex("name=\"([^\"]*)\"")]
    private static partial Regex PartName();

    [GeneratedRegex("filename=\"([^\"]*)\"")]
    private static partial Regex PartFilename();

    [GeneratedRegex("content-type: (.+)", RegexOptions.IgnoreCase)]
    private static partial Regex PartMimetype();

    /// <summary><paramref name="value"/> as a number in [0, <paramref name="max"/>], <paramref name="fallback"/> when absent.</summary>
    private static int Bounded(string? value, int fallback, int max) =>
        Math.Min(Math.Max(Number(value, fallback), 0), max);

    private static int Number(string? value, int fallback) =>
        value is null ? fallback : int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            values.TryAdd(Uri.UnescapeDataString(parts[0].Replace('+', ' ')), parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "");
        }

        return values;
    }

    private static async Task<byte[]> BodyAsync(HttpListenerRequest request)
    {
        using var body = new MemoryStream();
        await request.InputStream.CopyToAsync(body).ConfigureAwait(false);
        return body.ToArray();
    }

    private static Task HoldAsync() => Task.Delay(HeldRequestLimit);

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
