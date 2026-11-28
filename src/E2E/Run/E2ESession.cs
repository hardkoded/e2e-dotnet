// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;
using Report = E2E.Reports.Report;

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
    private readonly AttemptBudget _budget;
    private CancelsOnClose? _cancelsOnClose;
    private readonly IEngineSession _engine;
    private readonly IDisposable _errorRedaction;
    private IDisposable? _credentials;
    private ResolvedConfig? _config;
    private readonly TextWriter? _debugOutput;
    private readonly long _bodyStarted = System.Diagnostics.Stopwatch.GetTimestamp();
    private int _state;
    private int _teardown;
    private readonly object _timeoutGate = new();
    private int _disposed;
    private ExecuteVideo? _video;
    private SessionRecordings? _recordings;
    private TraceOutput? _trace;
    private SessionPlan? _sessions;
    private int _evidenceTried;
    private FailureEvidence? _evidence;
    private Exception? _error;
    private TimeSpan _testTimeout;
    private AttemptTimeout? _attemptTimeout;
    private Func<AttemptTimeout>? _newAttemptTimeout;
    private CancellationToken _openToken;

    // What the teardown threw (a required video that did not finalize, the engine session's close): upstream's secondaryErrors of phase cleanup.
    private readonly List<Exception> _cleanupErrors = [];
    private readonly List<Exception> _hookErrors = [];
    private readonly List<Exception> _secondaryErrors = [];
    private long _bodyMs;
    private ExpectedFailureInfo? _expectedFailure;
    private AttemptStatus _status = AttemptStatus.Failed;
    private Lazy<Report>? _report;
    private readonly Lazy<Task<IReadOnlyList<Exception>>> _ending;
    private Func<Task<IReadOnlyList<Exception>>>? _hostedClose;
    private SessionEvents? _events;
    private AttemptGuestSessions? _guests;
    private IDisposable? _published;
    private PublishedAttempt? _publishedAttempt;
    private int _bodyEnded;

    private E2ESession(
        AttemptScope scope,
        CancellationTokenSource timeout,
        AttemptBudget budget,
        IEngineSession engine,
        App app,
        Browser browser,
        Agent agent,
        Screen screen,
        TestContext context,
        IDisposable errorRedaction,
        TextWriter? debugOutput
    )
    {
        _ending = new Lazy<Task<IReadOnlyList<Exception>>>(EndOnceAsync);
        _debugOutput = debugOutput;
        _errorRedaction = errorRedaction;
        _scope = scope;
        _timeout = timeout;
        _budget = budget;
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

    /// <summary>The unique id of this attempt, the one its report names. Upstream's <c>attemptId</c>.</summary>
    public string AttemptId => _scope.AttemptId;

    /// <summary>The id of the run the session belongs to: the one its report names. Upstream's <c>runId</c>.</summary>
    public string RunId => AttemptReport.RunId;

    /// <summary>The name of the target the session runs on. Upstream's <c>target</c>.</summary>
    public string Target => _scope.TargetName;

    /// <summary>
    /// The session's fixtures, the same <see cref="TestContext"/> as <see cref="Context"/>. Upstream's <c>fixtures</c>, a <c>TestFixtures</c> that
    /// <c>test.extend</c> can add to; the port has no <c>extend</c>, so there is no fixtures type argument.
    /// </summary>
    public TestContext Fixtures => Context;

    /// <summary>The records of the artifacts the attempt kept, settled once the session has closed. .NET-only: upstream reads them from the report.</summary>
    internal IReadOnlyList<Reports.ArtifactRecord> Artifacts => _scope.Attached;

    /// <summary>The absolute directory of the attempt's artifacts. Upstream's <c>artifactsDir</c>.</summary>
    public string ArtifactsDir => _scope.ArtifactsDir!;

    /// <summary>
    /// The absolute path of the session's trace page. It is written when the session closes. A session of a test framework writes it if the
    /// trace mode keeps the attempt; a hosted one (<see cref="E2EHost.OpenSessionAsync"/>) always does, as upstream's. Upstream's <c>tracePath</c>.
    /// </summary>
    public string TracePath => Path.Combine(_trace!.TestDir, TracePages.TracePage);

    /// <summary>The attempt's step timeline as the report tells it. Upstream's <c>steps()</c>, which returns <c>StepRecord[]</c>.</summary>
    public IReadOnlyList<E2E.Reports.ReportStep> Steps() => [.. StepRecords.Select(StepProgressWire.Public)];

    /// <summary>The attempt's step timeline, which a program's agent reads the verified steps off (upstream's <c>attempt.steps</c>).</summary>
    internal StepRecorder Recorder => _scope.Steps;

    /// <summary>The attempt's scope, which a program's agent runs over (upstream's <c>attempt.agentRuntime</c>).</summary>
    internal AttemptScope Scope => _scope;

    /// <summary>The attempt's step timeline, with the screen each step left when the attempt keeps a trace.</summary>
    internal IReadOnlyList<StepRecord> StepRecords => _scope.Steps.All();

    /// <summary>The hook steps run in from now on; null for the test body (upstream's <c>enter</c>). A test framework's hook methods name theirs themselves.</summary>
    internal StepPhase? Phase
    {
        get => _scope.Steps.Phase;
        set
        {
            _scope.Steps.Phase = value;
            _scope.Steps.Owner = value switch
            {
                StepPhase.BeforeEach => "the beforeEach hook",
                StepPhase.AfterEach => "the afterEach hook",
                _ => "the test body",
            };
        }
    }

    /// <summary>What the app logged this attempt, each line under the step it happened in.</summary>
    internal IReadOnlyList<AppLogRecord> AppLog => _scope.Steps.AppLog();

    /// <summary>What the engine said the attempt runs on, redacted and bounded; null when it said nothing.</summary>
    internal IReadOnlyDictionary<string, string>? Environment => _scope.Environment.Read(_scope.Redactor.Redact);

    /// <summary>What the attempt saw the moment it failed, once the session is released and the trace page keeps the attempt; null otherwise.</summary>
    internal FailureEvidence? Evidence => _evidence;

    /// <summary>Redacts the values of the secrets this attempt knows.</summary>
    internal Redactor Redactor => _scope.Redactor;

    /// <summary>The secrets <see cref="Agent"/> may fill by name, and every output redacts.</summary>
    internal IReadOnlyList<Secret> Secrets => _scope.Secrets;

    /// <summary>Adds secrets the attempt knows from the start, as an <c>act</c> does with its params.</summary>
    internal void Remember(IEnumerable<Secret> secrets) =>
        _scope.Remember(secrets.ToDictionary(secret => secret.Name, secret => (object?)secret, StringComparer.Ordinal));

    /// <summary>Registers a value for redaction alone, as the redaction ledger does: the agent may not fill it, and a screenshot does not mask it.</summary>
    internal void Register(string name, string value) => _scope.Register(name, value);

    /// <summary>
    /// The video segments this attempt kept, set once the session is disposed: files in the attempt's <c>video</c>
    /// directory, or links to recordings a hosted service keeps. The port has no report to register them in.
    /// </summary>
    internal IReadOnlyList<VideoSegment> Videos { get; private set; } = [];

    /// <summary>The videos the attempt recorded as its owner hands them on: a file still on disk by its absolute path, and a link a hosted service keeps. Upstream's <c>StandaloneAttempt.videos</c>.</summary>
    internal IReadOnlyList<SessionVideo> SessionVideos() =>
        [
            .. Videos.Select(segment =>
            {
                var startedAt = DateTimeOffset.TryParse(
                    segment.StartedAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed
                )
                    ? parsed
                    : (DateTimeOffset?)null;
                return segment switch
                {
                    VideoFile file => (SessionVideo)
                        new SessionVideoFile(Path.Combine(ArtifactsDir, file.Path), startedAt),
                    VideoLink link => new SessionVideoLink(
                        link.Url,
                        link.MediaType,
                        startedAt ?? DateTimeOffset.UtcNow
                    ),
                    _ => throw new InvalidOperationException("unknown video segment"),
                };
            }),
        ];

    /// <summary>What the session explored, set by an exploration before it is released: its verdict is told by its findings, and gets no trace page.</summary>
    internal ReportExplore? Exploration { get; set; }

    /// <summary>
    /// The attempt's report-1 document once the session is disposed and its trace page, when the mode keeps one, is written; null before that and
    /// when the page cannot be written. It tells this attempt alone, as a single-result run: its status is the session's, not the verdict of the
    /// test around it. Artifact paths are relative to the results directory. Upstream's <c>session.report()</c>.
    /// </summary>
    public Report? Report() => Volatile.Read(ref _report)?.Value;

    public int Replayed => _scope.Replayed;

    public int HandedOff => _scope.HandedOff;

    public int Missed => _scope.Missed;

    public int ModelCalls => _scope.ModelCalls;

    public int InputTokens => _scope.InputTokens;

    public int OutputTokens => _scope.OutputTokens;

    public static Task<E2ESession> StartAsync(
        E2ESessionOptions options,
        CancellationToken cancellationToken = default
    ) => LaunchAsync(options, null, cancellationToken);

    /// <summary>Starts the session, as <see cref="StartAsync"/>, with the environment a host's prepare left for the engine's worker. Null leaves the engine to read the process environment.</summary>
    internal static async Task<E2ESession> LaunchAsync(
        E2ESessionOptions options,
        IReadOnlyDictionary<string, string?>? env,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TestTitle);
        var agents = ResolveAgents(options);
        var runLimits = ResolvedAgent.RunLimits(agents.Values);
        if (options.Agent is { } pinned && !agents.ContainsKey(pinned))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "unknown agent \""
                    + pinned
                    + "\"; configured: "
                    + string.Join(", ", agents.Keys)
                    + Suggestions.DidYouMean(pinned, [.. agents.Keys])
            );
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? null
            : Urls.NormalizeAppUrl(options.BaseUrl, "the base URL").Href;
        if (baseUrl is not null)
        {
            // A file URL reaches the engine only when it declares it opens one, and the engine's own file checks (a browser it cannot read the disk of) run as they do for a configured target.
            TargetApps.CheckFileAppUrl(
                new EngineAppDeclaration
                {
                    Url = baseUrl,
                    BundleId = options.BundleId,
                    AppPath = options.AppPath,
                    LaunchArguments = options.LaunchArguments,
                    LaunchArgumentValues = options.LaunchArgumentValues,
                    Permissions = options.Permissions,
                    AllPermissions = options.AllPermissions,
                },
                options.Engine,
                string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName
            );
        }

        var recording = options.VideoRecording is { } given
            ? ExecuteVideo.PlanGiven(options, given)
            : ExecuteVideo.Plan(options);

        // Resolved before the timeout source exists, so a bad path throws with nothing to dispose.
        var projectRoot = Path.GetFullPath(
            string.IsNullOrEmpty(options.ProjectRoot) ? Directory.GetCurrentDirectory() : options.ProjectRoot
        );
        // The attempt's id is minted here, before the engine starts, so the start options and every call's context name the one id.
        var attemptId = Guid.CreateVersion7().ToString();
        var attemptDir = Path.Combine([
            Path.GetFullPath(options.ResultsDirectory ?? Path.Combine(projectRoot, ".e2e", "results")),
            .. options.ArtifactSegments?.Invoke(attemptId)
                ?? ArtifactPaths.AttemptSegments(
                    options.RerunDir,
                    AttemptReport.ResultSegmentOf(options.TestFile, options.TestTitle),
                    options.Attempt - 1
                ),
        ]);

        var timeout = NewTimeoutSource(options.Clock, cancellationToken);
        Deadline? budgetDeadline = null;

        // The body's operations run under the test's signal; a teardown enters a budget of its own (see EnterTeardownBudget).
        var budget = new AttemptBudget(timeout, options.CancelsOnClose?.Closed);

        // What test.setTimeout moves: the signal's timer and the deadline the screen and browser cap their steps with.
        Screen? timedScreen = null;
        Browser? timedBrowser = null;
        Action<TimeSpan, Deadline> onMoved = (moved, deadline) =>
        {
            budget.Deadline = deadline;
            // An open, a restart, and a link open take the test's timeout, the new one included.
            if (timedBrowser is not null)
            {
                timedBrowser.DefaultLifecycleTimeout = moved;
            }

            if (timedScreen is not null)
            {
                timedScreen.BudgetDeadline = deadline;
            }

            if (timedBrowser is not null)
            {
                timedBrowser.BudgetDeadline = deadline;
            }
        };
        AttemptTimeout NewAttemptTimeout() => new(timeout, options.TestTimeout, options.Clock, onMoved);
        var attemptTimeout = NewAttemptTimeout();

        // The attempt's timeout starts before the timer is armed, so it has elapsed whenever the timer has fired (a close reads it).
        if (options.TestTimeout > TimeSpan.Zero && options.TestTimeout != Timeout.InfiniteTimeSpan)
        {
            try
            {
                timeout.CancelAfter(options.TestTimeout);
                budgetDeadline = new Deadline(options.TestTimeout, options.Clock);
                budget.Deadline = budgetDeadline;
            }
            catch
            {
                // An out-of-range timeout throws before the session owns the source.
                timeout.Dispose();
                throw;
            }
        }

        // The reason the test's own timeout aborts with, one error every step it cuts shares (upstream's signal.reason).
        var timedOut = new Lazy<TestException>(() =>
            TimeoutFailure(attemptTimeout.Timeout, null, options.PublishesAttempt)
        );

        Func<TestException?> reason = () =>
            budget.TestSignalIsOwn && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? timedOut.Value
                : null;

        var debug = new DebugTrace(options.Debug);
        IEngineSession engine;

        // An attempt that keeps a trace takes the screen each action leaves; one that does not asks the engine for none.
        var screens = new SessionScreens(
            RecordingModes.AttemptKeep(options.Trace.Mode, options.Attempt - 1) is not null
        );
        var environment = new EnvironmentFacts();
        var appLog = new AppLogRoute();
        var events = SessionEvents.For(
            options,
            string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName
        );

        // The values the engine registers while it starts, before the attempt's scope exists to take them.
        var ledger = new List<(string Name, string Value)>();
        var launch = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        try
        {
            if (Bounded(options.LaunchTimeout))
            {
                launch.CancelAfter(options.LaunchTimeout);
            }

            engine = EngineSession.Create(
                await debug
                    .Time("session.launch", () => options.Engine.StartAsync(
                            new EngineStartOptions
                            {
                                BaseUrl = baseUrl,
                                TargetName = options.TargetName,
                                HostId = options.HostId,
                                Headed = options.Headed,
                                RunId = AttemptReport.RunId,
                                AttemptId = attemptId,
                                Permissions = options.Permissions,
                                AllPermissions = options.AllPermissions,
                                BundleId = options.BundleId,
                                AppPath = options.AppPath,
                                LaunchArguments = options.LaunchArguments,
                                LaunchArgumentValues = options.LaunchArgumentValues,
                                ActionTimeout = options.ActionTimeout,
                                ProjectRoot = projectRoot,
                                Env = env,
                                ArtifactsDir = attemptDir,
                                Screen = screens.Traced ? screens.Push : null,
                                Environment = environment.Push,
                                AppLog = appLog.Push,
                                Navigation = appLog.Navigated,
                                ResolveSecret = async (secret, resolve) =>
                                {
                                    // Only a secret the engine declared in its secrets resolves (upstream's resolveEngineSecret, run/execute.ts).
                                    if (
                                        secret is null
                                        || !options.Engine.Secrets.Any(declared =>
                                            string.Equals(declared.Name, secret.Name, StringComparison.Ordinal)
                                        )
                                    )
                                    {
                                        var asked = secret is null
                                            ? "a value that is not a Secrets.Get handle"
                                            : "\"" + secret.Name + "\"";
                                        throw new ConfigurationException(
                                            "SECRET_UNAVAILABLE",
                                            "engine "
                                                + options.Engine.Name
                                                + " asked for secret "
                                                + asked
                                                + ", which it did not declare in its secrets"
                                        );
                                    }

                                    var plaintext = await Secrecy
                                        .ResolveSecretValueAsync(secret, ledger, launch.Token)
                                        .ConfigureAwait(false);
                                    Secrecy.RegisterDerivedSecrets(
                                        secret.Name,
                                        await Secrecy.DeriveAsync(resolve, plaintext).ConfigureAwait(false),
                                        ledger
                                    );
                                    return plaintext;
                                },
                            },
                            launch.Token
                        ))
                    .ConfigureAwait(false),
                AttemptReport.RunId,
                attemptId,
                options.Engine.Name
            );
        }
        catch (Exception ex)
            when ((ex is OperationCanceledException || ex is EngineException { Code: EngineErrorCodes.Cancelled })
                && launch.IsCancellationRequested
                && !timeout.IsCancellationRequested
            )
        {
            timeout.Dispose();
            WriteDebug(debug, options.DebugOutput);
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "The engine did not start within the launch timeout ("
                    + options.LaunchTimeout.TotalMilliseconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + " ms).",
                ex
            );
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

        AttemptScope? created = null;
        try
        {
            // The saved session restores before the video starts, and the values its saving session learned join the ledger the attempt starts with.
            if (options.State is { } restoredState)
            {
                if ((options.Engine.Capabilities & EngineCapabilities.State) == 0)
                {
                    throw new ConfigurationException(
                        "UNSUPPORTED_CAPABILITY",
                        $"target \"{(string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName)}\" has no engine state capability for session restore"
                    );
                }

                // Bounded by the launch timeout, as upstream's launch step 'restoring the session' is.
                using var restoreBudget = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                if (Bounded(options.LaunchTimeout))
                {
                    restoreBudget.CancelAfter(options.LaunchTimeout);
                }

                ledger.AddRange(restoredState.Secrets.Where(secret => !ledger.Contains(secret)));
                await engine
                    .RestoreStateAsync(restoredState.State, OperationContext.Test, restoreBudget.Token)
                    .ConfigureAwait(false);
            }
            else if (options.Sessions?.Restore is { } restoring)
            {
                if ((options.Engine.Capabilities & EngineCapabilities.State) == 0)
                {
                    throw new ConfigurationException(
                        "UNSUPPORTED_CAPABILITY",
                        $"target \"{(string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName)}\" has no engine state capability for session restore"
                    );
                }

                // Bounded by the launch timeout, as upstream's launch step 'restoring the session' is.
                using var restoreBudget = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                if (Bounded(options.LaunchTimeout))
                {
                    restoreBudget.CancelAfter(options.LaunchTimeout);
                }

                try
                {
                    var saved = await options
                        .Sessions.Store.LoadAsync(restoring, options.Sessions.Identity, restoreBudget.Token)
                        .ConfigureAwait(false);
                    ledger.AddRange(saved.Secrets.Where(secret => !ledger.Contains(secret)));
                    await engine
                        .RestoreStateAsync(saved.State, OperationContext.Test, restoreBudget.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                    when ((
                            ex is OperationCanceledException
                            || ex is EngineException { Code: EngineErrorCodes.Cancelled }
                        )
                        && restoreBudget.IsCancellationRequested
                        && !timeout.IsCancellationRequested
                    )
                {
                    throw new EngineException(
                        "ENVIRONMENT_UNAVAILABLE",
                        "Restoring the session did not finish within the launch timeout ("
                            + options.LaunchTimeout.TotalMilliseconds.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                            + " ms).",
                        ex
                    );
                }
            }

            var recordings = Recordings.SessionRecordings(
                engine,
                string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName,
                (options.Engine.Capabilities & EngineCapabilities.Video) != 0
            );
            var video = await ExecuteVideo
                .StartAsync(recording, recordings, attemptDir, options.LaunchTimeout, timeout.Token)
                .ConfigureAwait(false);

            // Outside strict mode, a later attempt never replays but still records, so the attempt number does not turn the cache off.
            // Strict mode treats the store as the reviewed source of truth: every attempt replays, and nothing is written or evicted.
            var cacheOn = options.CacheEnabled && options.CacheMode != CacheMode.Off && options.Cache is not null;
            // Upstream's createAgentCacheContext: the store's mode and replay eligibility, and the advice a stale strict replay carries.
            var cache = cacheOn
                ? CacheContexts.CreateAgentCacheContext(
                    new CacheConfig
                    {
                        Mode = options.CacheMode,
                        Directory = (options.Cache as FileStepCache)?.CacheDirectory
                            ?? Path.Combine(projectRoot, ".e2e", "cache"),
                        Strict = options.CacheStrict,
                        StrictSource = options.CacheStrict
                            ? options.CacheStrictSource ?? new CacheStrictSource(true, false)
                            : null,
                        Store = options.Cache is FileStepCache ? null : options.Cache,
                    },
                    projectRoot,
                    options.Attempt - 1
                )
                : null;
            var softFailures = new SoftFailures(options.OnSoftFailure);
            var testFailed = options.TestFailed ?? (static () => false);
            var scope = new AttemptScope
            {
                SharesSecrets = true,
                AttemptId = attemptId,
                ArtifactsDir = attemptDir,
                RerunDir = options.RerunDir,
                OnProgress = events is null ? options.OnProgress : events.Progress(options.OnProgress),
                Mirror = options.Mirror,
                ArtifactStore = options.ArtifactStore,
                Session = engine,
                Screens = screens,
                Environment = environment,
                Debug = debug,
                AiTrace = options.AiTrace
                    ? new AiTraceRecorder(text => created is null ? text : created.Redactor.Redact(text))
                    : null,
                AiTraceScope = options.AiTrace
                    ? new AiTraceScope
                    {
                        Test = options.TestTitle,
                        TestId = options.TestTitle,
                        Target = string.IsNullOrWhiteSpace(options.TargetName)
                            ? options.Engine.Platform
                            : options.TargetName,
                        Agent = options.Agent ?? "default",
                        Attempt = options.Attempt - 1,
                    }
                    : null,
                AiTracePath = options.AiTrace
                    ? AiTrace.PathFor(
                        options.ResultsDirectory is null
                            ? Path.Combine(projectRoot, ".e2e")
                            : Path.GetDirectoryName(
                                Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.ResultsDirectory))
                            ) ?? projectRoot
                    )
                    : null,
                BaseUrl = baseUrl,
                Agents = agents,
                AgentName = options.Agent ?? "default",
                Cache = options.Cache,
                CacheEnabled = cacheOn,
                AppIdentity = options.AppIdentity!,
                CacheWrite = cache?.Mode == CacheMode.ReadWrite,
                ReplayEligible = cache?.ReplayEligible ?? false,
                CacheStrict = options.CacheStrict,
                StrictAdvice = cache?.Strict?.Advice ?? "",
                CleanupTimeout = options.CleanupTimeout,
                TestTitle = options.TestTitle,
                TestFile = options.TestFile,
                TestMethod = options.TestMethod,
                ProjectRoot = projectRoot,
                RedactThrown = options.RedactThrown,
                RetainSteps = options.RetainSteps,
                TargetName = string.IsNullOrWhiteSpace(options.TargetName)
                    ? options.Engine.Platform
                    : options.TargetName,
                Ended = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token),
                EnginePlatform = options.Engine.Platform,
                EngineName = options.Engine.Name,
                EngineVersion = options.Engine.Version,
                EngineCapabilities = options.Engine.Capabilities,
                ActionKinds = engine.ActionKinds,
                Attempt = options.Attempt,
                ActionTimeout = options.ActionTimeout,
                ReplayTimeout = options.ReplayTimeout,
                StepTimeout = options.StepTimeout,
                Token = () => budget.Token,
                Reason = reason,
                TestFailed = () => softFailures.Any || testFailed(),
            };
            created = scope;
            budget.Reason = reason;
            // Seeded with the static values of the run's config, as upstream's sessionLedger is; what it carries into a saved state leaves them out.
            Secrecy.SessionLedger(scope, options.Config?.AllSecrets ?? []);
            Secrecy.AdoptSecrets(scope, ledger);

            // A screen is read once, when its step ends, redacted with what the attempt knows by then.
            screens.Route(screen =>
                scope.Steps.RecordScreen(() =>
                    StepScreens.TraceScreen(
                        screen,
                        new TraceScreenOptions(
                            scope.Redactor,
                            runLimits.MaxObservationBytes,
                            scope.AppOrigin,
                            scope.BaseUrl
                        )
                    )
                )
            );

            // What the app did on its own while the engine launched waits here, and goes under the step it happened in.
            appLog.Route((appEvent, at) => scope.Steps.RecordAppEvent(appEvent, at));

            var app = new App(
                engine,
                baseUrl,
                () => budget.Token,
                scope.Steps,
                (options.Engine.Capabilities & EngineCapabilities.PinnedApp) != 0,
                scope.TargetName,
                options.LinkBase
            )
            {
                ArtifactsDir = attemptDir,
                Redact = text => scope.Redactor.Redact(text),
                Attach = scope.AttachArtifact,
                Recordings = recordings,
                SinkOf = () => scope.Artifacts?.Sink,
            };
            var browser = new Browser(
                engine,
                options.Engine.Platform,
                baseUrl,
                projectRoot,
                options.ActionTimeout,
                options.AssertionTimeout,
                () => budget.Token,
                scope.MarkVerified,
                softFailures
            )
            {
                Steps = scope.Steps,
            };
            softFailures.Redact = text => scope.Redactor.Redact(text);
            browser.Redact = text => scope.Redactor.Redact(text);
            app.OperationBudget = api => browser.OperationBudget(api);

            // An open, a restart, and a link open take the test's timeout until a program sets a default one.
            browser.DefaultLifecycleTimeout = Bounded(options.TestTimeout)
                ? options.TestTimeout
                : E2EDefaults.TestTimeout;
            app.LifecycleBudget = browser.LifecycleBudget;
            browser.Failed = scope.Implicate;
            browser.Attach = scope.AttachArtifact;
            var agent = new Agent(scope);
            var locatorEngine = new LocatorEngine(
                new LocatorEngineOptions
                {
                    Observe = token => scope.ObserveAsync(OperationContext.Test, token),
                    ObserveWithin = (timeout, token) => scope.ObserveAsync(timeout, OperationContext.Test, token),
                    Locate = engine.CanLocate
                        ? (expression, timeout, token) =>
                            engine.LocateAsync(expression, timeout, OperationContext.Test, token)
                        : null,
                    ViewportIntersection = engine.MeasuresViewport ? engine.ViewportIntersectionAsync : null,
                    Perform = engine.PerformAsync,
                    PerformAt = engine.PerformAtAsync,
                    TypeText = engine.TypeTextAsync,
                    Cancellation = () => budget.Token,
                    ActionTimeout = options.ActionTimeout,
                    AssertionTimeout = options.AssertionTimeout,
                }
            );
            var screen = new Screen(locatorEngine, scope.MarkVerified, softFailures, (direction, momentum, token) =>
                engine.SwipeAsync(direction, momentum, OperationContext.Test, token)
            );
            var fixtureContext = new EngineFixtureContext(screen, () =>
                OperationContext.Test with
                {
                    Timeout = options.ActionTimeout,
                    Signal = budget.Token,
                }
            );
            browser.Context = fixtureContext;
            (engine as IEngineFixtureHost)?.CreateFixtures(fixtureContext);
            screen.Steps = scope.Steps;
            screen.Capabilities = options.Engine.Capabilities;
            screen.ActionKinds = engine.ActionKinds;
            screen.PointerActionKinds = engine.PointerActionKinds;
            screen.BudgetDeadline = budgetDeadline;
            browser.BudgetDeadline = budgetDeadline;
            timedScreen = screen;
            timedBrowser = browser;
            screen.ProjectRoot = projectRoot;
            screen.RememberSecret = secret =>
                scope.Remember(new Dictionary<string, object?>(StringComparer.Ordinal) { [secret.Name] = secret });
            screen.RegisterSecretValue = scope.Register;
            screen.Redact = text => scope.Redactor.Redact(text);
            screen.Failed = scope.Implicate;
            screen.RenderTree = observation =>
                Screen.TreeText(observation, scope.Redactor, scope.AppOrigin, runLimits.MaxObservationBytes);
            screen.Screenshots = scope.Screenshots = new ScreenshotContext
            {
                ProjectRoot = projectRoot,
                Suffix =
                    "-"
                    + (string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName)
                    + "-"
                    + ScreenshotContext.OperatingSystemName(),
                Title = options.TestTitle,
                ResultsDirectory = attemptDir,
                Update = options.UpdateSnapshots,
                Ci = options.Ci ?? E2EConfig.IsCi(),
                ScrollsIntoView = (options.Engine.Capabilities & EngineCapabilities.Scroll) != 0,
                DirectoryOf = options.SnapshotDirectoryOf ?? (static file => file is null ? null : file + "-snapshots"),
                Observe = (budget, token) =>
                    engine.ObserveAsync(
                        new ObserveOptions { Pixels = true, Comparable = true },
                        budget,
                        OperationContext.Test,
                        token
                    ),
            };
            var context = new TestContext
            {
                App = app,
                Browser = browser,
                Platform = options.Engine.Platform,
                Agent = agent,
                Screen = screen,
                Services = options.Services,
                CancellationToken = timeout.Token,
            };
            var keep = RecordingModes.AttemptKeep(options.Trace.Mode, options.Attempt - 1);
            var trace = new TraceOutput(
                keep,
                attemptDir,
                (options.Cache as FileStepCache)?.CacheDirectory,
                options.RerunDir
            );
            var hold = options.Credentials is { } credentials ? SecretRegistry.Hold(credentials) : null;
            try
            {
                var session = new E2ESession(
                    scope,
                    timeout,
                    budget,
                    engine,
                    app,
                    browser,
                    agent,
                    screen,
                    context,
                    ErrorRedaction.Install(screen.Redact),
                    options.DebugOutput
                )
                {
                    _cancelsOnClose = options.CancelsOnClose,
                    _video = video,
                    _recordings = recordings,
                    _trace = trace,
                    _sessions = options.Sessions,
                    _credentials = hold,
                    _config = options.Config,
                    _events = events,
                    _testTimeout = options.TestTimeout,
                    _attemptTimeout = attemptTimeout,
                    _newAttemptTimeout = NewAttemptTimeout,
                    _openToken = cancellationToken,
                };
                if (options.OffersGuests || options.PublishesAttempt)
                {
                    // The hosts and sessions the test opens beside its own: their steps are recorded with its own, and the attempt ends them.
                    AttemptGuestSessions? guests = null;
                    if (options.OffersGuests)
                    {
                        guests = new AttemptGuestSessions(
                            scope.Steps,
                            () => scope.Artifacts?.Sink,
                            new AttemptGuestOptions
                            {
                                Keep = recording?.Keep,
                                CleanupTimeout = Bounded(options.CleanupTimeout)
                                    ? options.CleanupTimeout
                                    : Timeout.InfiniteTimeSpan,
                            }
                        );
                        session._guests = guests;
                    }

                    session._publishedAttempt = new PublishedAttempt
                    {
                        AttemptId = scope.AttemptId,
                        Guests = guests?.Offer,
                        SetTimeout = options.OffersGuests ? session.SetTimeoutOf(attemptTimeout) : null,
                        ToPass = options.Config?.ToPass,
                        AssertionTimeout = options.AssertionTimeout,
                        RecordSecondaryError = session.RecordSecondaryError,
                        Budget = budget,
                        Polls =
                            options.OffersGuests ? new PollScope("the test body")
                            : options.PublishesAttempt ? new PollScope("the hosted session")
                            : null,
                    };
                    session._published = Attempts.PublishAttempt(session._publishedAttempt);
                }

                events?.Started();
                return session;
            }
            catch
            {
                hold?.Dispose();
                throw;
            }
        }
        catch
        {
            // The attempt never took ownership, so release what it started, and stop sharing what its scope learned.
            created?.ReleaseSessionLedger();
            var cleanup = engine.DisposeAsync().AsTask();
            await (
                Bounded(options.CleanupTimeout) ? cleanup.WaitAsync(options.CleanupTimeout) : cleanup
            ).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            timeout.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Names this session's attempt in the flow of the caller, for a later test of a serial group that shares the session: its setup does not start
    /// a session, so nothing else reserves the slot a host the test body launches finds its attempt through. .NET-only (upstream keeps one slot per process).
    /// Call it from a method that is not async, so the slot belongs to the flow of the test body that follows.
    /// </summary>
    internal void HandOverAttempt()
    {
        if (_publishedAttempt is null)
        {
            return;
        }

        // The new member's body ends on its own: what it leaves running on the guests it opened is abandoned at its first settle, as the first member's was.
        Interlocked.Exchange(ref _bodyEnded, 0);
        var previous = _published;
        Attempts.Reserve();

        // Each member of a serial group is an attempt of its own, with its own timeout counted from its start (upstream creates an AttemptTimeout per attempt).
        // The previous member's attempt ends with it: what it left running cannot move the new member's timeout.
        if (_newAttemptTimeout is { } create)
        {
            lock (_timeoutGate)
            {
                var attemptTimeout = create();
                _attemptTimeout?.End();
                _attemptTimeout = attemptTimeout;
                if (_testTimeout > TimeSpan.Zero && _testTimeout != Timeout.InfiniteTimeSpan)
                {
                    attemptTimeout.Set(_testTimeout);
                }

                _publishedAttempt = _publishedAttempt with { SetTimeout = SetTimeoutOf(attemptTimeout) };
            }
        }

        // Each member's body owns the polls it starts.
        _publishedAttempt = _publishedAttempt with { Polls = new PollScope("the test body") };
        _published = Attempts.PublishAttempt(_publishedAttempt);
        previous?.Dispose();
    }

    /// <summary>
    /// Captures the engine's state and stages it under <paramref name="name"/> with the secret values this session learned, to be saved
    /// when the setup test passes. Upstream's <c>session.save(name)</c> fixture. Fails with <c>SESSION_CONTRACT</c> outside a setup test,
    /// for a name the setup did not declare, or for a name saved twice, and with <c>UNSUPPORTED_CAPABILITY</c> on an engine with no
    /// state capability. Assert that the sign-in worked before you save: the state is saved as it is.
    /// </summary>
    internal async Task SaveSessionAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_sessions?.Staging is not { } staging)
        {
            throw new TestException("SESSION_CONTRACT", "session.save() is only available in a setup test");
        }

        if ((_scope.EngineCapabilities & EngineCapabilities.State) == 0)
        {
            throw new ConfigurationException(
                "UNSUPPORTED_CAPABILITY",
                $"target \"{_scope.TargetName}\" has no engine state capability for session.save()"
            );
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _scope.Token());
        if (Bounded(_scope.ActionTimeout))
        {
            linked.CancelAfter(_scope.ActionTimeout);
        }

        var state = await _engine.CaptureStateAsync(OperationContext.Test, linked.Token).ConfigureAwait(false);
        staging.Stage(name, state, Secrecy.CarriedSecrets(_scope, _config?.AllSecrets ?? []));
    }

    /// <summary>
    /// Moves the action, assertion, and lifecycle timeouts of the operations that start from now on, for an <c>@e2e-dev/core</c> session's
    /// <c>setDefaultTimeout</c>. A running operation keeps the deadline it started with. Upstream's <c>LocatorEngine.setDefaultTimeouts</c> with
    /// the <c>fixtureContext</c> timeouts, read as getters.
    /// </summary>
    internal void SetDefaultTimeouts(TimeSpan timeout)
    {
        Screen.Engine.SetDefaultTimeouts(timeout, timeout, timeout);
        Browser.SetDefaultTimeouts(timeout, timeout, timeout);
        _scope.ActionTimeout = timeout;
    }

    /// <summary>
    /// Captures the engine's state and the secret values this session learned, in memory. What an <c>@e2e-dev/core</c> session's
    /// <c>saveState()</c> reads off the attempt. Fails with <c>UNSUPPORTED_CAPABILITY</c> on an engine with no state capability.
    /// </summary>
    internal async Task<SavedSession> CaptureStateAsync(CancellationToken cancellationToken = default)
    {
        if ((_scope.EngineCapabilities & EngineCapabilities.State) == 0)
        {
            throw new ConfigurationException(
                "UNSUPPORTED_CAPABILITY",
                $"engine {_scope.EngineName} has no state capability, so a session on it cannot be saved"
            );
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _scope.Token());
        if (Bounded(_scope.ActionTimeout))
        {
            linked.CancelAfter(_scope.ActionTimeout);
        }

        var state = await _engine.CaptureStateAsync(OperationContext.Test, linked.Token).ConfigureAwait(false);
        return new SavedSession(state, Secrecy.CarriedSecrets(_scope, _config?.AllSecrets ?? []));
    }

    /// <summary>
    /// Ends a setup test's attempt: when it passed, fails with <c>SESSION_CONTRACT</c> if it left a declared session unsaved, and otherwise
    /// writes each staged session to the run's store. A failed attempt saves nothing. Does nothing for a test that is not a setup test.
    /// </summary>
    internal async Task FinishSetupAsync(bool passed, CancellationToken cancellationToken = default)
    {
        if (_sessions is not { Staging: { } staging } plan || !passed)
        {
            return;
        }

        var missing = staging.Missing();
        if (missing.Count > 0)
        {
            throw new TestException(
                "SESSION_CONTRACT",
                $"setup must save each declared session exactly once; missing: [{string.Join(", ", missing)}]"
            );
        }

        foreach (var (name, saved) in staging.Entries())
        {
            await plan.Store.SaveAsync(name, plan.Identity, saved, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends <c>expect.soft</c> collection and returns one <c>ASSERTION_FAILED</c>
    /// for every failure kept since the session started, or null. Call it when
    /// the test body settles and fail the test with the result. Later soft
    /// matchers throw as hard ones would.
    /// </summary>
    public TestException? CloseSoftFailures() => Screen.SoftFailures.Close();

    /// <summary>The signal the test's own timeout cancels.</summary>
    internal CancellationToken TimeoutSignal => _timeout.Token;

    /// <summary>The error a body the test's own timeout cut short fails with (upstream's <c>TestTimeoutError</c>).</summary>
    internal TestException TestTimeoutError() => TimeoutFailure(_testTimeout, null, IsHosted);

    /// <summary>
    /// Stops what the body left in flight on the hosts and sessions it opened, however it ended: an opening under way is cancelled and a session with a
    /// step still running is closed. A timeout cuts the test's own steps at once, and a session's must not go on driving the app through teardown.
    /// Upstream's <c>guests.quiesce()</c> after the body (<c>run/execute.ts</c>).
    /// </summary>
    internal Task QuiesceGuestsAsync() => _guests?.QuiesceAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Collects a failure the engine holds from a path no step awaited (a route or dialog handler), within the cleanup budget, and
    /// throws it as it was so its code is kept. An adapter calls it when the test body has settled, then again after the teardown, before
    /// the verdict. Upstream's <c>settleAttempt</c>.
    /// </summary>
    public async Task SettleAttemptAsync(CancellationToken cancellationToken = default)
    {
        // The first call is the body's end: what it left running on the sessions it opened fails the test, and is cut before teardown starts.
        if (_guests is { } guests && Interlocked.Exchange(ref _bodyEnded, 1) == 0)
        {
            // A poll is cancelled alone; left alone, its timeout would fail whatever ran then. Steps come first in the errors, polls last.
            var pollsNotAwaited = _publishedAttempt?.Polls?.Close();
            var abandoned = await guests.AbandonRunningAsync().ConfigureAwait(false);
            if (pollsNotAwaited is not null)
            {
                abandoned = [.. abandoned, pollsNotAwaited];
            }

            if (abandoned.Count > 0)
            {
                // The attempt's own settle still runs; the first of the errors is the test's, and the settle's own failure is secondary.
                try
                {
                    await SettleOwnAsync(cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The test fails with what the body left running.
                catch (Exception secondary)
#pragma warning restore CA1031
                {
                    abandoned[0].Data["secondary"] = secondary.GetType().Name;
                }

                throw abandoned[0];
            }
        }

        await SettleOwnAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SettleOwnAsync(CancellationToken cancellationToken)
    {
        var settling = _scope.Session.SettleAttemptAsync(
            _scope.CleanupTimeout,
            OperationContext.Test,
            cancellationToken
        );
        try
        {
            await (
                Bounded(_scope.CleanupTimeout) ? settling.WaitAsync(_scope.CleanupTimeout, cancellationToken) : settling
            ).ConfigureAwait(false);
        }
        catch (TimeoutException ex) when (!settling.IsCompleted)
        {
            // Only the wait timing out: a TimeoutException a handler threw (a Playwright timeout) is the engine-to-session adapter's ENGINE_FAILURE, not this.
            throw new EngineException("CLEANUP_TIMEOUT", "settling the attempt timed out", ex);
        }
    }

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

    /// <summary>Records a hook that failed after the body (a failed <c>[TearDown]</c>), which the report tells as an <c>afterEach</c> secondary error beside the body's error.</summary>
    internal void RecordHookFailure(Exception failure) => _hookErrors.Add(failure);

    private List<Exception> SecondaryErrorsSnapshot()
    {
        lock (_secondaryErrors)
        {
            return [.. _secondaryErrors];
        }
    }

    /// <summary>Records the last failed assertion of a poll its phase timeout cut, which the report tells as a <c>body</c> secondary error beside the timeout (upstream's <c>recordSecondaryError</c>).</summary>
    internal void RecordSecondaryError(Exception failure)
    {
        lock (_secondaryErrors)
        {
            _secondaryErrors.Add(failure);
        }
    }

    /// <summary>
    /// Enters the teardown's budget, as upstream's <c>enter('afterEach')</c> does before each teardown: the session's operations run under a
    /// signal that follows <paramref name="interrupt"/> (the run's cancel), not the test timeout, so a body its timeout cut short can still drive
    /// the session. The teardown's deadline is the cleanup timeout, which caps the screen and browser steps that wait on the phase.
    /// </summary>
    internal void EnterTeardownBudget(CancellationToken interrupt)
    {
        // With the gate a timeout change holds, so a change either lands before the cleanup deadlines or is refused.
        lock (_timeoutGate)
        {
            Volatile.Write(ref _teardown, 1);
        }

        _budget.Enter(interrupt);
        var deadline = Bounded(_scope.CleanupTimeout) ? new Deadline(_scope.CleanupTimeout, TimeProvider.System) : null;
        _budget.Deadline = deadline;
        Screen.BudgetDeadline = deadline;
        Browser.BudgetDeadline = deadline;
    }

    /// <summary>Whether this session is a hosted one, which runs no e2e test (see <see cref="Host"/>).</summary>
    internal bool IsHosted => _hostedClose is not null;

    /// <summary>
    /// What upstream's <c>phase !== 'beforeEach' &amp;&amp; phase !== 'body'</c> check (<c>run/execute.ts</c>) reads: the teardown budget was entered, so the
    /// body and its hooks are over. .NET-only: the port has no <c>phase</c> variable, as the test framework runs the phases.
    /// </summary>
    internal bool InTeardown => Volatile.Read(ref _teardown) != 0;

    /// <summary>
    /// The attempt's <c>setTimeout</c> (upstream's <c>attempt.setTimeout</c>), bound to <paramref name="bound"/>; false once the attempt ended or its timeout fired, and
    /// where the session has no test timeout to move.
    /// </summary>
    internal Func<TimeSpan, bool> SetTimeoutOf(AttemptTimeout bound) =>
        timeout =>
        {
            if (InTeardown)
            {
                throw Internal.TestTimeout.LateTimeoutError();
            }

            lock (_timeoutGate)
            {
                return Volatile.Read(ref _state) == 0 && ReferenceEquals(_attemptTimeout, bound) && bound.Set(timeout);
            }
        };

    /// <summary>
    /// Makes this session an upstream hosted one: <paramref name="close"/> is the owner's close, which ends it once through <see cref="EndAsync"/>
    /// and gives its slot back, and <see cref="DisposeAsync"/> throws what <see cref="CloseAsync"/> returns.
    /// </summary>
    internal void Host(Func<Task<IReadOnlyList<Exception>>> close) => _hostedClose = close;

    /// <summary>
    /// Ends the session, once, and returns what went wrong: the soft assertion failures (as one <c>ASSERTION_FAILED</c>), the steps the test
    /// left running (<c>STEP_NOT_AWAITED</c>, whose work is cancelled), and the cleanup failures. A second call returns the same list.
    /// Disposing a session its owner hosts throws them as an <see cref="AggregateException"/>. Upstream's <c>close()</c>, which returns
    /// <c>SerializedError[]</c>: the port keeps the exceptions, and <see cref="E2EException.Code"/> carries the code.
    /// </summary>
    public Task<IReadOnlyList<Exception>> CloseAsync() => _hostedClose is { } close ? close() : EndAsync();

    internal Task<IReadOnlyList<Exception>> EndAsync() => _ending.Value;

    private async Task<IReadOnlyList<Exception>> EndOnceAsync()
    {
        var errors = new List<Exception>();
        TestException? soft = null;
        TestException? abandoned = null;

        // Read before the close cancels the test's own signal. .NET timers can fire a few milliseconds ahead of the clock the attempt's timeout reads, so the timer having fired is the deadline having passed.
        var timerFired = _timeout.IsCancellationRequested && !_openToken.IsCancellationRequested;
        try
        {
            soft = CloseSoftFailures();
            if (_cancelsOnClose is { } cancels)
            {
                _scope.Steps.NameRunning(cancels.Closed(), cancels.Cut);
            }
            else
            {
                abandoned = _scope.Steps.AbandonRunning();
            }
        }
#pragma warning disable CA1031 // The close result is cached, so a failure here is one of the failures it returns, never a faulted task.
        catch (Exception cause)
#pragma warning restore CA1031
        {
            errors.Add(cause);
        }

        // A poll the hosted session left running fails it at the line of the call and is cancelled. A test body's polls ended with the body (SettleAttemptAsync).
        var abandonedPoll = _hostedClose is not null ? _publishedAttempt?.Polls?.Close() : null;
        if (abandoned is null)
        {
            abandoned = abandonedPoll;
            abandonedPoll = null;
        }

        foreach (var failure in new Exception?[] { soft, abandoned, abandonedPoll })
        {
            if (failure is not null)
            {
                errors.Add(failure);
            }
        }

        if (abandoned is not null || _cancelsOnClose is not null)
        {
            // The work the test left running stops with the session, as upstream's close cancels the operations it still holds.
            try
            {
                await _budget.CancelCurrentAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The session was released already.
            }
        }

        if (_hostedClose is not null && abandoned is null && _cancelsOnClose is null)
        {
            // A hosted close cancels the operations it still holds, a poll among them (upstream's close). CancelCurrentAsync tolerates a released session.
            await _budget.CancelCurrentAsync().ConfigureAwait(false);
        }

        // Upstream's standalone attempt fails as timed out when the deadline passed, whether or not its timer ran before close.
        TestException? timedOut = null;
        if (
            _hostedClose is not null
            && !_openToken.IsCancellationRequested
            && (timerFired || _attemptTimeout is { Expired: true })
        )
        {
            timedOut = new TestException(
                "TEST_TIMEOUT",
                "session timed out after "
                    + ((long)(_attemptTimeout?.Timeout ?? _testTimeout).TotalMilliseconds).ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + " ms"
            );
            errors.Add(timedOut);
        }

        // A poll the timeout cut tells its last failed assertion beside the timeout.
        errors.AddRange(SecondaryErrorsSnapshot());

        // A hosted session's cancel (upstream's <c>signal</c>) that cut a step ends the attempt as interrupted: upstream's <c>classifyAttemptStatus(failure, timedOut, interrupted)</c>.
        Exception? interrupted =
            _hostedClose is not null
            && _openToken.IsCancellationRequested
            && StepRecords.Any(step => step.Status == StepStatus.Cancelled)
                ? new OperationCanceledException("the session was cancelled while its test ran", _openToken)
                : null;

        // Upstream's standalone attempt `verdict()`: with no other failure, a hosted session's first step that did not pass is the verdict.
        var stepFailure =
            _hostedClose is not null && (timedOut ?? soft ?? abandoned ?? interrupted) is null ? Verdict() : null;
        Complete(
            timedOut ?? soft ?? abandoned ?? interrupted ?? stepFailure,
            _hostedClose is not null ? _openToken : default
        );
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The session's failures are reported together, whichever step they came from.
        catch (Exception cause)
#pragma warning restore CA1031
        {
            errors.Add(cause);
            CleanupFailed = true;
        }

        return errors;
    }

    /// <summary>Whether releasing the session failed: what it left may be unclean, unlike a test outcome in <see cref="CloseAsync"/>'s list.</summary>
    internal bool CleanupFailed { get; private set; }

    /// <summary>
    /// Why the test is marked as expected to fail (upstream's <c>fail</c> option or <c>test.fail</c>), null when it is not. A host that reads the mark
    /// from its test framework sets it before <see cref="Complete"/>, which applies it to the attempt (<see cref="E2E.ExpectedFailure.SettleExpectedFailure"/>).
    /// </summary>
    internal string? ExpectedFailure { get; set; }

    /// <summary>
    /// The runtime skip of a test that skipped itself with <c>Fixme</c>; null when it did not. Its skip reports the cause <c>fixme</c> and not
    /// <c>explicit</c> (upstream's <c>RuntimeSkip.cause</c>), with its reason as the host's own skip message may carry a prefix.
    /// </summary>
    internal RuntimeSkip? RuntimeSkip { get; set; }

    /// <summary>Whether this is a setup test: the one that declares the sessions others restore.</summary>
    internal bool IsSetup => _sessions is { Staging: not null };

    /// <summary>The mark of the settled attempt: the reason, and the error that made a failed attempt pass. Null when the test is not marked or skipped.</summary>
    internal ExpectedFailureInfo? SettledMark => _expectedFailure;

    /// <summary>The verdict of the settled attempt: what stopped it, null when it passed.</summary>
    internal Exception? SettledError => _error;

    public async ValueTask DisposeAsync()
    {
        if (_hostedClose is null)
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            return;
        }

        // Disposal has no result to return, so it throws the failures rather than dropping them, always as an aggregate.
        var failures = await CloseAsync().ConfigureAwait(false);
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "closing the e2e session failed: " + string.Join("; ", failures.Select(failure => failure.Message)),
                failures
            );
        }
    }

    private async ValueTask DisposeCoreAsync()
    {
        Exception? commitFailure = null;
        try
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                _scope.Debug.Record(
                    "test.body",
                    System.Diagnostics.Stopwatch.GetElapsedTime(_bodyStarted).TotalMilliseconds
                );
                try
                {
                    Commit(
                        new TestException("TEST_FAILED", "The session ended before it was completed."),
                        CancellationToken.None
                    );
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
            // A failure that first landed in the teardown has had no look yet.
            if (_error is { } failure)
            {
                await CaptureFailureEvidenceAsync(failure).ConfigureAwait(false);
            }

            // A recording the test left running is stopped and kept before the attempt's own video is collected.
            await StopRunningRecordingAsync().ConfigureAwait(false);

            if (_video is not null)
            {
                try
                {
                    await _video
                        .StopAsync(_status, _scope.CleanupTimeout, _scope.Artifacts?.Sink)
                        .ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The engine is released first, then this is rethrown.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    videoFailure = ex;
                    _cleanupErrors.Add(
                        ErrorSerializer.TranslateProvisioningError(
                            ex,
                            " while stopping the video",
                            CancellationToken.None
                        )
                    );
                }

                Videos = _video.Kept;
            }

            // What the test opened and left open ends here, its videos among the attempt's own, before the engine goes.
            if (_guests is { } guests)
            {
                foreach (var cause in await guests.SettleAsync(_status).ConfigureAwait(false))
                {
                    _cleanupErrors.Add(cause);
                    CleanupFailed = true;
                }
            }

            await _scope
                .Debug.Time("session.close", async () =>
                {
                    try
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
                    }
#pragma warning disable CA1031 // Recorded for the report, then rethrown.
                    catch (Exception ex) when (ex is not TimeoutException)
#pragma warning restore CA1031
                    {
                        _cleanupErrors.Add(
                            ErrorSerializer.TranslateProvisioningError(
                                ex,
                                " while ending the attempt",
                                CancellationToken.None
                            )
                        );
                        throw;
                    }
                })
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            var timeout = new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "The engine did not shut down within the cleanup timeout ("
                    + _scope.CleanupTimeout.TotalMilliseconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + " ms).",
                ex
            );
            _cleanupErrors.Add(timeout);
            throw timeout;
        }
        finally
        {
            _errorRedaction.Dispose();
            _credentials?.Dispose();
            _scope.ReleaseSessionLedger();
            _scope.Ended.Dispose();
            _budget.Dispose();
            WriteDebug(_scope.Debug, _debugOutput, _scope.Steps.All());
            WriteAiTrace(_scope);
            try
            {
                await WriteTracePageAsync().ConfigureAwait(false);
            }
            finally
            {
                // The hosts the test launched share their secrets until the artifacts have settled: a download still being rewritten is redacted with them.
                _guests?.Release();
            }
        }

        if (videoFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(videoFailure);
        }
    }

    /// <summary>
    /// Stops a recording the test started with <c>app.StartRecordingAsync</c> and left running, within the cleanup budget, and keeps it as
    /// the attempt's artifact. One that cannot be finalized is a cleanup failure. Upstream's <c>stopRunningRecording</c>.
    /// </summary>
    private async Task StopRunningRecordingAsync()
    {
        if (_recordings is not { SpanRunning: true } recordings || _scope.Artifacts?.Sink is not { } sink)
        {
            return;
        }

        try
        {
            using var cleanup = new CancellationTokenSource();
            if (Bounded(_scope.CleanupTimeout))
            {
                cleanup.CancelAfter(_scope.CleanupTimeout);
            }

            // Like the attempt's video, the recording belongs to the attempt: no step links it.
            await recordings
                .StopSpanAsync(
                    sink,
                    OperationContext.Test with
                    {
                        Timeout = Bounded(_scope.CleanupTimeout) ? _scope.CleanupTimeout : null,
                    },
                    cleanup.Token
                )
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A recording that cannot be finalized is a cleanup failure, and the rest of the teardown goes on.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _cleanupErrors.Add(
                ErrorSerializer.TranslateProvisioningError(ex, " while stopping the recording", CancellationToken.None)
            );
        }
    }

    // Tells the executors that hold state for the attempt it is over.
    private void EndAttempt()
    {
        _published?.Dispose();
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
    /// <summary>.NET-only: the test's signal. With the system clock it links to the caller's token; with an injected clock (<see cref="E2ESessionOptions.Clock"/>) its timer reads that clock too, and the caller's token cancels it.</summary>
    private static CancellationTokenSource NewTimeoutSource(TimeProvider clock, CancellationToken cancellationToken)
    {
        if (clock == TimeProvider.System)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        return new ClockedTimeoutSource(clock, cancellationToken);
    }

    /// <summary>.NET-only: a signal whose timer reads an injected clock and that the caller's token cancels; disposing it releases that registration, as a linked source does.</summary>
    private sealed class ClockedTimeoutSource : CancellationTokenSource
    {
        private readonly CancellationTokenRegistration _caller;

        public ClockedTimeoutSource(TimeProvider clock, CancellationToken cancellationToken)
            : base(Timeout.InfiniteTimeSpan, clock) =>
            _caller = cancellationToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), this);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _caller.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private static Dictionary<string, ResolvedAgent> ResolveAgents(E2ESessionOptions options)
    {
        var agents = new Dictionary<string, ResolvedAgent>(StringComparer.Ordinal)
        {
            ["default"] = ResolvedAgent.From(
                "default",
                new AgentOptions
                {
                    Model = options.Model,
                    Judge = options.Judge,
                    Executor = options.AgentExecutor,
                    System = options.AgentSystem,
                    Context = options.AgentContext,
                    MaxSteps = options.MaxSteps,
                    MaxModelCalls = options.MaxModelCalls,
                    JudgmentTimeout = options.JudgmentTimeout,
                    MaxObservationBytes = options.MaxObservationBytes,
                    MaxInputTokens = options.MaxInputTokens,
                    ProviderOptions = options.ProviderOptions,
                    Tools = options.AgentTools,
                }
            ),
        };
        if (options.Agents is null)
        {
            return WithTestContext(agents, options.TestAgentContext);
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
                    "Agents cannot hold \"default\"; the session's Model, Judge, AgentExecutor, AgentSystem, AgentTools, AgentContext, MaxSteps, MaxModelCalls, JudgmentTimeout, and ProviderOptions are the default agent"
                );
            }

            agents[pair.Key] = ResolvedAgent.From(pair.Key, pair.Value);
        }

        return WithTestContext(agents, options.TestAgentContext);
    }

    // Every prompt reads the agent's own context, then the test's agentContext, whichever agent a call picks.
    private static Dictionary<string, ResolvedAgent> WithTestContext(
        Dictionary<string, ResolvedAgent> agents,
        string? testContext
    )
    {
        return agents.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.WithTestContext(testContext),
            StringComparer.Ordinal
        );
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

    /// <summary>
    /// Looks at the app once more and keeps what the screen holds, the moment a failure is known: an adapter calls it when
    /// the test body fails, ahead of the test's own teardown, so a teardown that navigates away cannot replace the evidence
    /// (upstream's <c>captureEvidence</c>). It looks once per attempt. A failure that first lands in the teardown is
    /// looked at when the session is released. Only an attempt whose trace page is kept looks.
    /// </summary>
    public async Task CaptureFailureEvidenceAsync(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = ExecuteVideo.StatusOf(error);
        if (
            status == AttemptStatus.Passed
            || _trace is not { Keep: { } keep } trace
            || !RecordingModes.Keeps(keep, status)
            || Interlocked.Exchange(ref _evidenceTried, 1) != 0
        )
        {
            return;
        }

        // The look is a courtesy: one that fails must not hide the test's own result.
        try
        {
            _evidence = await FailureEvidence
                .CaptureAsync(
                    new FailureEvidenceOptions
                    {
                        Observe = token => _scope.Session.ObserveAsync(OperationContext.Test, token),
                        Capture = token => _scope.CaptureAsync(token),
                        Error = error,
                        Redactor = _scope.Redactor,
                        OwnStep =
                            StepRecords.LastOrDefault(step => step.Status != StepStatus.Passed) is not { } failed
                            || OwnSteps.IsOwnStep(failed),
                        AppOrigin = _scope.AppOrigin,
                        AppBase = _scope.BaseUrl,
                        MaxObservationBytes = ResolvedAgent.RunLimits(_scope.Agents.Values).MaxObservationBytes,
                        AttemptDir = trace.AttemptDir,
                    }
                )
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Evidence is a courtesy: a look that fails must not hide the test's own result.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _evidence = null;
        }
    }

    // The page is a courtesy as well: one that cannot be written must not hide the test's own result.
    private async Task WriteTracePageAsync()
    {
        if (_trace is not { } trace)
        {
            return;
        }

        // Every attempt joins its test's report, kept or not, so the page of a test tells all its attempts.
        var kept = trace.Keep is { } keep && RecordingModes.Keeps(keep, _status);
        try
        {
            // The artifacts' sizes and digests, and the hand-offs to the store, finish before the record is read.
            await SettleArtifactsAsync().ConfigureAwait(false);

            // Every attempt joins its test's report, and the run's report.json tells them all.
            var (single, id) = AttemptReport.Build(
                _scope,
                trace,
                _status,
                _error,
                _bodyMs,
                Exploration,
                _evidence,
                _cleanupErrors,
                _expectedFailure,
                _hookErrors,
                Skip.RuntimeSkipCause(RuntimeSkip?.Cause),
                RuntimeSkip?.Reason,
                SecondaryErrorsSnapshot()
            );
            if (_hostedClose is not null)
            {
                _events?.Finished(single.Run.Results[0], trace.ResultsRoot, _scope.Redactor.Redact);
                await WriteHostTracePageAsync(single, trace).ConfigureAwait(false);
                return;
            }

            var (report, traced, redact) = RunReport.Add(
                single,
                trace.ResultsRoot,
                _scope.Attempt,
                kept,
                // Read the redactor at each call: a value another session learns later rebuilds it, and the report is redacted again with it.
                text => _scope.Redactor.Redact(text)
            );
            // The document is a courtesy too: the page below is still written when it cannot be.
            try
            {
                await ReportFile
                    .WriteAsync(
                        report,
                        ReportFile.ProjectRootOf(_scope.ProjectRoot, trace.ResultsRoot),
                        trace.ResultsRoot,
                        redact,
                        _config
                    )
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The document is a courtesy: whatever fails in building it must not fail a test that passed.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                Console.Error.WriteLine(
                    "[e2e] could not write "
                        + ReportFile.FileName
                        + " for "
                        + _scope.TestTitle
                        + ": "
                        + _scope.Redactor.Redact(ex.Message)
                );
            }

            _events?.Finished(report.Run.Results[0], trace.ResultsRoot, redact);
            if (traced)
            {
                var pages = await TracePages
                    .WriteTracePagesAsync(
                        report,
                        new HashSet<string>(StringComparer.Ordinal) { id },
                        new WriteTracePagesOptions(_scope.ProjectRoot!, trace.ResultsRoot, trace.CacheDir),
                        redact
                    )
                    .ConfigureAwait(false);
                foreach (var (resultId, page) in pages)
                {
                    ReportFile.SetTrace(trace.ResultsRoot, resultId, page);
                }
            }

            var document = await ReportFile
                .SingleAsync(single, _scope.ProjectRoot, trace.ResultsRoot, redact, _config)
                .ConfigureAwait(false);
            Volatile.Write(ref _report, new Lazy<Report>(() => ReportSerializer.Read(document)));

            if (!traced && _scope.Attempt == 1)
            {
                // Upstream empties the results directory when a run starts. A first attempt that keeps nothing clears the page an earlier run left.
                try
                {
                    File.Delete(Path.Combine(trace.TestDir, TracePages.TracePage));
                    ReportFile.SetTrace(trace.ResultsRoot, id, null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A page that cannot be removed is stale, not a failure of the test.
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(
                "[e2e] could not write the trace page for "
                    + _scope.TestTitle
                    + ": "
                    + _scope.Redactor.Redact(ex.Message)
            );
        }
    }

    // A hosted attempt is a test of another runner: it writes its own page, and has no report.json or retries to join.
    private async Task WriteHostTracePageAsync(Report1Document single, TraceOutput trace)
    {
        var hosted = await HostTrace
            .WriteHostTraceAsync(
                new HostTraceOptions
                {
                    Attempt = single,
                    ProjectRoot = ReportFile.ProjectRootOf(_scope.ProjectRoot, trace.ResultsRoot),
                    ResultsDir = trace.ResultsRoot,
                    Segments = [Path.GetFileName(trace.TestDir)],
                    TracePath = TracePath,
                    Redact = _scope.Redactor.Redact,
                }
            )
            .ConfigureAwait(false);
        Volatile.Write(ref _report, new Lazy<Report>(() => hosted));
    }

    // A store that hangs must not hold the session: the hand-offs are bounded by the cleanup timeout, and one that is not done by then
    // leaves its record without a ref (the store's failure is its own).
    private async Task SettleArtifactsAsync()
    {
        if (_scope.CreatedArtifacts is not { } artifacts)
        {
            return;
        }

        using var bound = new CancellationTokenSource();
        if (Bounded(_scope.CleanupTimeout))
        {
            bound.CancelAfter(_scope.CleanupTimeout);
        }

        try
        {
            await artifacts.SettleAsync(bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested)
        {
            Console.Error.WriteLine(
                "[e2e] the artifacts of " + _scope.TestTitle + " were not all stored within the cleanup timeout."
            );
        }
    }

    private static bool Bounded(TimeSpan timeout) => timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan;

    /// <summary>
    /// How the attempt ended. The caller's <paramref name="cancellationToken"/> is the user's cancel (upstream's interrupt signal), kept apart
    /// from the verdict: a cancellation the user did not ask for (an HTTP timeout inside the test, with the token not cancelled) is a
    /// failure, not an interrupt. A failure is never upgraded, so a timeout the framework reported as a failure stays one.
    /// </summary>
    private AttemptStatus StatusOf(Exception? error, CancellationToken cancellationToken)
    {
        var status = ExecuteVideo.StatusOf(error);
        if (status != AttemptStatus.Interrupted || cancellationToken.IsCancellationRequested)
        {
            return status;
        }

        // The session's own test timeout cancels the calls the test awaits: that is a timeout, not a failure.
        return _timeout.IsCancellationRequested ? AttemptStatus.TimedOut : AttemptStatus.Failed;
    }

    /// <summary>The error of the first step that did not pass, as the exception the attempt's verdict is read from; null when every step passed.</summary>
    private Exception? Verdict()
    {
        if (StepRecords.FirstOrDefault(step => step.Status != StepStatus.Passed) is not { } step)
        {
            return null;
        }

        var code = step.Error?.Code ?? "TEST_FAILED";
        var message = step.Error?.Message ?? "step " + step.Api + " did not pass";
        return step.Status switch
        {
            StepStatus.Blocked => new AgentException(code, message, blocked: true) { Details = step.Error?.Details },
            StepStatus.TimedOut => new TestException("TEST_TIMEOUT", message) { Details = step.Error?.Details },
            _ => new TestException(code, message) { Details = step.Error?.Details },
        };
    }

    /// <summary>
    /// The failure the attempt ends with for <paramref name="error"/>, as <see cref="Complete"/> keeps it. A cancellation nobody asked for fails the
    /// test: left as is, its CANCELLED error would read as the interrupt category and exit 130. The session's own timeout is upstream's
    /// <c>TestTimeoutError</c>, <c>test timed out after N ms in phase body</c>.
    /// </summary>
    internal Exception? VerdictOf(Exception? error, CancellationToken cancellationToken)
    {
        // A step the session's own timeout cut short fails with its CANCELLED error: that is the timeout.
        if (
            error is E2EException { Code: "CANCELLED" }
            && !cancellationToken.IsCancellationRequested
            && _timeout.IsCancellationRequested
        )
        {
            return TimeoutFailure(error);
        }

        return VerdictOf(error, StatusOf(error, cancellationToken));
    }

    private TestException TimeoutFailure(Exception cause) =>
        TimeoutFailure(_attemptTimeout?.Timeout ?? _testTimeout, cause, IsHosted);

    private static TestException TimeoutFailure(TimeSpan testTimeout, Exception? cause, bool hosted)
    {
        // A hosted session runs no test: its timeout reads "session timed out" (upstream's standalone attempt), a test's "in phase body".
        var ms = ((long)testTimeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var message = hosted
            ? "session timed out after " + ms + " ms"
            : "test timed out after " + ms + " ms in phase body";
        return cause is null
            ? new TestException("TEST_TIMEOUT", message)
            : new TestException("TEST_TIMEOUT", message, cause);
    }

    private Exception? VerdictOf(Exception? error, AttemptStatus status)
    {
        if (
            error is null
            || status is not (AttemptStatus.Failed or AttemptStatus.TimedOut)
            || ExecuteVideo.StatusOf(error) != AttemptStatus.Interrupted
        )
        {
            return error;
        }

        return status == AttemptStatus.TimedOut
            ? TimeoutFailure(error)
            : new TestException("TEST_FAILED", error.Message, error);
    }

    /// <summary>
    /// Applies the test's mark to the attempt once its status is classified, before its recordings are kept or dropped, so they follow the
    /// verdict (upstream's <c>settleExpectedFailure</c> call in <c>TargetExecutor</c>, <c>run/execute.ts</c>).
    /// </summary>
    private void SettleMark()
    {
        if (ExpectedFailure is not { } mark)
        {
            return;
        }

        Exception? unexpectedPass = null;
        var record = new MarkableAttempt
        {
            Status = _status,
            Error = _error is null
                ? null
                : ReportError.Of(ErrorSerializer.Serialize(_error, _scope.ProjectRoot, _scope.Redactor.Redact), "body"),
        };
        E2E.ExpectedFailure.SettleExpectedFailure(
            record,
            mark,
            new ExpectedFailureSerializer(
                cause =>
                {
                    unexpectedPass = cause;
                    return ReportError.Of(
                        ErrorSerializer.Serialize(cause, _scope.ProjectRoot, _scope.Redactor.Redact),
                        "body"
                    );
                },
                _scope.Redactor.Redact
            )
        );
        _status = record.Status;
        _error = record.Error is null ? null : unexpectedPass ?? _error;
        _expectedFailure = record.ExpectedFailure;
    }

    private void Commit(Exception? error, CancellationToken cancellationToken)
    {
        _status = StatusOf(error, cancellationToken);
        _error = VerdictOf(error, _status);

        // A step the session's own timeout cut short is the timeout, as the host's hand-over reads it.
        if (
            _status == AttemptStatus.Failed
            && VerdictOf(error, cancellationToken) is TestException { Code: "TEST_TIMEOUT" } timedOut
        )
        {
            _status = AttemptStatus.TimedOut;
            _error = timedOut;
        }

        _bodyMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(_bodyStarted).TotalMilliseconds;

        // The cache below still settles on what the attempt did, read off `error`.
        SettleMark();
        if (!_scope.CacheWrite || _scope.Cache is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // A model outage says nothing about the recording, so it stays, unless a failure on the app implicated it too.
        // Strict mode never reaches here: it writes nothing.
        var preserve = (AttemptScope.KeepsCache(error) || _scope.KeepCache) && !_scope.ImplicatesUnconfirmed;
        var writes = Flush(_scope.Cache, _scope.Acts, preserve);
        foreach (var step in _scope.Steps.All())
        {
            if (step.Cache is not null && writes.TryGetValue(step.Index, out var write))
            {
                step.CacheWrite = write;
            }
        }
    }

    /// <summary>
    /// Settles the acts of one attempt: a verified act's recording is written, and an unverified one that
    /// recorded or replayed is evicted, unless <paramref name="preserve"/> says the failure implicates nothing.
    /// Returns what became of each step's recording, by step index.
    /// </summary>
    internal static IReadOnlyDictionary<int, CacheWrite> Flush(
        IStepCache cache,
        IEnumerable<PendingAct> acts,
        bool preserve
    ) => Flush(cache, acts, _ => preserve);

    /// <summary>As <see cref="Flush(IStepCache, IEnumerable{PendingAct}, bool)"/>, with <paramref name="preserveOf"/> decided per act: a program's session settles each failure it went on after separately.</summary>
    internal static IReadOnlyDictionary<int, CacheWrite> Flush(
        IStepCache cache,
        IEnumerable<PendingAct> acts,
        Func<PendingAct, bool> preserveOf
    )
    {
        var outcomes = new Dictionary<int, CacheWrite>();
        foreach (var act in acts)
        {
            var preserve = preserveOf(act);
            var recorded = act.Completed && act.Entry is { Actions.Count: > 0 } && !act.ParamCollision;
            var outcome = act.Decided;
            try
            {
                if (outcome is not null)
                {
                    // The step settled its own recording when it concluded.
                }
                else if (act.Verified && recorded)
                {
                    if (act.ReplayedWhole)
                    {
                        outcome = CacheWrite.Kept;
                        CompleteProvenance(cache, act.Key, act.Step);
                    }
                    else if (HoldsSameFlow(cache, act.Key, act.Entry!))
                    {
                        outcome = CacheWrite.Kept;
                    }
                    else
                    {
                        // A store that throws did not write the recording, and neither did one that answers null.
                        outcome = CacheWrite.NotWritten;
                        outcome = cache.Write(act.Key, act.Entry!) is null ? CacheWrite.NotWritten : CacheWrite.Saved;
                    }
                }
                // An entry read for an act that then passed with nothing to record is evicted too: it did not serve
                // this pass, and nothing replaces it, so every later run would hand off the same way.
                else if (!preserve && (recorded || act.ConsumedReplay || (act.Completed && act.ReadEntry)))
                {
                    // An entry the step read existed for certain, so deleting it is an eviction, reported only once the delete
                    // returned. A new recording's delete only clears what an earlier run left, so it stays unconfirmed. A step
                    // that recorded nothing and kept no whole replay reports nothing when the delete throws, as upstream's
                    // evict stages nothing then.
                    var evicts = !recorded || act.ReplayedWhole;
                    outcome = recorded || act.ReplayedWhole ? CacheWrite.Unconfirmed : null;
                    cache.Delete(act.Key);
                    if (evicts)
                    {
                        outcome = CacheWrite.Evicted;
                    }
                }
                else if (recorded)
                {
                    outcome = CacheWrite.Unconfirmed;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The cache is disposable, as a replay's read is: a store that fails here costs the next run a live step.
            }

            if (act.StepIndex is { } index && outcome is { } settled)
            {
                outcomes[index] = settled;
            }
        }

        return outcomes;
    }

    // A verified whole replay proved which step an entry belongs to when its stored provenance lacks the occurrence fields, or its
    // identity digest is not the step's (missing while the step has one, different, or present while the step has none), so it writes
    // the step's provenance in. Strict mode matches only entries that record the whole step. Every other entry stays as stored.
    private static void CompleteProvenance(IStepCache cache, string key, CacheEntry? step)
    {
        if (
            step is null
            || cache.Read(key).Entry is not { } existing
            || existing.CallIndex is not null
                && string.Equals(existing.IdentityDigest, step.IdentityDigest, StringComparison.Ordinal)
        )
        {
            return;
        }

        existing.SetStep(step);
        cache.Write(key, existing);
    }

    // Rewriting an identical flow would only churn a committed cache directory. The model that recorded it decides nothing
    // a replay does, and neither does the measured end wait, so another model recording the same flow rewrites nothing.
    private static bool HoldsSameFlow(IStepCache cache, string key, CacheEntry entry)
    {
        var existing = cache.Read(key).Entry;
        return existing is not null && string.Equals(FlowOf(existing), FlowOf(entry), StringComparison.Ordinal);
    }

    private static string FlowOf(CacheEntry entry)
    {
        var flow = JsonSerializer.SerializeToNode(entry, JsonDefaults.Options)!.AsObject();
        flow.Remove("recordedBy");
        flow.Remove("endWaitMs");
        flow.Remove("keyedBy");
        // The rule that flagged a gap's typed value follows how the agent read the value this time. A replay stops at a gap
        // whatever its rule, which only names the hand-off in the report (upstream's flowOf).
        foreach (var action in flow["actions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            if (
                string.Equals(
                    (action["kind"] as JsonValue)?.TryGetValue<string>(out var kind) == true ? kind : null,
                    "tool",
                    StringComparison.Ordinal
                )
            )
            {
                action.Remove("derived");
            }
        }

        return flow.ToJsonString();
    }
}

/// <summary>What closing a session does to what runs on it. Upstream's <c>cancelsOnClose</c> of <c>StandaloneSessionOptions</c>.</summary>
/// <param name="Closed">What an operation started after the close fails with.</param>
/// <param name="Cut">What a step still running at the close fails with; the argument is its api and, when it has one, its quoted label.</param>
internal sealed record CancelsOnClose(Func<E2EException> Closed, Func<string, E2EException> Cut);

/// <summary>How to open one <see cref="E2ESession"/>. Retries set <see cref="Attempt"/> above 1 so the cache records but does not replay, except under <see cref="CacheStrict"/>, where every attempt replays.</summary>
public sealed class E2ESessionOptions
{
    /// <summary>The values of the config's services for <see cref="TestContext.Services"/>.</summary>
    public ServiceValues Services { get; init; } = ServiceValues.Empty;

    public required IEngine Engine { get; init; }

    /// <summary>Throws a failed step's error classified and redacted, for a program with no report around it. Upstream's <c>redactThrown</c> of <c>StandaloneSessionOptions</c>.</summary>
    internal bool RedactThrown { get; init; }

    /// <summary>
    /// Makes closing the way the session's owner cancels what runs on it. Absent, a step still running at close was not awaited and fails the
    /// attempt as <c>STEP_NOT_AWAITED</c>. Upstream's <c>StandaloneSessionOptions.cancelsOnClose</c>.
    /// </summary>
    internal CancelsOnClose? CancelsOnClose { get; init; }

    /// <summary>Whether the engine shows its surface; handed to the engine's start. Upstream's <c>headed</c>.</summary>
    internal bool Headed { get; init; }

    /// <summary>Takes each progress of the session's steps with the record it is of, for a session whose steps a test's recorder mirrors (<see cref="GuestSteps"/>). Upstream's <c>mirror</c> of <c>StandaloneSessionOptions</c>.</summary>
    internal Action<StepProgress, StepRecord>? Mirror { get; init; }

    /// <summary>The video this session records, in place of what the target's mode says. Upstream's <c>video</c> of <c>StandaloneSessionOptions</c>.</summary>
    internal VideoRecording? VideoRecording { get; init; }

    /// <summary>
    /// Whether the session takes in the hosts and sessions its test opens beside it with <see cref="Launch"/> (<see cref="AttemptGuestSessions"/>), and
    /// is published for them to find (<see cref="Attempts.PublishAttempt"/>). A test's attempt does; a hosted one does not. Upstream's <c>guests</c> of
    /// the attempt <c>execute.ts</c> publishes.
    /// </summary>
    internal bool OffersGuests { get; init; }

    /// <summary>
    /// Whether the session publishes its attempt (<see cref="Attempts.PublishAttempt"/>) without offering guests, so an <c>expect.poll</c> in its flow reads
    /// the attempt's <c>assertionTimeout</c>, <c>toPass</c> defaults, and budget. A hosted session does (upstream's <c>openSession</c> publishes while it is open).
    /// </summary>
    internal bool PublishesAttempt { get; init; }

    /// <summary>How many finished steps the session keeps, for a program whose session has no report. Upstream's <c>retainSteps</c> of <c>StandaloneSessionOptions</c>.</summary>
    internal int? RetainSteps { get; init; }

    /// <summary>A state an earlier session captured, restored at launch. Upstream's <c>state</c> of <c>StandaloneSessionOptions</c>.</summary>
    internal SavedSession? State { get; init; }

    /// <summary>The config's accounts, which <see cref="E2E.Credentials.User"/> resolves while the session runs. A fixture passes <c>Config.Credentials</c>.</summary>
    internal IReadOnlyDictionary<string, ResolvedCredential>? Credentials { get; init; }

    /// <summary>The resolved config the session runs under, whose digest every report it writes carries as <c>project.configDigest</c> (upstream's <c>config</c> of <c>buildReport</c>). A fixture passes <c>HostTestFilters.Resolve</c> of its config.</summary>
    internal ResolvedConfig? Config { get; init; }

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

    /// <summary>The default agent's <c>maxObservationBytes</c>. See <see cref="AgentOptions.MaxObservationBytes"/>.</summary>
    public int MaxObservationBytes { get; init; } = E2EDefaults.MaxObservationBytes;

    /// <summary>The default agent's <c>maxInputTokens</c>. See <see cref="AgentOptions.MaxInputTokens"/>.</summary>
    public int MaxInputTokens { get; init; } = E2EDefaults.MaxInputTokens;

    /// <summary>The default agent's provider options, keyed by provider. See <see cref="ModelRequest.ProviderOptions"/>.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>
    /// Named agents besides the default one, picked per call with the <c>Agent</c> option.
    /// Each starts from the built-in defaults, not from the default agent.
    /// </summary>
    public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }

    /// <summary>The agent a call that names none uses, upstream's <c>agent</c> option of a test or run. Unset, it is <c>default</c>. An unknown name is <c>INVALID_ARGUMENT</c>.</summary>
    public string? Agent { get; init; }

    /// <summary>Upstream's <c>agentContext</c> of a test or group: appended to the context of every agent, after its own.</summary>
    public string? TestAgentContext { get; init; }

    public string? BaseUrl { get; init; }

    /// <summary>The target's <c>app.permissions</c>: what the engine grants the app. Upstream's <c>EngineAppInfo.permissions</c>.</summary>
    public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }

    /// <summary>The target's <c>app.permissions: 'all'</c>, handed to the engine as <see cref="EngineStartOptions.AllPermissions"/>.</summary>
    public bool AllPermissions { get; init; }

    /// <summary>The target's <c>app.bundleId</c>, handed to the engine as <see cref="EngineStartOptions.BundleId"/>.</summary>
    public string? BundleId { get; init; }

    /// <summary>The target's <c>app.linkBase</c>, which <c>App.OpenAsync(path)</c> joins a path to on a device. It applies only to a pinned app with no <see cref="BaseUrl"/>; with a base URL a path resolves against that URL.</summary>
    public string? LinkBase { get; init; }

    /// <summary>The target's <c>app.appPath</c>, handed to the engine as <see cref="EngineStartOptions.AppPath"/>.</summary>
    public string? AppPath { get; init; }

    /// <summary>The target's <c>app.launchArguments</c>, handed to the engine as <see cref="EngineStartOptions.LaunchArguments"/>.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }

    /// <summary>The target's <c>app.launchArguments</c> when declared as an object, handed to the engine as <see cref="EngineStartOptions.LaunchArgumentValues"/>.</summary>
    public IReadOnlyDictionary<string, LaunchArgumentValue>? LaunchArgumentValues { get; init; }

    /// <summary>Anchors the files a test names, such as a route's fulfill <c>Path</c>. Defaults to the working directory.</summary>
    public string? ProjectRoot { get; init; }

    public IStepCache? Cache { get; init; }

    /// <summary>
    /// Where artifacts go besides the results directory (upstream's <c>artifacts.store</c>): every artifact the attempt registers, a
    /// download or a video, is handed to the store as it is produced, and the store's reference is recorded as the artifact's <c>ref</c>.
    /// Null keeps files local only. The automatic failure screen text and screenshot are not handed over yet.
    /// </summary>
    public IArtifactStore? ArtifactStore { get; init; }

    /// <summary>
    /// The digest of the target app's resolved <c>identity</c> and <c>environment</c> that the replay cache keys on (upstream's
    /// <c>SessionIdentity.appIdentity</c>). Unset, entries key on the <c>test</c> environment alone.
    /// </summary>
    public string? AppIdentity { get; init; }

    /// <summary>The saved session this test restores and the sessions it saves; see <see cref="SessionPlan"/>. Null for a test that uses none.</summary>
    internal SessionPlan? Sessions { get; init; }

    public bool CacheEnabled { get; init; } = true;

    /// <summary><see cref="CacheMode.ReadOnly"/> replays but never writes or deletes recordings.</summary>
    public CacheMode CacheMode { get; init; } = CacheMode.ReadWrite;

    /// <summary>
    /// When true, a recording that no longer matches fails the act with <c>REPLAY_STALE</c>. So does a
    /// <c>no-entry</c> miss while a <see cref="FileStepCache"/> holds a recording of the same step under another key.
    /// Retries replay too, and the session never writes or deletes a recording, whatever <see cref="CacheMode"/> says.
    /// </summary>
    public bool CacheStrict { get; init; }

    /// <summary>What turned <see cref="CacheStrict"/> on, for the advice a stale strict replay carries. Unset, the config key did.</summary>
    internal CacheStrictSource? CacheStrictSource { get; init; }

    /// <summary>How long the engine may take to start. Upstream <c>launchTimeout</c>.</summary>
    public TimeSpan LaunchTimeout { get; init; } = E2EDefaults.LaunchTimeout;

    /// <summary>How long the engine may take to shut down on dispose. Upstream <c>cleanupTimeout</c>.</summary>
    public TimeSpan CleanupTimeout { get; init; } = E2EDefaults.CleanupTimeout;

    public required string TestTitle { get; init; }

    /// <summary>
    /// The full name of the test's class, which stands for upstream's test file: it joins the title in the result id and leads the
    /// name of the result's directory, so two tests with one title in two classes keep apart. The NUnit and xUnit adapters set it,
    /// and <c>e2e explore</c> sets <see cref="Explore.ExploreFile"/>. It also makes the session a test, as upstream's
    /// <c>environment.test</c> does: every prompt then opens with <c>Test: &lt;TestTitle&gt;</c>. Null keeps the title
    /// alone as the identity and adds no title line, as for an <c>e2e mcp</c> session, which runs no test.
    /// </summary>
    public string? TestFile { get; init; }

    /// <summary>The name of the test's method (.NET-only). With <see cref="TestFile"/> it is the test's id in the report (<see cref="Ids.TestId"/>), shared by every row of a parameterized test.</summary>
    internal string? TestMethod { get; init; }

    public TimeSpan TestTimeout { get; init; } = E2EDefaults.TestTimeout;

    /// <summary>.NET-only: the clock the test's deadline reads (see <see cref="OpenHostOptions.Clock"/>).</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

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
    /// Whether the session reports its start, steps, and end to the process's run events. Off for a session whose run a test supplies.
    /// </summary>
    internal bool EmitRunEvents { get; init; } = true;

    /// <summary>Live progress of the attempt's steps; null feeds none. Upstream's <c>onProgress</c> of the step recorder.</summary>
    internal Action<StepProgress>? OnProgress { get; init; }

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

    /// <summary>
    /// The directory a <c>--last-failed</c> rerun files its attempts in, inside each test's directory (<c>rerun-&lt;n&gt;</c>, upstream's <c>rerunDir</c>), so no
    /// attempt writes over the evidence of the report it reruns. Null for any other run.
    /// </summary>
    internal string? RerunDir { get; init; }

    /// <summary>The <c>launch()</c> host this session belongs to, handed to the engine; see <see cref="IEngineHostIdentity.HostId"/>.</summary>
    internal string? HostId { get; init; }

    /// <summary>
    /// The folders, under <see cref="ResultsDirectory"/>, the attempt's artifacts go in, given the attempt's id. Null names them after the test, as a
    /// run does. Upstream's <c>identity</c> of <c>openWorker</c>, whose <c>segments</c> a <c>launch()</c> host names after its token.
    /// </summary>
    internal Func<string, IReadOnlyList<string>>? ArtifactSegments { get; init; }

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
