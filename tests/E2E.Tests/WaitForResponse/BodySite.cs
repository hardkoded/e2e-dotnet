// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace E2E.Tests.WaitForResponse;

/// <summary>
/// One origin with a body for every outcome <c>WaitForResponseAsync</c> reports: a
/// full JSON body, a genuinely empty body under 200 and 204, a redirect whose body
/// the browser drops, a body cut short by a closed connection before the declared
/// <c>Content-Length</c> was sent, one whose body follows its headers 800ms later,
/// and one that never finishes. The fragment is flushed before the close so the
/// browser has seen the headers and reports the response rather than an empty
/// reply. <c>/api/held</c> sends nothing until <see cref="ReleaseHeld"/>, and <c>/api/orders</c> answers with the method it saw, so a
/// POST reads as 201. It writes raw HTTP, so headers go out before the body is ready.
/// </summary>
internal sealed class BodySite : IDisposable
{
    private readonly TcpListener _listener;
    private readonly ConcurrentBag<TcpClient> _clients = [];
    private readonly ConcurrentQueue<TcpClient> _held = [];

    private BodySite(TcpListener listener)
    {
        _listener = listener;
        Origin =
            "http://127.0.0.1:"
            + ((IPEndPoint)listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The site's origin, with no trailing slash.</summary>
    public string Origin { get; }

    /// <summary>Requests to <c>/api/held</c> whose response has not started.</summary>
    public int HeldCount => _held.Count;

    public static BodySite Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var site = new BodySite(listener);
        _ = Task.Run(site.ListenAsync);
        return site;
    }

    /// <summary>Answers every held request.</summary>
    public void ReleaseHeld()
    {
        while (_held.TryDequeue(out var client))
        {
            try
            {
                _ = EndAsync(client, "200 OK", "content-type: text/plain\r\n", "released");
            }
            catch (Exception)
            {
                client.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }

    private async Task ListenAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                return;
            }

            _clients.Add(client);
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var (method, path, _) = await ReadRequestAsync(stream);
            switch (path)
            {
                case "/api/held":
                    // No headers until ReleaseHeld.
                    _held.Enqueue(client);
                    return;
                case "/api/orders":
                    await EndAsync(
                        client,
                        method == "POST" ? "201 Created" : "200 OK",
                        "content-type: application/json\r\n",
                        "{\"method\":\"" + method + "\"}"
                    );
                    return;
                case "/":
                    await EndAsync(
                        client,
                        "200 OK",
                        "content-type: text/html\r\n",
                        "<!doctype html><title>Bodies</title><h1>Bodies</h1>"
                    );
                    return;
                case "/api/full":
                    await EndAsync(client, "200 OK", "content-type: application/json\r\n", "{\"ok\":true}");
                    return;
                case "/api/empty":
                    await EndAsync(client, "200 OK", "content-type: text/plain\r\n", "");
                    return;
                case "/api/no-content":
                    await WriteAsync(stream, "HTTP/1.1 204 No Content\r\nconnection: close\r\n\r\n");
                    client.Close();
                    return;
                case "/api/redirect":
                    await EndAsync(client, "302 Found", "location: /api/full\r\ncontent-type: text/plain\r\n", "moved");
                    return;
                case "/api/slow":
                    await WriteAsync(
                        stream,
                        "HTTP/1.1 200 OK\r\ncontent-type: text/plain\r\nconnection: close\r\n\r\n"
                    );
                    await Task.Delay(800);
                    await WriteAsync(stream, "slow-body");
                    client.Close();
                    return;
                case "/api/endless":
                    await WriteAsync(
                        stream,
                        "HTTP/1.1 200 OK\r\ncontent-type: text/plain\r\nconnection: close\r\n\r\na first chunk and never the rest"
                    );
                    return;
                case "/api/cut":
                    await WriteAsync(
                        stream,
                        "HTTP/1.1 200 OK\r\ncontent-type: application/json\r\ncontent-length: 1000\r\nconnection: close\r\n\r\n{\"partial\":"
                    );
                    await Task.Delay(50);
                    client.Close();
                    return;
                default:
                    await EndAsync(client, "404 Not Found", "", "");
                    return;
            }
        }
        catch (Exception)
        {
            client.Dispose();
        }
    }

    /// <summary>Reads the request line, and the body a request with a <c>content-length</c> declares.</summary>
    private static async Task<(string Method, string Path, string Body)> ReadRequestAsync(NetworkStream stream)
    {
        var request = new StringBuilder();
        var buffer = new byte[1024];
        int end;
        while ((end = request.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal)) < 0)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        var text = request.ToString();
        var head = end < 0 ? text : text[..end];
        var line = head.Split(' ');
        var body = end < 0 ? "" : text[(end + 4)..];
        var declared = System.Text.RegularExpressions.Regex.Match(
            head,
            @"content-length: (\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        while (declared.Success && body.Length < int.Parse(declared.Groups[1].Value))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            body += Encoding.ASCII.GetString(buffer, 0, read);
        }

        return (line[0], line[1], body);
    }

    private static async Task EndAsync(TcpClient client, string status, string headers, string body)
    {
        var length = Encoding.UTF8.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await WriteAsync(
            client.GetStream(),
            "HTTP/1.1 "
                + status
                + "\r\n"
                + headers
                + "content-length: "
                + length
                + "\r\nconnection: close\r\n\r\n"
                + body
        );
        client.Close();
    }

    private static async Task WriteAsync(NetworkStream stream, string text)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
        await stream.FlushAsync();
    }
}
