// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>The least, the most, and the default of a bound option. Upstream's inline <c>{ min, max, default }</c>.</summary>
public sealed record ExploreBounds<T>(T Min, T Max, T Default);

/// <summary>What <see cref="Explore.ExploreAsync"/> explores, as, and with.</summary>
public sealed class ExploreOptions
{
    /// <summary>What to explore for. Blank is <see cref="Explore.DefaultGoal"/>. At most <see cref="Explore.MaxGoalChars"/> characters once its secret values are redacted.</summary>
    public string? Goal { get; init; }

    /// <summary>Where the config is looked for, and the project root when <see cref="RawConfig"/> is set. Defaults to the working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>The config file, instead of the nearest <c>e2e.config.json</c> above <see cref="Cwd"/>.</summary>
    public string? ConfigPath { get; init; }

    /// <summary>The target to explore. The config declares one; a name that is not it is <c>UNKNOWN_TARGET</c>.</summary>
    public string? Target { get; init; }

    /// <summary>The agent (<c>agents.&lt;name&gt;</c>) the exploration runs as. Defaults to <c>default</c>.</summary>
    public string? Agent { get; init; }

    /// <summary>Exploration steps, <see cref="Explore.StepBounds"/>.</summary>
    public int? MaxSteps { get; init; }

    /// <summary>The run's wall clock, <see cref="Explore.TimeoutBounds"/>.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Shows the browser window. Has no effect on an <see cref="Engine"/> of your own.</summary>
    public bool Headed { get; init; }

    /// <summary>The results directory, over the default <c>.e2e/results</c> under the project root. Finding screenshots are kept under it.</summary>
    public string? Output { get; init; }

    /// <summary>
    /// A config value instead of a discovered file, for a host that builds one in code. <see cref="Cwd"/> is the project root then.
    /// </summary>
    public E2EConfig? RawConfig { get; init; }

    /// <summary>
    /// Agents given in code, by name, replacing the config's entry of the same name. Upstream's config file is code, so its agents carry
    /// models and tools; <c>e2e.config.json</c> names a provider and a model id only, so a model of your own or a project tool is set here.
    /// </summary>
    public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }

    /// <summary>The engine to drive. Defaults to a <see cref="WebEngine"/>.</summary>
    public IEngine? Engine { get; init; }

    /// <summary>
    /// The accounts the explorer can sign in with, by name. Each travels into every step as the username and a password secret
    /// named <c>&lt;name&gt;.password</c>, which the explorer fills with <c>fill_secret</c>; the password never reaches a model.
    /// Upstream reads them from the config's <c>credentials</c>, which the port rejects; <see cref="Credentials.User"/> reads one from the environment.
    /// </summary>
    public IReadOnlyDictionary<string, UserCredential>? Credentials { get; init; }

    /// <summary>Notices decided before the run starts: the agent chosen, a replaced executor.</summary>
    public Action<string>? Notice { get; init; }

    /// <summary>Receives the exploration's progress as it happens. Upstream's <c>onEvent</c> sink for the <c>explore</c> run events.</summary>
    public ExploreListener? OnProgress { get; init; }
}

/// <summary>What an exploration ended with. Upstream's <c>ExploreOutcome</c>, which extends the runner's <c>RunOutcome</c>.</summary>
/// <param name="Status">How the run ended. An agent step the run could not finish for lack of credentials, environment, or setup is <see cref="RunStatus.Blocked"/>.</param>
/// <param name="ExitCode">0 when the exploration reported no issue (warnings do not count), 1 when it reported one or explored nothing, 2 for a configuration or policy error, 3 for an engine, app, or model provider failure.</param>
/// <param name="Error">The error that ended the run, or null for a pass.</param>
/// <param name="Explore">What was asked, did, and found.</param>
public sealed record ExploreOutcome(RunStatus Status, int ExitCode, E2EException? Error, ReportExplore Explore);

/// <summary>
/// An exploration: a run whose one test is a goal. The config is loaded as a test run loads it, the exploration body runs as the
/// run's only test, and within that test alone the explorer takes the place of the agent the run uses. The exploration's progress
/// reaches <see cref="ExploreOptions.OnProgress"/>, and its record is <see cref="ExploreOutcome.Explore"/>.
/// </summary>
public static class Explore
{
    /// <summary>The goal when none is given.</summary>
    public const string DefaultGoal = "Explore the app and find bugs";

