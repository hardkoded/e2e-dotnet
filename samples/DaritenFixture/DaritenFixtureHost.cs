// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DaritenFixture;

/// <summary>
/// Serves the dashboard and the transaction register the Dariten locator sample checks.
/// The public demo at https://dariten.vercel.app renders those pages from Neon. When that
/// compute cannot start, the site answers with an error screen and the Dashboard heading
/// is absent. These pages keep the roles and names the demo renders when the database is up.
/// </summary>
internal sealed class DaritenFixtureHost : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _serve;
    private int _disposed;

    private DaritenFixtureHost(HttpListener listener, string url)
    {
        _listener = listener;
        Url = url;
        _serve = ServeAsync();
    }

    /// <summary>The base URL of this host, including the trailing slash.</summary>
    public string Url { get; }

    /// <summary>Binds a free loopback port and starts answering.</summary>
    public static DaritenFixtureHost Start()
    {
        const int attempts = 5;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var port = FreePort();
            var prefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                return new DaritenFixtureHost(listener, prefix);
            }
            catch (HttpListenerException) when (attempt < attempts)
            {
                listener.Close();
            }
            catch
            {
                listener.Close();
                throw;
            }
        }

        throw new InvalidOperationException("Could not bind a loopback port for the Dariten pages.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Stop unblocks GetContextAsync. Close comes after the loop observes that.
        _listener.Stop();
        await _serve.ConfigureAwait(false);
        _listener.Close();
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            try
            {
                await RespondAsync(context).ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                // The browser closed the socket before the response finished.
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task RespondAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var html = Page(path);
        var body = Encoding.UTF8.GetBytes(html ?? string.Empty);
        context.Response.StatusCode = html is null ? (int)HttpStatusCode.NotFound : (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        try
        {
            await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string? Page(string path) => path switch
    {
        "/" or "/index.html" => Dashboard,
        "/transactions" or "/transactions/" => Transactions,
        _ => null,
    };

    private const string Dashboard = """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Quicken Demo</title></head>
        <body>
          <div data-testid="demo-banner">Shared demo — no login. Everyone using this deploy edits the same sample household. Do not enter real bank data.</div>
          <nav>
            <a href="/">Dashboard</a>
            <a href="/transactions">Transactions</a>
          </nav>
          <main>
            <h1>Dashboard</h1>
            <p>Net worth</p>
          </main>
        </body>
        </html>
        """;

    private const string Transactions = """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Quicken Demo</title></head>
        <body>
          <main>
            <h1>Transactions</h1>
            <button type="button">Filter</button>
          </main>
        </body>
        </html>
        """;

    private static int FreePort()
    {
        using var listen = new TcpListener(IPAddress.Loopback, 0);
        listen.Start();
        var port = ((IPEndPoint)listen.LocalEndpoint).Port;
        listen.Stop();
        return port;
    }
}
