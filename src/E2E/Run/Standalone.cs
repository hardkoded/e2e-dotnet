// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using E2E.Engine;

namespace E2E;

/// <summary>What <see cref="Standalone.OpenWorkerAsync"/> is given. Upstream's <c>StandaloneHostOptions</c> (<c>run/standalone.ts</c>).</summary>
internal sealed record StandaloneHostOptions
{
    public required IEngine Engine { get; init; }

    public required string TargetName { get; init; }

    public required string ProjectRoot { get; init; }

    /// <summary>The run's environment: where an engine's prepare reads a token from, and what the worker reads. Null is the process environment.</summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>The budget of the engine's finish.</summary>
    public TimeSpan CleanupTimeout { get; init; } = E2EDefaults.CleanupTimeout;

    /// <summary>Ends the host: an opening attempt is cancelled and the engine is finished. Upstream's <c>signal</c>.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Progress outside steps: provisioning and leases. Upstream's <c>notice</c>. Null drops it.</summary>
    public Action<string, string>? Notice { get; init; }

    /// <summary>The run's id, when the host belongs to a hosted run. Null mints one.</summary>
    public string? RunId { get; init; }
}

/// <summary>One attempt on a standalone host. Upstream's <c>StandaloneAttempt</c> (<c>run/standalone-attempt.ts</c>), over the port's session.</summary>
internal sealed class StandaloneAttempt(E2ESession session, Func<Task<IReadOnlyList<Exception>>> close) : IAsyncDisposable
{
    private int _reported;

    public E2ESession Session { get; } = session;

    /// <summary>Ends the attempt, once, and returns what went wrong. A one-shot attempt (<see cref="Standalone.OpenAttemptAsync"/>) also finishes the engine.</summary>
    public Task<IReadOnlyList<Exception>> CloseAsync() => close();

    /// <summary>Closes the attempt and throws what <see cref="CloseAsync"/> found: the one failure as it is, several as an <see cref="AggregateException"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        var errors = await close().ConfigureAwait(false);

        // The failures are told once: a second dispose, as `await using` after an explicit one, finds them told.
        if (Interlocked.Exchange(ref _reported, 1) == 1)
        {
            return;
        }

        switch (errors.Count)
        {
            case 0:
                return;
            case 1:
                ExceptionDispatchInfo.Throw(errors[0]);
                return;
            default:
                throw new AggregateException(errors);
        }
    }
}

/// <summary>
/// The worker that <c>e2e explore</c> and <c>e2e mcp</c> keep alive for their one attempt (upstream <c>run/standalone.ts</c>): it prepares the
/// engine for one slot of a run of its own, starts the attempt on what prepare left, and finishes that run when the attempt closes, on every
/// path. The upstream hosted kind, which publishes the attempt for another test runner, has no counterpart: the port's tests run in their own.
/// </summary>
internal static class Standalone
{
    /// <summary>
    /// One attempt on a worker of its own, closed together. Upstream's <c>openStandaloneAttempt</c>. Disposing the attempt disposes the
    /// session and finishes the engine, so a hosted device the engine leased goes back whichever way the attempt ends.
    /// </summary>
    public static async Task<StandaloneAttempt> OpenAttemptAsync(E2ESessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Engine);
        var target = string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName;
        var host = await OpenWorkerAsync(new StandaloneHostOptions
        {
            Engine = options.Engine,
            TargetName = target,
            ProjectRoot = Path.GetFullPath(string.IsNullOrEmpty(options.ProjectRoot) ? Directory.GetCurrentDirectory() : options.ProjectRoot),
            CleanupTimeout = options.CleanupTimeout,
            CancellationToken = cancellationToken,
        }).ConfigureAwait(false);
        try
        {
            var attempt = await host.OpenAttemptAsync(options, cancellationToken).ConfigureAwait(false);
            return new StandaloneAttempt(attempt.Session, host.CloseAsync);
        }
        catch (Exception cause)
        {
            var cleanup = await host.CloseAsync().ConfigureAwait(false);
            if (cleanup.Count == 0)
            {
                throw;
            }

            throw StandaloneHost.Combine(cause, cleanup);
        }
    }

    /// <summary>Prepares the engine and returns the worker that opens attempts on it. Upstream's <c>openWorker</c>.</summary>
    internal static async Task<StandaloneHost> OpenWorkerAsync(StandaloneHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Engine);
        var host = new StandaloneHost(options);
        try
        {
            await host.PrepareAsync().ConfigureAwait(false);
            return host;
        }
        catch (Exception cause)
        {
            // What prepare acquired before it failed is still owed back; the failure of prepare is the one to report.
            var cleanup = await host.CloseAsync().ConfigureAwait(false);
            if (cleanup.Count == 0)
            {
                throw;
            }

            throw StandaloneHost.Combine(cause, cleanup);
        }
    }
}

/// <summary>The prepared worker: one attempt at a time, the next once the previous closed. Upstream's <c>StandaloneHost</c>.</summary>
internal sealed class StandaloneHost
{
    /// <summary>How much longer than the cleanup budget the end of the process waits for the release, to let a hook that ignores its token be abandoned.</summary>
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(2);

