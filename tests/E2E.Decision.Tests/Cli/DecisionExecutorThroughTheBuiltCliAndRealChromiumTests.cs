// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Decision.Sdk;
using E2E.Engine;

namespace E2E.Decision.Tests.Cli;

/// <summary>
/// Upstream's <c>describe('decision executor through the built CLI and real Chromium')</c> of <c>cli.test.ts</c>. Upstream builds the CLI and
/// runs <c>e2e run</c> over a scripted decision model and a scripted text model on real Chromium. The port runs the same three flows over
/// the same scripted models on real Chromium through <see cref="E2ESession"/>, so each method is <c>Partial_</c>: it does not run the built
/// CLI, nor validate <c>report.json</c> against the report schema (the port's CLI has no run of a project's tests to start from a test).
/// </summary>
public sealed partial class DecisionExecutorThroughTheBuiltCliAndRealChromiumTests : IAsyncLifetime
{
    private const string Password = "decision-secret-test-value";

    private readonly Dictionary<string, string?> _errors = [];
    private readonly List<string> _requests = [];
    private readonly List<string> _artifacts = [];
    private readonly List<HashSet<string>> _asked = [];
    private HttpListener? _listener;
    private string? _appUrl;
    private CancellationTokenSource? _stop;
    private string? _results;

