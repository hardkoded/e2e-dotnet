// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace E2E.Tests.BrowserProvider;

/// <summary>A lease with a field of the provider's own: it never reaches a worker, and release gets it back.</summary>
internal sealed class ToyLease : BrowserLease
{
    public string Secret { get; init; } = "";
}

/// <summary>How the scripted provider's sweep behaves, when it has one.</summary>
internal enum ToySweep
{
    None,
    Open,
    Fail,
}

/// <summary>A scripted provider: leases <c>lease-&lt;n&gt;</c> against <c>wss://&lt;n&gt;.example</c>, remembering every call.</summary>
internal class ToyCloud : IBrowserProvider
{
    private int _sequence;

    public ToyCloud(BrowserProviderScope scope = BrowserProviderScope.Worker, int? failSlot = null, bool failRelease = false, bool reconnect = false, ToySweep sweep = ToySweep.None)
    {
        Scope = scope;
        FailSlot = failSlot;
        FailRelease = failRelease;
        Reconnect = reconnect;
        Sweep = sweep;
    }

    public string Name => "toy-cloud";

    public BrowserProviderScope Scope { get; }

    public int? FailSlot { get; }

    public bool FailRelease { get; }

    public bool Reconnect { get; }

    public ToySweep Sweep { get; }

    public BrowserProviderDownloads? Downloads { get; init; }

    public List<BrowserRequest> Acquired { get; } = [];

    public List<BrowserLease> Released { get; } = [];

    public List<BrowserReleaseContext> Swept { get; } = [];

    public Dictionary<string, BrowserLease> Open { get; } = [];

    /// <summary>Replaces the scripted acquire, as a test that needs one to wait or fail does.</summary>
    public Func<BrowserRequest, CancellationToken, Task<BrowserLease>>? OnAcquire { get; set; }

    /// <summary>Replaces the answer to <see cref="RecordAsync"/>, which is that the provider does not record.</summary>
    public Func<BrowserLease, ProviderRecordContext, CancellationToken, Task<E2E.Engine.ProviderRecording?>>? OnRecord { get; set; }

    /// <summary>The ids of what was released, in order.</summary>
    public IEnumerable<string> ReleasedIds => Released.Select(lease => lease.Id);

    public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        if (OnAcquire is { } custom)
        {
            return custom(request, cancellationToken);
        }

        return Grant(request);
    }

    /// <summary>The scripted grant, for a test that wraps <see cref="OnAcquire"/> around it.</summary>
    public Task<BrowserLease> Grant(BrowserRequest request)
    {
        Acquired.Add(request);
        if (request.Slot == FailSlot)
        {
            throw new InvalidOperationException($"no capacity for slot {request.Slot}");
        }

        request.Log("starting");
        var n = _sequence++;
        var lease = new ToyLease
        {
            Id = $"lease-{n}",
            CdpEndpoint = $"wss://{n}.example",
            ReconnectEndpoint = Reconnect ? $"wss://{n}.example/reconnect" : null,

            // A provider's own bookkeeping never reaches a worker.
            Secret = new string('x', 100),
        };
        Open[lease.Id] = lease;
        return Task.FromResult<BrowserLease>(lease);
    }

    public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        Released.Add(lease);
        if (FailRelease)
        {
            throw new InvalidOperationException("stop failed");
        }

        Open.Remove(lease.Id);
        return Task.CompletedTask;
    }

    public Task<E2E.Engine.ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken) =>
        OnRecord is { } record ? record(lease, context, cancellationToken) : Task.FromResult<E2E.Engine.ProviderRecording?>(null);

    public Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        if (Sweep == ToySweep.None)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        Swept.Add(context);
        if (Sweep == ToySweep.Fail)
        {
            throw new InvalidOperationException("list failed");
        }

        IReadOnlyList<string> ids = [.. Open.Keys];
        Open.Clear();
        return Task.FromResult(ids);
    }
}

