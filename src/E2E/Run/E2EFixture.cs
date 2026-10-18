// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E;

/// <summary>
/// The part of a test base class that every test framework shares. It holds the settings and
/// starts one <see cref="E2ESession"/> per test. A framework adapter, such as <c>E2E.NUnit.E2ETest</c>,
/// calls <see cref="StartSessionAsync"/> before the test body and commits the session from the
/// framework's result after it.
/// <para>
/// Settings come from <see cref="Config"/>, the nearest <c>e2e.config.json</c> above the
/// test assembly or the working directory. Override a property to change one value for a fixture.
/// </para>
/// </summary>
public abstract class E2EFixture
{
    private static readonly Lazy<E2EConfig> DiscoveredConfig = new(() => E2EConfig.Discover(), LazyThreadSafetyMode.ExecutionAndPublication);

    private E2ESession? _session;

    protected App App => Session.App;

    protected Browser Browser => Session.Browser;

    protected Agent Agent => Session.Agent;

    protected Screen Screen => Session.Screen;

    protected TestContext Context => Session.Context;

    /// <summary>The discovered <c>e2e.config.json</c>, or the defaults when there is none. Loaded once per test run.</summary>
    protected virtual E2EConfig Config => DiscoveredConfig.Value;

    /// <summary>Secrets declared in the config, with <c>E2E_SECRET_{NAME}</c> overrides.</summary>
    protected Secrets Secrets => Config.Secrets;

    /// <summary>Browser engine. Override to supply a <see cref="DocumentEngine"/> or a custom engine.</summary>
    protected virtual IEngine CreateEngine() => new WebEngine();

    /// <summary>The model for <c>agents.default</c>, or null when the config sets no model.</summary>
    protected virtual IAgentModel? CreateModel() => Config.Agent.CreateModel();

    /// <summary>The judge for <c>agents.default</c>: <c>agents.default.judge</c>, or null to judge with <see cref="CreateModel"/>.</summary>
    protected virtual IAgentModel? CreateJudge() => Config.Agent.CreateJudge();

