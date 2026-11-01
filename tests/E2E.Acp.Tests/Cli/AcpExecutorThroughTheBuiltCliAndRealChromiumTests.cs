// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using E2E.Engine;
using static E2E.Acp.Tests.AcpKit;

namespace E2E.Acp.Tests.Cli;

/// <summary>
/// <c>cli.test.ts</c>: <c>describe('acp executor through the built CLI and real Chromium')</c>. The port has no <c>e2e run</c>, so the
/// two runs of upstream's <c>beforeAll</c> are two passes of the same two tests through <see cref="E2ESession"/> with the web engine and
/// a replay cache read-write; the credentials come from <see cref="E2E.Secret"/>. It needs Chromium, so it runs with the real-model tests.
/// </summary>
[Trait("TestCategory", "RealModel")]
public sealed class AcpExecutorThroughTheBuiltCliAndRealChromiumTests(AcpExecutorThroughTheBuiltCliAndRealChromiumTests.Runs runs) : IClassFixture<AcpExecutorThroughTheBuiltCliAndRealChromiumTests.Runs>
{
    [Fact]
    public void Runs_act_and_assert_steps_on_the_agent()
    {
        Assert.Equal("passed", runs.First.Status["todo flow"]);
        Assert.Contains(runs.First.Prompts, prompt => prompt.Contains("Step: Add a todo named Buy milk.", StringComparison.Ordinal));
        Assert.Contains(runs.First.Prompts, prompt => prompt.Contains("Assertion: The list shows Buy milk.", StringComparison.Ordinal));
    }

    [Fact]
    public void Fills_a_secret_by_name_without_the_value_reaching_the_agent_or_the_artifacts()
    {
        Assert.Equal("passed", runs.First.Status["secret login"]);
        Assert.Contains("Secrets to fill with fill_secret, by name: admin.password", string.Join('\n', runs.First.Prompts), StringComparison.Ordinal);
        Assert.Contains("\"call\":\"fill_secret\"", runs.First.Log, StringComparison.Ordinal);
        Assert.DoesNotContain(Runs.Password, runs.First.Log, StringComparison.Ordinal);
        foreach (var artifact in runs.Artifacts)
        {
            Assert.DoesNotContain(Runs.Password, artifact, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Replays_recorded_act_steps_without_the_agent()
    {
        Assert.Equal("passed", runs.Second.Status["todo flow"]);
        Assert.Equal("passed", runs.Second.Status["secret login"]);
        Assert.DoesNotContain(runs.Second.Prompts, prompt => prompt.Contains("Step:", StringComparison.Ordinal));
        // Assertions are never cached: they still reach the agent.
        Assert.Contains(runs.Second.Prompts, prompt => prompt.Contains("Assertion: The list shows Buy milk.", StringComparison.Ordinal));
    }

    /// <summary>What one pass of the suite left: the tests' statuses, the prompts the agent received, and its whole log, tool results included.</summary>
    public sealed record Pass(Dictionary<string, string> Status, List<string> Prompts, string Log);

    /// <summary>Upstream's <c>beforeAll</c>: the project, the app, and two runs of the suite.</summary>
    public sealed class Runs : IAsyncLifetime
    {
        public const string Password = "acp-secret-test-value";

        private static readonly Dictionary<string, object[]> Script = new()
        {
            ["Step: Add a todo named Buy milk."] =
            [
                new { call = "fill", args = new { role = "textbox", name = "New todo", value = "Buy milk" } },
                new { call = "tap", args = new { role = "button", name = "Add" } },
                new { call = "done", args = new { status = "passed", summary = "done" } },
            ],
            ["Assertion: The list shows Buy milk."] = [new { call = "done", args = new { status = "passed", summary = "done" } }],
            ["Step: Sign in as Ada."] =
            [
                new { call = "fill", args = new { role = "textbox", name = "Name", value = "Ada" } },
                new { call = "fill_secret", args = new { role = "textbox", name = "Password", secret = "admin.password" } },
                new { call = "tap", args = new { role = "button", name = "Sign in" } },
                new { call = "done", args = new { status = "passed", summary = "done" } },
            ],
        };

        private readonly string _directory = Directory.CreateTempSubdirectory("e2e-acp-cli-").FullName;
        private readonly HttpListener _app = new();
        private readonly CancellationTokenSource _stop = new();
        private string _url = "";

        public Pass First { get; private set; } = null!;

        public Pass Second { get; private set; } = null!;

        public List<string> Artifacts { get; private set; } = [];

        public async Task InitializeAsync()
        {
            _url = StartApp();
            First = await RunAsync(0);
            Second = await RunAsync(1);
            Artifacts = Directory.Exists(Path.Combine(_directory, ".e2e"))
                ? [.. Directory.EnumerateFiles(Path.Combine(_directory, ".e2e"), "*", SearchOption.AllDirectories).Select(File.ReadAllText)]
                : [];
        }

        public async Task DisposeAsync()
        {
            await _stop.CancelAsync();
            _app.Close();
            _stop.Dispose();
            Directory.Delete(_directory, recursive: true);
        }

        private string StartApp()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            _app.Prefixes.Add($"http://127.0.0.1:{port}/");
            _app.Start();
            _ = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _app.GetContextAsync();
                    }
                    catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                    {
                        return;
                    }

                    var login = context.Request.Url!.AbsolutePath.StartsWith("/login", StringComparison.Ordinal);
                    var html = login
                        ? $"<!doctype html><html><body><h1>Login</h1><label>Name<input id=\"name\"></label><label>Password<input id=\"password\" type=\"password\"></label><button id=\"go\">Sign in</button><p role=\"status\" id=\"status\"></p><script>document.getElementById(\"go\").onclick = () => {{ document.getElementById(\"status\").textContent = document.getElementById(\"password\").value === {JsonSerializer.Serialize(Password)} ? \"Hello \" + document.getElementById(\"name\").value : \"Invalid password\"; }};</script></body></html>"
                        : "<!doctype html><html><body><h1>Todos</h1><label>New todo<input id=\"name\"></label><button id=\"add\">Add</button><ul id=\"list\"></ul><script>document.getElementById(\"add\").onclick = () => { const li = document.createElement(\"li\"); li.textContent = document.getElementById(\"name\").value; document.getElementById(\"list\").appendChild(li); };</script></body></html>";
                    var bytes = Encoding.UTF8.GetBytes(html);
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            });
            return $"http://127.0.0.1:{port}";
        }

