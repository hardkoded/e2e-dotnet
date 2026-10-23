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

    // A session's test lines, upstream's /test-lines page of fixture-pages/forms.ts: a sign-in form a role and name reaches, a button
    // repeated per row that only its row tells apart, twins in plain divs only their order tells apart, a shadow root's twin that observe
    // lists before a light one the locator engine lists first, and an icon button only its test id names. The summary says what a replay
    // of the lines did.
    private const string TestLines = """
        <!doctype html>
        <html>
        <head><title>Test lines</title></head>
        <body>
          <h1>Test lines</h1>
          <form onsubmit="event.preventDefault(); summarize()">
            <label for="tl-user">Username</label>
            <input id="tl-user" autocomplete="username" />
            <label for="tl-password">Password</label>
            <input id="tl-password" type="password" autocomplete="current-password" />
            <label for="tl-plan">Plan</label>
            <select id="tl-plan"><option>Free</option><option>Pro</option></select>
            <label><input id="tl-terms" type="checkbox" /> Accept terms</label>
            <button type="submit">Sign in</button>
          </form>
          <ul>
            <li><span>Offer A</span> <button onclick="state.offer = 'A'">Reserve</button></li>
            <li><span>Offer B</span> <button onclick="state.offer = 'B'">Reserve</button></li>
          </ul>
          <tl-keep></tl-keep>
          <div><button onclick="state.kept = 'light'">Keep</button></div>
          <div><span>Row one</span> <button onclick="state.picked = 1">Pick</button></div>
          <div><span>Row two</span> <button onclick="state.picked = 2">Pick</button></div>
          <button data-testid="star" onclick="state.starred = true"><svg aria-hidden="true" width="16" height="16"><circle cx="8" cy="8" r="6"></circle></svg></button>
          <output id="tl-summary" role="status" aria-label="Summary">empty</output>
          <script>
            const state = { offer: 'none', kept: 'none', picked: 0, starred: false };
            customElements.define('tl-keep', class extends HTMLElement {
              connectedCallback() {
                const root = this.attachShadow({ mode: 'open' });
                root.innerHTML = '<div><button>Keep</button></div>';
                root.querySelector('button').addEventListener('click', () => { state.kept = 'shadow'; });
              }
            });
            function summarize() {
              const field = (id) => document.getElementById(id);
              field('tl-summary').textContent = [
                'user=' + field('tl-user').value,
                'password=' + (field('tl-password').value === '' ? 'empty' : 'set'),
                'plan=' + field('tl-plan').value,
                'terms=' + field('tl-terms').checked,
                'offer=' + state.offer,
                'kept=' + state.kept,
                'picked=' + state.picked,
                'starred=' + state.starred,
              ].join(' ');
            }
          </script>
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
                case "/test-lines":
                    await Html(context, TestLines);
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
