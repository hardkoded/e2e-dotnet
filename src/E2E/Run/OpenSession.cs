// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>What <see cref="E2EHost.OpenAsync"/> is given. Upstream's <c>OpenHostOptions</c> (<c>run/open-session.ts</c>).</summary>
public sealed record OpenHostOptions
{
    /// <summary>Where the config file is searched for upward; default the current directory. Upstream's <c>cwd</c>.</summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// The config as a value, which the host uses instead of a file; its <see cref="E2EConfig.ProjectRoot"/> is the project root. Upstream's
    /// <c>config</c> when it is an object. A value cannot hold a live engine here (see <see cref="EngineFactory"/>).
    /// </summary>
    public E2EConfig? Config { get; init; }

    /// <summary>
    /// The config file, as a path relative to <see cref="Cwd"/>; default the nearest <c>e2e.config.json</c>. It is loaded fresh per host. Upstream's
    /// <c>config</c> when it is a string, kept apart from <see cref="Config"/> because C# has no union of the two.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>The target to open. It may be omitted only when the config declares one target. Upstream's <c>target</c>.</summary>
    public string? Target { get; init; }

    /// <summary>The variables the config and the engine's prepare and worker read; null is the process environment. Upstream's <c>env</c>.</summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>Whether a web target opens a visible window. Upstream's <c>headed</c>.</summary>
    public bool Headed { get; init; }

    /// <summary>Progress outside steps: provisioning, leases, and app processes, as the target's name and a line. Must not throw. Upstream's <c>onNotice</c>.</summary>
    public Action<string, string>? OnNotice { get; init; }

    /// <summary>
    /// .NET-only: builds the engine of the selected target. Upstream's config holds a live engine handle; <c>e2e.config.json</c> names a platform
    /// instead, so the host would build a <see cref="WebEngine"/> for a <c>web</c> target and fail for the others. A device or Windows target, or a
    /// test engine, comes from here, and a factory builds a fresh engine for each host.
    /// </summary>
    public Func<TargetConfig, IEngine>? EngineFactory { get; init; }
}

/// <summary>What <see cref="E2EHost.OpenSessionAsync"/> is given. Upstream's <c>OpenSessionOptions</c> (<c>run/open-session.ts</c>).</summary>
public sealed record OpenSessionOptions
{
    /// <summary>The test's title, treated as untrusted text and never a raw path component. Upstream's <c>name</c>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The deadline of the session's operations, counted from when it opens; default the config's <c>timeout</c>. Positive and finite.
    /// Expiry cuts running operations and fails the session as timed out; it does not bound opening (use the cancellation token) and does not
    /// close the session. Upstream's <c>timeout</c>.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The configured agent calls use when they name none. Upstream's <c>agent</c>.</summary>
    public string? Agent { get; init; }

    /// <summary>The step progress feed reporters receive. Must not block or throw. Upstream's <c>onStep</c>.</summary>
    public Action<Reports.StepProgress>? OnStep { get; init; }
}

/// <summary>
/// The worker-scoped owner of preparation, app processes, and engine initialization, that opens one isolated session at a time. Upstream's
/// <c>E2EHost</c> (<c>run/open-session.ts</c>). Open one with <see cref="OpenAsync"/>.
/// </summary>
public sealed class E2EHost : IAsyncDisposable
{
    // The longest deadline a CancellationTokenSource takes.
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly StandaloneHost _host;
    private readonly E2EConfig _config;
    private readonly TargetConfig _declared;
    private readonly TargetConfig _target;
    private readonly IEngine _engine;
    private readonly IAppProcesses _apps;
    private readonly Action _release;
    private readonly Lazy<Task<IReadOnlyList<Exception>>> _closing;

    private E2EHost(StandaloneHost host, E2EConfig config, TargetConfig declared, TargetConfig target, IEngine engine, IAppProcesses apps, Action release)
    {
        _host = host;
        _config = config;
        _declared = declared;
        _target = target;
        _engine = engine;
        _apps = apps;
        _release = release;
        _closing = new Lazy<Task<IReadOnlyList<Exception>>>(CloseOnceAsync);
    }

    /// <summary>The id of the run the host belongs to; the same as its sessions'. Upstream's <c>runId</c>.</summary>
    public string RunId => _host.RunId;

    /// <summary>The name of the target the host opened. Upstream's <c>target</c>.</summary>
    public string Target => _target.Name;

    /// <summary>
    /// Opens the worker-scoped owner of preparation, app processes, and engine initialization. A cancelled <paramref name="cancellationToken"/> cancels
    /// provisioning, initialization, and every session's operations; close still releases resources. Upstream's <c>openHost</c>, which is a
    /// free function: C# has none, so the type it returns owns it.
    /// </summary>
    public static Task<E2EHost> OpenAsync(OpenHostOptions? options = null, CancellationToken cancellationToken = default) =>
        OpenSession.RedactOpening(() => OpenWorkerAsync(options ?? new OpenHostOptions(), cancellationToken));

