// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace E2E.OAuth;

/// <summary>
/// The one-shot local HTTP server a browser login redirects back to. It accepts one answer for the expected
/// state, a code or the vendor's refusal, replies to the browser with a page, and hands the answer to the flow.
/// When the port is taken, <see cref="TryStart"/> returns null and the flow falls back to a pasted code.
/// It listens on the IPv4 loopback address only, like upstream. <c>HttpListener</c> binds <c>localhost</c> to
/// <c>::1</c>, and on macOS a connection reset before accept then throws an unhandled exception in the runtime
/// (dotnet/runtime#121848).
/// </summary>
internal sealed class CallbackServer : IDisposable
{
    private const int MaxRequestHeadBytes = 16 * 1024;
    private readonly TimeSpan _requestTimeout;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly string _path;
    private readonly string _state;
    private readonly string _product;
    private readonly TaskCompletionSource<(string? Code, string? Error)> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CallbackServer(TcpListener listener, int port, string path, string state, string product, TimeSpan requestTimeout)
    {
        _requestTimeout = requestTimeout;
        _listener = listener;
        _path = path;
        _state = state;
        _product = product;
        RedirectUri = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + path;
    }

    /// <summary>Vendors register <c>localhost</c>, not the loopback address, as the redirect host.</summary>
    public string RedirectUri { get; }

    public static CallbackServer? TryStart(int port, string path, string state, string product, TimeSpan? requestTimeout = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        if (OperatingSystem.IsWindows())
        {
            // Windows lets a second process share a port unless it is exclusive; the code must reach only this server.
            listener.ExclusiveAddressUse = true;
        }

        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            listener.Stop();
            return null;
        }

        var server = new CallbackServer(listener, port, path, state, product, requestTimeout ?? TimeSpan.FromSeconds(10));
        _ = server.ServeAsync();
        return server;
    }

    /// <summary>The browser's answer: a code, or the vendor's error. Throws <see cref="OAuthException.Timeout"/> after <paramref name="timeout"/>.</summary>
    public async Task<(string? Code, string? Error)> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await _answer.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new OAuthException(OAuthException.Timeout, "the browser did not return in time", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new OAuthException(OAuthException.Cancelled, "the login was cancelled", ex);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }

    private async Task ServeAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
            {
                // A connection reset before accept; the next one may be fine.
                continue;
            }
            catch (SocketException)
            {
                return;
            }

            // One task per connection: a browser may open a connection and send nothing.
            _ = ServeConnectionAsync(client, token);
        }
    }

    private async Task ServeConnectionAsync(TcpClient client, CancellationToken stopToken)
    {
        try
        {
            using (client)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
                timeout.CancelAfter(_requestTimeout);
                var stream = client.GetStream();
                if (await ReadRequestTargetAsync(stream, timeout.Token).ConfigureAwait(false) is { } target)
                {
                    await HandleAsync(stream, target, timeout.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // The browser went away or the server stopped; an answer already set still counts.
        }
    }

    /// <summary>The request target of the first line, or null when the head is not a well-formed HTTP request.</summary>
    private static async Task<string?> ReadRequestTargetAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxRequestHeadBytes];
        var length = 0;
        // A head that does not end within the buffer is refused, not read as far as it goes.
        while (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) < 0)
        {
            if (length == buffer.Length)
            {
                return null;
            }

            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            length += read;
        }

        var head = Encoding.ASCII.GetString(buffer, 0, length);
        var lineEnd = head.IndexOf("\r\n", StringComparison.Ordinal);
        return lineEnd > 0 && head[..lineEnd].Split(' ') is ["GET", var target, var version]
            && version.StartsWith("HTTP/", StringComparison.Ordinal) && target.StartsWith('/')
            ? target
            : null;
    }

    private async Task HandleAsync(NetworkStream stream, string target, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate("http://localhost" + target, UriKind.Absolute, out var url))
        {
            await ReplyAsync(stream, 400, "Login failed", "The request is not valid.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        if (url.AbsolutePath != _path)
        {
            await ReplyAsync(stream, 404, "Not found", "This address is not part of the login.", cancellationToken).ConfigureAwait(false);
            return;
        }

        // The state binds the answer to this attempt; anything else on the port is ignored.
        if (query["state"] != _state)
        {
            await ReplyAsync(stream, 400, "Login failed", "The state does not match this login attempt.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (query["error"] is { } error)
        {
            // Reply first: the flow disposes this server as soon as it has the answer, which would cut the page.
            try
            {
                await ReplyAsync(stream, 400, "Login failed", query["error_description"] ?? error, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _answer.TrySetResult((null, error));
            }

            return;
        }

        if (string.IsNullOrEmpty(query["code"]))
        {
            try
            {
                await ReplyAsync(stream, 400, "Login failed", "The redirect carried no authorization code.", cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _answer.TrySetResult((null, "invalid_callback"));
            }

            return;
        }

        // The reply goes first, as above. A browser that drops the connection must not lose the code: the
        // caller ignores that failure and the answer is set either way.
        try
        {
            await ReplyAsync(stream, 200, "Signed in", "You can close this window and return to the terminal.", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _answer.TrySetResult((query["code"], null));
        }
    }

    private async Task ReplyAsync(NetworkStream stream, int status, string heading, string message, CancellationToken cancellationToken)
    {
        var html = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />"
            + "<title>" + WebUtility.HtmlEncode(_product + ": " + heading) + "</title>"
            + "<style>html{color-scheme:light dark}body{margin:0;min-height:100vh;display:grid;place-items:center;font-family:ui-sans-serif,system-ui,sans-serif}"
            + "main{max-width:32rem;padding:2rem;text-align:center}h1{font-size:1.5rem;margin:0 0 .5rem}p{margin:0;opacity:.75}</style></head>"
            + "<body><main><h1>" + WebUtility.HtmlEncode(heading) + "</h1><p>" + WebUtility.HtmlEncode(message) + "</p></main></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        var reason = status switch { 200 => "OK", 404 => "Not Found", _ => "Bad Request" };
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + reason + "\r\n"
            + "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\n"
            + "Content-Length: " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n");
        await stream.WriteAsync(head, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
