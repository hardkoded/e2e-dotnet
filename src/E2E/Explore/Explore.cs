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

    /// <summary>The engine to drive. Defaults to the engine of the target's platform: a <see cref="WebEngine"/> for <c>web</c>.</summary>
    public IEngine? Engine { get; init; }

    /// <summary>
    /// Builds the engine of an <c>ios</c> or <c>android</c> target when <see cref="Engine"/> is not set. .NET-only: <c>E2E</c> cannot reference
    /// <c>E2E.Mobile</c>, so the host (<c>e2e explore</c>) supplies it.
    /// </summary>
    internal Func<TargetConfig, IEngine>? CreateMobileEngine { get; init; }

    /// <summary>
    /// Builds the engine of a <c>windows</c> target when <see cref="Engine"/> is not set. .NET-only: <c>E2E</c> cannot reference
    /// <c>E2E.WinApp</c>, so the host (<c>e2e explore</c>) supplies it.
    /// </summary>
    internal Func<TargetConfig, IEngine>? CreateWindowsEngine { get; init; }

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

    /// <summary>
    /// Which attempts keep a trace page, upstream's <c>--trace [mode]</c>; over the target's and the config's <c>trace</c>. The exploration is
    /// one attempt, so a retry mode keeps none. Defaults to the target's mode, <c>retain-on-failure</c> unless the config says otherwise.
    /// An exploration's own verdict, the issues its findings tell, gets no page.
    /// </summary>
    public RecordingMode? Trace { get; init; }

    /// <summary>
    /// Which attempts record a video, upstream's <c>--video [mode]</c>; over the target's and the config's <c>video</c>. The exploration is
    /// one attempt, so a retry mode records none. Defaults to the target's mode, <c>off</c> unless the config says otherwise.
    /// </summary>
    public RecordingMode? Video { get; init; }

    /// <summary>Upstream's <c>--debug</c>: prints the phase timings and the agent step table to stderr. <c>E2E_DEBUG=1</c> turns it on too.</summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Upstream's <c>--ai-trace</c>: records every model call to <c>ai-trace-&lt;process id&gt;.json</c> next to the results directory
    /// (<c>.e2e</c> by default). <c>E2E_AI_TRACE=1</c> turns it on too.
    /// </summary>
    public bool AiTrace { get; init; }
}

