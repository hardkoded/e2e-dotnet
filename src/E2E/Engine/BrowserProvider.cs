// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E2E.Engine;

/// <summary>
/// How long a lease lives. <see cref="Worker"/> (the default) is one browser per worker slot, acquired in prepare and
/// released in finish. <see cref="Attempt"/> is a fresh browser per attempt, acquired when the attempt starts and
/// released when it ends, reconnected through the lease's <see cref="BrowserLease.ReconnectEndpoint"/> after a
/// transport drop.
/// </summary>
public enum BrowserProviderScope
{
    Worker = 0,
    Attempt = 1,
}

/// <summary>
/// The protocol a leased browser's endpoint speaks. .NET-only: upstream's lease always carries a CDP endpoint.
/// </summary>
public enum BrowserProtocol
{
    /// <summary>The Chrome DevTools Protocol, dialed with Playwright's <c>connectOverCDP</c>. A browser the engine finds with a default context.</summary>
    Cdp = 0,

    /// <summary>
    /// Playwright's own protocol, dialed with Playwright's <c>connect</c>: a Playwright server or a grid that hosts one.
    /// The browser has no context yet, so the engine opens its own, and a dropped connection cannot be reattached.
    /// </summary>
    Playwright = 1,
}

/// <summary>What the engine asks a provider for: one browser for one worker slot, or for one attempt, of a run.</summary>
public sealed record BrowserRequest
{
    /// <summary>The run id, for naming the session at the provider.</summary>
    public required string RunId { get; init; }

    /// <summary>The target the browser serves.</summary>
    public required string TargetName { get; init; }

    /// <summary>Worker slot the browser serves, <c>0</c> to <c>Slots - 1</c>. In <see cref="BrowserProviderScope.Worker"/> scope <see cref="Slots"/> leases are acquired at once.</summary>
    public required int Slot { get; init; }

    /// <summary>Worker slots the run uses for this target, in all.</summary>
    public required int Slots { get; init; }

    /// <summary>Present only for a per-attempt lease.</summary>
    public string? AttemptId { get; init; }

    /// <summary>The run's environment: where a provider reads its token from, never the process environment.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>Reports one line of progress to the run's reporter (a session URL to watch), from the runner and from a worker alike.</summary>
    public required Action<string> Log { get; init; }
}

/// <summary>
/// One leased browser, as <see cref="IBrowserProvider.AcquireAsync"/> returns it and
/// <see cref="IBrowserProvider.ReleaseAsync"/> gets it back. A provider may derive from it and keep further fields:
/// release gets that same object, while only the declared fields travel from the runner to the worker that attaches to it.
/// </summary>
public class BrowserLease
{
    /// <summary>The provider's handle on the lease (a session id), named in progress lines and handed back to release.</summary>
    public string Id { get; init; } = "";

    /// <summary><c>ws://</c>, <c>wss://</c>, or <c>http://</c> endpoint to attach to, over CDP unless <see cref="Protocol"/> says otherwise.</summary>
    public string CdpEndpoint { get; init; } = "";

    /// <summary>
    /// What <see cref="CdpEndpoint"/> speaks. A <see cref="BrowserProtocol.Playwright"/> lease needs <see cref="BrowserProviderScope.Worker"/>
    /// scope and no <see cref="ReconnectEndpoint"/>: its connection cannot be reattached. .NET-only.
    /// </summary>
    public BrowserProtocol Protocol { get; init; }

    /// <summary>
    /// Endpoint to reattach to the same browser after a transport drop, when it differs from <see cref="CdpEndpoint"/>;
    /// only meaningful for a per-attempt lease.
    /// </summary>
    public string? ReconnectEndpoint { get; init; }
}

/// <summary>Handed to release, once per lease.</summary>
public sealed record BrowserReleaseContext
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    /// <summary>The run's environment, the same acquire saw.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    public required Action<string> Log { get; init; }
}

/// <summary>Handed to <see cref="BrowserProviderDownloads.ReadAsync"/>, once per file.</summary>
public sealed record BrowserDownloadContext
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    /// <summary>The run's environment, the same acquire saw.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }
}