    /// <summary>The most characters a goal may have once its secret values are redacted.</summary>
    public const int MaxGoalChars = 2_000;

    /// <summary>Each step's action and model-call budget when the agent sets none: a charter is longer than a scripted step.</summary>
    private const int DefaultStepBudget = 40;

    /// <summary>One charter's own deadline. The run's remaining time caps it.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(240);

    /// <summary>Added to the test timeout so the body's own clock ends the exploration before the attempt's does.</summary>
    private static readonly TimeSpan TimeoutGrace = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Every charter carries every configured account as a step parameter, and step parameters have a size limit. The inventory takes
    /// at most a quarter of it, leaving room for the findings that ride along, so an oversized one is a configuration error before
    /// anything starts and not a failed first step.
    /// </summary>
    private const int MaxCredentialBytes = ActParams.MaxBytes / 4;

    /// <summary>Exploration steps per run: breadth first, and diminishing returns come fast.</summary>
    public static ExploreBounds<int> StepBounds { get; } = new(1, 12, 8);

    /// <summary>The run's wall clock: three to fifteen minutes.</summary>
    public static ExploreBounds<TimeSpan> TimeoutBounds { get; } = new(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(10));

    /// <summary>
    /// Runs one exploration and returns the outcome with the exploration record. The config is resolved once here, with the agent
    /// <see cref="ExploreOptions.Agent"/> names, so the target, the agent the explorer is built from, and the credential inventory are
    /// what the run uses; a config that does not resolve, an unknown agent, or an unknown target is an error before anything starts,
    /// as a bad flag is. A step the app or the model fails is not thrown: it ends the run, and the outcome carries the error.
    /// A cancelled <paramref name="cancellationToken"/> throws <see cref="OperationCanceledException"/>; the run's own timeout ends it as an outcome whose error is <c>CANCELLED</c>.
    /// </summary>
    public static async Task<ExploreOutcome> ExploreAsync(ExploreOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var notice = options.Notice ?? (static _ => { });
        var goal = ResolveGoal(options.Goal);
        var budgets = new ExploreBudgets(
            ResolveBounded(options.MaxSteps, StepBounds, "maxSteps", value => value),
            ResolveBounded(options.Timeout, TimeoutBounds, "timeout", value => (int)value.TotalMilliseconds));

        var config = LoadConfig(options);
        var projectRoot = options.RawConfig is null ? config.ProjectRoot : Path.GetFullPath(options.Cwd ?? config.ProjectRoot);
        var target = PickTarget(config.Targets, options.Target);
        var credentials = NormalizeCredentials(options.Credentials);
        CheckCredentialInventory(credentials);

        // An exploration runs as exactly one agent: the one named, else `default`.
        var entries = config.Agents.ToDictionary(pair => pair.Key, pair => pair.Value.CreateOptions(), StringComparer.Ordinal);
        foreach (var pair in options.Agents ?? new Dictionary<string, AgentOptions>())
        {
            entries[pair.Key] = pair.Value;
        }

        var agentName = options.Agent ?? "default";
        if (!entries.TryGetValue(agentName, out var entry))
        {
            throw new ConfigurationException("INVALID_CONFIG", "unknown agent \"" + agentName + "\"; configured: " + string.Join(", ", entries.Keys));
        }

        if (!string.Equals(agentName, "default", StringComparison.Ordinal))
        {
            notice("exploring with agent \"" + agentName + "\"");
        }

        var secrets = config.Secrets.Names.Select(config.Secrets.Get).Concat(credentials.Values.Select(credential => credential.Password)).ToList();
        var state = new ExploreState(RedactGoal(goal, secrets), budgets);
        if (options.OnProgress is { } listener)
        {
            state.Subscribe(listener);
        }

        var resolved = ResolvedAgent.From(agentName, entry);
        var explorer = WithStepBudgets(ExploreExecutor.ExplorerAgent(new ExplorerOptions(state, agentName, entry, resolved, notice)));
        var resolvedExplorer = ResolvedAgent.From(agentName, explorer);

        // Every exploration step calls the model, a custom executor's included, since the built-in agent replaces it: no model is a
        // failure before anything starts, not a run-level one once the first step asks.
        if (resolvedExplorer.Model is null)
        {
            throw ResolvedAgent.MissingModelError(agentName, "explore runs the built-in agent, which requires a model");
        }

        // The explorer replaces the agent for the exploration alone. The exploration pins no retries and runs with the cache off.
        entries[agentName] = ExploreExecutor.WithFindingTool(explorer, state);
        var defaultAgent = entries["default"];
        var sessionOptions = new E2ESessionOptions
        {
            Engine = options.Engine ?? new WebEngine(options.Headed ? false : null),
            Model = defaultAgent.Model,
            Judge = defaultAgent.Judge,
            AgentExecutor = defaultAgent.Executor,
            AgentSystem = defaultAgent.System,
            AgentTools = defaultAgent.Tools,
            AgentContext = defaultAgent.Context,
            MaxSteps = defaultAgent.MaxSteps ?? E2EDefaults.MaxSteps,
            MaxModelCalls = defaultAgent.MaxModelCalls ?? E2EDefaults.MaxModelCalls,
            JudgmentTimeout = defaultAgent.JudgmentTimeout,
            ProviderOptions = defaultAgent.ProviderOptions,
            Agents = entries.Where(pair => !string.Equals(pair.Key, "default", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            BaseUrl = target.App.Url,
            ProjectRoot = projectRoot,
            TargetName = target.Name,
            ResultsDirectory = options.Output is null ? null : Path.GetFullPath(options.Output, projectRoot),
            CacheEnabled = false,
            CacheMode = CacheMode.Off,
            TestTitle = state.Goal,
            TestTimeout = budgets.Timeout + TimeoutGrace,
            LaunchTimeout = config.LaunchTimeout,
            CleanupTimeout = config.CleanupTimeout,
            ActionTimeout = config.ActionTimeout,
            AssertionTimeout = config.AssertionTimeout,
            StepTimeout = StepTimeout,
        };

        await using var session = await E2ESession.StartAsync(sessionOptions, cancellationToken).ConfigureAwait(false);

        // The values the goal, the app, or a model may echo are redacted wherever they appear.
        foreach (var secret in secrets)
        {
            session.Register(secret.Name, secret.Value);
        }

        var body = ExploreBody.Create(new ExploreBodyOptions(
            state,
            StepTimeout,
            target.App.Url is not null,
            credentials,
            string.Equals(agentName, "default", StringComparison.Ordinal) ? null : agentName));
        E2EException? error = null;
        try
        {
            await body(new ExploreFixtures(new SessionAgent(session.Agent), token => session.App.OpenAsync(null, token)), cancellationToken).ConfigureAwait(false);
        }
        catch (E2EException ex)
        {
            error = ex;
        }

        session.Complete(error, cancellationToken);
        return new ExploreOutcome(Runner.StatusOf(error), Runner.ExitCodeOf(error), error, state.Snapshot());
    }

    private static E2EConfig LoadConfig(ExploreOptions options)
    {
        if (options.RawConfig is { } raw)
        {
            return raw;
        }

        var cwd = Path.GetFullPath(options.Cwd ?? Directory.GetCurrentDirectory());
        var path = options.ConfigPath is null ? E2EConfig.Find(cwd) : Path.GetFullPath(options.ConfigPath, cwd);
        if (path is null || !File.Exists(path))
        {
            throw new ConfigurationException(
                "CONFIG_NOT_FOUND",
                path is null ? "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one" : "no config file at " + path);
        }

        return E2EConfig.Load(path);
    }

    private static string ResolveGoal(string? goal)
    {
        var trimmed = goal?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return DefaultGoal;
        }

        return trimmed.Length > MaxGoalChars
            ? throw new ConfigurationException("INVALID_CONFIG", "the goal must be at most " + MaxGoalChars.ToString(CultureInfo.InvariantCulture) + " characters, got " + trimmed.Length.ToString(CultureInfo.InvariantCulture))
            : trimmed;
    }

    /// <summary>
    /// The goal with every secret value it spells out redacted. The goal becomes the test's title, its results directory, the report's
    /// goal, and model input, so it is redacted once, here. A marker can be longer than the value it replaces, so the ceiling is
    /// checked again on what the run will keep.
    /// </summary>
    private static string RedactGoal(string goal, IEnumerable<Secret> secrets)
    {
        var redacted = Redactor.For(secrets).Redact(goal);
        return redacted.Length > MaxGoalChars
            ? throw new ConfigurationException(
                "INVALID_CONFIG",
                "the goal must be at most " + MaxGoalChars.ToString(CultureInfo.InvariantCulture) + " characters with its secret values redacted, got " + redacted.Length.ToString(CultureInfo.InvariantCulture))
            : redacted;
    }

    private static T ResolveBounded<T>(T? value, ExploreBounds<T> bounds, string label, Func<T, int> number)
        where T : struct, IComparable<T>
    {
        if (value is not { } given)
        {
            return bounds.Default;
        }

        if (given.CompareTo(bounds.Min) < 0 || given.CompareTo(bounds.Max) > 0)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                label + " must be an integer from " + number(bounds.Min).ToString(CultureInfo.InvariantCulture) + " through " + number(bounds.Max).ToString(CultureInfo.InvariantCulture)
                + ", got " + number(given).ToString(CultureInfo.InvariantCulture));
        }