/// <summary>A recording that starts at a fixed time and ends by running the test's own stop.</summary>
internal sealed class ToyRecording(string startedAt, Func<string, Task<ProviderRecordingResult>> stop) : E2E.Engine.ProviderRecording
{
    public override DateTimeOffset StartedAt { get; } = DateTimeOffset.Parse(startedAt, System.Globalization.CultureInfo.InvariantCulture);

    public override Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken) => stop(context.Dir);
}

/// <summary>What a test needs to run the hooks a runner would: the run's infos, and an engine whose CDP attach is the fakes'.</summary>
internal static class Runner
{
    public const string RunId = "run-1";

    public const string TargetName = "web";

    public static readonly IReadOnlyDictionary<string, string?> Token = new Dictionary<string, string?> { ["BROWSER_TOKEN"] = "t" };

    public static EnginePrepareInfo PrepareInfo(int slots, Action<string>? log = null, IReadOnlyDictionary<string, string?>? env = null) =>
        new() { RunId = RunId, TargetName = TargetName, Slots = slots, Env = env ?? Token, Log = log ?? (_ => { }) };

    public static EngineFinishInfo FinishInfo(Action<string>? log = null) =>
        new() { RunId = RunId, TargetName = TargetName, Env = Token, Log = log ?? (_ => { }) };

    public static EngineInitInfo InitInfo(int workerSlot, IReadOnlyDictionary<string, string?>? env = null, Action<string>? log = null) =>
        new() { RunId = RunId, TargetName = TargetName, WorkerSlot = workerSlot, Env = env ?? new Dictionary<string, string?>(), Log = log ?? (_ => { }) };

    public static IReadOnlyDictionary<string, string?> Widen(IReadOnlyDictionary<string, string> env) =>
        env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

    /// <summary>The variable <c>PrepareAsync</c> wrote for the <c>web</c> target, found by its readable prefix (the suffix is a digest of the name).</summary>
    public static string LeasesVariableIn(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.FirstOrDefault(name => System.Text.RegularExpressions.Regex.IsMatch(name, "^E2E_WEB_BROWSERS_WEB_[0-9A-F]{8}$"))
            ?? throw new InvalidOperationException("no leases variable in " + string.Join(", ", list));
    }

    public static EngineStartOptions Attempt(string attemptId) =>
        new() { AttemptId = attemptId, BaseUrl = "http://app.test/", ActionTimeout = TimeSpan.FromSeconds(1) };

    /// <summary>Runs <c>PrepareAsync</c> on the runner's engine and returns it with the environment it left for the workers.</summary>
    public static async Task<(WebEngine Runner, Dictionary<string, string?> Env)> PreparedAsync(IBrowserProvider cloud, int slots)
    {
        var runner = new WebEngine(new WebEngineOptions { Browser = cloud });
        var result = await runner.PrepareAsync(PrepareInfo(slots), CancellationToken.None);
        return (runner, Widen(result.Env).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    /// <summary>Starts an attempt and ends it at once: a connection that was made and closed.</summary>
    public static async Task RunAttemptAsync(WebEngine worker, string attemptId)
    {
        var session = await worker.StartAsync(Attempt(attemptId), CancellationToken.None);
        await session.DisposeAsync();
    }
}

/// <summary>A provider whose acquire a test writes out, for the grants the scripted cloud would never make.</summary>
internal sealed class InlineProvider(string name, Func<BrowserRequest, Task<BrowserLease>> acquire) : IBrowserProvider
{
    public string Name => name;

    public List<BrowserLease> Released { get; } = [];

    public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken) => acquire(request);

    public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        Released.Add(lease);
        return Task.CompletedTask;
    }
}

/// <summary>Plays the remote browsers: each dial hands out the next fake and remembers the endpoint it was asked for.</summary>
internal sealed class Dialer
{
    private readonly Func<string, FakeBrowser>? _create;

    public Dialer(Func<string, FakeBrowser>? create = null)
    {
        _create = create;
    }

    public List<string> Endpoints { get; } = [];

    /// <summary>The endpoints dialed with Playwright's own protocol; <see cref="Endpoints"/> holds the CDP ones.</summary>
    public List<string> PlaywrightEndpoints { get; } = [];