/// <summary>What an exploration ended with. Upstream's <c>ExploreOutcome</c>, which extends the runner's <c>RunOutcome</c>.</summary>
/// <param name="Status">How the run ended. An agent step the run could not finish for lack of credentials, environment, or setup is <see cref="RunStatus.Blocked"/>.</param>
/// <param name="ExitCode">0 when the exploration reported no issue (warnings do not count), 1 when it reported one or explored nothing, 2 for a configuration or policy error, 3 for an engine, app, or model provider failure. A failure of closing the exploration joins it: the code is the verdict's combined with the cleanup failure's (precedence 130, 4, 3, 2, 1, 0).</param>
/// <param name="Error">The error that ended the run, or else the first cleanup failure, or null for a pass.</param>
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

    /// <summary>
    /// The virtual file of the exploration's test: it joins the goal in the result id and leads the result's directory, and
    /// it makes the exploration a test, so every prompt opens with <c>Test: &lt;goal&gt;</c>. Upstream's <c>EXPLORE_FILE</c>.
    /// </summary>
    internal const string ExploreFile = "explore";

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
    /// as a bad flag is. A step the app or the model fails is not thrown: it ends the run, and the outcome carries the error. A failure of closing the attempt (the engine's finish hook) is not thrown either: it joins the outcome as a cleanup error, and its exit code (3 for an engine failure) outranks a verdict's 1, as a run's run errors do.
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
        var redactor = Redactor.For(secrets);
        var state = new ExploreState(RedactGoal(goal, redactor), budgets, redactor: redactor);
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
        var engine = options.Engine ?? TargetEngines.Create(target, options.Headed ? false : null, options.CreateMobileEngine, options.CreateWindowsEngine);
        var trace = options.Trace is { } traceFlag ? new ResolvedRecording(traceFlag, RecordingSource.Run) : target.Trace;
        var video = options.Video is { } videoFlag ? new ResolvedRecording(videoFlag, RecordingSource.Run) : target.Video;
        foreach (var message in RecordingNotices(trace.Mode, video, (engine.Capabilities & EngineCapabilities.Video) != 0, target.Name, engine.Platform))
        {
            notice(message);
        }

        // The target's app command starts before the session opens, and the URL is on the port it was started on.
        var (appTarget, appProcesses) = await RunApps.AcquireAsync(config, target, notice, cancellationToken).ConfigureAwait(false);
        target = appTarget;
        var appFailures = new List<Exception>();
        StandaloneAttempt attempt;
        try
        {
            var sessionOptions = new E2ESessionOptions
            {
                Engine = engine,
                Model = defaultAgent.Model,
                Judge = defaultAgent.Judge,
                AgentExecutor = defaultAgent.Executor,
                AgentSystem = defaultAgent.System,
                AgentTools = defaultAgent.Tools,
                AgentContext = defaultAgent.Context,
                MaxSteps = defaultAgent.MaxSteps ?? E2EDefaults.MaxSteps,
                MaxModelCalls = defaultAgent.MaxModelCalls ?? E2EDefaults.MaxModelCalls,
                JudgmentTimeout = defaultAgent.JudgmentTimeout,
                MaxObservationBytes = defaultAgent.MaxObservationBytes,
                MaxInputTokens = defaultAgent.MaxInputTokens,
                ProviderOptions = defaultAgent.ProviderOptions,
                Agents = entries.Where(pair => !string.Equals(pair.Key, "default", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                BaseUrl = target.App.Url,
                Permissions = target.App.Permissions,
                ProjectRoot = projectRoot,
                TargetName = target.Name,
                ResultsDirectory = options.Output is null ? null : Path.GetFullPath(options.Output, projectRoot),
                CacheEnabled = false,
                CacheMode = CacheMode.Off,
                TestTitle = state.Goal,
                TestFile = ExploreFile,
                TestTimeout = budgets.Timeout + TimeoutGrace,
                LaunchTimeout = config.LaunchTimeout,
                CleanupTimeout = config.CleanupTimeout,
                ActionTimeout = config.ActionTimeout,
                AssertionTimeout = config.AssertionTimeout,
                StepTimeout = StepTimeout,
                Trace = trace,
                Video = video,
                Debug = options.Debug || E2EFixture.IsOn("E2E_DEBUG"),
                AiTrace = options.AiTrace || E2EFixture.IsOn("E2E_AI_TRACE"),
            };

            attempt = await Standalone.OpenAttemptAsync(sessionOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The app this exploration started stops with it, whichever way the open failed.
            await appProcesses.StopAsync(appFailures.Add).ConfigureAwait(false);
            throw;
        }

        E2EException? error = null;
        ReportExplore record;
        IReadOnlyList<Exception> cleanup;
        try
        {
            var session = attempt.Session;

            // The values the goal, the app, or a model may echo are redacted wherever they appear.
            foreach (var secret in secrets)
            {
                session.Register(secret.Name, secret.Value);
            }

            var body = ExploreBody.Create(new ExploreBodyOptions(
                state,
                StepTimeout,
                target.App.Url is not null || TargetEngines.LaunchesPinnedApp(target, engine),
                credentials,
                string.Equals(agentName, "default", StringComparison.Ordinal) ? null : agentName));
            try
            {
                await body(new ExploreFixtures(new SessionAgent(session.Agent), token => session.App.OpenAsync(null, token)), cancellationToken).ConfigureAwait(false);
            }
            catch (E2EException ex)
            {
                error = ex;
            }
            finally
            {
                // Set whatever ended the body, so the release never writes a page for the verdict.
                session.Exploration = state.Snapshot();
            }

            session.Complete(error, cancellationToken);
            record = state.Snapshot();
        }
        finally
        {
            // The attempt is closed on every path. Its failures are the run's cleanup errors: they join the outcome and never replace it.
            IReadOnlyList<Exception> closed;
            try
            {
                closed = await attempt.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                // The app command stops once the engine is done with it, even when closing the attempt throws; its failure joins the cleanup errors the same way.
                await appProcesses.StopAsync(appFailures.Add).ConfigureAwait(false);
            }

            cleanup = [.. closed, .. appFailures];
        }

        var cleanupErrors = cleanup
            .Select(failure => ErrorSerializer.TranslateProvisioningError(failure, " while closing the exploration", CancellationToken.None))
            .Select(failure => failure as E2EException ?? new EngineException(EngineErrorCodes.EngineFailure, failure.Message + " while closing the exploration", failure))
            .Select(failure => (E2EException)ErrorRedaction.Cause(failure, redactor.Redact)!)
            .ToList();
        var exitCode = ErrorSerializer.CombineExitCodes(
            cleanupErrors.Select(failure => ErrorSerializer.ExitCodeForCategory(ErrorSerializer.Classify(failure).Category)).Append(Runner.ExitCodeOf(error)));
        var status = cleanupErrors.Count > 0 ? RunStatus.Failed : Runner.StatusOf(error);
        return new ExploreOutcome(status, exitCode, error ?? cleanupErrors.FirstOrDefault(), record);
    }

    /// <summary>
    /// The notices about a retry mode: an exploration runs its goal once, so a mode that records retries only records nothing, and the
    /// notice says to pass the flag. A video notice is made only when the engine can record one. Upstream's <c>recordingNotices</c> in
    /// <c>run/provision.ts</c>, for <c>explore</c>.
    /// </summary>
    internal static IEnumerable<string> RecordingNotices(RecordingMode trace, ResolvedRecording video, bool recordsVideo, string target, string engine)
    {
        // A run-wide video mode asked of an engine that cannot record it is skipped, and says so.
        if (!recordsVideo && video.Source == RecordingSource.Run && RecordingModes.RecordsOnSomeAttempt(video.Mode, 0))
        {
            yield return "video records only on targets whose engine can record it; target \"" + target + "\" (engine " + engine + ") records no video";
        }

        if (RecordingModes.IsRetryMode(trace))
        {
            yield return RetryOnlyNotice("trace", trace);
        }

        if (recordsVideo && RecordingModes.IsRetryMode(video.Mode))
        {
            yield return RetryOnlyNotice("video", video.Mode);
        }
    }

    private static string RetryOnlyNotice(string kind, RecordingMode mode) =>
        kind + ": '" + RecordingModes.Name(mode) + "' records retries only, and explore runs its goal once, so no " + kind + "s will be recorded; pass --" + kind + " on";

    private static E2EConfig LoadConfig(ExploreOptions options)
    {
        if (options.RawConfig is { } raw)
        {
            return raw;
        }

        var cwd = Path.GetFullPath(options.Cwd ?? Directory.GetCurrentDirectory());
        var path = options.ConfigPath is null ? E2EConfig.Find(cwd) : Path.GetFullPath(options.ConfigPath, cwd);
        if (path is null)
        {
            throw new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one");
        }

        if (!File.Exists(path))
        {
            throw options.ConfigPath is { } explicitPath
                ? E2EConfig.ExplicitConfigNotFound(cwd, explicitPath, path)
                : new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one");
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
    private static string RedactGoal(string goal, Redactor redactor)
    {
        var redacted = redactor.Redact(goal);
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
        MaxObservationBytes = explorer.MaxObservationBytes,
        MaxInputTokens = explorer.MaxInputTokens,
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
