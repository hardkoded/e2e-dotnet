// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Engine;
using E2E.Internal;
using E2E.OAuth;

namespace E2E.Cli;

/// <summary>
/// <c>pass</c>; <c>warn</c> for what a run handles on its own; <c>fail</c> for what stops a run; <c>skip</c> for a check that does not
/// apply; <c>unknown</c> for what doctor cannot tell. Upstream's <c>DoctorStatus</c>.
/// </summary>
internal enum DoctorStatus
{
    Pass,
    Warn,
    Fail,
    Skip,
    Unknown,
}

/// <summary>A model as the config names it: the provider and the model id.</summary>
internal sealed record DoctorModel(string Provider, string ModelId);

/// <summary>One target of the <c>config</c> check.</summary>
internal sealed record DoctorTarget(string Name, string Platform, string? Engine);

/// <summary>One check. <see cref="Target"/> names the target an engine check is about, <see cref="Agent"/> the agent a model check is about. Upstream's <c>DoctorCheck</c>.</summary>
internal sealed record DoctorCheck(string Id, DoctorStatus Status)
{
    public string? Target { get; init; }

    public string? Agent { get; init; }

    /// <summary>The e2e error code of a failure.</summary>
    public string? Code { get; init; }

    public string? Message { get; init; }

    /// <summary><c>config</c>: the config file.</summary>
    public string? Path { get; init; }

    /// <summary><c>config</c>: every target the config declares, or the ones <c>--target</c> named.</summary>
    public IReadOnlyList<DoctorTarget>? Targets { get; init; }

    /// <summary><c>model</c>: the agent's model.</summary>
    public DoctorModel? Model { get; init; }

    /// <summary><c>model</c>: the agent's judgment model, when it is not <see cref="Model"/>.</summary>
    public DoctorModel? Judge { get; init; }

    /// <summary><c>model</c>: the <c>e2e login</c> provider whose stored login the model reads.</summary>
    public string? Login { get; init; }
}

/// <summary>The report <c>e2e doctor --json</c> prints (schema <c>doctor-1</c>). Upstream's <c>DoctorReport</c>.</summary>
/// <param name="Version">The tool's version.</param>
/// <param name="Ok">False when any check failed.</param>
/// <param name="Checks">The checks, in the order they ran.</param>
internal sealed record DoctorReport(string Version, bool Ok, IReadOnlyList<DoctorCheck> Checks)
{
    /// <summary>The <c>schemaVersion</c> of the report, described by <c>schema/doctor-v1.schema.json</c>.</summary>
    public const string SchemaVersion = "doctor-1";

    /// <summary>The report as <c>e2e doctor --json</c> prints it.</summary>
    public string ToJson()
    {
        var checks = new JsonArray();
        foreach (var check in Checks)
        {
            var node = new JsonObject { ["id"] = check.Id, ["status"] = check.Status.ToString().ToLowerInvariant() };
            Add(node, "target", check.Target);
            Add(node, "agent", check.Agent);
            Add(node, "code", check.Code);
            Add(node, "message", check.Message);
            Add(node, "path", check.Path);
            if (check.Targets is { } targets)
            {
                node["targets"] = new JsonArray([.. targets.Select(target =>
                {
                    var item = new JsonObject { ["name"] = target.Name, ["platform"] = target.Platform };
                    Add(item, "engine", target.Engine);
                    return (JsonNode?)item;
                })]);
            }

            AddModel(node, "model", check.Model);
            AddModel(node, "judge", check.Judge);
            Add(node, "login", check.Login);
            checks.Add(node);
        }

        var report = new JsonObject { ["schemaVersion"] = SchemaVersion, ["version"] = Version, ["ok"] = Ok, ["checks"] = checks };
        return report.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static void Add(JsonObject node, string name, string? value)
    {
        if (value is not null)
        {
            node[name] = value;
        }
    }

    private static void AddModel(JsonObject node, string name, DoctorModel? model)
    {
        if (model is not null)
        {
            node[name] = new JsonObject { ["provider"] = model.Provider, ["modelId"] = model.ModelId };
        }
    }
}

/// <summary>What <see cref="Doctor.RunAsync"/> reads. Upstream's <c>DoctorOptions</c>; its <c>signal</c> is the call's <see cref="CancellationToken"/>.</summary>
internal sealed class DoctorOptions
{
    /// <summary>The tool's version, as the report names it.</summary>
    public required string Version { get; init; }

