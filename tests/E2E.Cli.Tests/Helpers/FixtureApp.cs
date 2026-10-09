// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// The fixture app the MCP tests drive, served over HTTP: the home page of upstream's
/// <c>tests/helpers/fixture-pages/home.ts</c> and the routes that answer late or never.
/// </summary>
internal sealed class FixtureApp : IDisposable
{
    private const string Home = """
        <!doctype html>
        <html>
        <head><title>Fixture Home</title></head>
        <body>
          <h1>Home</h1>
          <a href="/about">About</a>
          <button id="increment" onclick="document.getElementById('count').textContent = String(Number(document.getElementById('count').textContent) + 1)">Increment</button>
          <output id="count" role="status" aria-label="Counter">0</output>

          <label for="email">Email</label>
          <input id="email" type="email" placeholder="you@example.test" autocomplete="username" />

          <label for="focus-target">Focus target</label>
          <input id="focus-target" />

          <label for="password">Password</label>
          <input id="password" type="password" autocomplete="current-password" />

          <label for="notifications">Notifications</label>
          <input id="notifications" type="checkbox" />

          <label for="plan">Plan</label>
          <select id="plan">
            <option value="free">Free</option>
            <option value="pro">Pro</option>
            <option value="team">Team</option>
          </select>

          <span>Duplicated</span>
          <span>Duplicated</span>
        </body>
        </html>
        """;

    private const string About = "<!doctype html><title>About</title><h1>About</h1><a href=\"/\">Home</a>";

    private const string Slow = "<!doctype html><title>Slow</title><h1>Slow</h1>";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();

    private FixtureApp(string url)
    {
        Url = url;
    }

    /// <summary>The app's origin, without a trailing slash.</summary>
    public string Url { get; }

    public static FixtureApp Start()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var app = new FixtureApp("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        app._listener.Prefixes.Add(app.Url + "/");
        app._listener.Start();
        _ = Task.Run(() => app.ListenAsync(app._stop.Token));
        return app;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private static async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            switch (context.Request.Url!.AbsolutePath)
            {
                case "/":
                    await Html(context, Home);
                    break;
                case "/about":
                    await Html(context, About);
                    break;
                case "/slow":
                    // Answers after a while, so a call to it is still in flight when the client goes away.
                    await Task.Delay(1_500, cancellationToken);
                    await Html(context, Slow);
                    break;
                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    break;
            }
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }

    private static async Task Html(HttpListenerContext context, string html)
    {
        var page = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = page.Length;
        await context.Response.OutputStream.WriteAsync(page);
        context.Response.Close();
    }
}