/// <summary>
/// How a download reaches the runner from a browser on another machine. The browser saves every file to its own disk,
/// where the runner cannot open it, so the engine points the browser's downloads at <see cref="Dir"/> there and reads
/// each finished file back through <see cref="ReadAsync"/>.
/// </summary>
public sealed record BrowserProviderDownloads
{
    /// <summary>Absolute directory on the browser's machine the browser saves downloads to; created by the browser if missing.</summary>
    public required string Dir { get; init; }

    /// <summary>The bytes of one finished download, <c>file</c> an absolute path under <see cref="Dir"/>, read off the leased browser's disk.</summary>
    public required Func<BrowserLease, string, BrowserDownloadContext, CancellationToken, Task<byte[]>> ReadAsync { get; init; }
}

/// <summary>
/// A source of hosted browsers: the seam a hosted browser service plugs into (upstream <c>BrowserProvider</c>). In
/// <see cref="BrowserProviderScope.Worker"/> scope <see cref="AcquireAsync"/> is called once per worker slot, for every
/// slot at once, in the runner; a worker whose browser drops calls it again, from its own process, for a replacement.
/// In <see cref="BrowserProviderScope.Attempt"/> scope every attempt start calls it from the worker. <see cref="ReleaseAsync"/>
/// is called exactly once per lease, by the process that acquired it, also after an acquire of another slot failed; a worker
/// that dies first never calls it, and <see cref="SweepAsync"/> releases what it left. A provider must not rely on
/// runner-process state to serve a request from a worker. Nothing here knows any vendor.
/// </summary>
public interface IBrowserProvider
{
    /// <summary>Label in progress lines and error messages.</summary>
    string Name { get; }

    /// <summary>How long a lease lives; <see cref="BrowserProviderScope.Worker"/> by default.</summary>
    BrowserProviderScope Scope => BrowserProviderScope.Worker;

    /// <summary>
    /// Serves <c>browser.waitForDownload</c> for a browser that runs on another machine. Without it a download on such
    /// a browser fails, since the file lands on the browser's disk; a provider whose browsers run on the runner's own
    /// machine leaves it out.
    /// </summary>
    BrowserProviderDownloads? Downloads => null;

    Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken);

    Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Records the leased browser for an attempt that records video, in place of the engine's screencast of the page: the
    /// service's own recording of the browser, called from the worker when the attempt starts its video. A provider that
    /// cannot record its browsers (a headless session the service cannot replay) returns null, and the engine records the
    /// screencast.
    /// </summary>
    Task<ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken) =>
        Task.FromResult<ProviderRecording?>(null);

    /// <summary>
    /// Releases every lease of the run and target the provider still holds open, and resolves to their ids. Called once
    /// per target from finish, in the runner, after every worker exited and the leases prepare made were released:
    /// whatever it finds is a lease a worker acquired (a per-attempt browser, a replacement) and never gave back,
    /// because the worker died before it could. Find them by what acquire tagged them with at the service: the run id
    /// and the target.
    /// </summary>
    Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

/// <summary>The downloads of the browser an attempt rides, read through its provider.</summary>
internal sealed record LeaseDownloads
{
    /// <summary>The provider as error messages name it: <c>browser provider "kernel"</c>.</summary>
    public required string Provider { get; init; }

    public required string Dir { get; init; }

    public required Func<string, CancellationToken, Task<byte[]>> ReadAsync { get; init; }
}

/// <summary>A provider recording the attempt started, with who made it and which lease it covers, for stopping it.</summary>
internal sealed record LeaseRecording
{
    public required ProviderRecording Recording { get; init; }

    /// <summary>The provider as error messages name it: <c>browser provider "kernel"</c>.</summary>
    public required string Provider { get; init; }

    public required string LeaseId { get; init; }
}

/// <summary>
/// Browsers a provider leases for one target, on both sides of the worker boundary. In the runner, prepare leases one
/// per slot, all at once (hosted browsers start in parallel and are billed from the moment they do), and holds every
/// lease granted for finish before the outcome is known, so a slot that fails to lease fails the run and the others are
/// still released. In a worker, init reads the slot's lease from the environment; a browser that drops is replaced by a
/// lease the worker acquires itself, and per-attempt leases come and go with the attempt. Each lease is released exactly
/// once, by the side that acquired it.
/// </summary>
internal sealed partial class LeasedBrowsers(IBrowserProvider provider)
{
    /// <summary>
    /// The most the serialized hand-off of one target may occupy in a worker's environment: a lease comes from
    /// third-party code, and an oversized environment fails the worker spawn with an opaque error.
    /// </summary>
    private const int MaxHandoffEnvBytes = 16 * 1024;

