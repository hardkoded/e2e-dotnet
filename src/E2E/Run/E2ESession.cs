// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// One test attempt: an engine session, the fixtures, and the replay cache.
/// Call <see cref="Complete"/> after the test body. Disposing without
/// <see cref="Complete"/> records the attempt as failed.
/// </summary>
public sealed class E2ESession : IAsyncDisposable
{
    private readonly AttemptScope _scope;
    private readonly CancellationTokenSource _timeout;
    private readonly IEngineSession _engine;
    private readonly IDisposable _errorRedaction;
    private readonly TextWriter? _debugOutput;
    private readonly long _bodyStarted = System.Diagnostics.Stopwatch.GetTimestamp();
    private int _state;
    private int _disposed;
    private ExecuteVideo? _video;
    private TraceOutput? _trace;
    private Exception? _error;
    private long _bodyMs;
    private AttemptStatus _status = AttemptStatus.Failed;

    private E2ESession(AttemptScope scope, CancellationTokenSource timeout, IEngineSession engine, App app, Browser browser, Agent agent, Screen screen, TestContext context, IDisposable errorRedaction, TextWriter? debugOutput)
    {
        _debugOutput = debugOutput;
        _errorRedaction = errorRedaction;
        _scope = scope;
        _timeout = timeout;
        _engine = engine;
        App = app;
        Browser = browser;
        Agent = agent;
        Screen = screen;
        Context = context;
    }

    public App App { get; }

    public Browser Browser { get; }

    public Agent Agent { get; }

    public Screen Screen { get; }

    public TestContext Context { get; }

    /// <summary>The engine session, for a host that drives the app without a test body.</summary>
    internal IEngineSession Engine => _engine;

    /// <summary>The attempt's step timeline, with the screen each step left when the attempt keeps a trace.</summary>
    internal IReadOnlyList<StepRecord> Steps => _scope.Steps.All();

    /// <summary>What the engine said the attempt runs on, redacted and bounded; null when it said nothing.</summary>
    internal IReadOnlyDictionary<string, string>? Environment => _scope.Environment.Read(_scope.Redactor.Redact);

    /// <summary>Redacts the values of the secrets this attempt knows.</summary>
    internal Redactor Redactor => _scope.Redactor;

    /// <summary>The secrets <see cref="Agent"/> may fill by name, and every output redacts.</summary>
    internal IReadOnlyList<Secret> Secrets => _scope.Secrets;

    /// <summary>Adds secrets the attempt knows from the start, as an <c>act</c> does with its params.</summary>
    internal void Remember(IEnumerable<Secret> secrets) =>
        _scope.Remember(secrets.ToDictionary(secret => secret.Name, secret => (object?)secret, StringComparer.Ordinal));

    /// <summary>Registers a value for redaction alone, as the redaction ledger does: the agent may not fill it, and it withholds no pixels.</summary>
    internal void Register(string name, string value) => _scope.Register(name, value);

    /// <summary>
    /// The video segments this attempt kept, set once the session is disposed: files in the attempt's <c>video</c>
    /// directory, or links to recordings a hosted service keeps. The port has no report to register them in.
    /// </summary>
    internal IReadOnlyList<VideoSegment> Videos { get; private set; } = [];

    public int Replayed => _scope.Replayed;

    public int HandedOff => _scope.HandedOff;

    public int Missed => _scope.Missed;

    public int ModelCalls => _scope.ModelCalls;

    public int InputTokens => _scope.InputTokens;

    public int OutputTokens => _scope.OutputTokens;

