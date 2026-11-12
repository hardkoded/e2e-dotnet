// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.Cli.Tests.CliLogin;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;
using E2E.OAuth;
using E2E.Tests;

namespace E2E.Cli.Tests.CliDoctor;

/// <summary>
/// <c>describe('e2e doctor')</c> of upstream <c>tests/integration/cli-doctor.test.ts</c>: the doctor-1 report a host parses, the exit code
/// it gates on, an engine's own checks, and what it says about each agent's model. Upstream spawns the built CLI with a config that holds
/// engine handles; the port runs <see cref="DoctorCommand"/> in process over a config file that names a platform, and the target's engine
/// comes from a factory. The <c>node</c> check is <c>runtime</c> here. A model is scripted by its id through <see cref="Models"/>, where upstream scripts an AI SDK instance in the config.
/// </summary>
[Collection(E2eLoginFlagsTests.Name)]
public sealed class E2eDoctorTests : IDisposable
{
    private static readonly JsonSchemaCheck Schema = JsonSchemaCheck.FromFile("doctor-v1.schema.json");

    private const string Stripe = "sk_live_generic_4242";

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-doctor-").FullName;
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    private readonly string? _credentials = Environment.GetEnvironmentVariable(CredentialStores.EnvironmentVariable);

    public E2eDoctorTests()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_dir, ".config"));
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, "");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _xdg);
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, _credentials);
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>A config with one target on an engine whose doctor reports <c>toolchain</c> as the mode says, a second on the engine with no checks, and <paramref name="agents"/>.</summary>
    private void WriteConfig(string agents = "")
    {
        File.WriteAllText(
            Path.Combine(_dir, "e2e.config.json"),
            $$"""
            {
              "secrets": { "stripe-key": "{{Stripe}}" },
              "targets": [
                { "name": "checked", "platform": "web", "app": { "url": "http://127.0.0.1:3000" } },
                { "name": "plain", "platform": "web", "app": { "url": "http://127.0.0.1:3001" } }
              ]{{(agents.Length == 0 ? "" : ", \"agents\": " + agents)}}
            }
            """);
    }

    /// <summary>The engine of the <c>checked</c> target: its doctor reports <c>toolchain</c> as <paramref name="mode"/> says.</summary>
    private static Func<TargetConfig, bool, IEngine> Engines(string mode, CancellationTokenSource? interrupt = null) =>
        (target, _) => target.Name != "checked"
            ? new FakeEngine()
            : new DoctorEngine("checked", async (info, token) =>
            {
                switch (mode)
                {
                    case "throw":
                        throw new InvalidOperationException("toolchain probe crashed");
                    case "malformed":
                        return [new EngineDoctorCheck("Bad Id", (EngineDoctorStatus)99)];
                    case "reserved":
                        return [new EngineDoctorCheck("model", EngineDoctorStatus.Pass)];
                    case "hang":
                        info.Log("checking");
                        interrupt!.Cancel();
                        await Task.Delay(Timeout.Infinite, token);
                        return [];
                    case "leak":
                        return [new EngineDoctorCheck("toolchain", EngineDoctorStatus.Fail, "token " + Stripe + " rejected")];
                    default:
                        return [new EngineDoctorCheck("toolchain", mode == "missing" ? EngineDoctorStatus.Fail : EngineDoctorStatus.Pass, $"{info.TargetName} headed={info.Headed.ToString().ToLowerInvariant()}")];
                }
            });

    /// <summary>How many calls each scripted model got.</summary>
    private readonly Dictionary<string, int> _calls = [];

    /// <summary>The models a test names by id: <c>answers</c>, <c>counted</c>, <c>refuses</c>, <c>fails</c>, and <c>errors</c>. Upstream's scripted instances.</summary>
    private IAgentModel? Models(AgentConfig agent, bool judge)
    {
        var id = judge ? agent.Judge : agent.Model;
        return id is null
            ? null
            : new ScriptedModel(_ =>
            {
                lock (_calls)
                {
                    _calls[id] = _calls.GetValueOrDefault(id) + 1;
                }

                return id switch
                {
                    "refuses" => new ModelResponse { FinishReason = ModelResponse.ContentFilter },
                    "errors" => new ModelResponse { FinishReason = ModelResponse.ErrorFinish, RawFinishReason = "error" },
                    "fails" => throw new AgentException("MODEL_PROVIDER_FAILED", "The model provider failed: provider is down"),
                    _ => new ModelResponse { Content = "OK" },
                };
            });
    }

    private async Task<(int Code, string Stdout, string Stderr)> RunAsync(string[] args, string mode = "ok", CancellationToken cancellationToken = default, CancellationTokenSource? interrupt = null)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await DoctorCommand.RunAsync(args, stdout, stderr, interactive: false, cancellationToken, _dir, Engines(mode, interrupt), new Dictionary<string, string?>(), Models);
        return (code, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Runs <c>e2e doctor --json</c> and parses the report, which must match doctor-1.</summary>
    private async Task<(int Code, JsonObject Report, JsonNode[] Checks)> JsonAsync(string[] args, string mode = "ok", CancellationToken cancellationToken = default, CancellationTokenSource? interrupt = null)
    {
        var (code, stdout, _) = await RunAsync(["--json", .. args], mode, cancellationToken, interrupt);
        var report = JsonNode.Parse(stdout)!.AsObject();
        Assert.True(Schema.IsValid(report), stdout);
        return (code, report, [.. report["checks"]!.AsArray().Select(check => check!)]);
    }

    private static string[] Row(JsonNode check) => [(string)check["id"]!, (string)check["status"]!, (string?)check["target"] ?? ""];

    [Fact]
    public async Task Reports_the_runtime_the_config_and_its_targets_and_each_engine_and_exits_0()
    {
        WriteConfig();
        var (code, report, checks) = await JsonAsync(["--headed"]);
        Assert.Equal(0, code);
        Assert.True((bool)report["ok"]!);
        Assert.Equal(
            [["runtime", "pass", ""], ["config", "pass", ""], ["toolchain", "pass", "checked"], ["engine", "skip", "plain"], ["model", "skip", ""]],
            checks.Select(Row));
        Assert.Equal("checked headed=true", (string)checks.First(check => (string)check["id"]! == "toolchain")["message"]!);
        Assert.Equal(
            """[{"name":"checked","platform":"web","engine":"checked"},{"name":"plain","platform":"web","engine":"web"}]""",
            checks.First(check => (string)check["id"]! == "config")["targets"]!.ToJsonString());
    }

    [Fact]
    public async Task Exits_2_on_an_engines_failed_check_or_a_throw_and_prints_one_line_per_check()
    {
        WriteConfig();
        var missing = await JsonAsync([], "missing");
        Assert.Equal(2, missing.Code);
        Assert.False((bool)missing.Report["ok"]!);
        var thrown = await JsonAsync(["--target", "checked"], "throw");
        Assert.Equal(2, thrown.Code);
        var engine = thrown.Checks.First(check => (string?)check["target"] == "checked");
        Assert.Equal(("engine", "fail", "toolchain probe crashed"), ((string)engine["id"]!, (string)engine["status"]!, (string)engine["message"]!));
        var list = await RunAsync([], "missing");
        Assert.Equal(2, list.Code);
        Assert.Contains("✗ toolchain  [checked] checked headed=false", list.Stdout, StringComparison.Ordinal);
        Assert.Contains("1 check failed", list.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_an_engine_whose_check_is_malformed_and_redacts_the_configs_secrets_from_every_message()
    {
        WriteConfig();
        foreach (var mode in new[] { "malformed", "reserved" })
        {
            var result = await JsonAsync(["--target", "checked"], mode);
            Assert.Equal(2, result.Code);
            var check = result.Checks[^2];
            Assert.Equal(["engine", "fail", "checked"], Row(check));
            Assert.Equal("ENGINE_FAILURE", (string)check["code"]!);
        }

        var leaked = await JsonAsync(["--target", "checked"], "leak");
        Assert.Equal("token <secret:stripe-key> rejected", (string)leaked.Checks.First(check => (string)check["id"]! == "toolchain")["message"]!);
        Assert.DoesNotContain(Stripe, (await RunAsync([], "leak")).Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_at_a_config_that_does_not_load_with_its_error_code()
    {
        var missing = await JsonAsync([]);
        Assert.Equal(2, missing.Code);
        Assert.Equal(
            [("runtime", "pass", (string?)null), ("config", "fail", "CONFIG_NOT_FOUND")],
            missing.Checks.Select(check => ((string)check["id"]!, (string)check["status"]!, (string?)check["code"])));
        WriteConfig();
        var unknown = await JsonAsync(["--target", "nope"]);
        Assert.Equal(("config", "fail", "UNKNOWN_TARGET"), ((string)unknown.Checks[^1]["id"]!, (string)unknown.Checks[^1]["status"]!, (string)unknown.Checks[^1]["code"]!));
    }

    private const string Unchecked = "the model reads its own credential; --check-model calls it";

    [Fact]
    public async Task Partial_Names_each_agents_model_and_calls_it_only_with_check_model()
    {
        // Partial: the agent of upstream's test that holds a custom executor has no port: the config names a model and a provider.
        WriteConfig("""
            { "default": { "model": "answers" }, "down": { "model": "fails" }, "split": { "model": "answers", "judge": "fails" },
              "broken": { "model": "errors" }, "both": { "model": "fails", "judge": "errors" } }
            """);
        var named = await JsonAsync([]);
        Assert.Equal(0, named.Code);
        Assert.Equal(
            [
                $$$"""{"id":"model","status":"unknown","agent":"default","message":"{{{Unchecked}}}","model":{"provider":"openai","modelId":"answers"}}""",
                $$$"""{"id":"model","status":"unknown","agent":"down","message":"{{{Unchecked}}}","model":{"provider":"openai","modelId":"fails"}}""",
                $$$"""{"id":"model","status":"unknown","agent":"split","message":"{{{Unchecked}}}","model":{"provider":"openai","modelId":"answers"},"judge":{"provider":"openai","modelId":"fails"}}""",
                $$$"""{"id":"model","status":"unknown","agent":"broken","message":"{{{Unchecked}}}","model":{"provider":"openai","modelId":"errors"}}""",
                $$$"""{"id":"model","status":"unknown","agent":"both","message":"{{{Unchecked}}}","model":{"provider":"openai","modelId":"fails"},"judge":{"provider":"openai","modelId":"errors"}}""",
            ],
            named.Checks.Where(check => (string)check["id"]! == "model").Select(check => check.ToJsonString()));
        Assert.Empty(_calls);

        var checkedModels = await JsonAsync(["--check-model"]);
        Assert.Equal(2, checkedModels.Code);
        var rows = checkedModels.Checks.Where(check => (string)check["id"]! == "model").Select(check => ((string)check["agent"]!, (string)check["status"]!, (string?)check["code"], (string?)check["message"])).ToList();
        Assert.Equal(("default", "pass", null, "answered"), rows[0]);
        Assert.Equal(("down", "fail", "MODEL_PROVIDER_FAILED"), (rows[1].Item1, rows[1].Item2, rows[1].Item3));
        Assert.Contains("provider is down", rows[1].Item4, StringComparison.Ordinal);
        Assert.Equal(("split", "fail", "MODEL_PROVIDER_FAILED"), (rows[2].Item1, rows[2].Item2, rows[2].Item3));
        Assert.Matches(@"\Ajudge: .*provider is down", rows[2].Item4);
        Assert.Equal(("broken", "fail", "MODEL_PROVIDER_FAILED"), (rows[3].Item1, rows[3].Item2, rows[3].Item3));
        Assert.Contains("error finish", rows[3].Item4, StringComparison.Ordinal);
        Assert.Equal(("both", "fail", "MODEL_PROVIDER_FAILED"), (rows[4].Item1, rows[4].Item2, rows[4].Item3));
        Assert.Matches(@"\Amodel: .*provider is down.*; judge: .*error finish", rows[4].Item4);
    }

    [Fact]
    public async Task Makes_one_call_for_agents_with_equal_provider_options_and_counts_a_refusal_as_an_answer()
    {
        WriteConfig("""
            { "default": { "model": "counted", "providerOptions": { "openai": { "effort": "low" } } },
              "twin": { "model": "counted", "providerOptions": { "openai": { "effort": "low" } } },
              "narrow": { "model": "counted", "providerOptions": { "openai": { "effort": "low" } }, "maxInputTokens": 50000 },
              "shy": { "model": "refuses" } }
            """);
        var (code, _, checks) = await JsonAsync(["--check-model"]);
        Assert.Equal(0, code);
        Assert.Equal(
            [("default", "pass"), ("twin", "pass"), ("narrow", "pass"), ("shy", "pass")],
            checks.Where(check => (string)check["id"]! == "model").Select(check => ((string)check["agent"]!, (string)check["status"]!)));
        Assert.Equal(2, _calls["counted"]);
    }

    [Fact]
    public async Task Partial_Retries_a_refused_connection_once_and_reports_its_error_not_the_call_timeout()
    {
        // Partial: the port's models are not the AI SDK's, so the connection is a local server that answers 503, which the transport retries
        // like a refused connection; the server counts the requests, where upstream's scripted instance counts its calls.
        using var server = new HttpListener();
        var port = Random.Shared.Next(20000, 40000);
        server.Prefixes.Add($"http://127.0.0.1:{port}/");
        server.Start();
        var requests = 0;
        _ = Task.Run(async () =>
        {
            while (server.IsListening)
            {
                try
                {
                    var context = await server.GetContextAsync();
                    Interlocked.Increment(ref requests);
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        });
        Environment.SetEnvironmentVariable("E2E_DOCTOR_TEST_KEY", "doctor-test-key-0123456789");
        try
        {
            WriteConfig($$"""{ "default": { "model": "refused", "baseUrl": "http://127.0.0.1:{{port}}/v1", "apiKeyEnv": "E2E_DOCTOR_TEST_KEY" } }""");
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var code = await DoctorCommand.RunAsync(["--json", "--check-model"], stdout, stderr, interactive: false, CancellationToken.None, _dir, Engines("ok"), new Dictionary<string, string?>());
            var last = JsonNode.Parse(stdout.ToString())!["checks"]!.AsArray()[^1]!;
            Assert.Equal(2, code);
            Assert.Equal(("model", "fail", "MODEL_PROVIDER_FAILED"), ((string)last["id"]!, (string)last["status"]!, (string)last["code"]!));
            Assert.Contains("503", (string)last["message"]!, StringComparison.Ordinal);
            Assert.Equal(2, Volatile.Read(ref requests));
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_DOCTOR_TEST_KEY", null);
            server.Stop();
        }
    }

    [Fact]
    public async Task Partial_Stops_the_checks_at_Ctrl_C_reports_them_and_exits_130()
    {
        // Partial: the port cancels the token Program.Main's Ctrl-C handler cancels; no signal is sent to a child process.
        WriteConfig();
        using var interrupt = new CancellationTokenSource();
        var (code, _, checks) = await JsonAsync(["--target", "checked,plain"], "hang", interrupt.Token, interrupt);
        Assert.Equal(130, code);
        var engine = checks.First(check => (string?)check["target"] == "checked");
        Assert.Equal(("engine", "fail", "CANCELLED"), ((string)engine["id"]!, (string)engine["status"]!, (string)engine["code"]!));
        Assert.Equal(engine.ToJsonString(), checks[^1].ToJsonString());
    }

    [Fact]
    public async Task Partial_Reports_a_config_still_loading_at_Ctrl_C_as_cancelled()
    {
        // Partial: the port loads a config in one synchronous read, so no load is still running when Ctrl-C comes; the token is already cancelled.
        WriteConfig();
        using var interrupt = new CancellationTokenSource();
        await interrupt.CancelAsync();
        var (code, _, checks) = await JsonAsync([], cancellationToken: interrupt.Token);
        Assert.Equal(130, code);
        Assert.Equal(("config", "fail", "CANCELLED"), ((string)checks[^1]["id"]!, (string)checks[^1]["status"]!, (string)checks[^1]["code"]!));
    }

    [Fact]
    public async Task Checks_the_stored_login_of_a_subscription_model_without_calling_it()
    {
        WriteConfig("""{ "default": { "provider": "chatgpt", "model": "gpt-6-luna" } }""");
        var absent = await JsonAsync([]);
        Assert.Equal(2, absent.Code);
        var last = absent.Checks[^1];
        Assert.Equal(("model", "fail", "openai", "MODEL_UNAVAILABLE"), ((string)last["id"]!, (string)last["status"]!, (string)last["login"]!, (string)last["code"]!));
        Environment.SetEnvironmentVariable(CredentialStores.EnvironmentVariable, """{ "openai": { "access": "a", "refresh": "r", "expires": 0 } }""");
        var stored = await JsonAsync([]);
        Assert.Equal(0, stored.Code);
        last = stored.Checks[^1];
        Assert.Equal(("model", "pass", "openai", "gpt-6-luna"), ((string)last["id"]!, (string)last["status"]!, (string)last["login"]!, (string)last["model"]!["modelId"]!));
    }
}