    /// <summary>The directory the config is searched from, and a relative <see cref="Config"/> resolves against.</summary>
    public required string Cwd { get; init; }

    /// <summary>The environment the config resolves against and a run would start with.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary><c>--config</c>.</summary>
    public string? Config { get; init; }

    /// <summary><c>--target</c>: the targets to check; all when null or empty.</summary>
    public IReadOnlyList<string>? Targets { get; init; }

    /// <summary><c>--headed</c>.</summary>
    public bool Headed { get; init; }

    /// <summary>Where an engine's progress lines go, under the target they concern.</summary>
    public required Action<string, string> Notice { get; init; }

    /// <summary>
    /// <c>--check-model</c>: call each agent's model once with a short prompt and the agent's provider options, and fail the check of one
    /// that does not answer.
    /// </summary>
    public bool CheckModel { get; init; }

    /// <summary>
    /// Builds the model of an agent for <c>--check-model</c>, the agent's judge when the flag is true. .NET-only test seam: upstream's tests
    /// bring a scripted instance in the config. Null builds it from the agent's provider, as a run does.
    /// </summary>
    public Func<AgentConfig, bool, IAgentModel?>? CreateModel { get; init; }

    /// <summary>How long each engine's doctor may take; 30 seconds by default.</summary>
    public TimeSpan? EngineTimeout { get; init; }

    /// <summary>
    /// Builds the engine of a target, given whether the run is headed. .NET-only: upstream's config holds the engine handle; the port's
    /// config names a platform, as <c>e2e explore</c> and <c>e2e mcp</c> read it. Null builds the engine of the target's platform.
    /// </summary>
    public Func<TargetConfig, bool, IEngine>? CreateEngine { get; init; }
}

/// <summary>
/// <c>e2e doctor</c>: whether <c>e2e explore</c> and <c>e2e mcp</c> can start here, checked without starting anything. It loads the config,
/// asks each target's engine what it needs on this machine, and names the model each agent resolved to. It never claims which credential a
/// model reads: only a login e2e stores is checked. Upstream's <c>cli/doctor.ts</c>.
/// </summary>
internal static partial class Doctor
{
    /// <summary>How long one engine's doctor may take: it should only look things up.</summary>
    private static readonly TimeSpan EngineDoctorTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a timed-out or cancelled engine doctor may take to settle.</summary>
    private static readonly TimeSpan EngineDoctorTeardown = TimeSpan.FromSeconds(15);

    /// <summary>How long one <c>--check-model</c> call may take, transport retries included.</summary>
    private static readonly TimeSpan ModelCallTimeout = TimeSpan.FromMinutes(1);

    /// <summary>Transport retries of a <c>--check-model</c> call: one rides out a blip, more would hide a refused connection behind the timeout.</summary>
    private const int ModelCallRetries = 1;

    /// <summary>The ids of doctor's own checks, which an engine's finding cannot take. <c>runtime</c> is .NET-only: upstream's <c>node</c> check names the runtime it needs.</summary>
    private static readonly HashSet<string> ReservedIds = new(["node", "config", "runtime", "engine", "model"], StringComparer.Ordinal);

    /// <summary>A check id: kebab case, as <see cref="EngineDoctorCheck.Id"/> declares.</summary>
    [GeneratedRegex(@"\A[a-z][a-z0-9-]*\z")]
    private static partial Regex CheckId();

    /// <summary>
    /// Runs every check: the runtime, then the config, then each selected target's engine and each agent's model. A config that does not
    /// load ends the checks there, since the rest read it; so does <paramref name="cancellationToken"/>, after the check it cut short.
    /// </summary>
    public static async Task<DoctorReport> RunAsync(DoctorOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var checks = new List<DoctorCheck> { RuntimeCheck() };
        var redactor = Redactor.For(DeviceTargets.Credentials());
        Loaded loaded;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            loaded = Load(options);
        }
        catch (Exception ex) when (ex is E2EException or OperationCanceledException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            checks.Add(Failed("config", ex));
            return Report(options.Version, checks, redactor);
        }