        return given;
    }

    /// <summary>The target to explore: the one <see cref="ExploreOptions.Target"/> names (an unknown one is <c>UNKNOWN_TARGET</c>), else the first configured.</summary>
    private static TargetConfig PickTarget(IReadOnlyList<TargetConfig> targets, string? requested)
    {
        if (requested is null)
        {
            return targets[0];
        }

        return targets.FirstOrDefault(target => string.Equals(target.Name, requested, StringComparison.Ordinal))
            ?? throw new ConfigurationException(
                "UNKNOWN_TARGET",
                "unknown target ID \"" + requested + "\"; the config declares " + string.Join(", ", targets.Select(target => "\"" + target.Name + "\"")));
    }

    /// <summary>
    /// The explorer's agent with per-step budgets that default higher than a scripted step's, a charter being longer. An agent that
    /// left a budget out has set none, whatever number it would default to.
    /// </summary>
    internal static AgentOptions WithStepBudgets(AgentOptions explorer) => new()
    {
        Model = explorer.Model,
        Judge = explorer.Judge,
        System = explorer.System,
        Context = explorer.Context,
        MaxSteps = explorer.MaxSteps ?? DefaultStepBudget,
        MaxModelCalls = explorer.MaxModelCalls ?? DefaultStepBudget,
        JudgmentTimeout = explorer.JudgmentTimeout,
        ProviderOptions = explorer.ProviderOptions,
        Tools = explorer.Tools,
    };

    /// <summary>Each account's password as the secret <c>&lt;name&gt;.password</c> for a password field, as upstream's registry names it.</summary>
    private static Dictionary<string, UserCredential> NormalizeCredentials(IReadOnlyDictionary<string, UserCredential>? credentials) =>
        (credentials ?? new Dictionary<string, UserCredential>()).ToDictionary(
            pair => pair.Key,
            pair => new UserCredential(pair.Value.Username, Secret.Create(pair.Key + ".password", pair.Value.Password.Value, "password")),
            StringComparer.Ordinal);

    /// <summary>
    /// The accounts every charter carries, bounded to what a step parameter holds. Measured as the step sees them: the usernames and
    /// the secret placeholders the passwords become there.
    /// </summary>
    private static void CheckCredentialInventory(IReadOnlyDictionary<string, UserCredential> credentials)
    {
        var carried = new JsonObject();
        foreach (var (name, credential) in credentials)
        {
            carried[name] = new JsonObject
            {
                ["username"] = credential.Username,
                ["password"] = new JsonObject { ["kind"] = "secret", ["name"] = credential.Password.Name, ["purpose"] = "password" },
            };
        }

        var bytes = Encoding.UTF8.GetByteCount(new JsonObject { ["credentials"] = carried }.ToJsonString());
        if (bytes > MaxCredentialBytes)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                "explore carries every configured credential into each step: " + credentials.Count.ToString(CultureInfo.InvariantCulture) + " account(s) serialize to "
                + bytes.ToString(CultureInfo.InvariantCulture) + " bytes, the maximum is " + MaxCredentialBytes.ToString(CultureInfo.InvariantCulture) + "; explore with fewer credentials");
        }
    }

    /// <summary>The session's <see cref="Agent"/> as the exploration body reads it.</summary>
    private sealed class SessionAgent(Agent agent) : IExploreAgent
    {
        public Task<ActResult> ActAsync(string instruction, ActOptions? options, CancellationToken cancellationToken) =>
            agent.ActAsync(instruction, options, cancellationToken);

        public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken) =>
            agent.ExtractAsync<T>(instruction, options, cancellationToken);
    }
}
