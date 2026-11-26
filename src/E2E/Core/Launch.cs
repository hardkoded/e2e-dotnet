// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>The app a host drives: what <c>app.open()</c> opens on a browser, or launches on a device. Upstream's <c>LaunchApp</c> (<c>core/launch.ts</c>), a pick of the target's app.</summary>
public sealed record LaunchApp
{
    /// <summary>The app's URL on a browser.</summary>
    public string? Url { get; init; }

    /// <summary>The bundle id or package of an installed app a device launches.</summary>
    public string? BundleId { get; init; }

    /// <summary>The build a device installs.</summary>
    public string? AppPath { get; init; }

    /// <summary>
    /// The base <c>App.OpenAsync(path)</c> joins a path to on a device; see <see cref="AppConfig.LinkBase"/>. The engine must open links, as the device engine does.
    /// </summary>
    public string? LinkBase { get; init; }

    /// <summary>The stable identity a saved state is bound to; see <see cref="AppConfig.Identity"/>.</summary>
    public string? Identity { get; init; }

    /// <summary>Arguments every fresh launch of the app takes, as a list.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }

    /// <summary>Arguments every fresh launch of the app takes, as an object of argument name to value; see <see cref="AppConfig.LaunchArgumentValues"/>. At most one of this and <see cref="LaunchArguments"/> is set.</summary>
    public IReadOnlyDictionary<string, LaunchArgumentValue?>? LaunchArgumentValues { get; init; }

    /// <summary>Permissions the app holds, by name.</summary>
    public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }
}

/// <summary>What <see cref="Launch.LaunchAsync"/> is given. Upstream's <c>LaunchOptions</c> (<c>core/launch.ts</c>).</summary>
public sealed record LaunchOptions
{
    /// <summary>The app: <see cref="LaunchApp.Url"/> on a browser; <see cref="LaunchApp.BundleId"/> or <see cref="LaunchApp.AppPath"/> on a device.</summary>
    public LaunchApp? App { get; init; }

    /// <summary>
    /// Secret values by name, at least 6 characters each. Fill one through <see cref="Host.Secret"/>; an agent receives it by name only. The values
    /// are redacted from what <c>Screen.Tree()</c> and the agent read, and from the errors this host throws.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Secrets { get; init; }

    /// <summary>Shows the browser window; default false. Devices ignore it. Upstream's <c>headed</c>.</summary>
    public bool Headed { get; init; }

    /// <summary>
    /// Default timeout of one operation: an action, a wait, a read, and <c>App.OpenAsync</c> or <c>App.RestartAsync</c>. Without it an action, a wait,
    /// or a read gets 30 seconds, and an app open or restart 120 seconds. A session changes it with <see cref="Session.SetDefaultTimeout"/>. An agent
    /// step has its own, <c>ActAsync</c>'s timeout. There is no session deadline.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Where screenshots, downloads, and recordings are written, from <see cref="Cwd"/>. Default: a fresh directory under the system temp directory,
    /// deleted when the host closes.
    /// </summary>
    public string? ArtifactsDir { get; init; }

    /// <summary>The directory upload paths and relative files resolve against, <see cref="ArtifactsDir"/> included; default the current directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>The environment the engine reads; default the process environment.</summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>Progress outside operations: provisioning, downloads, device leases. Must not throw.</summary>
    public Action<string>? OnNotice { get; init; }
}

/// <summary>What <see cref="Host.NewSessionAsync"/> is given. Upstream's <c>NewSessionOptions</c> (<c>core/launch.ts</c>).</summary>
public sealed record NewSessionOptions
{
    /// <summary>A state <see cref="Session.SaveStateAsync"/> captured, or <see cref="SessionState.ImportAsync"/> read, on a host of the same engine, platform, and app.</summary>
    public SessionState? State { get; init; }
}

/// <summary>
/// One isolated session: a browser context, or the app on a device. Holds <see cref="Screen"/>, <see cref="App"/>, <see cref="Platform"/>, and
/// <see cref="Browser"/>. Upstream's <c>Session</c> and <c>SessionControls</c> (<c>core/launch.ts</c>), over the port's <see cref="E2ESession"/>.
/// </summary>
public class Session : IAsyncDisposable
{
    private readonly E2ESession _session;
    private readonly StandaloneAttempt _attempt;
    private readonly StateIdentity _identity;
    private readonly Action _onClosed;
    private readonly ISessionGuest _guest;
    private readonly Lazy<Task> _closing;