        redactor = Redactor.For(loaded.Config.AllSecrets.Concat(DeviceTargets.Credentials()));
        checks.Add(new DoctorCheck("config", DoctorStatus.Pass)
        {
            Path = loaded.Config.ConfigPath,
            Targets = [.. loaded.Targets.Select(item => new DoctorTarget(item.Target.Name, item.Target.Platform, item.Engine?.Name))],
        });
        foreach (var item in loaded.Targets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Report(options.Version, checks, redactor);
            }

            checks.AddRange(await EngineChecksAsync(item, loaded.Config, options, redactor, cancellationToken).ConfigureAwait(false));
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            checks.AddRange(await ModelChecksAsync(loaded.Config, options, redactor, cancellationToken).ConfigureAwait(false));
        }

        return Report(options.Version, checks, redactor);
    }

    /// <summary>The report for a person: one line per check, then the verdict. Upstream's <c>formatDoctorReport</c>.</summary>
    public static string FormatDoctorReport(DoctorReport report, Colors? colors = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        var pc = colors ?? new Colors(false);
        var width = report.Checks.Count == 0 ? 0 : report.Checks.Max(check => check.Id.Length);
        var lines = report.Checks.Select(check => $"{Mark(pc, check.Status)} {check.Id.PadRight(width)}  {Format.Bounded(Whitespace().Replace(Describe(check), " ").Trim())}");
        var failures = report.Checks.Count(check => check.Status == DoctorStatus.Fail);
        var verdict = failures == 0
            ? pc.Green("e2e explore and e2e mcp can start here")
            : pc.Red(failures.ToString(CultureInfo.InvariantCulture) + (failures == 1 ? " check failed" : " checks failed"));
        return string.Join('\n', lines) + "\n\n" + verdict + "\n";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static string Mark(Colors pc, DoctorStatus status) => status switch
    {
        DoctorStatus.Pass => pc.Green("✓"),
        DoctorStatus.Warn => pc.Yellow("!"),
        DoctorStatus.Fail => pc.Red("✗"),
        DoctorStatus.Skip => pc.Dim("-"),
        _ => pc.Dim("?"),
    };

    private static string Describe(DoctorCheck check)
    {
        var scope = check.Target is not null ? $"[{check.Target}] " : check.Agent is not null ? $"[{check.Agent}] " : "";
        var parts = new List<string>();
        if (check.Path is not null)
        {
            parts.Add(check.Path);
        }

        if (check.Targets is not null)
        {
            parts.Add("targets: " + string.Join(", ", check.Targets.Select(target => $"{target.Name} ({target.Engine ?? target.Platform})")));
        }

        if (check.Model is not null)
        {
            parts.Add(check.Model.Provider + "/" + check.Model.ModelId);
        }

        if (check.Judge is not null)
        {
            parts.Add("judge " + check.Judge.Provider + "/" + check.Judge.ModelId);
        }

        if (check.Message is not null)
        {
            parts.Add(check.Code is null ? check.Message : check.Code + ": " + check.Message);
        }

        return scope + string.Join(" · ", parts);
    }

    /// <summary>The report, every message redacted of the config's secrets: an engine or a provider error may quote one.</summary>
    private static DoctorReport Report(string version, IReadOnlyList<DoctorCheck> checks, Redactor redactor) =>
        new(version, checks.All(check => check.Status != DoctorStatus.Fail), [.. checks.Select(check => check.Message is null ? check : check with { Message = redactor.Redact(check.Message) })]);

    /// <summary>A failed check of <paramref name="id"/> carrying the e2e error code and message of <paramref name="cause"/>.</summary>
    private static DoctorCheck Failed(string id, Exception cause, string? target = null)
    {
        var (_, code, message, _) = ErrorSerializer.Classify(cause);
        return new DoctorCheck(id, DoctorStatus.Fail) { Target = target, Code = code, Message = message };
    }

    /// <summary>The runtime, which passed: a host that cannot start this program never reaches the check. .NET-only: upstream's <c>node</c>.</summary>
    private static DoctorCheck RuntimeCheck() =>
        new("runtime", DoctorStatus.Pass) { Message = RuntimeInformation.FrameworkDescription };

    /// <summary>A target the check selected, with its resolved app and the engine built for it, or why that failed.</summary>
    private sealed record TargetItem(ResolvedTarget Target, TargetConfig Config, ResolvedApp App, IEngine? Engine, Exception? Failure);

    private sealed record Loaded(E2EConfig Config, IReadOnlyList<TargetItem> Targets);

    /// <summary>Loads the config the way <c>e2e explore</c> does, selects the targets, and resolves their apps: a problem in any is the config's.</summary>
    private static Loaded Load(DoctorOptions options)
    {
        var cwd = Path.GetFullPath(options.Cwd);
        string path;
        if (options.Config is not null)
        {
            var absolute = Path.GetFullPath(options.Config, cwd);
            path = File.Exists(absolute) ? absolute : throw E2EConfig.ExplicitConfigNotFound(cwd, options.Config, absolute);
        }
        else
        {
            path = E2EConfig.Find(cwd) ?? throw new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one");
        }

        var config = E2EConfig.Load(path, name => options.Env.GetValueOrDefault(name));
        var declared = config.Targets.Select(target => new ResolvedTarget(target.Name, target.Platform, target.App)).ToList();
        var create = options.CreateEngine ?? ((target, headed) => TargetEngines.Create(target, headed ? false : null, DeviceTargets.Create, WindowsTargets.Create));
        var items = new List<TargetItem>();
        foreach (var selected in Selector.SelectTargets(declared, options.Targets))
        {
            var target = config.Targets.First(candidate => candidate.Name == selected.Name);
            var app = TargetApps.ResolveTargetApp(target.Name, target.App, config.ProjectRoot);
            IEngine? engine = null;
            Exception? failure = null;
            try
            {
                engine = create(target, options.Headed);
            }
#pragma warning disable CA1031 // A target whose engine cannot be built is that target's failed engine check; the others still run.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                failure = ex;
            }

            items.Add(new TargetItem(selected, target, app, engine, failure));
        }

        return new Loaded(config, items);
    }

    /// <summary>What the target's engine reports through its doctor, each finding under the target.</summary>
    private static async Task<IReadOnlyList<DoctorCheck>> EngineChecksAsync(TargetItem item, E2EConfig config, DoctorOptions options, Redactor redactor, CancellationToken cancellationToken)
    {
        var name = item.Target.Name;
        if (item.Failure is not null)
        {
            return [Failed("engine", item.Failure, name)];
        }

        var engine = item.Engine!;
        if (engine is not IDoctorEngine hook)
        {
            return [new DoctorCheck("engine", DoctorStatus.Skip) { Target = name, Message = $"engine \"{engine.Name}\" has no doctor checks" }];
        }

        var timeout = options.EngineTimeout ?? EngineDoctorTimeout;
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var info = new EngineDoctorInfo
        {
            TargetName = name,
            ProjectRoot = config.ProjectRoot,
            App = TargetApps.EngineAppInfoOf(item.App),
            Env = options.Env,
            Headed = options.Headed,
            Log = line => options.Notice(name, redactor.Redact(line)),
        };
        var pending = Task.Run(() => hook.DoctorAsync(info, linked.Token), CancellationToken.None);
        try
        {
            var findings = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
            return Findings(engine.Name, name, findings);
        }
#pragma warning disable CA1031 // Whatever the hook throws is the target's failed engine check.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // The hook stops at the token; waiting for it keeps doctor from returning while an engine still shuts down.
            var timedOut = deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            await Settled(pending).ConfigureAwait(false);
            Exception failure = timedOut
                ? new EngineException(EngineErrorCodes.EngineFailure, $"engine \"{engine.Name}\" did not finish its checks within {Time.BudgetText((long)timeout.TotalMilliseconds)}")
                : ex;
            return [Failed("engine", failure, name)];
        }
    }

    /// <summary>Waits for a hook that was asked to stop, within the teardown budget, and ignores how it ends.</summary>
    private static async Task Settled(Task pending)
    {
        try
        {
            await pending.WaitAsync(EngineDoctorTeardown).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The hook's own failure is already the check's; only its end matters here.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static IReadOnlyList<DoctorCheck> Findings(string engine, string target, IReadOnlyList<EngineDoctorCheck>? findings)
    {
        if (findings is null)
        {
            return [Failed("engine", Malformed(engine, "null"), target)];
        }

        var checks = new List<DoctorCheck>();
        foreach (var finding in findings)
        {
            if (!IsEngineFinding(finding))
            {
                return [Failed("engine", Malformed(engine, finding is null ? "null" : $"{{ id: \"{finding.Id}\", status: {(int)finding.Status} }}"), target)];
            }

            checks.Add(new DoctorCheck(finding.Id, finding.Status switch
            {
                EngineDoctorStatus.Pass => DoctorStatus.Pass,
                EngineDoctorStatus.Warn => DoctorStatus.Warn,
                EngineDoctorStatus.Fail => DoctorStatus.Fail,
                _ => DoctorStatus.Skip,
            })
            { Target = target, Message = finding.Message });
        }

        return checks;
    }

    /// <summary>Whether <paramref name="value"/> is a usable <see cref="EngineDoctorCheck"/>: an id in kebab case that doctor does not use itself, and a known status.</summary>
    private static bool IsEngineFinding(EngineDoctorCheck? value) =>
        value is { Id: { } id } && CheckId().IsMatch(id) && !ReservedIds.Contains(id) && Enum.IsDefined(value.Status);

    private static EngineException Malformed(string engine, string value) =>
        new(EngineErrorCodes.EngineFailure, $"engine \"{engine}\" returned a malformed doctor check: {Format.Bounded(value)}");

    /// <summary>
    /// One <c>model</c> check per agent. Without <c>--check-model</c>, a model on a stored login passes or fails on that login and any other
    /// is <c>unknown</c>; with it, each model answers one call per set of provider options and input limit the agents send it.
    /// </summary>
    private static async Task<IReadOnlyList<DoctorCheck>> ModelChecksAsync(E2EConfig config, DoctorOptions options, Redactor redactor, CancellationToken cancellationToken)
    {
        var calls = new Dictionary<string, Task<ModelFailure?>>(StringComparer.Ordinal);
        Func<AgentConfig, bool, Task<ModelFailure?>>? call = options.CheckModel
            ? (agent, judge) =>
            {
                var key = CallKey(agent, judge);
                if (!calls.TryGetValue(key, out var pending))
                {
                    pending = CallModelAsync(() => options.CreateModel is { } create ? create(agent, judge) : judge ? agent.CreateJudge() : agent.CreateModel(), agent, redactor, cancellationToken);
                    calls[key] = pending;
                }

                return pending;
            }
        : null;
        var checks = new List<DoctorCheck>();
        foreach (var (name, agent) in config.Agents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            checks.Add(await ModelCheckAsync(name, agent, call, cancellationToken).ConfigureAwait(false));
        }

        return checks;
    }

    /// <summary>Why a model's call failed: the e2e error code and message.</summary>
    private sealed record ModelFailure(string Code, string Message);

    /// <summary>What makes two agents' calls to one model the same call: the model, how it is reached, and the agent's provider options and input limit.</summary>
    private static string CallKey(AgentConfig agent, bool judge) =>
        JsonSerializer.Serialize(new object?[] { agent.Provider, judge ? agent.Judge : agent.Model, agent.BaseUrl, agent.ApiKeyEnv, agent.ProviderOptions, agent.MaxInputTokens });

    /// <summary>The <c>model</c> check of one agent: its model and, when it is another one, its judge, each checked on its own.</summary>
    private static async Task<DoctorCheck> ModelCheckAsync(string name, AgentConfig agent, Func<AgentConfig, bool, Task<ModelFailure?>>? call, CancellationToken cancellationToken)
    {
        var judge = agent.Judge is null || agent.Judge == agent.Model ? null : agent.Judge;
        if (agent.Model is null && judge is null)
        {
            return new DoctorCheck("model", DoctorStatus.Skip) { Agent = name, Message = "no model configured: tests without agent steps run, agent steps fail" };
        }

        var login = LoginModel.LoginOf(agent.Provider);
        DoctorCheck Result(DoctorStatus status, string? code, string message) => new("model", status)
        {
            Agent = name,
            Model = agent.Model is null ? null : new DoctorModel(agent.Provider, agent.Model),
            Judge = judge is null ? null : new DoctorModel(agent.Provider, judge),
            Login = login,
            Code = code,
            Message = message,
        };

        string? credential = null;
        if (login is not null)
        {
            var (ok, message) = await LoginCredentialAsync(login, cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                return Result(DoctorStatus.Fail, "MODEL_UNAVAILABLE", message);
            }

            credential = message;
        }

        if (call is not null)
        {
            var roles = new List<(string Role, bool Judge)>();
            if (agent.Model is not null)
            {
                roles.Add(("model", false));
            }

            if (judge is not null)
            {
                roles.Add(("judge", true));
            }

            var failures = new List<ModelFailure>();
            foreach (var (role, isJudge) in roles)
            {
                if (await call(agent, isJudge).ConfigureAwait(false) is { } failure)
                {
                    failures.Add(failure with { Message = roles.Count == 1 ? failure.Message : role + ": " + failure.Message });
                }
            }

            return failures.Count > 0
                ? Result(DoctorStatus.Fail, failures[0].Code, string.Join("; ", failures.Select(failure => failure.Message)))
                : Result(DoctorStatus.Pass, null, roles.Count == 1 ? "answered" : "both answered");
        }

        return credential is not null
            ? Result(DoctorStatus.Pass, null, credential)
            : Result(DoctorStatus.Unknown, null, "the model reads its own credential; --check-model calls it");
    }

    /// <summary>
    /// One small call to the model <paramref name="createModel"/> builds, with the agent's provider options, resolving to the failure or null. A reply that is not
    /// the JSON asked for, or a refusal, still proves the model answered; a call the provider ended on an error does not. Upstream's <c>callModel</c>.
    /// </summary>
    private static async Task<ModelFailure?> CallModelAsync(Func<IAgentModel?> createModel, AgentConfig agent, Redactor redactor, CancellationToken cancellationToken)
    {
        IAgentModel? model = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(ModelCallTimeout);
        try
        {
            model = createModel();
            if (model is null)
            {
                return null;
            }

            var response = await model.CompleteAsync(
                new ModelRequest
                {
                    System = "You are checking that this model is reachable.",
                    Messages = [new ModelMessage { Role = "user", Content = "Reply with the JSON object {\"ok\": true} and nothing else." }],
                    Tools = [],
                    ProviderOptions = agent.ProviderOptions,
                    MaxRetries = ModelCallRetries,
                    Redactor = redactor,
                },
                deadline.Token).ConfigureAwait(false);
            if (string.Equals(response.FinishReason, ModelResponse.ErrorFinish, StringComparison.Ordinal))
            {
                throw ModelHttp.ErrorFinishError(response.RawFinishReason, redactor);
            }

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return new ModelFailure("MODEL_PROVIDER_FAILED", "the model did not answer within " + Time.BudgetText((long)ModelCallTimeout.TotalMilliseconds));
        }
#pragma warning disable CA1031 // Any failure of the call, or of building the model, is the agent's failed model check; a cancel by the caller is not one.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            var (_, code, message, _) = ErrorSerializer.Classify(ex);
            return code is "MODEL_REFUSED" or "MODEL_OUTPUT_INVALID" ? null : new ModelFailure(code, message);
        }
        finally
        {
            (model as IDisposable)?.Dispose();
        }
    }

    /// <summary>The credential <paramref name="login"/>'s calls read: its API key variable when set, else the stored login, which must be there.</summary>
    private static async Task<(bool Ok, string Message)> LoginCredentialAsync(string login, CancellationToken cancellationToken)
    {
        var provider = OAuthProviders.Get(login);
        if (provider.ApiKeyEnv is { } variable && OAuthProviders.ApiKeyFor(provider) is not null)
        {
            return (true, variable + " is set");
        }

        try
        {
            return await CredentialStores.Default().GetAsync(login, cancellationToken).ConfigureAwait(false) is not null
                ? (true, $"the stored {login} login is present")
                : (false, $"no stored {login} login: {OAuthProviders.LoginHint(provider)}");
        }
#pragma warning disable CA1031 // A store that cannot be read is the login's failed check, whatever it threw.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return (false, $"the {login} login could not be read: {ex.Message}");
        }
    }
}
