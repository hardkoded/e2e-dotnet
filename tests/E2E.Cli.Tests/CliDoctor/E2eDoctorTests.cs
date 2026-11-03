// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
/// comes from a factory. The <c>node</c> check is <c>runtime</c> here. The <c>--check-model</c> tests have no port yet: the flag is refused.
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

    private async Task<(int Code, string Stdout, string Stderr)> RunAsync(string[] args, string mode = "ok", CancellationToken cancellationToken = default, CancellationTokenSource? interrupt = null)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await DoctorCommand.RunAsync(args, stdout, stderr, interactive: false, cancellationToken, _dir, Engines(mode, interrupt), new Dictionary<string, string?>());
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

    [Fact]
    public async Task Partial_Names_each_agents_model_and_calls_it_only_with_check_model()
    {
        // The agents of upstream's test that hold a custom executor have no port: the config names a model and a provider. The
        // --check-model half has no port yet: the flag is refused, and no model is called.
        WriteConfig("""{ "default": { "model": "answers" }, "split": { "model": "answers", "judge": "fails" } }""");
        var (code, _, checks) = await JsonAsync([]);
        Assert.Equal(0, code);
        var models = checks.Where(check => (string)check["id"]! == "model").ToList();
        Assert.Equal(
            ["""{"id":"model","status":"unknown","agent":"default","message":"the model reads its own credential","model":{"provider":"openai","modelId":"answers"}}""",
             """{"id":"model","status":"unknown","agent":"split","message":"the model reads its own credential","model":{"provider":"openai","modelId":"answers"},"judge":{"provider":"openai","modelId":"fails"}}"""],
            models.Select(check => check.ToJsonString()));
        var refused = await RunAsync(["--check-model"]);
        Assert.Equal((2, ""), (refused.Code, refused.Stdout));
        Assert.Contains("option '--check-model' is not supported by the .NET port yet", refused.Stderr, StringComparison.Ordinal);
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