    internal Session(
        StandaloneAttempt attempt,
        StateIdentity identity,
        SessionInternals internals,
        Action onClosed,
        ISessionGuest guest
    )
    {
        _guest = guest;
        Internals = internals;
        _attempt = attempt;
        _session = attempt.Session;
        _identity = identity;
        _onClosed = onClosed;
        _closing = new Lazy<Task>(CloseOnceAsync);
    }

    /// <summary>Semantic queries and the actions on their matches.</summary>
    public Screen Screen => _session.Screen;

    /// <summary>App lifecycle: open, restart, clear state, back, screenshot, recording.</summary>
    public App App => _session.App;

    /// <summary>The browser fixture. Its members fail with <c>UNSUPPORTED_CAPABILITY</c> on an engine without a browser.</summary>
    public Browser Browser => _session.Browser;

    /// <summary>The engine's platform label: <c>web</c>, <c>ios</c>, <c>android</c>, or an engine's own string.</summary>
    public string Platform => _session.Context.Platform;

    /// <summary>The engine's own session behind this one, which <see cref="IEngine{TFixtures}.CreateFixtures"/> builds the engine's fixtures from.</summary>
    internal IEngineSession EngineSession => Engine.EngineSession.Unwrap(_session.Engine);

    internal E2ESession E2E => _session;

    /// <summary>What <see cref="CoreAgents.CreateAgent"/> reads off the session; see <see cref="SessionInternals"/>.</summary>
    internal SessionInternals Internals { get; }

    /// <summary>Captures the session's state (cookies, storage, app data) in memory. Persist it only with <see cref="SessionState.ExportAsync"/>. Upstream's <c>saveState</c>.</summary>
    public Task<SessionState> SaveStateAsync(CancellationToken cancellationToken = default) =>
        OpeningRedaction.RedactOpening(async () =>
        {
            var saved = await _session.CaptureStateAsync(cancellationToken).ConfigureAwait(false);
            return SessionState.Captured(_identity, saved.State, saved.Secrets);
        });

    /// <summary>
    /// The default timeout of operations started from now on: actions, waits, reads, and app opens and restarts. A running operation keeps the
    /// deadline it started with. Throws <c>INVALID_ARGUMENT</c> unless <paramref name="timeout"/> is positive and finite. Upstream's <c>setDefaultTimeout</c>.
    /// </summary>
    public void SetDefaultTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new TestException("INVALID_ARGUMENT", "setDefaultTimeout takes a positive, finite time span");
        }

        _session.SetDefaultTimeouts(timeout);
    }

    /// <summary>Ends the session. Idempotent. Throws an <see cref="AggregateException"/> of the failures closing found, such as a cleanup that did not finish. Upstream's <c>close</c>.</summary>
    public Task CloseAsync() => _closing.Value;

    /// <inheritdoc cref="CloseAsync"/>
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    private async Task CloseOnceAsync()
    {
        // The closers run in order, before the attempt ends: an agent settles its replay cache here.
        var errors = new List<Exception>();
        foreach (var closer in Internals.Closers)
        {
            try
            {
                await closer().ConfigureAwait(false);
            }
            catch (Exception cause)
            {
                // Whatever a closer throws, a cancellation included, is reported after the attempt is closed.
                errors.Add(cause);
            }
        }

        try
        {
            errors.AddRange(await _attempt.CloseAsync().ConfigureAwait(false));
            try
            {
                await _guest.VideosAsync(_attempt.Videos()).ConfigureAwait(false);
            }
            catch (Exception cause)
            {
                errors.Add(cause);
            }
        }
        catch (Exception cause) when (errors.Count > 0)
        {
            // What the closers found is reported with the failure of the attempt's own close.
            errors.Add(cause);
        }
        finally
        {
            _onClosed();
        }

        Launch.ThrowClosingErrors(errors, "closing the session");
    }
}