    public async Task InitializeAsync()
    {
        _stop = new CancellationTokenSource();
        (_listener, _appUrl) = Listen();
        _ = Task.Run(() => ServeAsync(_listener, _stop.Token));
        _results = Path.Combine(Path.GetTempPath(), "e2e-decision-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_results);
        var model = new ScriptedDecision(_requests, _asked);
        var executor = global::E2E.Decision.Executor.DecisionExecutor(
            new DecisionExecutorOptions { Model = model, TextModel = new ScriptedField(_requests) }
        );
        await RunAsync("todo flow in plain language", executor, async session =>
        {
            await session.Context.App.OpenAsync("/todos");
            await session.Context.Agent.ActAsync("Add a todo named Buy milk.");
            await Expect.That(session.Context.Screen.GetByText("Buy milk")).ToBeVisibleAsync();
        });
        await RunAsync("secret login", executor, async session =>
        {
            await session.Context.App.OpenAsync("/login");
            await session.Context.Agent.ActAsync(
                "Sign in with Ada.",
                new ActOptions
                {
                    Params = new Dictionary<string, object?>
                    {
                        ["password"] = Secret.Create("admin.password", Password, "password"),
                    },
                }
            );
            await Expect.That(session.Context.Screen.GetByRole("status")).ToHaveTextAsync("Saved Ada");
        });
        await RunAsync("long page", executor, async session =>
        {
            await session.Context.App.OpenAsync("/many");
            await session.Context.Agent.ActAsync("Tap target 5.");
            await Expect.That(session.Context.Screen.GetByRole("status")).ToHaveTextAsync("picked 5");
        });
        foreach (var file in Directory.EnumerateFiles(_results, "*", SearchOption.AllDirectories))
        {
            _artifacts.Add(await File.ReadAllTextAsync(file));
        }
    }

    public Task DisposeAsync()
    {
        try
        {
            _stop?.Cancel();
            _listener?.Close();
        }
        finally
        {
            _stop?.Dispose();
            if (_results is not null && Directory.Exists(_results))
            {
                Directory.Delete(_results, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void Partial_completes_the_todo_flow_in_plain_language() =>
        Assert.Null(_errors["todo flow in plain language"]);

    [Fact]
    public void Partial_fills_a_runner_authorized_secret_without_sending_it_anywhere()
    {
        Assert.Null(_errors["secret login"]);
        Assert.DoesNotContain(Password, string.Join('\n', _requests), StringComparison.Ordinal);
        foreach (var artifact in _artifacts)
        {
            Assert.DoesNotContain(Password, artifact, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Partial_completes_a_long_page_past_the_choice_cap() => Assert.Null(_errors["long page"]);

    [Fact]
    public void Partial_asks_the_operation_first_and_the_target_in_its_own_call()
    {
        Assert.Contains(_asked, keys => keys.SetEquals(["operation"]));
        Assert.Contains(_asked, keys => keys.SetEquals(["target"]));
    }

    private async Task RunAsync(string title, IStepExecutor executor, Func<E2ESession, Task> flow)
    {
        await using var session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                AgentExecutor = executor,
                BaseUrl = _appUrl,
                CacheEnabled = false,
                TestTitle = title,
                ResultsDirectory = _results,
                AssertionTimeout = TimeSpan.FromSeconds(5),
                ActionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(30),
                TestTimeout = TimeSpan.FromSeconds(60),
            }
        );
        Exception? error = null;
        try
        {
            await flow(session);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        _errors[title] = error?.ToString();
    }

    private static (HttpListener Listener, string Url) Listen()
    {
        // HttpListener cannot bind port 0, so take a free port and try again when another process takes it first.
        for (var attempt = 0; ; attempt++)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var url = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            try
            {
                listener.Start();
                return (listener, url);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }

    private static async Task ServeAsync(HttpListener listener, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (stop.IsCancellationRequested || !listener.IsListening)
            {
                return;
            }

            var page = Encoding.UTF8.GetBytes(Page(context.Request.Url!.AbsolutePath));
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = page.Length;
            await context.Response.OutputStream.WriteAsync(page, stop);
            context.Response.Close();
        }
    }

    /// <summary>The three pages of upstream's test app.</summary>
    private static string Page(string url)
    {
        if (url.StartsWith("/many", StringComparison.Ordinal))
        {
            var buttons = string.Concat(
                Enumerable
                    .Range(0, 300)
                    .Select(index => "<button data-i=\"" + index + "\">target " + index + "</button>")
            );
            return "<!doctype html><html><body><h1>Many</h1>"
                + buttons
                + "<p role=\"status\" id=\"status\"></p><script>for (const b of document.querySelectorAll(\"button\")) b.onclick = () => { document.getElementById(\"status\").textContent = \"picked \" + b.dataset.i; };</script></body></html>";
        }

        var login = url.StartsWith("/login", StringComparison.Ordinal);
        var handler = login
            ? "const valid = document.getElementById(\"password\").value === "
                + JsonSerializer.Serialize(Password)
                + ";document.getElementById(\"status\").textContent = valid ? \"Saved Ada\" : \"Invalid password\";"
            : "const name = document.getElementById(\"name\").value;const li = document.createElement(\"li\"); li.textContent = name; document.getElementById(\"list\").appendChild(li);";
        return "<!doctype html><html><body><h1>"
            + (login ? "Login" : "Todos")
            + "</h1>"
            + (login ? "" : "<label>New todo<input id=\"name\"></label>")
            + (
                login
                    ? "<label>Name<input id=\"name\"></label><label>Password<input id=\"password\" type=\"password\"></label>"
                    : ""
            )
            + "<button id=\"save\">"
            + (login ? "Sign in" : "Add")
            + "</button><ul id=\"list\"></ul><p role=\"status\" id=\"status\"></p><script>document.getElementById(\"save\").onclick = () => {"
            + handler
            + "};</script></body></html>";
    }

    [GeneratedRegex(
        "The next operation is \"([a-zA-Z_]+)\"",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex NextOperation();

    /// <summary>Upstream's scripted <c>decisionModel</c>: it picks the operation, the target, and the verdict from the state.</summary>
    private sealed class ScriptedDecision(List<string> requests, List<HashSet<string>> asked) : IDecisionModel
    {
        public string Provider => "scripted";

        public string ModelId => "scripted-1";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["choice"];

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            var state = call.State.OfType<DecisionJsonPart>().First().Value;
            lock (requests)
            {
                requests.Add(state.GetRawText());
                asked.Add([.. call.Questions.Keys]);
            }

            var goal = state.TryGetProperty("goal", out var goalValue) ? goalValue.GetString() ?? "" : "";
            var done = state.TryGetProperty("recentActions", out var recent)
                ? recent.EnumerateArray().Select(entry => entry.GetProperty("action").GetString() ?? "").ToList()
                : [];
            var answers = new Dictionary<string, DecisionAnswer>();
            foreach (var (id, question) in call.Questions)
            {
                var entries = question.Choices!;
                var keys = entries.Select(entry => entry.Value).ToList();
                string choice;
                if (id == "operation")
                {
                    choice = PickOperation(goal, done, keys);
                }
                else if (id == "verdict")
                {
                    var page =
                        state.TryGetProperty("page", out var pageValue)
                        && pageValue.TryGetProperty("text", out var text)
                            ? text.GetString() ?? ""
                            : "";
                    var holds =
                        page.Contains("Buy milk", StringComparison.Ordinal)
                        || page.Contains("Saved Ada", StringComparison.Ordinal)
                        || page.Contains("picked 5", StringComparison.Ordinal);
                    choice = holds ? "holds" : "fails";
                }
                else if (id == "secret")
                {
                    choice = "password";
                }
                else
                {
                    choice = PickTarget(question.Instructions ?? "", entries, goal);
                }

                answers[id] = new DecisionAnswer
                {
                    Type = "choice",
                    Choice = choice,
                    Probabilities = keys.ToDictionary(key => key, key => key == choice ? 1.0 : 0.0),
                };
            }

            return Task.FromResult(
                new DecisionModelResult
                {
                    Answers = answers,
                    Usage = new DecisionUsage(5, 0),
                    ResponseModelId = "scripted-1",
                }
            );
        }

        private static string PickOperation(string goal, List<string> done, List<string> keys)
        {
            string[] order = goal.Contains("Sign in", StringComparison.Ordinal)
                ? ["typeSecret", "type", "tap", "done"]
                : ["type", "tap", "done"];
            foreach (var op in order)
            {
                if (!keys.Contains(op))
                {
                    continue;
                }

                if (
                    op != "done"
                    && done.Any(action => action == op || action.StartsWith(op + " ", StringComparison.Ordinal))
                )
                {
                    continue;
                }

                return op;
            }

            return "blocked";
        }

        private static string PickTarget(string instructions, IReadOnlyList<DecisionChoice> entries, string goal)
        {
            string Label(DecisionChoice entry) => entry.Description ?? "";
            if (goal.Contains("target 5", StringComparison.Ordinal))
            {
                var hit = entries.FirstOrDefault(entry => Label(entry).Contains("target 5", StringComparison.Ordinal));
                if (hit is not null)
                {
                    return hit.Value;
                }
            }

            var op = NextOperation().Match(instructions) is { Success: true } match ? match.Groups[1].Value : "";
            if (op == "typeSecret")
            {
                var secret = entries.FirstOrDefault(entry =>
                    Label(entry).Contains("password", StringComparison.OrdinalIgnoreCase)
                );
                return (secret ?? entries[0]).Value;
            }

            var role =
                op is "type" or "typeSecret" ? "textbox"
                : op == "tap" ? "button"
                : "";
            var found = entries.FirstOrDefault(entry =>
                Label(entry).Contains("] " + role + " ", StringComparison.Ordinal)
            );
            return (found ?? entries[0]).Value;
        }
    }

    /// <summary>Upstream's scripted <c>textModel</c>: it answers a field prompt from the field's label.</summary>
    private sealed class ScriptedField(List<string> requests) : IAgentModel
    {
        public string Name => "scripted-text/s-1";

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            var raw = request.Messages.First(message => message.Role == "user").Content ?? "{}";
            lock (requests)
            {
                requests.Add(raw);
            }

            var asked = JsonNode.Parse(raw[raw.IndexOf('{', StringComparison.Ordinal)..])!;
            var label = asked["field"]?["label"]?.GetValue<string>() ?? "";
            var wanted =
                label.Contains("todo", StringComparison.OrdinalIgnoreCase) ? "Buy milk"
                : label.Contains("Name", StringComparison.Ordinal) ? "Ada"
                : null;
            return Task.FromResult(
                new ModelResponse
                {
                    Content = new JsonObject { ["text"] = wanted }.ToJsonString(),
                    Usage = new ModelCallUsage { InputTokens = 1, OutputTokens = 1 },
                }
            );
        }
    }
}