    /// <summary>
    /// The named agents besides <c>default</c>, picked per call with the <c>Agent</c> option.
    /// Defaults to every other <c>agents.&lt;name&gt;</c> entry in the config.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, AgentOptions> CreateAgents()
    {
        return Config.Agents
            .Where(pair => !string.Equals(pair.Key, "default", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value.CreateOptions(), StringComparer.Ordinal);
    }

    /// <summary><c>agents.default.system</c>: text appended to the act rules.</summary>
    protected virtual string? AgentSystem => Config.Agent.System;

    /// <summary>The project tools of <c>agents.default</c>, offered to the model beside the agent's own. None by default; a device test passes <c>MobileTools.Create(engine)</c>.</summary>
    protected virtual IReadOnlyDictionary<string, AgentTool>? AgentTools => null;

    /// <summary>
    /// The custom brain of <c>agents.default</c>, upstream's <c>executor</c>. None by default: an executor is code, so the config file cannot name one.
    /// With one, <see cref="AgentSystem"/> and <see cref="AgentTools"/> must be unset.
    /// </summary>
    protected virtual IStepExecutor? AgentExecutor => null;

    /// <summary><c>agents.default.context</c>: project context told to every model call.</summary>
    protected virtual string? AgentContext => Config.Agent.Context;

    /// <summary><c>agents.default.judgmentTimeout</c>.</summary>
    protected virtual TimeSpan JudgmentTimeout => Config.Agent.JudgmentTimeout;

    /// <summary><c>agents.default.providerOptions</c>.</summary>
    protected virtual IReadOnlyDictionary<string, System.Text.Json.JsonElement>? ProviderOptions => Config.Agent.ProviderOptions;

    /// <summary><c>targets[0].app.url</c>.</summary>
    protected virtual string? BaseUrl => Config.Target.App.Url;

    /// <summary>Anchors the files a test names, such as a route's fulfill path. Defaults to the config's directory.</summary>
    protected virtual string ProjectRoot => Config.ProjectRoot;

    /// <summary><c>cache.dir</c>, resolved against the config directory.</summary>
    protected virtual string CacheDirectory => Config.Cache.Directory;

    /// <summary><c>cache.mode</c>. Unset, it is read-write locally and read-only in CI.</summary>
    protected virtual CacheMode CacheMode => Config.Cache.Mode;

    /// <summary><c>cache.strict</c>.</summary>
    protected virtual bool CacheStrict => Config.Cache.Strict;

    /// <summary>
    /// <c>E2E_UPDATE_SNAPSHOTS</c>: when set to <c>1</c>, <c>ToHaveScreenshotAsync</c> writes each stored screenshot it
    /// finds missing or different, and passes. Override to turn it on in code.
    /// </summary>
    protected virtual bool UpdateSnapshots => Environment.GetEnvironmentVariable("E2E_UPDATE_SNAPSHOTS")?.Trim() == "1";

    /// <summary>Where a failed screenshot comparison puts its diff, actual, and expected images. Defaults to <c>.e2e/results</c> under <see cref="ProjectRoot"/>.</summary>
    protected virtual string? ResultsDirectory => null;

    /// <summary><c>targets[0].name</c>: the target part of a stored screenshot's file name.</summary>
    protected virtual string TargetName => Config.Target.Name;

    /// <summary>
    /// <c>trace</c> for the target: <c>E2E_TRACE</c>, else <c>targets[0].trace</c>, else the config's <c>trace</c>, else
    /// <c>retain-on-failure</c>. Override to set it for one fixture.
    /// </summary>
    protected virtual ResolvedRecording Trace => Config.Target.Trace;

    /// <summary>
    /// <c>video</c> for the target: <c>E2E_VIDEO</c>, else <c>targets[0].video</c>, else the config's <c>video</c>, else
    /// <c>off</c>. Override to set it for one fixture.
    /// </summary>
    protected virtual ResolvedRecording Video => Config.Target.Video;

    /// <summary>
    /// <c>E2E_DEBUG</c>: when set to <c>1</c> or <c>true</c>, the port's counterpart of <c>--debug</c>. Each test prints its phase
    /// timings to stderr and keeps the transcript of every agent step. Override to turn it on in code.
    /// </summary>
    protected virtual bool Debug => IsOn("E2E_DEBUG");

    /// <summary>
    /// <c>E2E_AI_TRACE</c>: when set to <c>1</c> or <c>true</c>, the port's counterpart of <c>--ai-trace</c>. Each test records every model call
    /// in the AI SDK devtools shape. The process writes them to <c>ai-trace-&lt;process id&gt;.json</c> next to the results directory
    /// (<c>.e2e</c> by default); read it with <c>npx unbox-ai</c>. Override to turn it on in code.
    /// </summary>
    protected virtual bool AiTrace => IsOn("E2E_AI_TRACE");

    private static bool IsOn(string variable) => Environment.GetEnvironmentVariable(variable)?.Trim() is { } value && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    protected virtual TimeSpan TestTimeout => Config.Timeout;

    protected virtual TimeSpan LaunchTimeout => Config.LaunchTimeout;

    protected virtual TimeSpan ActionTimeout => Config.ActionTimeout;

    protected virtual TimeSpan AssertionTimeout => Config.AssertionTimeout;

    protected virtual TimeSpan CleanupTimeout => Config.CleanupTimeout;

    /// <summary>How long one <c>ActAsync</c> may run unless its options say otherwise. A .NET-only setting.</summary>
    protected virtual TimeSpan StepTimeout => E2EDefaults.StepTimeout;

    /// <summary>How long a replay waits for each recorded target and for the recorded end state.</summary>
    protected virtual TimeSpan ReplayTimeout => E2EDefaults.ReplayTimeout;

    /// <summary><c>agents.default.maxModelCalls</c>.</summary>
    protected virtual int MaxModelCalls => Config.Agent.MaxModelCalls ?? E2EDefaults.MaxModelCalls;

    /// <summary><c>agents.default.maxSteps</c>: actions one <c>ActAsync</c> may take. <c>ActOptions.MaxSteps</c> can only lower it.</summary>
    protected virtual int MaxSteps => Config.Agent.MaxSteps ?? E2EDefaults.MaxSteps;

    private E2ESession Session => _session ?? throw new InvalidOperationException("The E2E session is available after setup, during the test.");

    /// <summary>
    /// Starts the session for one test. <paramref name="title"/> is the cache identity,
    /// <paramref name="attempt"/> is 1 for a first attempt, <paramref name="onSoftFailure"/> reports
    /// an <c>Expect.Soft</c> failure to the framework (null keeps them on the session), and
    /// <paramref name="testFailed"/> says whether the framework already failed the test.
    /// </summary>
    protected async Task StartSessionAsync(string title, int attempt, Action<TestException>? onSoftFailure, Func<bool> testFailed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("CacheTitle returned an empty test name.");
        }

        var cacheMode = CacheMode;
        _session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = CreateEngine(),
                Model = CreateModel(),
                Judge = CreateJudge(),
                AgentExecutor = AgentExecutor,
                AgentSystem = AgentSystem,
                AgentTools = AgentTools,
                AgentContext = AgentContext,
                JudgmentTimeout = JudgmentTimeout,
                ProviderOptions = ProviderOptions,
                Agents = CreateAgents(),
                BaseUrl = BaseUrl,
                ProjectRoot = ProjectRoot,
                TargetName = TargetName,
                UpdateSnapshots = UpdateSnapshots,
                ResultsDirectory = ResultsDirectory,
                Cache = cacheMode == CacheMode.Off ? null : new FileStepCache(CacheDirectory),
                CacheEnabled = cacheMode != CacheMode.Off,
                CacheMode = cacheMode,
                CacheStrict = CacheStrict,
                TestTitle = title,
                Debug = Debug,
                AiTrace = AiTrace,
                Trace = Trace,
                Video = Video,
                TestTimeout = TestTimeout,
                LaunchTimeout = LaunchTimeout,
                CleanupTimeout = CleanupTimeout,
                ActionTimeout = ActionTimeout,
                AssertionTimeout = AssertionTimeout,
                StepTimeout = StepTimeout,
                ReplayTimeout = ReplayTimeout,
                MaxModelCalls = MaxModelCalls,
                MaxSteps = MaxSteps,
                Attempt = attempt,
                OnSoftFailure = onSoftFailure,
                TestFailed = testFailed,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Hands the running session to the adapter to complete and dispose, or null when setup never started one.</summary>
    protected E2ESession? TakeSession()
    {
        var session = _session;
        _session = null;
        return session;
    }
}