/// <summary>
/// A session on an engine that contributes fixtures of its own: <see cref="Fixtures"/> holds them, such as <c>Device</c> on a mobile engine. Upstream's
/// <c>Session&lt;Fixtures&gt;</c> (<c>core/launch.ts</c>), the session type intersected with the engine's fixtures; C# has no intersection, so the
/// engine's fixtures are one member.
/// </summary>
/// <typeparam name="TFixtures">What the engine contributes, as <see cref="IEngine{TFixtures}"/> declares.</typeparam>
public sealed class Session<TFixtures> : Session
    where TFixtures : class
{
    internal Session(
        StandaloneAttempt attempt,
        StateIdentity identity,
        SessionInternals internals,
        Action onClosed,
        ISessionGuest guest,
        Func<IEngineSession, Session, TFixtures> fixtures
    )
        : base(attempt, identity, internals, onClosed, guest) => Fixtures = fixtures(EngineSession, this);

    /// <summary>What the engine contributes beside <see cref="Session.Screen"/>, <see cref="Session.App"/>, and <see cref="Session.Platform"/>.</summary>
    public TFixtures Fixtures { get; }
}

/// <summary>One engine instance, initialized: the owner of the sessions opened on it, one at a time. Upstream's <c>Host</c> (<c>core/launch.ts</c>).</summary>
public sealed class Host : IAsyncDisposable
{
    private readonly StandaloneHost _host;
    private readonly IEngine _engine;
    private readonly string _target;
    private readonly string _projectRoot;
    private readonly string _output;
    private readonly bool _ownsArtifacts;
    private readonly IReadOnlyDictionary<string, string> _secrets;
    private readonly StateIdentity _identity;
    private readonly TimeSpan? _timeout;
    private readonly LaunchApp? _app;
    private readonly bool _headed;
    private readonly string _hostId;
    private readonly Func<
        StandaloneAttempt,
        StateIdentity,
        SessionInternals,
        Action,
        ISessionGuest,
        Session
    > _newSession;
    private readonly Action _withdrawSecrets;

    /// <summary>Whether the test that launched the host has taken charge of its end: it then withdraws the host's secrets at its own end, not at the host's.</summary>
    private bool _owned;
    private readonly Lock _gate = new();
    private readonly Lazy<Task> _closing;
    private Session? _open;

    internal Host(
        StandaloneHost host,
        IEngine engine,
        string target,
        string projectRoot,
        string output,
        bool ownsArtifacts,
        IReadOnlyDictionary<string, string> secrets,
        StateIdentity identity,
        TimeSpan? timeout,
        LaunchApp? app,
        bool headed,
        Action withdrawSecrets,
        string hostId,
        Func<StandaloneAttempt, StateIdentity, SessionInternals, Action, ISessionGuest, Session>? newSession = null
    )
    {
        _hostId = hostId;
        _withdrawSecrets = withdrawSecrets;
        _newSession = newSession
            ?? (
                (attempt, sessionIdentity, internals, onClosed, guest) =>
                    new Session(attempt, sessionIdentity, internals, onClosed, guest)
            );
        _headed = headed;
        _app = app;
        _host = host;
        _engine = engine;
        _target = target;
        _projectRoot = projectRoot;
        _output = output;
        _ownsArtifacts = ownsArtifacts;
        _secrets = secrets;
        _identity = identity;
        _timeout = timeout;
        _closing = new Lazy<Task>(CloseOnceAsync);
    }

    /// <summary>The engine's platform label: <c>web</c>, <c>ios</c>, <c>android</c>, or an engine's own string.</summary>
    public string Platform => _identity.Platform;

    /// <summary>The handle of a secret passed to <see cref="Launch.LaunchAsync"/>, for <c>FillAsync</c> or an agent's params. An unknown name throws <c>SECRET_UNAVAILABLE</c>.</summary>
    public Secret Secret(string name)
    {
        if (name is null || !_secrets.TryGetValue(name, out var value))
        {
            var known = _secrets.Keys.ToList();
            throw new ConfigurationException(
                "SECRET_UNAVAILABLE",
                $"secret {System.Text.Json.JsonSerializer.Serialize(name)} was not passed to launch; {(known.Count == 0 ? "it was given no secrets" : "it has " + string.Join(", ", known))}"
            );
        }

        return E2E.Secret.Create(name, value);
    }