    /// <summary>How the hand-off names <see cref="BrowserProtocol.Playwright"/>; a CDP lease leaves the field out.</summary>
    private const string PlaywrightProtocol = "playwright";

    private static readonly JsonSerializerOptions HandoffJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Leases granted so far, per target, filled as each acquire settles.</summary>
    private readonly Dictionary<string, List<BrowserLease>> _held = [];

    /// <summary>Targets whose prepare succeeded, so workers ran and may have left leases open.</summary>
    private readonly HashSet<string> _workersRan = [];

    private WorkerRun? _run;

    /// <summary>The lease the worker's shared browser attaches to.</summary>
    private WorkerLease? _current;

    /// <summary>Whether the connection to <see cref="_current"/> was found dropped, so the next endpoint is a replacement.</summary>
    private bool _dropped;

    private BrowserLease? _attempt;

    /// <summary>The attempt whose lease is being acquired or held; a lease that arrives for any other attempt is released, not committed.</summary>
    private object? _opened;

    /// <summary>This worker's tenure, replaced by init and dispose; a replacement lease that arrives for an earlier one is released, not committed.</summary>
    private object _tenure = new();

    public BrowserProviderScope Scope { get; } = provider.Scope;

    /// <summary>The provider's name, for messages.</summary>
    public string Name { get; } = provider.Name;

    /// <summary>Whether <see cref="Init"/> ran: the worker half has a run to serve.</summary>
    public bool IsInitialized => _run is not null;

    // --- runner side ---

    /// <summary>
    /// Leases one browser per worker slot the run will use, in <see cref="BrowserProviderScope.Worker"/> scope, and hands
    /// the leases to the workers through the environment; <see cref="BrowserProviderScope.Attempt"/> scope leases per
    /// attempt instead and hands over the slot count alone. A target nothing runs on leases nothing.
    /// </summary>
    public async Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
    {
        if (info.Slots == 0)
        {
            return new EnginePrepareResult();
        }

        var variable = HandoffVariable(info.TargetName);
        var held = new List<BrowserLease>();
        lock (_held)
        {
            _held[info.TargetName] = held;
        }

        if (Scope == BrowserProviderScope.Attempt)
        {
            lock (_held)
            {
                _workersRan.Add(info.TargetName);
            }

            return new EnginePrepareResult { Env = new Dictionary<string, string> { [variable] = EncodeHandoff(info.Slots, []) } };
        }

        info.Log($"leasing {info.Slots} browser(s) from {Name}");
        var leases = await AllOrFirstFailureAsync(
            Enumerable.Range(0, info.Slots).Select<int, Func<Task<BrowserLease>>>(slot => async () =>
            {
                var lease = await AcquireLeaseAsync(
                    new BrowserRequest
                    {
                        RunId = info.RunId,
                        TargetName = info.TargetName,
                        Slot = slot,
                        Slots = info.Slots,
                        Env = info.Env,
                        Log = line => info.Log($"{Name} ({slot + 1} of {info.Slots}): {line}"),
                    },
                    cancellationToken).ConfigureAwait(false);
                lock (held)
                {
                    held.Add(lease);
                }

                info.Log($"{Name}: leased {lease.Id}");
                return lease;
            }),
            $"browser provider \"{Name}\" could not lease a browser").ConfigureAwait(false);
        var env = new Dictionary<string, string> { [variable] = EncodeHandoff(leases.Count, leases) };
        lock (_held)
        {
            _workersRan.Add(info.TargetName);
        }

        return new EnginePrepareResult { Workers = leases.Count, Env = env };
    }

