// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Text;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.CdpHeaders;

/// <summary>
/// Ports <c>packages/web/tests/integration/cdp-headers.test.ts</c>: handshake headers reach CDP discovery and nothing else, and
/// a rejected upgrade never echoes them.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class CdpHeadersTests
{
    [Fact]
    public async Task Sends_authorization_during_CDP_discovery_without_adding_it_to_page_requests()
    {
        await using var remote = await RemoteChrome.LaunchAsync();
        var handshakeHeaders = new List<string?>();
        var pageHeaders = new List<string?>();
        await using var server = new HeaderServer((path, headers, _) =>
        {
            if (path.StartsWith("/devtools/", StringComparison.Ordinal))
            {
                lock (handshakeHeaders)
                {
                    handshakeHeaders.Add(headers.GetValueOrDefault("authorization"));
                }

                return Ok("application/json", $$"""{"webSocketDebuggerUrl":"{{remote.Endpoint}}"}""");
            }

            lock (pageHeaders)
            {
                pageHeaders.Add(headers.GetValueOrDefault("authorization"));
            }

            return Ok("text/html", "<title>Test app</title>");
        });
        using var playwright = await Playwright.CreateAsync();
        var browser = await BrowserConnection.ConnectCdpAsync(
            playwright,
            new CdpTarget { Endpoint = $"{server.Url}/devtools", Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer synthetic-token" } },
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        try
        {
            var page = await (await browser.NewContextAsync()).NewPageAsync();
            await page.GotoAsync($"{server.Url}/app");
            Assert.Equal("Test app", await page.TitleAsync());
            Assert.Equal(["Bearer synthetic-token"], handshakeHeaders);
            Assert.NotEmpty(pageHeaders);
            Assert.All(pageHeaders, header => Assert.Null(header));
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    [Theory]
    [InlineData("Unauthorized")]
    [InlineData("Authorization Bearer synthetic-token, token synthetic-token")]
    public async Task Sanitizes_rejected_WebSocket_upgrades_with_reason(string reason)
    {
        await using var server = new HeaderServer((_, _, upgrade) => upgrade
            ? Encoding.ASCII.GetBytes($"HTTP/1.1 401 {reason}\r\nContent-Length: 0\r\n\r\n")
            : Ok("text/plain", ""));
        using var playwright = await Playwright.CreateAsync();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => BrowserConnection.ConnectCdpAsync(
            playwright,
            new CdpTarget { Endpoint = server.Url.Replace("http:", "ws:", StringComparison.Ordinal), Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer synthetic-token" } },
            TimeSpan.FromSeconds(5),
            CancellationToken.None));
        Assert.Equal("CDP connection failed; check the endpoint, handshake headers, and connection timeout", failure.Message);
        Assert.DoesNotContain("synthetic-token", failure.ToString(), StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
    }

    private static byte[] Ok(string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        return [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), .. bytes];
    }

    /// <summary>A loopback HTTP server that answers each request with the bytes the test builds from its path and headers; every connection closes after one answer.</summary>
    private sealed class HeaderServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<string, Dictionary<string, string>, bool, byte[]> _respond;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        public HeaderServer(Func<string, Dictionary<string, string>, bool, byte[]> respond)
        {
            _respond = respond;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _loop = Task.Run(AcceptAsync);
        }

        public string Url { get; }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync(_stop.Token);
                    if (requestLine is null)
                    {
                        return;
                    }

                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 } line)
                    {
                        var colon = line.IndexOf(':', StringComparison.Ordinal);
                        if (colon > 0)
                        {
                            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                        }
                    }

                    var path = requestLine.Split(' ')[1];
                    var upgrade = headers.TryGetValue("upgrade", out var value) && value.Equals("websocket", StringComparison.OrdinalIgnoreCase);
                    await stream.WriteAsync(_respond(path, headers, upgrade), _stop.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    // A client that went away, or the server stopping, ends its own connection.
                }
            }
        }
    }
}
