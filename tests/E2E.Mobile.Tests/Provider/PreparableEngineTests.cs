// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Provider;

/// <summary>
/// .NET-only: upstream's engine carries <c>prepare</c> and <c>finish</c> as members and has no <c>it</c> for the lease a hosted device
/// takes through them. A hosted device bills until it is released, so these test that <see cref="MobileEngine"/> leases in
/// <see cref="IPreparableEngine.PrepareAsync"/>, releases in <see cref="IPreparableEngine.FinishAsync"/>, and leases nothing else. The
/// host that calls the pair around an attempt is tested in <c>OpenHostLifecycleTests</c> and <c>EngineTargetsTests</c> of <c>E2E.Tests</c>.
/// </summary>
public sealed class PreparableEngineTests
{
    /// <summary>A provider that counts what it leased and what came back.</summary>
    private sealed class CountingCloud : IDeviceProvider
    {
        private int _acquired;
        private int _released;

        public string Name => "counting-cloud";

        public bool AttachesToRunningApp { get; init; }

        public int Acquired => Volatile.Read(ref _acquired);

        public int Released => Volatile.Read(ref _released);

        public async Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _acquired);
            await Task.Yield();
            return new DeviceLease { Id = $"lease-{number}", Daemon = new DeviceDaemon($"https://{number}.example", $"token-{number}"), Device = $"sim-{number}" };
        }

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _released);
            return Task.CompletedTask;
        }
    }

    private static Harness Over(CountingCloud cloud) => Harness.Create(options => options with { Provider = cloud });

    private static EnginePrepareInfo PrepareInfo(string targetName) => new()
    {
        RunId = "run",
        TargetName = targetName,
        Slots = 1,
        Env = new Dictionary<string, string?>(),
        ProjectRoot = Harness.ProjectRoot,
        Log = _ => { },
    };

    private static EngineFinishInfo FinishInfo(string targetName) => new()
    {
        RunId = "run",
        TargetName = targetName,
        Env = new Dictionary<string, string?>(),
        Log = _ => { },
        CleanupTimeout = TimeSpan.FromSeconds(5),
    };

    [Fact]
    public async Task Leases_in_prepare_and_releases_in_finish_and_never_when_the_engine_starts()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var engine = (IPreparableEngine)h.Engine;

        // Building the engine leases nothing: a hosted session bills from the moment it is prepared.
        Assert.Equal(0, cloud.Acquired);
        var prepared = await engine.PrepareAsync(PrepareInfo("ios-simulator"), CancellationToken.None);
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        Assert.Equal(1, prepared.Workers);

        // The attempt rides the device prepare bound, and its end gives nothing back.
        var session = await h.Engine.StartAsync(new EngineStartOptions { ProjectRoot = Harness.ProjectRoot, Env = prepared.Env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value) }, CancellationToken.None);
        Assert.Equal(new DeviceDaemon("https://1.example", "token-1"), h.Connections[^1]?.Daemon);
        await session.DisposeAsync();
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));

        await engine.FinishAsync(FinishInfo("ios-simulator"), CancellationToken.None);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Refuses_a_target_with_a_url_before_it_starts_and_leases_nothing_for_it()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => h.Engine.StartAsync(new EngineStartOptions { BaseUrl = "https://example.com" }, CancellationToken.None));
        Assert.Contains("app.url", refused.Message, StringComparison.Ordinal);
        Assert.Equal(0, cloud.Acquired);
    }

    [Fact]
    public async Task Closes_what_warm_opened_when_a_finish_overtook_the_prepare_and_the_prepare_then_fails()
    {
        var h = Harness.Create(options => options);
        var leasing = new TaskCompletionSource();
        var go = new TaskCompletionSource();
        h.Fake.RespondAsync(
            DeviceMethods.DevicesList,
            async (_, _) =>
            {
                leasing.TrySetResult();
                await go.Task;
                return new JsonObject { ["devices"] = new JsonArray(new JsonObject { ["id"] = "sim-1", ["name"] = "sim", ["platform"] = "ios", ["kind"] = "simulator", ["booted"] = true }) };
            });
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => throw new InvalidOperationException("no simulator"));
        var info = h.PrepareInfo(1, targetName: h.Engine.Options.TargetName);
        var preparing = h.Engine.PrepareAsync(info, CancellationToken.None);
        await leasing.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The run ends while the pool is still listing devices; warm then opens a session and its boot fails.
        await h.Engine.FinishAsync(Harness.FinishInfo(targetName: info.TargetName), CancellationToken.None);
        go.SetResult();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => preparing);
        Assert.Contains("no simulator", failure.Message, StringComparison.Ordinal);
        Assert.Contains("sessions.close", h.Fake.Methods());
    }

    [Fact]
    public async Task Attaches_to_the_running_app_instead_of_relaunching_it_when_the_provider_cannot_relaunch()
    {
        var h = Harness.Create(options => options with { Provider = new CountingCloud { AttachesToRunningApp = true } });
        var session = await StartPrepared(h);
        h.Fake.Calls.Clear();
        await session.OpenAsync("", CancellationToken.None);
        var open = Assert.Single(h.Fake.Calls, call => call.Method == DeviceMethods.AppsOpen);
        Assert.Null(open.Args?["relaunch"]);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Relaunches_the_app_for_a_provider_that_can()
    {
        var h = Over(new CountingCloud());
        var session = await StartPrepared(h);
        h.Fake.Calls.Clear();
        await session.OpenAsync("", CancellationToken.None);
        var open = Assert.Single(h.Fake.Calls, call => call.Method == DeviceMethods.AppsOpen);
        Assert.Equal(true, open.Args?["relaunch"]?.GetValue<bool>());
        await session.DisposeAsync();
    }

    private static async Task<IEngineSession> StartPrepared(Harness h)
    {
        var prepared = await h.Engine.PrepareAsync(h.PrepareInfo(1, targetName: h.Engine.Options.TargetName), CancellationToken.None);
        return await h.Engine.StartAsync(new EngineStartOptions { ProjectRoot = Harness.ProjectRoot, Env = Harness.WorkerEnv(prepared) }, CancellationToken.None);
    }
}