    /// <summary>
    /// Releases what prepare leased for the target, every lease before the first failure is reported, then has the
    /// provider sweep up the leases a worker that died left open, in either scope, once workers ran: the sweep runs
    /// whatever the releases did, and is skipped when prepare failed and no worker started.
    /// </summary>
    public async Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
    {
        List<BrowserLease>? leases;
        bool swept;
        lock (_held)
        {
            _held.Remove(info.TargetName, out leases);
            swept = _workersRan.Remove(info.TargetName);
        }

        if (leases is null)
        {
            return;
        }

        var context = new BrowserReleaseContext
        {
            RunId = info.RunId,
            TargetName = info.TargetName,
            Env = info.Env,
            Log = line => info.Log($"{Name}: {line}"),
        };
        try
        {
            if (leases.Count == 0)
            {
                return;
            }

            await AllOrFirstFailureAsync(
                leases.Select<BrowserLease, Func<Task<object?>>>(lease => async () =>
                {
                    await provider.ReleaseAsync(lease, context, cancellationToken).ConfigureAwait(false);
                    return null;
                }),
                $"browser provider \"{Name}\" could not release a browser").ConfigureAwait(false);
            info.Log($"{Name}: released {leases.Count} browser(s)");
        }
        finally
        {
            if (swept)
            {
                await SweepAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The provider's sweep of the leases workers left open, bounded by the cleanup budget. A backstop, so a sweep that
    /// fails is reported as a line, never as the run's failure: the service still ends an idle browser.
    /// </summary>
    private async Task SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? released;
        try
        {
            released = await Timing.RaceAbortAsync(() => provider.SweepAsync(context, cancellationToken), cancellationToken, $"sweeping browsers of \"{Name}\"").ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            context.Log($"could not release the browsers a worker left open: {cause.Message}");
            return;
        }

        var ids = (released ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (ids.Count > 0)
        {
            context.Log($"released {ids.Count} browser(s) a worker left open: {string.Join(", ", ids)}");
        }
    }

    // --- worker side ---

    /// <summary>
    /// Records the run this worker serves and, in <see cref="BrowserProviderScope.Worker"/> scope, the lease its slot was
    /// handed. A missing hand-off is a broken invariant: the runner prepares every target before it starts a worker, and
    /// caps the slots at the workers prepare reported.
    /// </summary>
    public void Init(EngineInitInfo info)
    {
        _current = null;
        _dropped = false;
        _tenure = new object();
        var handoff = DecodeHandoff(info.Env.GetValueOrDefault(HandoffVariable(info.TargetName)))
            ?? throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"browser provider \"{Name}\" prepared nothing for target \"{info.TargetName}\"; the runner must prepare the target before a worker starts",
                retryable: false);
        _run = new WorkerRun(info.RunId, info.TargetName, info.Env, info.WorkerSlot, handoff.Slots, line => info.Log($"{Name}: {line}"));
        if (Scope == BrowserProviderScope.Attempt)
        {
            return;
        }

        if (info.WorkerSlot < 0 || info.WorkerSlot >= handoff.Leases.Count)
        {
            throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"worker slot {info.WorkerSlot} is outside the {handoff.Leases.Count} browser(s) \"{Name}\" leased; the runner must cap the target at the engine's reported workers",
                retryable: false);
        }

        _current = new WorkerLease(handoff.Leases[info.WorkerSlot], Owned: false);
    }

    /// <summary>
    /// The lease the worker's shared browser attaches to, in <see cref="BrowserProviderScope.Worker"/> scope: the
    /// slot's lease while its browser holds, and a replacement the worker leases itself once <see cref="ConnectionEnded"/>
    /// found the browser dropped. A dead lease is released by the side that acquired it: the worker here, best effort, or
    /// the runner in finish. A replacement that arrives once the attempt was cancelled or the worker disposed is given
    /// straight back and never committed.
    /// </summary>
    public async Task<BrowserLease> EndpointAsync(CancellationToken cancellationToken)
    {
        var run = RequireRun();
        var previous = _current;
        if (previous is not null && !_dropped)
        {
            return previous.Lease;
        }

        var tenure = _tenure;
        var lease = await AcquireLeaseAsync(NewRequest(run, attemptId: null), cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_tenure, tenure))
        {
            await GiveBackAsync(lease, run).ConfigureAwait(false);
            throw new EngineException(EngineErrorCodes.Cancelled, $"browser lease from \"{Name}\" arrived after the worker gave up; released", retryable: false);
        }

        _current = new WorkerLease(lease, Owned: true);
        _dropped = false;
        if (previous is { Owned: true })
        {
            await GiveBackAsync(previous.Lease, run).ConfigureAwait(false);
        }

        return lease;
    }

    /// <summary>
    /// Tells the worker the connection to its browser ended, and whether the browser had dropped by then. A dropped
    /// browser is replaced by the next <see cref="EndpointAsync"/>; a connected one is kept for the worker's next attempt.
    /// </summary>
    public void ConnectionEnded(bool connected)
    {
        if (!connected)
        {
            _dropped = true;
        }
    }