    public List<FakeBrowser> Browsers { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>The engine of a provider, attaching to the fakes this dialer hands out.</summary>
    public WebEngine Engine(IBrowserProvider provider, IReadOnlyList<WebInitScript>? initScripts = null)
    {
        return new WebEngine(new WebEngineOptions { Browser = provider, Viewport = new WebViewport(320, 200), InitScripts = initScripts })
        {
            CreatePlaywright = FakeRemote.Playwright,
            ConnectCdp = (playwright, endpoint, timeout, cancellationToken) => DialAsync(provider, endpoint),
            RegisterCdpSelectors = (_, _, _) => Task.CompletedTask,
            ConnectPlaywright = (playwright, endpoint, timeout, cancellationToken) =>
            {
                PlaywrightEndpoints.Add(endpoint);
                return DialAsync(provider, endpoint, record: false);
            },
            Clock = new FakeTimeProvider(),
        };
    }

    private Task<IBrowser> DialAsync(IBrowserProvider provider, string endpoint, bool record = true)
    {
        if (record)
        {
            Endpoints.Add(endpoint);
        }

        if (Failure is { } failure)
        {
            return Task.FromException<IBrowser>(failure);
        }

        // A per-attempt lease rides the browser's persistent default context; a shared browser gets a context per attempt.
        var fake = _create?.Invoke(endpoint)
            ?? (provider.Scope == BrowserProviderScope.Attempt ? FakeBrowser.Persistent($"context-{endpoint}") : FakeBrowser.Ordinary());
        Browsers.Add(fake);
        return Task.FromResult(fake.Browser);
    }
}

/// <summary>
/// A browser the dial hands back: enough of one for a context per attempt (worker scope) or a persistent default context
/// (attempt scope). <see cref="Drop"/> disconnects it, as a lost transport does.
/// </summary>
internal sealed class FakeBrowser
{
    private readonly FakeRemote? _remote;
    private bool _connected = true;

    private FakeBrowser(IBrowser browser, FakeRemote? remote)
    {
        Browser = browser;
        _remote = remote;
    }

    public IBrowser Browser { get; }

    /// <summary>A browser that gives each attempt a context of its own.</summary>
    public static FakeBrowser Ordinary()
    {
        var (browser, browserFake) = Fake<IBrowser>.New();
        var fake = new FakeBrowser(browser, null);
        var (context, contextFake) = Fake<IBrowserContext>.New();
        var page = FakeTarget.Create("page").Page;
        contextFake.Members["get_Pages"] = _ => (IReadOnlyList<IPage>)[page];
        contextFake.Members["NewPageAsync"] = _ => Task.FromResult(page);
        contextFake.Members["AddInitScriptAsync"] = _ => Task.FromResult(FakeRemote.Registration);
        contextFake.Members["add_Page"] = _ => null;
        contextFake.Members["CloseAsync"] = _ => Task.CompletedTask;
        browserFake.Members["NewContextAsync"] = _ => Task.FromResult(context);
        browserFake.Members["get_IsConnected"] = _ => fake._connected;
        browserFake.Members["CloseAsync"] = _ =>
        {
            fake._connected = false;
            return Task.CompletedTask;
        };
        return fake;
    }

    /// <summary>A browser whose one default context the attempt rides; <paramref name="contextId"/> is its identity.</summary>
    public static FakeBrowser Persistent(string contextId)
    {
        var remote = FakeRemote.Create(contextId);
        var target = FakeTarget.Create("page-" + contextId);

        // A reconnect checks the page it lands on for closed-shadow tracking, frame by frame.
        target.PageFake.Members["get_Frames"] = _ => (IReadOnlyList<IFrame>)[];
        remote.Pages.Add(target.Page);
        return new FakeBrowser(remote.Browser, remote);
    }

    /// <summary>Drops the transport, as the engine sees it.</summary>
    public void Drop()
    {
        _connected = false;
        _remote?.Disconnect();
    }
}