    public static async Task<E2ESession> StartAsync(E2ESessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TestTitle);
        var agents = ResolveAgents(options);
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? null : Urls.NormalizeAppUrl(options.BaseUrl, "the base URL").Href;
        var recording = ExecuteVideo.Plan(options);

        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.TestTimeout > TimeSpan.Zero && options.TestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeout.CancelAfter(options.TestTimeout);
        }

        var debug = new DebugTrace(options.Debug);
        var projectRoot = Path.GetFullPath(string.IsNullOrEmpty(options.ProjectRoot) ? Directory.GetCurrentDirectory() : options.ProjectRoot);
        var attemptDir = Path.Combine(
            Path.GetFullPath(options.ResultsDirectory ?? Path.Combine(projectRoot, ".e2e", "results")),
            ScreenshotAssertion.Slug(options.TestTitle, 100, "test"),
            "attempt-" + options.Attempt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        IEngineSession engine;

        // An attempt that keeps a trace takes the screen each action leaves; one that does not asks the engine for none.
        var screens = new SessionScreens(RecordingModes.AttemptKeep(options.Trace.Mode, options.Attempt - 1) is not null);
        var environment = new EnvironmentFacts();

        // The values the engine registers while it starts, before the attempt's scope exists to take them.
        var ledger = new List<(string Name, string Value)>();
        var launch = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        try
        {
            if (Bounded(options.LaunchTimeout))
            {
                launch.CancelAfter(options.LaunchTimeout);
            }

            engine = await debug.Time("session.launch", () => options.Engine.StartAsync(
                new EngineStartOptions
                {
                    BaseUrl = baseUrl,
                    ActionTimeout = options.ActionTimeout,
                    ProjectRoot = projectRoot,
                    ArtifactsDir = attemptDir,
                    Screen = screens.Traced ? screens.Push : null,
                    Environment = environment.Push,
                    ResolveSecret = (secret, resolve) =>
                    {
                        var plaintext = Secrecy.ResolveSecretValue(secret, ledger);
                        Secrecy.RegisterDerivedSecrets(secret.Name, resolve?.Derived?.Invoke(plaintext) ?? [], ledger);
                        return Task.FromResult(plaintext);
                    },
                },
                launch.Token)).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is OperationCanceledException || ex is EngineException { Code: EngineErrorCodes.Cancelled }) && launch.IsCancellationRequested && !timeout.IsCancellationRequested)
        {
            timeout.Dispose();
            WriteDebug(debug, options.DebugOutput);
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "The engine did not start within the launch timeout (" + options.LaunchTimeout.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms).",
                ex);
        }
        catch
        {
            timeout.Dispose();
            WriteDebug(debug, options.DebugOutput);
            throw;
        }
        finally
        {
            launch.Dispose();
        }

        try
        {
            var video = await ExecuteVideo.StartAsync(recording, engine, attemptDir, options.LaunchTimeout, timeout.Token).ConfigureAwait(false);

            // Outside strict mode, a later attempt never replays but still records, so the attempt number does not turn the cache off.
            // Strict mode treats the store as the reviewed source of truth: every attempt replays, and nothing is written or evicted.
            var cacheOn = options.CacheEnabled && options.CacheMode != CacheMode.Off && options.Cache is not null;
            var softFailures = new SoftFailures(options.OnSoftFailure);
            var testFailed = options.TestFailed ?? (static () => false);
            var scope = new AttemptScope
            {
                Session = engine,
                Screens = screens,
                Environment = environment,
                Debug = debug,
                AiTrace = options.AiTrace ? new AiTraceRecorder() : null,
                AiTraceScope = options.AiTrace ? new AiTraceScope { Test = options.TestTitle, TestId = options.TestTitle, Target = string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName, Agent = "default", Attempt = options.Attempt - 1 } : null,
                AiTracePath = options.AiTrace ? AiTrace.PathFor(options.ResultsDirectory is null ? Path.Combine(projectRoot, ".e2e") : Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.ResultsDirectory))) ?? projectRoot) : null,
                BaseUrl = baseUrl,
                Agents = agents,
                Cache = options.Cache,
                CacheEnabled = cacheOn,
                CacheWrite = cacheOn && options.CacheMode == CacheMode.ReadWrite && !options.CacheStrict,
                ReplayEligible = options.CacheStrict || options.Attempt == 1,
                CacheStrict = options.CacheStrict,
                CleanupTimeout = options.CleanupTimeout,
                TestTitle = options.TestTitle,
                ProjectRoot = projectRoot,
                TargetName = string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName,
                Ended = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token),
                EnginePlatform = options.Engine.Platform,
                EngineCapabilities = options.Engine.Capabilities,
                Attempt = options.Attempt,
                ActionTimeout = options.ActionTimeout,
                ReplayTimeout = options.ReplayTimeout,
                StepTimeout = options.StepTimeout,
                Token = () => timeout.Token,
                TestFailed = () => softFailures.Any || testFailed(),
            };
            foreach (var (name, value) in ledger)
            {
                scope.Register(name, value);
            }

            // A screen is read once, when its step ends, redacted with what the attempt knows by then.
            screens.Route(screen => scope.Steps.RecordScreen(() => StepScreens.TraceScreen(screen, new TraceScreenOptions(scope.Redactor, StepScreens.DefaultObservationBytes, scope.AppOrigin))));

            var app = new App(engine, baseUrl, () => timeout.Token, scope.Steps);
            var browser = new Browser(engine, options.Engine.Platform, baseUrl, projectRoot, options.ActionTimeout, options.AssertionTimeout, () => timeout.Token, scope.MarkVerified, softFailures) { Steps = scope.Steps };
            softFailures.Redact = text => scope.Redactor.Redact(text);
            browser.Redact = text => scope.Redactor.Redact(text);
            browser.Failed = scope.Implicate;
            var agent = new Agent(scope);
            var screen = new Screen(
                scope.ObserveAsync,
                (node, action, token) => engine.PerformAsync(node, action, token),
                () => timeout.Token,
                scope.MarkVerified,
                options.ActionTimeout,
                options.AssertionTimeout,
                softFailures);
            screen.Steps = scope.Steps;
            screen.Exposure = scope.Exposure;
            screen.RememberSecret = secret => scope.Remember(new Dictionary<string, object?>(StringComparer.Ordinal) { [secret.Name] = secret });
            screen.Redact = text => scope.Redactor.Redact(text);
            screen.Failed = scope.Implicate;
            screen.Screenshots = scope.Screenshots = new ScreenshotContext
            {
                ProjectRoot = projectRoot,
                Suffix = "-" + (string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName) + "-" + ScreenshotContext.OperatingSystemName(),
                Title = options.TestTitle,
                ResultsDirectory = attemptDir,
                Update = options.UpdateSnapshots,
                Ci = options.Ci ?? E2EConfig.IsCi(),
                ScrollsIntoView = (options.Engine.Capabilities & EngineCapabilities.Scroll) != 0,
                DirectoryOf = options.SnapshotDirectoryOf ?? (static file => file is null ? null : file + "-snapshots"),
                Capture = token => engine.ScreenshotAsync(token),
                WithholdsPixels = () => scope.Exposure.WithholdsPixels || scope.Secrets.Count > 0,
            };
            var context = new TestContext
            {
                App = app,
                Browser = browser,
                Platform = options.Engine.Platform,
                Agent = agent,
                Screen = screen,
                CancellationToken = timeout.Token,
            };
            var keep = RecordingModes.AttemptKeep(options.Trace.Mode, options.Attempt - 1);
            var trace = new TraceOutput(keep, attemptDir, (options.Cache as FileStepCache)?.CacheDirectory);
            return new E2ESession(scope, timeout, engine, app, browser, agent, screen, context, ErrorRedaction.Install(screen.Redact), options.DebugOutput) { _video = video, _trace = trace };
        }
        catch
        {
            // The attempt never took ownership, so release what it started.
            var cleanup = engine.DisposeAsync().AsTask();
            await (Bounded(options.CleanupTimeout) ? cleanup.WaitAsync(options.CleanupTimeout) : cleanup).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            timeout.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Ends <c>expect.soft</c> collection and returns one <c>ASSERTION_FAILED</c>
    /// for every failure kept since the session started, or null. Call it when
    /// the test body settles and fail the test with the result. Later soft
    /// matchers throw as hard ones would.
    /// </summary>
    public TestException? CloseSoftFailures() => Screen.SoftFailures.Close();

    /// <summary>
    /// Writes verified acts and evicts the unverified ones this attempt recorded or
    /// replayed. A passed or skipped attempt confirms only what a later check verified.
    /// An act that missed the cache and never passed leaves its key alone. An entry a
    /// verified replay finished, or one that already holds the same flow, is not
    /// rewritten. A model outage leaves existing entries in place, unless a failure on the app implicated them too. A cancelled
    /// <paramref name="cancellationToken"/> writes nothing.
    /// </summary>
    public void Complete(Exception? error = null, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }

        _scope.Debug.Record("test.body", System.Diagnostics.Stopwatch.GetElapsedTime(_bodyStarted).TotalMilliseconds);
        try
        {
            Commit(error, cancellationToken);
        }
        finally
        {
            EndAttempt();
            Volatile.Write(ref _state, 2);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Exception? commitFailure = null;
        try
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                _scope.Debug.Record("test.body", System.Diagnostics.Stopwatch.GetElapsedTime(_bodyStarted).TotalMilliseconds);
                try
                {
                    Commit(new TestException("TEST_FAILED", "The session ended before it was completed."), CancellationToken.None);
                }
                finally
                {
                    EndAttempt();
                    Volatile.Write(ref _state, 2);
                }
            }
        }