    /// <summary>
    /// Leases a fresh browser for the attempt, in <see cref="BrowserProviderScope.Attempt"/> scope;
    /// <see cref="EndAttemptAsync"/> releases it on every path. A lease that arrives once the attempt was cancelled or
    /// already ended (a provider that ignored the abort) is given straight back and never committed, so a retry never
    /// finds it.
    /// </summary>
    public async Task<BrowserLease> StartAttemptAsync(string attemptId, CancellationToken cancellationToken)
    {
        var run = RequireRun();
        var opened = new object();
        _opened = opened;
        var lease = await AcquireLeaseAsync(NewRequest(run, attemptId), cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_opened, opened))
        {
            await GiveBackAsync(lease, run).ConfigureAwait(false);
            throw new EngineException(EngineErrorCodes.Cancelled, $"browser lease from \"{Name}\" arrived after the attempt ended; released", retryable: false);
        }

        _attempt = lease;
        return lease;
    }

    /// <summary>Releases the attempt's lease, when one was granted; idempotent, and safe after a failed <see cref="StartAttemptAsync"/>.</summary>
    public async Task EndAttemptAsync(CancellationToken cleanup)
    {
        var lease = _attempt;
        _attempt = null;
        _opened = null;
        if (lease is null)
        {
            return;
        }

        await ReleaseAsync(lease, cleanup).ConfigureAwait(false);
    }

    /// <summary>Releases the replacement lease the worker acquired for itself; what prepare leased is the runner's to release.</summary>
    public async Task DisposeAsync(CancellationToken cleanup)
    {
        var current = _current;
        _current = null;
        _dropped = false;
        _tenure = new object();
        if (current is not { Owned: true })
        {
            return;
        }

        await ReleaseAsync(current.Lease, cleanup).ConfigureAwait(false);
    }

    /// <summary>
    /// How the attempt records its browser when the provider records: starts the provider's recording of the browser the
    /// attempt rides, the attempt's lease in <see cref="BrowserProviderScope.Attempt"/> scope and the worker's current
    /// one in <see cref="BrowserProviderScope.Worker"/> scope. Null when the provider does not record this browser. A
    /// start that fails is named after the provider, or is the cancellation when the attempt gave up first;
    /// <see cref="ProviderRecordings.StopProviderRecordingAsync"/> checks the stop.
    /// </summary>
    public Func<CancellationToken, Task<LeaseRecording?>> Recorder(string attemptId)
    {
        var label = $"browser provider \"{Name}\"";
        return async cancellationToken =>
        {
            var run = RequireRun();
            var lease = CurrentLease();
            if (lease is null)
            {
                throw new EngineException(EngineErrorCodes.InvalidState, $"{label} was asked to record before the attempt had a browser");
            }

            ProviderRecording? recording;
            try
            {
                recording = await provider.RecordAsync(
                    lease,
                    new ProviderRecordContext { RunId = run.RunId, TargetName = run.TargetName, AttemptId = attemptId, Env = run.Env },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception cause)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw ConnectionAbort($"recording from \"{Name}\"");
                }

                throw new EngineException(EngineErrorCodes.EngineFailure, $"{label} could not start recording browser {lease.Id}: {cause.Message}", retryable: false, cause);
            }

            return recording is null ? null : new LeaseRecording { Recording = recording, Provider = label, LeaseId = lease.Id };
        };
    }

    /// <summary>
    /// The downloads of the browser the attempt rides, when the provider serves them: the attempt's lease in
    /// <see cref="BrowserProviderScope.Attempt"/> scope and the worker's current one in <see cref="BrowserProviderScope.Worker"/>
    /// scope. A read that fails is named after the provider, or is the cancellation when the caller gave up first.
    /// </summary>
    public LeaseDownloads? Downloads()
    {
        if (provider.Downloads is not { } served)
        {
            return null;
        }

        var label = $"browser provider \"{Name}\"";
        return new LeaseDownloads
        {
            Provider = label,
            Dir = served.Dir,
            ReadAsync = async (file, cancellationToken) =>
            {
                var run = RequireRun();
                var lease = CurrentLease();
                if (lease is null)
                {
                    throw new EngineException(EngineErrorCodes.InvalidState, $"{label} was asked for a download before the attempt had a browser");
                }

                try
                {
                    return await served.ReadAsync(lease, file, new BrowserDownloadContext { RunId = run.RunId, TargetName = run.TargetName, Env = run.Env }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception cause)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw ConnectionAbort($"download from \"{Name}\"");
                    }

                    throw new EngineException(EngineErrorCodes.EngineFailure, $"{label} could not read download {file} from browser {lease.Id}: {cause.Message}", retryable: false, cause);
                }
            },
        };
    }

    /// <summary>The browser the attempt rides: the attempt's lease in attempt scope, the worker's current one in worker scope.</summary>
    private BrowserLease? CurrentLease() => Scope == BrowserProviderScope.Attempt ? _attempt : _current?.Lease;

    private static EngineException ConnectionAbort(string label) => new(EngineErrorCodes.Cancelled, label + " cancelled", retryable: false);

    private WorkerRun RequireRun() =>
        _run ?? throw new EngineException(EngineErrorCodes.EngineFailure, $"browser provider \"{Name}\" was asked for a browser before init", retryable: false);

    private BrowserRequest NewRequest(WorkerRun run, string? attemptId) =>
        new() { RunId = run.RunId, TargetName = run.TargetName, Slot = run.Slot, Slots = run.Slots, AttemptId = attemptId, Env = run.Env, Log = run.Log };

    /// <summary>One acquire, its result checked field by field; a failure is named after the provider.</summary>
    private async Task<BrowserLease> AcquireLeaseAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        BrowserLease? lease;
        try
        {
            lease = await provider.AcquireAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.Cancelled, $"browser lease from \"{Name}\" cancelled", retryable: false, cause);
            }

            throw new EngineException(EngineErrorCodes.EngineFailure, $"browser provider \"{Name}\" could not lease a browser: {cause.Message}", retryable: false, cause);
        }

        return IsBrowserLease(lease)
            ? lease
            : throw new EngineException(EngineErrorCodes.EngineFailure, $"browser provider \"{Name}\" returned a lease without an id and a cdpEndpoint", retryable: false);
    }

    /// <summary>One release from the worker, bounded by the cleanup budget; a failure is a cleanup error named after the provider.</summary>
    private async Task ReleaseAsync(BrowserLease lease, CancellationToken cleanup)
    {
        var run = RequireRun();
        try
        {
            await Timing.RaceAbortAsync(() => provider.ReleaseAsync(lease, ReleaseContext(run), cleanup), cleanup, $"releasing browser {lease.Id}").ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not EngineException)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"browser provider \"{Name}\" could not release browser {lease.Id}: {cause.Message}", retryable: false, cause);
        }
    }

    /// <summary>
    /// Gives back a lease that arrived too late, best effort. It gets its own cleanup budget: the token the lease was
    /// asked under is the one that gave up, and a release under it would be cancelled before it began.
    /// </summary>
    private async Task GiveBackAsync(BrowserLease lease, WorkerRun run)
    {
        using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
        try
        {
            await provider.ReleaseAsync(lease, ReleaseContext(run), budget.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best effort: a lease that cannot be given back is the sweep's to find.
        }
    }

    private static BrowserReleaseContext ReleaseContext(WorkerRun run) =>
        new() { RunId = run.RunId, TargetName = run.TargetName, Env = run.Env, Log = run.Log };

    /// <summary>Runs every task, then reports the first failure: nothing is skipped because a sibling failed.</summary>
    private static async Task<List<T>> AllOrFirstFailureAsync<T>(IEnumerable<Func<Task<T>>> tasks, string describe)
    {
        // A task that throws before its first await is a failed task like any other, not an escape.
        var running = tasks.Select(async task => await task().ConfigureAwait(false)).ToList();
        try
        {
            await Task.WhenAll(running).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Every task has finished; the first failure in order is reported below.
        }

        if (running.Find(task => task.IsFaulted || task.IsCanceled) is { } failed)
        {
            var cause = failed.Exception?.GetBaseException() ?? new OperationCanceledException();
            throw cause as EngineException ?? new EngineException(EngineErrorCodes.EngineFailure, $"{describe}: {cause.Message}", retryable: false, cause);
        }

        return [.. running.Select(task => task.Result)];
    }

    /// <summary>A lease as a provider returned it or as JSON parsed it back, checked field by field: the engine trusts nothing it did not write.</summary>
    private static bool IsBrowserLease(BrowserLease? lease) =>
        lease is not null
        && !string.IsNullOrWhiteSpace(lease.Id)
        && !string.IsNullOrWhiteSpace(lease.CdpEndpoint)
        && (lease.ReconnectEndpoint is null || !string.IsNullOrWhiteSpace(lease.ReconnectEndpoint))
        && Enum.IsDefined(lease.Protocol)
        && (lease.Protocol == BrowserProtocol.Cdp || lease.ReconnectEndpoint is null);

    /// <summary>
    /// The environment variable a target's hand-off travels to the workers in: the name made environment-safe for
    /// reading, plus a digest of the exact name so <c>web-a</c> and <c>web.a</c> never share a key.
    /// </summary>
    internal static string HandoffVariable(string targetName)
    {
        var readable = NotAlphanumeric().Replace(targetName, "_").ToUpperInvariant();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(targetName)))[..8];
        return $"E2E_WEB_BROWSERS_{readable}_{digest}";
    }

    /// <summary>Serializes the hand-off for the environment: declared lease fields only, bounded.</summary>
    private static string EncodeHandoff(int slots, IReadOnlyList<BrowserLease> leases)
    {
        var encoded = JsonSerializer.Serialize(
            new HandoffWire(slots, [.. leases.Select(lease => new LeaseWire(lease.Id, lease.CdpEndpoint, lease.ReconnectEndpoint, lease.Protocol == BrowserProtocol.Playwright ? PlaywrightProtocol : null))]),
            HandoffJson);
        var bytes = Encoding.UTF8.GetByteCount(encoded);
        return bytes > MaxHandoffEnvBytes
            ? throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"the browser leases take {bytes} bytes; the worker environment carries at most {MaxHandoffEnvBytes}",
                retryable: false)
            : encoded;
    }

    /// <summary>The hand-off a prepare left in the environment, or null when there is none it can read.</summary>
    private static Handoff? DecodeHandoff(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("slots", out var slots)
                || slots.ValueKind != JsonValueKind.Number
                || !slots.TryGetInt32(out var count)
                || count < 0
                || !root.TryGetProperty("leases", out var leases)
                || leases.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            List<BrowserLease> decoded = [];
            foreach (var entry in leases.EnumerateArray())
            {
                var lease = entry.ValueKind == JsonValueKind.Object
                    ? new BrowserLease { Id = ReadString(entry, "id") ?? "", CdpEndpoint = ReadString(entry, "cdpEndpoint") ?? "", ReconnectEndpoint = ReadString(entry, "reconnectEndpoint"), Protocol = ReadString(entry, "protocol") == PlaywrightProtocol ? BrowserProtocol.Playwright : BrowserProtocol.Cdp }
                    : null;
                if (!IsBrowserLease(lease) || HasNonString(entry, "reconnectEndpoint") || HasNonString(entry, "protocol") || ReadString(entry, "protocol") is not (null or PlaywrightProtocol))
                {
                    return null;
                }

                decoded.Add(lease!);
            }

            return new Handoff(count, decoded);
        }
        catch (JsonException)
        {
            // Not ours to read.
            return null;
        }
    }

    private static string? ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Whether a field that must be a string, when present, is something else.</summary>
    private static bool HasNonString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null);

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NotAlphanumeric();

    /// <summary>What prepare hands the workers of one target: the slots the run uses, and in worker scope the lease each slot attaches to.</summary>
    private sealed record Handoff(int Slots, IReadOnlyList<BrowserLease> Leases);

    private sealed record HandoffWire(int Slots, IReadOnlyList<LeaseWire> Leases);

    private sealed record LeaseWire(string Id, string CdpEndpoint, string? ReconnectEndpoint, string? Protocol);

    /// <summary>The run a worker serves, as init reported it: what every request and release from the worker names.</summary>
    private sealed record WorkerRun(string RunId, string TargetName, IReadOnlyDictionary<string, string?> Env, int Slot, int Slots, Action<string> Log);

    /// <summary>A lease a worker attaches to, and whether the worker (not the runner's prepare) acquired it.</summary>
    private sealed record WorkerLease(BrowserLease Lease, bool Owned);
}
