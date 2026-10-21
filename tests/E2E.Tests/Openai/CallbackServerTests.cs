// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Text;
using E2E.OAuth;

namespace E2E.Tests.Openai;

/// <summary>No upstream <c>it</c>: Node ignores a write to a closed socket, .NET throws.</summary>
public sealed class CallbackServerTests
{
    [Fact]
    public async Task Keeps_the_code_and_keeps_serving_when_the_browser_drops_the_connection_before_the_reply()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test");
        Assert.NotNull(server);

        using (var browser = new TcpClient())
        {
            await browser.ConnectAsync("localhost", port);
            browser.LingerState = new LingerOption(true, 0);
            await browser.GetStream().WriteAsync(Encoding.ASCII.GetBytes("GET /auth/callback?code=abc&state=st HTTP/1.1\r\nHost: localhost\r\n\r\n"));
        }

        Assert.Equal(("abc", (string?)null), await server.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None));

        using var http = new HttpClient();
        using var stray = await http.GetAsync("http://localhost:" + port + "/elsewhere");
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
    }

    [Fact]
    public async Task Survives_connections_reset_before_they_are_accepted()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test");
        Assert.NotNull(server);

        for (var i = 0; i < 50; i++)
        {
            using var browser = new TcpClient();
            browser.LingerState = new LingerOption(true, 0);
            await browser.ConnectAsync(IPAddress.Loopback, port);
        }

        using var http = new HttpClient();
        using var reply = await http.GetAsync("http://localhost:" + port + "/auth/callback?code=abc&state=st");
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.Equal(("abc", (string?)null), await server.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_a_head_over_16_KB_and_still_takes_a_later_code()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test");
        Assert.NotNull(server);

        var oversized = "GET /auth/callback?code=oversized&state=st HTTP/1.1\r\nX-Pad: " + new string('a', 20 * 1024) + "\r\n\r\n";
        Assert.Equal("", await SendRawAsync(port, oversized));

        await AssertLaterCodeAsync(server, port);
    }

    [Fact]
    public async Task Ignores_a_method_other_than_GET_and_still_takes_a_later_code()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test");
        Assert.NotNull(server);

        Assert.Equal("", await SendRawAsync(port, "POST /auth/callback?code=posted&state=st HTTP/1.1\r\nHost: localhost\r\n\r\n"));

        await AssertLaterCodeAsync(server, port);
    }

    [Fact]
    public async Task Drops_a_request_that_stalls_past_the_timeout_and_still_takes_a_later_code()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test", TimeSpan.FromMilliseconds(200));
        Assert.NotNull(server);

        // The head never ends, so the server must close the connection by itself.
        Assert.Equal("", await SendRawAsync(port, "GET /auth/callback?code=stalled&state=st HTTP/1.1\r\n"));

        await AssertLaterCodeAsync(server, port);
    }

    [Fact]
    public async Task Answers_400_to_a_wrong_state_and_still_takes_a_later_code()
    {
        var port = FreePort();
        using var server = CallbackServer.TryStart(port, "/auth/callback", "st", "Test");
        Assert.NotNull(server);

        Assert.StartsWith("HTTP/1.1 400", await SendRawAsync(port, "GET /auth/callback?code=forged&state=other HTTP/1.1\r\nHost: localhost\r\n\r\n"));

        await AssertLaterCodeAsync(server, port);
    }

    private static async Task AssertLaterCodeAsync(CallbackServer server, int port)
    {
        using var http = new HttpClient();
        using var reply = await http.GetAsync("http://localhost:" + port + "/auth/callback?code=abc&state=st");
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.Equal(("abc", (string?)null), await server.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    /// <summary>Sends the text, then reads until the server closes the connection. Returns what the server wrote.</summary>
    private static async Task<string> SendRawAsync(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var reply = new MemoryStream();
        try
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), timeout.Token);
            await stream.CopyToAsync(reply, timeout.Token);
        }
        catch (IOException)
        {
            // The server refused the request and reset the connection instead of closing it; it wrote nothing more.
        }

        return Encoding.ASCII.GetString(reply.ToArray());
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
