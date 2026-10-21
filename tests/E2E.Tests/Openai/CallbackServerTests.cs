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

        using var http = new HttpClient();
        using (var browser = new TcpClient())
        {
            await browser.ConnectAsync("localhost", port);
            browser.LingerState = new LingerOption(true, 0);
            var stream = browser.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /auth/callback?code=abc&state=st HTTP/1.1\r\nHost: localhost\r\n"));

            // The listener's socket is IPv6, and on macOS the runtime throws from its accept callback, with no frame
            // of ours, when a connection is reset before it is accepted (dotnet/runtime#121848). The queue is first in,
            // first out, so once a later connection has been answered this one has been accepted.
            using var sync = await http.GetAsync("http://localhost:" + port + "/sync");
            Assert.Equal(HttpStatusCode.NotFound, sync.StatusCode);

            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
        }

        Assert.Equal(("abc", (string?)null), await server.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None));

        using var stray = await http.GetAsync("http://localhost:" + port + "/elsewhere");
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
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