    /// <summary>Loads the selected target and opens its worker behind the redacted public boundary. Upstream's <c>openHostWorker</c>.</summary>
    private static async Task<E2EHost> OpenWorkerAsync(OpenHostOptions options, CancellationToken cancellationToken)
    {
        var release = PublicOwnership.ReserveHostedRun();
        IAppProcesses? apps = null;
        try
        {
            var cwd = Path.GetFullPath(options.Cwd ?? Directory.GetCurrentDirectory());
            var config = OpenSession.LoadHostConfig(cwd, options.Config, options.ConfigPath, options.Env);
            OpenSession.RegisterStaticSecrets(config.Secrets.Names.Select(config.Secrets.Get));
            var declared = SelectTarget.Select(config.Targets, options.Target, "pass `Target` to E2EHost.OpenAsync");
            var engine = options.EngineFactory?.Invoke(declared) ?? TargetEngines.Create(declared, options.Headed ? false : null, null);
            Action<string>? notice = options.OnNotice is { } onNotice ? line => OpenSession.Notify(onNotice, declared.Name, line) : null;

            // The target's app command starts before the host opens, and the URL is on the port it was started on.
            var (target, processes) = await RunApps.AcquireAsync(config, declared, notice, cancellationToken).ConfigureAwait(false);
            apps = processes;
            var host = await Standalone.OpenWorkerAsync(new StandaloneHostOptions
            {
                Engine = engine,
                TargetName = target.Name,
                ProjectRoot = config.ProjectRoot,
                Env = options.Env,
                CleanupTimeout = config.CleanupTimeout,
                CancellationToken = cancellationToken,
                Notice = options.OnNotice is { } callback ? (name, line) => OpenSession.Notify(callback, name, line) : null,
                RunId = AttemptReport.RunId,
            }).ConfigureAwait(false);
            return new E2EHost(host, config, declared, target, engine, apps, release);
        }
        catch
        {
            if (apps is not null)
            {
                await apps.StopAsync(_ => { }).ConfigureAwait(false);
            }

            release();
            throw;
        }
    }

    /// <summary>
    /// Opens one isolated session. One opening, live, or closing session per host; open them one after another. Upstream's <c>openSession</c>.
    /// The token cancels the opening and, as the session's own, every operation it runs.
    /// </summary>
    public Task<E2ESession> OpenSessionAsync(OpenSessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return OpenSession.RedactOpening(async () =>
        {
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                throw new ConfigurationException("INVALID_CONFIG", "a hosted session requires a name");
            }

            var timeout = options.Timeout ?? _config.Timeout;
            if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
            {
                throw new ConfigurationException("INVALID_CONFIG", "the session timeout must be positive and finite");
            }

            var attempt = await _host.OpenAttemptAsync(SessionOptions(options, timeout), cancellationToken, hosted: true).ConfigureAwait(false);
            return attempt.Session;
        });
    }

    /// <summary>
    /// Cancels or ends a pending or active session, then disposes the worker and releases resources, once. A second call returns what the first did.
    /// Upstream's <c>close</c>, which returns <c>SerializedError[]</c>: see <see cref="E2ESession.CloseAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<Exception>> CloseAsync() => _closing.Value;

    /// <summary>Closes the host and throws what <see cref="CloseAsync"/> returns as an <see cref="AggregateException"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        var failures = await CloseAsync().ConfigureAwait(false);
        if (failures.Count > 0)
        {
            throw new AggregateException("closing the e2e host failed: " + string.Join("; ", failures.Select(failure => failure.Message)), failures);
        }
    }

    private async Task<IReadOnlyList<Exception>> CloseOnceAsync()
    {
        var errors = new List<Exception>();
        try
        {
            errors.AddRange(await _host.CloseAsync().ConfigureAwait(false));
        }
        finally
        {
            try
            {
                await _apps.StopAsync(errors.Add).ConfigureAwait(false);
            }
            finally
            {
                _release();
            }
        }

        return errors;
    }

    private E2ESessionOptions SessionOptions(OpenSessionOptions options, TimeSpan timeout)
    {
        var agent = _config.Agent;
        var cacheMode = _config.Cache.Mode;
        var identity = Sessions.TargetIdentity(
            _target.Name, _declared.Platform, _engine, TargetApps.ResolveTargetApp(_target.Name, _declared.App, _config.ProjectRoot)).AppIdentity;
        return new E2ESessionOptions
        {
            Engine = _engine,
            AppIdentity = identity,
            Model = agent.CreateModel(),
            Judge = agent.CreateJudge(),
            AgentSystem = agent.System,
            AgentContext = agent.Context,
            JudgmentTimeout = agent.JudgmentTimeout,
            MaxObservationBytes = agent.MaxObservationBytes,
            MaxInputTokens = agent.MaxInputTokens,
            ProviderOptions = agent.ProviderOptions,
            Agents = _config.Agents
                .Where(pair => !string.Equals(pair.Key, "default", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value.CreateOptions(), StringComparer.Ordinal),
            Agent = options.Agent,
            BaseUrl = _target.App.Url,
            Permissions = _target.App.Permissions,
            ProjectRoot = _config.ProjectRoot,
            TargetName = _target.Name,
            Cache = cacheMode == CacheMode.Off ? null : new FileStepCache(_config.Cache.Directory),
            CacheEnabled = cacheMode != CacheMode.Off,
            CacheMode = cacheMode,
            CacheStrict = _config.Cache.Strict,
            TestTitle = options.Name,
            Trace = _target.Trace,
            Video = _target.Video,
            TestTimeout = timeout,
            LaunchTimeout = _config.LaunchTimeout,
            CleanupTimeout = _config.CleanupTimeout,
            ActionTimeout = _config.ActionTimeout,
            AssertionTimeout = _config.AssertionTimeout,
            MaxModelCalls = agent.MaxModelCalls ?? E2EDefaults.MaxModelCalls,
            MaxSteps = agent.MaxSteps ?? E2EDefaults.MaxSteps,
            OnProgress = options.OnStep is { } onStep ? progress => OpenSession.Notify(onStep, StepProgressWire.Public(progress)) : null,
        };
    }
}