        /// <summary>One <c>e2e run</c>: both tests, with the agent logging to a file of its own.</summary>
        private async Task<Pass> RunAsync(int index)
        {
            var logFile = Path.Combine(_directory, $"agent-{index}.jsonl");
            var executor = AcpExecutors.Acp(new AcpOptions
            {
                Command = "node",
                Args = [AcpKit.Agent],
                Env = new Dictionary<string, string> { ["ACP_SCRIPT"] = JsonSerializer.Serialize(Script), ["ACP_LOG"] = logFile },
            });
            var status = new Dictionary<string, string>();
            foreach (var title in new[] { "todo flow", "secret login" })
            {
                status[title] = await RunTestAsync(title, executor);
            }

            var log = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
            var prompts = log.Split('\n').Where(line => line.Trim().Length > 0).Select(line => JsonDocument.Parse(line).RootElement)
                .Where(entry => entry.TryGetProperty("prompt", out _)).Select(entry => entry.GetProperty("prompt").GetString()!).ToList();
            return new Pass(status, prompts, log);
        }

        private async Task<string> RunTestAsync(string title, IStepExecutor executor)
        {
            await using var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                AgentExecutor = executor,
                BaseUrl = _url,
                ProjectRoot = _directory,
                ResultsDirectory = Path.Combine(_directory, ".e2e", "results"),
                Cache = new FileStepCache(Path.Combine(_directory, ".e2e", "cache")),
                CacheMode = CacheMode.ReadWrite,
                TestTitle = title,
                ActionTimeout = TimeSpan.FromSeconds(10),
                AssertionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(60),
                TestTimeout = TimeSpan.FromSeconds(120),
            });
            try
            {
                if (title == "todo flow")
                {
                    await session.App.OpenAsync("/todos");
                    await session.Agent.ActAsync("Add a todo named Buy milk.");
                    await Expect.That(session.Screen.GetByText("Buy milk")).ToBeVisibleAsync();
                    await session.Agent.AssertAsync("The list shows Buy milk.");
                }
                else
                {
                    await session.App.OpenAsync("/login");
                    await session.Agent.ActAsync(
                        "Sign in as Ada.",
                        new ActOptions { Params = new Dictionary<string, object?> { ["password"] = E2E.Secret.Create("admin.password", Password, "password") } });
                    await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("Hello Ada");
                }

                session.Complete();
                return "passed";
            }
            catch (Exception ex)
            {
                session.Complete(ex);
                return "failed: " + ex.Message;
            }
        }
    }
}