    private readonly StandaloneHostOptions _options;
    private readonly string _runId;
    private readonly IReadOnlyDictionary<string, string?> _env;
    private readonly PreparedEngines _engines = new();
    private readonly CancellationTokenSource _hostEnd;
    private readonly CancellationToken _signal;
    private readonly List<Exception> _errors = [];
    private readonly Lazy<Task<IReadOnlyList<Exception>>> _closing;
    private readonly Lazy<Task> _tearingDown;
    private readonly EventHandler _exit;
    private readonly object _gate = new();
    private IReadOnlyDictionary<string, string?>? _workerEnv;
    private Task<StandaloneAttempt>? _opening;
    private StandaloneAttempt? _live;
    private bool _closed;
    private bool _unsafeToReuse;

    internal StandaloneHost(StandaloneHostOptions options)
    {
        _options = options;
        _runId = options.RunId ?? Guid.NewGuid().ToString("n");
        _env = options.Env ?? PreparedEngines.ProcessEnvironment();
        _workerEnv = options.Env;
        _hostEnd = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        _signal = _hostEnd.Token;
        _closing = new Lazy<Task<IReadOnlyList<Exception>>>(CloseOnceAsync);
        _tearingDown = new Lazy<Task>(TearDownAsync);
        _exit = (_, _) => ReleaseAtExit();
    }

    public string RunId => _runId;

    /// <summary>
    /// Opens the attempt, one at a time. The slot is taken before any engine work runs. A launch that fails has ended what it opened, and the
    /// host stays usable for the next attempt, unless an attempt's cleanup failed: what it left may be unclean, so the host refuses another.
    /// A <paramref name="hosted"/> attempt is upstream's hosted kind: its session closes through <see cref="E2ESession.CloseAsync"/> and is disposed by its owner.
    /// </summary>
    public Task<StandaloneAttempt> OpenAttemptAsync(E2ESessionOptions session, CancellationToken cancellationToken = default, bool hosted = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        var result = new TaskCompletionSource<StandaloneAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_closed)
            {
                return Task.FromException<StandaloneAttempt>(new EngineException("WORKER_PROTOCOL", "the e2e host is closed"));
            }

            if (_unsafeToReuse)
            {
                return Task.FromException<StandaloneAttempt>(new EngineException("WORKER_PROTOCOL", "the e2e host cannot open another session after one closed with failures"));
            }

            if (_opening is not null || _live is not null)
            {
                return Task.FromException<StandaloneAttempt>(new EngineException("WORKER_PROTOCOL", "the e2e host already has an opening or live session"));
            }