/// <summary>Hosted test runners keep a worker alive while opening isolated per-test attempts. Upstream's <c>run/open-session.ts</c>.</summary>
internal static class OpenSession
{
    private static readonly Lock Gate = new();
    private static readonly List<Secret> Secrets = [];
    private static readonly HashSet<(string Name, string Value)> Known = [];
    private static Redactor _redactor = Redactor.None;

    /// <summary>Opening errors go straight to the external runner, never through a report writer. Upstream's <c>redactOpening</c>.</summary>
    public static async Task<T> RedactOpening<T>(Func<Task<T>> open)
    {
        try
        {
            return await open().ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            // Arbitrary engine causes can carry non-text secret data; only the sanitized record crosses out. The stack and the inner exception stay behind.
            var safe = ErrorSerializer.Serialize(cause, null, Redact);
            throw safe.Category switch
            {
                "configuration" => new ConfigurationException(safe.Code, safe.Message),
                "infrastructure" => new EngineException(safe.Code, safe.Message, safe.Retryable),
                _ => new E2EException(safe.Code, safe.Message) { Details = safe.Details },
            };
        }
    }

    /// <summary>Makes the config's secrets known to the process before the first engine call, so an opening failure is redacted like any text. Upstream's <c>registerStaticSecrets</c>.</summary>
    public static void RegisterStaticSecrets(IEnumerable<Secret> secrets)
    {
        lock (Gate)
        {
            foreach (var secret in secrets)
            {
                if (Known.Add((secret.Name, secret.Value)))
                {
                    Secrets.Add(secret);
                }
            }

            _redactor = Redactor.For(Secrets);
        }
    }

    /// <summary>The config a host opens: a value is used as it is; a file is read fresh, with the host's environment. Upstream's <c>loadHostConfig</c>.</summary>
    public static E2EConfig LoadHostConfig(string cwd, E2EConfig? config, string? configPath, IReadOnlyDictionary<string, string?>? env)
    {
        if (config is not null)
        {
            return config;
        }

        var path = configPath is null ? E2EConfig.Find(cwd) : Path.GetFullPath(configPath, cwd);
        if (path is null)
        {
            throw new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one");
        }

        if (!File.Exists(path))
        {
            throw configPath is null
                ? new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one")
                : E2EConfig.ExplicitConfigNotFound(cwd, configPath, path);
        }

        return E2EConfig.Load(path, env is null ? null : name => env.GetValueOrDefault(name));
    }

    /// <summary>Calls an owner's callback, which must not throw: one that does never stops the host.</summary>
    public static void Notify<T>(Action<T> callback, T value)
    {
        try
        {
            callback(value);
        }
#pragma warning disable CA1031 // The contract is that the callback does not throw; a throwing one is not a failure of the session.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <inheritdoc cref="Notify{T}(Action{T}, T)"/>
    public static void Notify(Action<string, string> callback, string target, string line)
    {
        try
        {
            callback(target, line);
        }
#pragma warning disable CA1031 // As above.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static string Redact(string text)
    {
        Redactor redactor;
        lock (Gate)
        {
            redactor = _redactor;
        }

        return redactor.Redact(text);
    }
}
