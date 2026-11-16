// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

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

    /// <summary>Arguments every fresh launch of the app takes.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }

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

    /// <summary>
    /// Closes the host on SIGINT, SIGTERM, and SIGHUP, so the browsers and devices it launched do not outlive the program; the signal is raised again
    /// afterwards unless the program listens for it itself. Default <c>true</c>. A closing that fails is said on stderr. Upstream's <c>handleSignals</c>.
    /// </summary>
    public bool HandleSignals { get; init; } = true;

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
    private readonly Lazy<Task> _closing;

    internal Session(StandaloneAttempt attempt, StateIdentity identity, Action onClosed)
    {
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
        IReadOnlyList<Exception> errors;
        try
        {
            errors = await _attempt.CloseAsync().ConfigureAwait(false);
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
    internal Session(StandaloneAttempt attempt, StateIdentity identity, Action onClosed, Func<IEngineSession, TFixtures> fixtures)
        : base(attempt, identity, onClosed) =>
        Fixtures = fixtures(EngineSession);

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
    private readonly Func<StandaloneAttempt, StateIdentity, Action, Session> _newSession;
    private readonly Lock _gate = new();
    private readonly Lazy<Task> _closing;
    private Action _unregister = () => { };
    private Session? _open;

    internal Host(StandaloneHost host, IEngine engine, string target, string projectRoot, string output, bool ownsArtifacts, IReadOnlyDictionary<string, string> secrets, StateIdentity identity, TimeSpan? timeout, LaunchApp? app, bool headed, Func<StandaloneAttempt, StateIdentity, Action, Session>? newSession = null)
    {
        _newSession = newSession ?? ((attempt, sessionIdentity, onClosed) => new Session(attempt, sessionIdentity, onClosed));
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
                $"secret {System.Text.Json.JsonSerializer.Serialize(name)} was not passed to launch; {(known.Count == 0 ? "it was given no secrets" : "it has " + string.Join(", ", known))}");
        }

        return E2E.Secret.Create(name, value);
    }

    internal void CloseOnSignals() => _unregister = Signals.CloseOnSignals(CloseAsync);

    /// <summary>Opens a session. One at a time: the previous one must be closed.</summary>
    public Task<Session> NewSessionAsync(NewSessionOptions? options = null, CancellationToken cancellationToken = default) =>
        OpeningRedaction.RedactOpening(async () =>
        {
            if (_closing.IsValueCreated)
            {
                throw new EngineException("WORKER_PROTOCOL", "the host is closed");
            }

            var restored = options?.State is { } state ? SessionState.RestoredStateFor(state, _identity) : null;
            var attempt = await _host.OpenAttemptAsync(SessionOptions(restored), cancellationToken, hosted: true).ConfigureAwait(false);
            try
            {
                var session = _newSession(attempt, _identity, () => Release(attempt));
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

                return session;
            }
            catch
            {
                await attempt.CloseAsync().ConfigureAwait(false);
                throw;
            }
        });

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
        _unregister();
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

    private E2ESessionOptions SessionOptions(SavedSession? restored) => new()
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
        Closed = () => new EngineException(EngineErrorCodes.Cancelled, "the session is closed"),
        // No report stands between an operation's error and the program, so the error itself is classified and redacted.
        RedactThrown = true,
        RetainSteps = Launch.RetainedSteps,
        State = restored,
    };

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
    public async Task<Session<TFixtures>> NewSessionAsync(NewSessionOptions? options = null, CancellationToken cancellationToken = default) =>
        (Session<TFixtures>)await _host.NewSessionAsync(options, cancellationToken).ConfigureAwait(false);

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
    public static Task<Host> LaunchAsync(IEngine engine, LaunchOptions? options = null, CancellationToken cancellationToken = default) =>
        LaunchCoreAsync(engine, options, null, cancellationToken);

    /// <summary>
    /// Launches an engine that contributes fixtures (<see cref="IEngine{TFixtures}"/>), so each session of the host holds them as <see cref="Session{TFixtures}.Fixtures"/>.
    /// Upstream's <c>launch&lt;Fixtures&gt;</c>.
    /// </summary>
    public static async Task<Host<TFixtures>> LaunchAsync<TFixtures>(IEngine<TFixtures> engine, LaunchOptions? options = null, CancellationToken cancellationToken = default)
        where TFixtures : class
    {
        var host = await LaunchCoreAsync(
            engine,
            options,
            (attempt, identity, onClosed) => new Session<TFixtures>(attempt, identity, onClosed, engine.CreateFixtures),
            cancellationToken).ConfigureAwait(false);
        return new Host<TFixtures>(host);
    }

    private static async Task<Host> LaunchCoreAsync(IEngine engine, LaunchOptions? options, Func<StandaloneAttempt, StateIdentity, Action, Session>? newSession, CancellationToken cancellationToken)
    {
        if (engine is null)
        {
            throw new TestException("INVALID_ARGUMENT", "launch takes an engine as its first argument and got null: pass an engine such as new WebEngine()");
        }

        options ??= new LaunchOptions();
        if (options.Timeout is { } timeout && (timeout <= TimeSpan.Zero || timeout == System.Threading.Timeout.InfiniteTimeSpan))
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
        try
        {
            return host = await OpeningRedaction.RedactOpening(async () =>
            {
                Claim(engine);
                try
                {
                    var target = engine.Platform;
                    var app = options.App;
                    TargetApps.CheckLinkBase(app?.LinkBase, engine, "target \"" + target + "\" app");
                    var resolved = TargetApps.ResolveTargetApp(
                        target,
                        new AppConfig { Url = app?.Url, BundleId = app?.BundleId, AppPath = app?.AppPath, LinkBase = app?.LinkBase, Identity = app?.Identity, LaunchArguments = app?.LaunchArguments, Permissions = app?.Permissions },
                        projectRoot);
                    var identity = new StateIdentity(
                        engine.Name,
                        target,
                        Sessions.TargetIdentity(target, target, engine, resolved).AppIdentity,
                        resolved.Identity);
                    OpenSession.RegisterStaticSecrets(secrets.Select(pair => E2E.Secret.Create(pair.Key, pair.Value)));
                    var worker = await Standalone.OpenWorkerAsync(new StandaloneHostOptions
                    {
                        Engine = engine,
                        TargetName = target,
                        ProjectRoot = projectRoot,
                        Env = options.Env,
                        CancellationToken = cancellationToken,
                        Headed = options.Headed,
                        Notice = options.OnNotice is { } notice ? (_, message) => OpenSession.Notify(notice, message) : null,
                    }).ConfigureAwait(false);
                    var opened = new Host(worker, engine, target, projectRoot, output, ownsArtifacts, secrets, identity, options.Timeout, app, options.Headed, newSession);
                    if (options.HandleSignals)
                    {
                        try
                        {
                            opened.CloseOnSignals();
                        }
                        catch
                        {
                            await opened.CloseAsync().ConfigureAwait(false);
                            throw;
                        }
                    }

                    return opened;
                }
                catch
                {
                    Unclaim(engine);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        catch when (host is null)
        {
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

        throw new AggregateException($"{what} failed: {string.Join("; ", errors.Select(error => error.Message))}", errors);
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
                    $"launch secret \"{name}\" must be a string of at least {Secret.MinLength.ToString(CultureInfo.InvariantCulture)} characters, so that redacting it does not take ordinary text with it");
            }

            checkedSecrets[name] = value;
        }

        return checkedSecrets;
    }
}
