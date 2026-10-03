// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.Tests;

public sealed class WebEngineTests
{
    private const string BillingPage = """
        <!DOCTYPE html>
        <html><body>
        <h1>Billing</h1>
        <button type="button">Upgrade to Pro</button>
        <p id="invoice" hidden>Prorated amount: $12</p>
        <div role="status" hidden>Pro</div>
        <script>
          document.querySelector("button").addEventListener("click", () => {
            document.getElementById("invoice").hidden = false;
            document.querySelector("[role=status]").hidden = false;
          });
        </script>
        </body></html>
        """;

    private const string ProjectsPage = """
        <!DOCTYPE html>
        <html><body>
        <ul>
          <li>Alpha <button type="button" onclick="removed('Alpha')">Delete</button></li>
          <li>Beta <button type="button" onclick="removed('Beta')">Delete</button></li>
        </ul>
        <div role="status"></div>
        <script>
          function removed(name) { document.querySelector("[role=status]").textContent = "Deleted " + name; }
        </script>
        </body></html>
        """;

    [Fact]
    public async Task Chromium_upgrades_the_plan_when_a_browser_is_installed()
    {
        using var site = await TinySite.StartAsync(BillingPage);
        var session = await TryStartAsync(site, UpgradeModel(() => { }), cache: null);
        if (session is null)
        {
            return;
        }

        await RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
            await Expect.That(session.Screen.GetByRole("status")).ToContainTextAsync("Pro");
        });
    }

    [Fact]
    public async Task Chromium_acts_on_the_element_a_ref_names_when_two_share_a_role_and_name()
    {
        using var site = await TinySite.StartAsync(ProjectsPage);
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "deleted");
            }

            var target = Regex.Matches(text, @"button ""Delete"" \[ref=(e\d+)\]")[^1].Groups[1].Value;
            return ModelResponses.Call("tap", new { @ref = target });
        });
        var session = await TryStartAsync(site, model, cache: null);
        if (session is null)
        {
            return;
        }

        await RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("delete the Beta project");
            await Expect.That(session.Screen.GetByRole("status")).ToContainTextAsync("Deleted Beta");
            await session.Screen.GetByRole("button", "Delete").First().TapAsync();
            await Expect.That(session.Screen.GetByRole("status")).ToContainTextAsync("Deleted Alpha");
        });
    }

    [Fact]
    public async Task Chromium_replays_a_recorded_act_without_the_model()
    {
        using var site = await TinySite.StartAsync(BillingPage);
        var directory = Path.Combine(Path.GetTempPath(), "e2e-web", Guid.NewGuid().ToString("n"));
        for (var run = 0; run < 2; run++)
        {
            var calls = 0;
            var session = await TryStartAsync(site, UpgradeModel(() => calls++), new FileStepCache(directory));
            if (session is null)
            {
                return;
            }

            await RunAsync(session, async () =>
            {
                await session.App.OpenAsync("/");
                await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
                await Expect.That(session.Screen.GetByRole("status")).ToContainTextAsync("Pro");
            });
            Assert.Equal(run, session.Replayed);
            Assert.Equal(run == 0, calls > 0);
        }
    }

    [Fact]
    public async Task Chromium_reads_names_and_states_the_way_accname_and_aria_do()
    {
        const string page = """
            <!DOCTYPE html>
            <html><body>
            <span id="heading">Referenced name</span>
            <button type="button" data-testid="labelled" aria-labelledby="heading" aria-label="Own label">x</button>
            <button type="button" data-testid="aria-disabled" aria-disabled="true">Save</button>
            <div aria-disabled="true">
              <button type="button" data-testid="inherited">Inherited</button>
              <div aria-disabled="false"><button type="button" data-testid="cut">Cut</button></div>
            </div>
            <fieldset disabled>
              <legend><input type="checkbox" data-testid="legend"></legend>
              <input type="text" data-testid="fieldset">
            </fieldset>
            <button type="button" data-testid="enabled">Enabled</button>
            <div role="checkbox" aria-checked="true" data-testid="custom-checked">Custom</div>
            <div role="switch" aria-checked="true" data-testid="switch">Switch</div>
            <div role="checkbox" aria-checked="mixed" data-testid="mixed">Mixed</div>
            <input type="checkbox" aria-checked="true" data-testid="native">
            </body></html>
            """;
        using var site = await TinySite.StartAsync(page);
        IEngineSession session;
        try
        {
            session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions { BaseUrl = site.Url }, CancellationToken.None);
        }
        catch (EngineException ex) when (ex.Code == "ENVIRONMENT_UNAVAILABLE")
        {
            return;
        }

        await using (session)
        {
            await session.OpenAsync(site.Url, CancellationToken.None);
            var observation = await session.ObserveAsync(CancellationToken.None);
            var nodes = Flatten(observation.Roots).Where(node => node.TestId is not null).ToDictionary(node => node.TestId!);

            Assert.Equal("Referenced name", nodes["labelled"].Name);
            Assert.True(nodes["aria-disabled"].States.Disabled);
            Assert.True(nodes["inherited"].States.Disabled);
            Assert.False(nodes["cut"].States.Disabled);
            Assert.True(nodes["fieldset"].States.Disabled);
            Assert.False(nodes["legend"].States.Disabled);
            Assert.False(nodes["enabled"].States.Disabled);
            Assert.True(nodes["custom-checked"].States.Checked);
            Assert.True(nodes["switch"].States.Checked);
            Assert.False(nodes["mixed"].States.Checked);
            Assert.False(nodes["native"].States.Checked);
        }
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static ScriptedModel UpgradeModel(Action onCall)
    {
        return new ScriptedModel(request =>
        {
            onCall();
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("tapped", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "upgraded");
            }

            return ModelResponses.Tap("button", "Upgrade to Pro");
        });
    }

    private static async Task<E2ESession?> TryStartAsync(TinySite site, ScriptedModel model, FileStepCache? cache)
    {
        try
        {
            return await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                Model = model,
                BaseUrl = site.Url,
                Cache = cache,
                CacheEnabled = cache is not null,
                TestTitle = "web > step",
                AssertionTimeout = TimeSpan.FromSeconds(5),
                ActionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(20),
                TestTimeout = TimeSpan.FromSeconds(30),
            });
        }
        catch (EngineException ex) when (ex.Code == "ENVIRONMENT_UNAVAILABLE")
        {
            return null;
        }
    }

    private static async Task RunAsync(E2ESession session, Func<Task> body)
    {
        await using (session)
        {
            Exception? error = null;
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            session.Complete(error);
            Assert.Null(error);
        }
    }
}

internal sealed class TinySite : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<HttpListenerContext, Task> _handler;

    private TinySite(string url, Func<HttpListenerContext, Task> handler)
    {
        Url = url;
        _handler = handler;
    }

    public string Url { get; }

    public static Task<TinySite> StartAsync(string html)
    {
        return StartAsync(context => RespondAsync(context, html));
    }

    public static async Task RespondAsync(HttpListenerContext context, string html)
    {
        var page = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = page.Length;
        await context.Response.OutputStream.WriteAsync(page);
        context.Response.Close();
    }

    public static async Task<TinySite> StartAsync(Func<HttpListenerContext, Task> handler)
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var site = new TinySite("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/", handler);
        site._listener.Prefixes.Add(site.Url);
        site._listener.Start();
        _ = Task.Run(() => site.ListenAsync(site._stop.Token));
        await Task.Delay(30);
        return site;
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

            _ = Task.Run(() => HandleAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            await _handler(context);
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }
}