            _opening = result.Task;
        }

        _ = LaunchAsync(session, cancellationToken, result, hosted);
        return result.Task;
    }

    /// <summary>
    /// Cancels an opening attempt before awaiting it, ends the live one, then finishes the engine, once. A second call waits for the first and
    /// returns what it returned. The errors are the attempt's and the engine's cleanup failures; none stops the next step.
    /// </summary>
    public Task<IReadOnlyList<Exception>> CloseAsync()
    {
        lock (_gate)
        {
            _closed = true;
        }

        return _closing.Value;
    }

    /// <summary>The error a failed open reports when closing the host failed too: the open's own failure leads, with the cleanup failure added.</summary>
    internal static EngineException Combine(Exception cause, IReadOnlyList<Exception> cleanup)
    {
        var code = cause switch
        {
            E2EException coded => coded.Code,
            OperationCanceledException => EngineErrorCodes.Cancelled,
            _ => EngineErrorCodes.EngineFailure,
        };
        return new EngineException(code, $"{cause.Message}; closing the standalone host also failed: {cleanup[0].Message}", cause);
    }

    internal async Task PrepareAsync()
    {
        // A prepare that outlives the process holds a hosted resource nothing else will give back.
        if (_options.Engine is IPreparableEngine)
        {
            AppDomain.CurrentDomain.ProcessExit += _exit;
        }

        var prepared = await _engines.PrepareAsync(
            _options.Engine,
            _options.TargetName,
            1,
            new PrepareScope { RunId = _runId, ProjectRoot = _options.ProjectRoot, Env = _env, Notice = Notice },
            _signal).ConfigureAwait(false);
        if (prepared is not null)
        {
            // What prepare left is the worker's environment; the run's own variables are never overwritten.
            var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (name, value) in prepared.Env)
            {
                merged[name] = value;
            }

            foreach (var (name, value) in _env)
            {
                merged[name] = value;
            }

            _workerEnv = merged;
        }

        if (_signal.IsCancellationRequested)
        {
            throw new EngineException(EngineErrorCodes.Cancelled, $"opening a host on target \"{_options.TargetName}\" was cancelled");
        }
    }

    private async Task LaunchAsync(E2ESessionOptions session, CancellationToken cancellationToken, TaskCompletionSource<StandaloneAttempt> result, bool hosted)
    {
        // The attempt's own scope outlives the open: it ends the session when the host or its caller does.
        var scope = CancellationTokenSource.CreateLinkedTokenSource(_signal, cancellationToken);
        E2ESession? started = null;
        try
        {
            started = await E2ESession.LaunchAsync(session, _workerEnv, scope.Token).ConfigureAwait(false);
            if (scope.IsCancellationRequested)
            {
                // An engine that ignored the cancel must not hand back a session nobody wants.
                var cancelled = new EngineException(EngineErrorCodes.Cancelled, "opening the e2e session was cancelled");
                started.Complete(cancelled, scope.Token);
                await started.DisposeAsync().ConfigureAwait(false);
                throw cancelled;
            }

            var attempt = Wrap(started, scope, hosted);
            lock (_gate)
            {
                _live = attempt;
                _opening = null;
            }

            result.SetResult(attempt);
        }
        catch (Exception cause)
        {
            scope.Dispose();
            lock (_gate)
            {
                _opening = null;
            }

            if (cause is OperationCanceledException && !cancellationToken.IsCancellationRequested && _signal.IsCancellationRequested)
            {
                cause = new EngineException(EngineErrorCodes.Cancelled, "opening the e2e session was cancelled", cause);
            }

            result.SetException(cause);
        }
    }

    private StandaloneAttempt Wrap(E2ESession session, CancellationTokenSource scope, bool hosted)
    {
        StandaloneAttempt? attempt = null;
        var ending = new Lazy<Task<IReadOnlyList<Exception>>>(async () =>
        {
            var errors = new List<Exception>();
            try
            {
                if (hosted)
                {
                    errors.AddRange(await session.EndAsync().ConfigureAwait(false));
                }
                else
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // The attempt's failures are reported together, whichever step they came from.
            catch (Exception cause)
#pragma warning restore CA1031
            {
                errors.Add(cause);
            }
            finally
            {
                scope.Dispose();
                lock (_gate)
                {
                    if (ReferenceEquals(_live, attempt))
                    {
                        _live = null;
                    }
                }
            }

            // Only a hosted session reports its failures as a list, and it also holds test outcomes (a soft failure, a step nobody awaited), which leave the worker clean.
            if (hosted && session.CleanupFailed)
            {
                lock (_gate)
                {
                    _unsafeToReuse = true;
                }
            }

            return errors;
        });
        attempt = new StandaloneAttempt(session, () => ending.Value);
        if (hosted)
        {
            session.Host(attempt.CloseAsync);
        }

        return attempt;
    }

    private async Task<IReadOnlyList<Exception>> CloseOnceAsync()
    {
        // Cancel a launch before awaiting it, then end the attempt before finishing the engine it ran on. A live attempt closes before the
        // host's token ends, so its own cleanup does not run under a cancelled session.
        Task<StandaloneAttempt>? opening;
        lock (_gate)
        {
            opening = _opening;
        }

        if (opening is not null)
        {
            await _hostEnd.CancelAsync().ConfigureAwait(false);
            await ((Task)opening).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        StandaloneAttempt? live;
        lock (_gate)
        {
            live = _live;
        }

        if (live is not null)
        {
            Add(await live.CloseAsync().ConfigureAwait(false));
        }

        await _hostEnd.CancelAsync().ConfigureAwait(false);
        await _tearingDown.Value.ConfigureAwait(false);
        _hostEnd.Dispose();
        lock (_gate)
        {
            return [.. _errors];
        }
    }

    /// <summary>Releases what prepare acquired, once, whichever of the host's close and the end of the process gets there first.</summary>
    private async Task TearDownAsync()
    {
        try
        {
            await _engines.FinishAsync(new FinishScope
            {
                RunId = _runId,
                Env = _env,
                CleanupTimeout = _options.CleanupTimeout,
                Notice = Notice,
                OnFailure = failure => Add([failure]),
            }).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Whatever finish threw is a cleanup failure to report, and the host is closed either way.
        catch (Exception cause)
#pragma warning restore CA1031
        {
            Add([cause]);
        }
        finally
        {
            AppDomain.CurrentDomain.ProcessExit -= _exit;
        }
    }

    /// <summary>The release for a process that ends while the host is open (an unhandled exit), which no close will reach. It waits, because nothing runs after it.</summary>
    internal void ReleaseAtExit()
    {
        var bounded = _options.CleanupTimeout > TimeSpan.Zero && _options.CleanupTimeout != Timeout.InfiniteTimeSpan;
        try
        {
            _tearingDown.Value.Wait(bounded ? _options.CleanupTimeout + ExitGrace : Timeout.InfiniteTimeSpan);
        }
#pragma warning disable CA1031 // The process is going away; there is no log left to report to, and the provider's own time limit ends the lease.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private void Add(IEnumerable<Exception> errors)
    {
        lock (_gate)
        {
            _errors.AddRange(errors);
        }
    }

    // A host with no reporter has nowhere to send a progress line, and a lease id or session URL in it would only be one more thing to redact.
    // One that has an owner hands each line over, and what the owner's callback throws never stops provisioning.
    private void Notice(string target, string line)
    {
        try
        {
            _options.Notice?.Invoke(target, line);
        }
#pragma warning disable CA1031 // The callback must not throw; a throwing one is not a failure of the host.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