    /// <summary>Opens a session. One at a time: the previous one must be closed.</summary>
    public Task<Session> NewSessionAsync(
        NewSessionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        // Decided now, when the call is made: a session a running test opens is that test's, however long the opening takes.
        var guest = SessionGuests.SessionGuest();
        return OpenGuestSessionAsync(options, guest, cancellationToken);
    }

    /// <summary>Hands a test the host this seat was reserved for; false when the test gave the seat up, and the host is the caller's to close.</summary>
    internal bool TakeBy(IGuestSeat<IGuestHost, bool> seat) => _owned = seat.Take(new GuestHost(this));

    private async Task<Session> OpenGuestSessionAsync(
        NewSessionOptions? options,
        ISessionGuest guest,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await OpeningRedaction
                .RedactOpening(() => OpenSessionAsync(options, guest, cancellationToken))
                .ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            guest.Failed(cause);
            throw;
        }
    }

    // Opens one session on the host, for the guest that was decided when the call was made.
    private async Task<Session> OpenSessionAsync(
        NewSessionOptions? options,
        ISessionGuest guest,
        CancellationToken cancellationToken
    )
    {
        if (_closing.IsValueCreated)
        {
            throw new EngineException("WORKER_PROTOCOL", "the host is closed");
        }

        var restored = options?.State is { } state ? SessionState.RestoredStateFor(state, _identity) : null;
        var failures = new FailureWatch();
        var attempt = await _host
            .OpenAttemptAsync(
                SessionOptions(restored, failures, guest),
                Time.Link(cancellationToken, guest.Signal),
                hosted: true
            )
            .ConfigureAwait(false);
        try
        {
            failures.Watch(attempt.Session.Recorder);
            var session = _newSession(
                attempt,
                _identity,
                new SessionInternals(attempt, _projectRoot, failures),
                () => Release(attempt),
                guest
            );
            if (_timeout is { } timeout)
            {
                session.SetDefaultTimeout(timeout);
            }

            // What a program configured is known to the session from the start: the agent fills it by name, and every output redacts it.
            attempt.Session.Remember(_secrets.Select(pair => E2E.Secret.Create(pair.Key, pair.Value)));
            lock (_gate)
            {
                if (_closing.IsValueCreated)
                {
                    throw new EngineException("WORKER_PROTOCOL", "the host is closed");
                }

                _open = session;
            }

            if (!guest.Attach(new GuestSession(_projectRoot, session, attempt)))
            {
                await session.CloseAsync().ConfigureAwait(false);
                throw new EngineException(
                    EngineErrorCodes.Cancelled,
                    "the test that opened the session ended before it was open"
                )
                {
                    Mapped = true,
                };
            }

            return session;
        }
        catch
        {
            await attempt.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Closes the open session, then the engine. Idempotent. Throws an <see cref="AggregateException"/> of the failures closing found. Upstream's <c>close</c>.</summary>
    public Task CloseAsync() => _closing.Value;

    /// <inheritdoc cref="CloseAsync"/>
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    private void Release(StandaloneAttempt attempt)
    {
        lock (_gate)
        {
            if (_open is { } open && ReferenceEquals(open.E2E, attempt.Session))
            {
                _open = null;
            }
        }
    }

    private async Task CloseOnceAsync()
    {
        var errors = new List<Exception>();
        Session? open;
        lock (_gate)
        {
            open = _open;
        }

        try
        {
            if (open is not null)
            {
                await open.CloseAsync().ConfigureAwait(false);
            }
        }
        catch (AggregateException cause)
        {
            errors.AddRange(cause.InnerExceptions);
        }
        catch (Exception cause)
        {
            errors.Add(cause);
        }

        try
        {
            errors.AddRange(await _host.CloseAsync().ConfigureAwait(false));
        }
        finally
        {
            Launch.Unclaim(_engine);
            // The host's values are shared with the sessions beside it for as long as it is open. A test that launched the host keeps them
            // shared until the test ends: it may close the host early and go on reading screens that show them.
            if (!_owned)
            {
                _withdrawSecrets();
            }
        }

        if (_ownsArtifacts)
        {
            try
            {
                Directory.Delete(_output, recursive: true);
            }
            catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
            {
                // A directory that is gone, or that a process still holds, is no reason to fail the close.
            }
        }

        Launch.ThrowClosingErrors(errors, "closing the host");
    }

    private E2ESessionOptions SessionOptions(SavedSession? restored, FailureWatch failures, ISessionGuest guest) =>
        new()
        {
            Engine = _engine,
            TargetName = _target,
            ProjectRoot = _projectRoot,
            ResultsDirectory = _output,
            AppIdentity = _identity.AppIdentity,
            BaseUrl = _app?.Url,
            BundleId = _app?.BundleId,
            AppPath = _app?.AppPath,
            LinkBase = _app?.LinkBase,
            LaunchArguments = _app?.LaunchArguments,
            LaunchArgumentValues = LaunchArgumentRules.WithoutNulls(_app?.LaunchArgumentValues),
            Permissions = _app?.Permissions,
            TestTitle = "core",
            Trace = new ResolvedRecording(RecordingMode.Off, RecordingSource.Default),
            Video = new ResolvedRecording(RecordingMode.Off, RecordingSource.Default),
            CacheEnabled = false,
            CacheMode = CacheMode.Off,
            EmitRunEvents = false,
            // No session deadline: each operation keeps its own timeout.
            TestTimeout = System.Threading.Timeout.InfiniteTimeSpan,
            ActionTimeout = _timeout ?? E2EDefaults.ActionTimeout,
            AssertionTimeout = _timeout ?? E2EDefaults.AssertionTimeout,
            Headed = _headed,
            HostId = _hostId,
            // Named after the token, never the target, whose name is the platform an engine reports, and a platform can be a secret's value.
            ArtifactSegments = attemptId =>
                ["core", .. ArtifactPaths.AttemptSegments(null, $"host-{_hostId}-{attemptId}", 0)],
            CancelsOnClose = new CancelsOnClose(
                () => new EngineException(EngineErrorCodes.Cancelled, "the session is closed"),
                call => new EngineException(
                    EngineErrorCodes.Cancelled,
                    "the session closed while " + call + " was running"
                )
            ),
            // No report stands between an operation's error and the program, so the error itself is classified and redacted.
            RedactThrown = true,
            RetainSteps = Launch.RetainedSteps,
            OnProgress = failures.OnProgress,
            Mirror = guest.Mirror,
            // A test that keeps videos keeps one of each session it opens too, when the engine can record it; a program's own session records none.
            VideoRecording = guest.RecordsVideo ? new VideoRecording(Keep.Always, VideoPolicy.BestEffort) : null,
            State = restored,
        };

    // What the attempt a test runs in takes charge of: closing the host. Upstream's GuestHost.
    private sealed class GuestHost(Host host) : IGuestHost
    {
        public Task CloseAsync() => host.CloseAsync();

        public void Release() => host._withdrawSecrets();
    }

    // What the attempt a test runs in takes charge of: closing the session and cutting what it left running. Upstream's GuestSession.
    private sealed class GuestSession(string root, Session session, StandaloneAttempt attempt) : IGuestSession
    {
        public string Root { get; } = root;

        public bool Busy() => attempt.Session.Recorder.Busy;

        public Exception? AbandonRunning() => attempt.Session.Recorder.AbandonRunning();

        public Task CloseAsync() => session.CloseAsync();
    }
}

/// <summary>
/// A host on an engine that contributes fixtures: its sessions are <see cref="Session{TFixtures}"/>. Upstream's <c>Host&lt;Fixtures&gt;</c> (<c>core/launch.ts</c>).
/// </summary>
/// <typeparam name="TFixtures">What the engine contributes, as <see cref="IEngine{TFixtures}"/> declares.</typeparam>
public sealed class Host<TFixtures> : IAsyncDisposable
    where TFixtures : class
{
    private readonly Host _host;

    internal Host(Host host) => _host = host;

    /// <inheritdoc cref="Host.Platform"/>
    public string Platform => _host.Platform;

    /// <inheritdoc cref="Host.Secret"/>
    public Secret Secret(string name) => _host.Secret(name);

    /// <inheritdoc cref="Host.NewSessionAsync"/>
    public async Task<Session<TFixtures>> NewSessionAsync(
        NewSessionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => (Session<TFixtures>)await _host.NewSessionAsync(options, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc cref="Host.CloseAsync"/>
    public Task CloseAsync() => _host.CloseAsync();

    /// <inheritdoc cref="CloseAsync"/>
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}

/// <summary>
/// <c>launch</c> from <c>@e2e-dev/core</c>: drive a browser or a device from plain code, with no test runner, config file, or report. A host is
/// one worker on one engine instance: it prepares the engine, initializes it, and opens sessions on it one at a time. Parallel work is more hosts.
/// Upstream's <c>core/launch.ts</c>.
/// </summary>
public static class Launch
{
    /// <summary>The finished steps a session keeps, what its agent reads as the steps before its own; a bot's session runs for days. Upstream's <c>RETAINED_STEPS</c>.</summary>
    internal const int RetainedSteps = 500;

    private static readonly Lock Gate = new();

    private static readonly HashSet<IEngine> Owners = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Launches <paramref name="engine"/>: prepares it (a first-run browser download, a device lease), initializes one worker, and returns the host that
    /// opens sessions on it. Each launch needs its own engine instance.
    /// </summary>
    public static Task<Host> LaunchAsync(
        IEngine engine,
        LaunchOptions? options = null,
        CancellationToken cancellationToken = default
    ) => LaunchCoreAsync(engine, options, null, cancellationToken);

    /// <summary>
    /// Launches an engine that contributes fixtures (<see cref="IEngine{TFixtures}"/>), so each session of the host holds them as <see cref="Session{TFixtures}.Fixtures"/>.
    /// Upstream's <c>launch&lt;Fixtures&gt;</c>.
    /// </summary>
    public static async Task<Host<TFixtures>> LaunchAsync<TFixtures>(
        IEngine<TFixtures> engine,
        LaunchOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where TFixtures : class
    {
        var host = await LaunchCoreAsync(
                engine,
                options,
                (attempt, identity, internals, onClosed, guest) =>
                    new Session<TFixtures>(attempt, identity, internals, onClosed, guest, engine.CreateFixtures),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new Host<TFixtures>(host);
    }

    private static async Task<Host> LaunchCoreAsync(
        IEngine engine,
        LaunchOptions? options,
        Func<StandaloneAttempt, StateIdentity, SessionInternals, Action, ISessionGuest, Session>? newSession,
        CancellationToken cancellationToken
    )
    {
        if (engine is null)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "launch takes an engine as its first argument and got null: pass an engine such as new WebEngine()"
            );
        }

        // What the test launching the host offers, when one is, and the host's seat in it: reserved before anything is awaited, so the host is that test's and no later one's.
        var owner = SessionGuests.CurrentGuests();
        var seat = owner?.Host();

        options ??= new LaunchOptions();
        if (
            options.Timeout is { } timeout
            && (timeout <= TimeSpan.Zero || timeout == System.Threading.Timeout.InfiniteTimeSpan)
        )
        {
            throw new TestException("INVALID_ARGUMENT", "launch timeout must be a positive, finite time span");
        }

        var secrets = CheckSecrets(options.Secrets);
        var ownsArtifacts = options.ArtifactsDir is null;
        var projectRoot = Path.GetFullPath(options.Cwd ?? Directory.GetCurrentDirectory());
        var output = options.ArtifactsDir is null
            ? Directory.CreateTempSubdirectory("e2e-core-").FullName
            : Path.GetFullPath(options.ArtifactsDir, projectRoot);
        Host? host = null;
        Action withdrawSecrets = static () => { };
        try
        {
            host = await OpeningRedaction
                .RedactOpening(async () =>
                {
                    Claim(engine);
                    try
                    {
                        var target = engine.Platform;
                        var app = options.App;
                        TargetApps.CheckLinkBase(app?.LinkBase, engine, "target \"" + target + "\" app");
                        TargetApps.CheckLaunchArgumentsFor(
                            new AppConfig
                            {
                                LaunchArguments = app?.LaunchArguments,
                                LaunchArgumentValues = app?.LaunchArgumentValues,
                            },
                            engine,
                            "target \"" + target + "\" app"
                        );
                        var resolved = TargetApps.ResolveTargetApp(
                            target,
                            new AppConfig
                            {
                                Url = app?.Url,
                                BundleId = app?.BundleId,
                                AppPath = app?.AppPath,
                                LinkBase = app?.LinkBase,
                                Identity = app?.Identity,
                                LaunchArguments = app?.LaunchArguments,
                                LaunchArgumentValues = app?.LaunchArgumentValues,
                                Permissions = app?.Permissions,
                            },
                            projectRoot
                        );
                        var identity = new StateIdentity(
                            engine.Name,
                            target,
                            Sessions.TargetIdentity(target, target, engine, resolved).AppIdentity,
                            resolved.Identity
                        );
                        withdrawSecrets = Secrecy.ShareStaticSecrets(
                            secrets.Select(pair => E2E.Secret.Create(pair.Key, pair.Value))
                        );
                        // Hosts name their targets alike, so the engine gets a token per host to keep what it holds apart: a device session.
                        // Random, so hosts in other processes or copies of this library differ too.
                        var hostId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
                        var worker = await Standalone
                            .OpenWorkerAsync(
                                new StandaloneHostOptions
                                {
                                    Engine = engine,
                                    TargetName = target,
                                    HostId = hostId,
                                    ProjectRoot = projectRoot,
                                    Env = options.Env,
                                    CancellationToken = Time.Link(cancellationToken, seat?.Signal ?? default),
                                    Headed = options.Headed,
                                    Notice = options.OnNotice is { } notice
                                        ? (_, message) => OpenSession.Notify(notice, message)
                                        : null,
                                }
                            )
                            .ConfigureAwait(false);
                        return new Host(
                            worker,
                            engine,
                            target,
                            projectRoot,
                            output,
                            ownsArtifacts,
                            secrets,
                            identity,
                            options.Timeout,
                            app,
                            options.Headed,
                            withdrawSecrets,
                            hostId,
                            newSession
                        );
                    }
                    catch
                    {
                        Unclaim(engine);
                        throw;
                    }
                })
                .ConfigureAwait(false);
            // A host a test launched is the test's to end: the attempt closes it if the test does not.
            if (seat is not null && !host.TakeBy(seat))
            {
                await host.CloseAsync().ConfigureAwait(false);
                var gaveUp = new EngineException(
                    EngineErrorCodes.Cancelled,
                    "the test that launched the host ended before it was open"
                )
                {
                    Mapped = true,
                };
                owner?.GaveUp(gaveUp);
                throw gaveUp;
            }

            return host;
        }
        catch (Exception opening) when (host is null)
        {
            withdrawSecrets();
            if (seat?.Signal.IsCancellationRequested == true)
            {
                owner?.GaveUp(opening);
            }

            if (ownsArtifacts)
            {
                try
                {
                    Directory.Delete(output, recursive: true);
                }
                catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
                {
                    // Nothing was written there; a directory that stays is the system's to clean.
                }
            }

            throw;
        }
    }

    internal static void Unclaim(IEngine engine)
    {
        lock (Gate)
        {
            Owners.Remove(engine);
        }
    }

    /// <summary>Throws what closing found, as one <see cref="AggregateException"/>; nothing when it found nothing.</summary>
    internal static void ThrowClosingErrors(IReadOnlyList<Exception> errors, string what)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new AggregateException(
            $"{what} failed: {string.Join("; ", errors.Select(error => error.Message))}",
            errors
        );
    }

    // No host may drive an engine instance another host owns.
    private static void Claim(IEngine engine)
    {
        lock (Gate)
        {
            if (!Owners.Add(engine))
            {
                throw new EngineException("WORKER_PROTOCOL", "the engine instance already belongs to an e2e host");
            }
        }
    }

    private static Dictionary<string, string> CheckSecrets(IReadOnlyDictionary<string, string>? secrets)
    {
        var checkedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in secrets ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigurationException("INVALID_CONFIG", "launch secrets are named by a non-empty string");
            }

            if (!ConfigSecrets.IsSecretValue(value))
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"launch secret \"{name}\" must be a string of at least {Secret.MinLength.ToString(CultureInfo.InvariantCulture)} characters, so that redacting it does not take ordinary text with it"
                );
            }

            checkedSecrets[name] = value;
        }

        return checkedSecrets;
    }
}