#pragma warning disable CA1031 // The engine is released first, then this is rethrown.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            commitFailure = ex;
        }

        try
        {
            await ReleaseAsync().ConfigureAwait(false);
        }
        catch (Exception) when (commitFailure is not null)
        {
            // The commit failure is the one to report.
        }

        if (commitFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(commitFailure);
        }
    }

    private async ValueTask ReleaseAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // The video is finalized before the engine goes, and a required one that cannot be is reported once the engine is released.
        Exception? videoFailure = null;
        try
        {
            if (_video is not null)
            {
                try
                {
                    await _video.StopAsync(_status, _scope.CleanupTimeout).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The engine is released first, then this is rethrown.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    videoFailure = ex;
                }

                Videos = _video.Kept;
            }

            await _scope.Debug.Time("session.close", async () =>
            {
                var cleanup = _engine.DisposeAsync().AsTask();
                if (Bounded(_scope.CleanupTimeout))
                {
                    await cleanup.WaitAsync(_scope.CleanupTimeout).ConfigureAwait(false);
                }
                else
                {
                    await cleanup.ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "The engine did not shut down within the cleanup timeout (" + _scope.CleanupTimeout.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms).",
                ex);
        }
        finally
        {
            _errorRedaction.Dispose();
            _scope.Ended.Dispose();
            _timeout.Dispose();
            WriteDebug(_scope.Debug, _debugOutput, _scope.Steps.All());
            WriteAiTrace(_scope);
            await WriteTracePageAsync().ConfigureAwait(false);
        }

        if (videoFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(videoFailure);
        }
    }

    // Tells the executors that hold state for the attempt it is over.
    private void EndAttempt()
    {
        try
        {
            _scope.Ended.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The attempt was released already.
        }
        catch (AggregateException)
        {
            // An executor's own release callback failed. That is its failure to report, and it must not hide the attempt's result.
        }
    }

    // The session's own agent settings are the default agent. Agents names the others.
    private static Dictionary<string, ResolvedAgent> ResolveAgents(E2ESessionOptions options)
    {
        var agents = new Dictionary<string, ResolvedAgent>(StringComparer.Ordinal)
        {
            ["default"] = ResolvedAgent.From("default", new AgentOptions
            {
                Model = options.Model,
                Judge = options.Judge,
                Executor = options.AgentExecutor,
                System = options.AgentSystem,
                Context = options.AgentContext,
                MaxSteps = options.MaxSteps,
                MaxModelCalls = options.MaxModelCalls,
                JudgmentTimeout = options.JudgmentTimeout,
                ProviderOptions = options.ProviderOptions,
                Tools = options.AgentTools,
            }),
        };
        if (options.Agents is null)
        {
            return agents;
        }

        foreach (var pair in options.Agents)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ConfigurationException("INVALID_CONFIG", "agent names must be non-empty");
            }

            if (string.Equals(pair.Key, "default", StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    "Agents cannot hold \"default\"; the session's Model, Judge, AgentExecutor, AgentSystem, AgentTools, AgentContext, MaxSteps, MaxModelCalls, JudgmentTimeout, and ProviderOptions are the default agent");
            }

            agents[pair.Key] = ResolvedAgent.From(pair.Key, pair.Value);
        }

        return agents;
    }

    // A table is a courtesy: a stderr that cannot be written must not hide the test's own result.
    private static void WriteDebug(DebugTrace debug, TextWriter? output, IReadOnlyList<StepRecord>? steps = null)
    {
        if (!debug.Enabled)
        {
            return;
        }

        try
        {
            var writer = output ?? Console.Error;
            writer.Write(debug.Summary());
            if (steps is not null)
            {
                writer.Write(DebugSteps.AgentStepTable([steps]));
            }
        }
        catch (IOException)
        {
            // Nothing reads the table.
        }
    }

    // The trace is a courtesy as well: a file that cannot be written must not hide the test's own result.
    private static void WriteAiTrace(AttemptScope scope)
    {
        if (scope.AiTrace is null || scope.AiTracePath is null)
        {
            return;
        }

        try
        {
            AiTrace.Collect(scope.AiTrace, scope.AiTracePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine("[e2e] could not record the AI trace " + scope.AiTracePath + ": " + ex.Message);
        }
    }

    // The page is a courtesy as well: one that cannot be written must not hide the test's own result.
    private async Task WriteTracePageAsync()
    {
        if (_trace is not { } trace)
        {
            return;
        }

        if (trace.Keep is not { } keep || !RecordingModes.Keeps(keep, _status))
        {
            // Upstream empties the results directory when a run starts. A first attempt that keeps nothing clears the page an earlier run left.
            if (_scope.Attempt == 1)
            {
                try
                {
                    File.Delete(Path.Combine(trace.TestDir, TracePages.TracePage));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A page that cannot be removed is stale, not a failure of the test.
                }
            }

            return;
        }

        try
        {
            // The screen the failure left is a file beside the attempt's others, which the page quotes.
            if (_error is not null && _status != AttemptStatus.Passed && _scope.Steps.LastScreen is { } screen)
            {
                await TracePages.WriteTextAsync(Path.Combine(trace.AttemptDir, AttemptReport.ScreenFile), TextRules.TruncateUtf8(AttemptReport.ScreenListing(screen), StepScreens.DefaultObservationBytes), CancellationToken.None).ConfigureAwait(false);
            }

            var (report, id) = AttemptReport.Build(_scope, trace, _status, _error, _bodyMs, Videos);
            await TracePages.WriteTracePagesAsync(
                report,
                new HashSet<string>(StringComparer.Ordinal) { id },
                new WriteTracePagesOptions(_scope.ProjectRoot!, trace.ResultsRoot, trace.CacheDir),
                _scope.Redactor.Redact).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine("[e2e] could not write the trace page for " + _scope.TestTitle + ": " + _scope.Redactor.Redact(ex.Message));
        }
    }

    private static bool Bounded(TimeSpan timeout) => timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan;

    private void Commit(Exception? error, CancellationToken cancellationToken)
    {
        _status = ExecuteVideo.StatusOf(error);
        _error = error;
        _bodyMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(_bodyStarted).TotalMilliseconds;
        if (!_scope.CacheWrite || _scope.Cache is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // A model outage says nothing about the recording, so it stays, unless a failure on the app implicated it too.
        // Strict mode never reaches here: it writes nothing.
        var preserve = (AttemptScope.KeepsCache(error) || _scope.KeepCache) && !_scope.ImplicatesUnconfirmed;
        Flush(_scope.Cache, _scope.Acts, preserve);
    }

    /// <summary>
    /// Settles the acts of one attempt: a verified act's recording is written, and an unverified one that
    /// recorded or replayed is evicted, unless <paramref name="preserve"/> says the failure implicates nothing.
    /// </summary>
    internal static void Flush(IStepCache cache, IEnumerable<PendingAct> acts, bool preserve)
    {
        foreach (var act in acts)
        {
            var recorded = act.Completed && act.Entry is { Actions.Count: > 0 } && !act.ParamCollision;
            try
            {
                if (act.Verified && recorded)
                {
                    if (act.ReplayedWhole)
                    {
                        CompleteProvenance(cache, act.Key, act.Step);
                    }
                    else if (!HoldsSameFlow(cache, act.Key, act.Entry!))
                    {
                        cache.Write(act.Key, act.Entry!);
                    }
                }
                // An entry read for an act that then passed with nothing to record is evicted too: it did not serve
                // this pass, and nothing replaces it, so every later run would hand off the same way.
                else if (!preserve && (recorded || act.ConsumedReplay || (act.Completed && act.ReadEntry)))
                {
                    cache.Delete(act.Key);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The cache is disposable, as a replay's read is: a store that fails here costs the next run a live step.
            }
        }
    }

    // A verified whole replay proved which step an entry recorded before the provenance fields belongs to,
    // so it writes them in. Strict mode matches only entries that record the whole step. Every other entry stays as stored.
    private static void CompleteProvenance(IStepCache cache, string key, CacheEntry? step)
    {
        if (step is null || cache.Read(key).Entry is not { CallIndex: null } existing)
        {
            return;
        }

        existing.SetStep(step);
        cache.Write(key, existing);
    }

    // Rewriting an identical flow would only churn a committed cache directory.
    private static bool HoldsSameFlow(IStepCache cache, string key, CacheEntry entry)
    {
        var existing = cache.Read(key).Entry;
        return existing is not null
            && string.Equals(
                JsonSerializer.Serialize(existing, JsonDefaults.Options),
                JsonSerializer.Serialize(entry, JsonDefaults.Options),
                StringComparison.Ordinal);
    }
}

/// <summary>How to open one <see cref="E2ESession"/>. Retries set <see cref="Attempt"/> above 1 so the cache records but does not replay, except under <see cref="CacheStrict"/>, where every attempt replays.</summary>
public sealed class E2ESessionOptions
{
    public required IEngine Engine { get; init; }

    /// <summary>The default agent's model.</summary>
    public IAgentModel? Model { get; init; }

    /// <summary>The default agent's judge for <c>assert</c>, <c>waitFor</c>, and <c>extract</c>. Defaults to <see cref="Model"/>.</summary>
    public IAgentModel? Judge { get; init; }

    /// <summary>The default agent's upstream <c>system</c>: text appended to the act rules. Judges never see it.</summary>
    public string? AgentSystem { get; init; }

    /// <summary>The default agent's project tools, offered to the model beside the agent's own. See <see cref="AgentOptions.Tools"/>.</summary>
    public IReadOnlyDictionary<string, AgentTool>? AgentTools { get; init; }

    /// <summary>The default agent's custom brain, upstream's <c>executor</c>. See <see cref="AgentOptions.Executor"/>.</summary>
    public IStepExecutor? AgentExecutor { get; init; }

    /// <summary>The default agent's upstream <c>context</c>: told to every model call, at most 16384 UTF-8 bytes.</summary>
    public string? AgentContext { get; init; }

    /// <summary>The default agent's <c>judgmentTimeout</c>: the deadline of one <c>assert</c>, <c>waitFor</c>, or <c>extract</c>.</summary>
    public TimeSpan JudgmentTimeout { get; init; } = E2EDefaults.JudgmentTimeout;

    /// <summary>The default agent's provider options, keyed by provider. See <see cref="ModelRequest.ProviderOptions"/>.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>
    /// Named agents besides the default one, picked per call with the <c>Agent</c> option.
    /// Each starts from the built-in defaults, not from the default agent.
    /// </summary>
    public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }

    public string? BaseUrl { get; init; }

    /// <summary>Anchors the files a test names, such as a route's fulfill <c>Path</c>. Defaults to the working directory.</summary>
    public string? ProjectRoot { get; init; }

    public IStepCache? Cache { get; init; }

    public bool CacheEnabled { get; init; } = true;

    /// <summary><see cref="CacheMode.ReadOnly"/> replays but never writes or deletes recordings.</summary>
    public CacheMode CacheMode { get; init; } = CacheMode.ReadWrite;

    /// <summary>
    /// When true, a recording that no longer matches fails the act with <c>REPLAY_STALE</c>. So does a
    /// <c>no-entry</c> miss while a <see cref="FileStepCache"/> holds a recording of the same step under another key.
    /// Retries replay too, and the session never writes or deletes a recording, whatever <see cref="CacheMode"/> says.
    /// </summary>
    public bool CacheStrict { get; init; }

    /// <summary>How long the engine may take to start. Upstream <c>launchTimeout</c>.</summary>
    public TimeSpan LaunchTimeout { get; init; } = E2EDefaults.LaunchTimeout;

    /// <summary>How long the engine may take to shut down on dispose. Upstream <c>cleanupTimeout</c>.</summary>
    public TimeSpan CleanupTimeout { get; init; } = E2EDefaults.CleanupTimeout;

    public required string TestTitle { get; init; }

    public TimeSpan TestTimeout { get; init; } = E2EDefaults.TestTimeout;

    public TimeSpan ActionTimeout { get; init; } = E2EDefaults.ActionTimeout;

    public TimeSpan AssertionTimeout { get; init; } = E2EDefaults.AssertionTimeout;

    /// <summary>How long one <c>ActAsync</c> may run unless <see cref="ActOptions.Timeout"/> says otherwise. Judgments use <see cref="JudgmentTimeout"/>.</summary>
    public TimeSpan StepTimeout { get; init; } = E2EDefaults.StepTimeout;

    /// <summary>How long a replay waits for each recorded target and for the recorded end state.</summary>
    public TimeSpan ReplayTimeout { get; init; } = E2EDefaults.ReplayTimeout;

    /// <summary>The default agent's model calls per agent call, 1 through 100.</summary>
    public int MaxModelCalls { get; init; } = E2EDefaults.MaxModelCalls;

    /// <summary>Actions one <c>ActAsync</c> may take, 1 through 100. <see cref="ActOptions.MaxSteps"/> can only lower it.</summary>
    public int MaxSteps { get; init; } = E2EDefaults.MaxSteps;

    /// <summary>
    /// Receives each <c>expect.soft</c> failure as it happens. Null keeps them
    /// on the session until <see cref="E2ESession.CloseSoftFailures"/>.
    /// </summary>
    public Action<TestException>? OnSoftFailure { get; init; }

    /// <summary>
    /// Reports whether the test has already failed. Once it returns true, a passing check no longer
    /// verifies acts, so a teardown assertion after a failure does not record them.
    /// </summary>
    public Func<bool>? TestFailed { get; init; }

    /// <summary>The target's name in a stored screenshot's file name. Defaults to the engine's platform.</summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// <c>E2E_UPDATE_SNAPSHOTS=1</c>: <c>ToHaveScreenshotAsync</c> writes each stored screenshot it finds missing or
    /// different, and passes.
    /// </summary>
    public bool UpdateSnapshots { get; init; }

    /// <summary>Where a failed screenshot comparison puts its diff, actual, and expected images. Defaults to <c>.e2e/results</c> under the project root.</summary>
    public string? ResultsDirectory { get; init; }

    /// <summary>Where the stored screenshots of the test file a call was made from live. Null keeps them in <c>&lt;test file&gt;-snapshots</c>.</summary>
    internal Func<string?, string?>? SnapshotDirectoryOf { get; init; }

    /// <summary>Overrides the <c>CI</c> environment variable for <c>ToHaveScreenshotAsync</c>.</summary>
    internal bool? Ci { get; init; }

    /// <summary>Which attempts record a trace. The session only carries the setting; <see cref="Attempt"/> says which attempt this is.</summary>
    public ResolvedRecording Trace { get; init; } = new(RecordingMode.RetainOnFailure, RecordingSource.Default);

    /// <summary>Which attempts record a video. The session only carries the setting; <see cref="Attempt"/> says which attempt this is.</summary>
    public ResolvedRecording Video { get; init; } = new(RecordingMode.Off, RecordingSource.Default);

    /// <summary>
    /// <c>E2E_DEBUG=1</c>, the port's counterpart of <c>--debug</c>: the session times its phases and the agent's observe, model,
    /// and action phases, writes the table to stderr when it is released, and keeps each agent step's transcript.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// <c>E2E_AI_TRACE=1</c>, the port's counterpart of <c>--ai-trace</c>: every model call of the test is recorded in the AI SDK devtools
    /// shape and added to <c>ai-trace-&lt;process id&gt;.json</c> next to the results directory (<c>.e2e</c> by default) when the session is released.
    /// </summary>
    public bool AiTrace { get; init; }

    /// <summary>Where the phase table goes when <see cref="Debug"/> is on. Defaults to stderr.</summary>
    internal TextWriter? DebugOutput { get; init; }

    /// <summary>1 is the first try. Later attempts do not replay, and still record verified acts.</summary>
    public int Attempt { get; init; } = 1;
}
