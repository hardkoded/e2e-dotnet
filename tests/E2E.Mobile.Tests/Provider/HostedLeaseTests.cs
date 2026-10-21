// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Provider;

/// <summary>
/// .NET-only: upstream's <c>openStandaloneAttempt</c> prepares and finishes the engine around one attempt, and has no <c>it</c> for the
/// lease count. A hosted device bills until it is released, so every path that ends an attempt must give it back exactly once.
/// </summary>
public sealed class HostedLeaseTests
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

        public int Held => Acquired - Released;

        public Func<CancellationToken, Task>? OnAcquire { get; set; }

        public Func<CancellationToken, Task>? OnRelease { get; set; }

        public List<string> LeaseIds { get; } = [];

        public async Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _acquired);
            if (OnAcquire is { } wait)
            {
                await wait(cancellationToken);
            }

            lock (LeaseIds)
            {
                LeaseIds.Add($"lease-{number}");
            }

            return new DeviceLease { Id = $"lease-{number}", Daemon = new DeviceDaemon($"https://{number}.example", $"token-{number}"), Device = $"sim-{number}" };
        }

        public async Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _released);
            if (OnRelease is { } wait)
            {
                await wait(cancellationToken);
            }
        }
    }

    private static Harness Over(CountingCloud cloud, TimeSpan? cleanup = null) => Harness.Create(options => options with { Provider = cloud }, cleanup: cleanup);

    private static Task<IEngineSession> Start(Harness h, CancellationToken cancellationToken = default) =>
        h.Engine.StartAsync(new EngineStartOptions { ProjectRoot = Harness.ProjectRoot }, cancellationToken);

    [Fact]
    public async Task Leases_when_the_attempt_starts_and_releases_when_its_session_is_disposed()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);

        // Building the engine leases nothing: a hosted session bills from the moment it starts.
        Assert.Equal(0, cloud.Acquired);
        var session = await Start(h);
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        Assert.Equal(new DeviceDaemon("https://1.example", "token-1"), h.Connections[^1]?.Daemon);

        await session.DisposeAsync();
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));

        // A second dispose gives nothing back twice.
        await session.DisposeAsync();
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Leases_nothing_for_a_target_the_engine_refuses_to_start()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => h.Engine.StartAsync(new EngineStartOptions { BaseUrl = "https://example.com" }, CancellationToken.None));
        Assert.Contains("app.url", refused.Message, StringComparison.Ordinal);
        Assert.Equal(0, cloud.Acquired);
    }

    [Fact]
    public async Task Releases_the_lease_when_warming_the_device_fails()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => throw new InvalidOperationException("no simulator"));
        var failure = await Assert.ThrowsAsync<EngineException>(() => Start(h));
        Assert.Contains("no simulator", failure.Message, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_when_the_session_cannot_boot_after_the_lease_was_granted()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var boots = 0;

        // The warm-up boot works; the attempt's own boot does not.
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => Interlocked.Increment(ref boots) == 1 ? new JsonObject() : throw new InvalidOperationException("device went away"));
        var failure = await Assert.ThrowsAsync<EngineException>(() => Start(h));
        Assert.Contains("device went away", failure.Message, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
        Assert.Contains("sessions.close", h.Fake.Methods());
    }

    [Fact]
    public async Task Releases_a_lease_that_was_granted_and_then_rejected()
    {
        var cloud = new CountingCloud();
        var h = Harness.Create(options => options with { Provider = new Rejecting(cloud) });
        var failure = await Assert.ThrowsAsync<EngineException>(() => Start(h));
        Assert.Contains("could not lease a device", failure.Message, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    /// <summary>Leases, and answers with a lease that names no way to reach the device.</summary>
    private sealed class Rejecting(CountingCloud inner) : IDeviceProvider
    {
        public string Name => "rejecting";

        public async Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
        {
            var lease = await inner.AcquireAsync(request, cancellationToken);
            return new DeviceLease { Id = lease.Id };
        }

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => inner.ReleaseAsync(lease, context, cancellationToken);
    }

    [Fact]
    public async Task Releases_the_lease_when_the_request_is_cancelled_after_the_lease_and_reports_cancellation()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        using var request = new CancellationTokenSource();
        var warming = new TaskCompletionSource();
        h.Fake.RespondAsync(
            DeviceMethods.DevicesBoot,
            async (_, token) =>
            {
                warming.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return null;
            });
        var starting = Start(h, request.Token);
        await warming.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await request.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => starting);
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Gives_back_what_the_provider_granted_when_the_request_is_cancelled_while_it_leases()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        using var request = new CancellationTokenSource();
        var leasing = new TaskCompletionSource();
        cloud.OnAcquire = async token =>
        {
            leasing.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        var starting = Start(h, request.Token);
        await leasing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await request.CancelAsync();

        // The pool calls a cancelled lease a failed one; the caller who cancelled gets cancellation, not a provider error or a timeout.
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => starting);
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);

        // The request was cancelled before the provider granted anything, so nothing is owed back.
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));

        // The engine is free for the next attempt.
        cloud.OnAcquire = null;
        await (await Start(h)).DisposeAsync();
        Assert.Equal((2, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Holds_nothing_when_the_provider_refuses_and_leases_again_on_the_next_start()
    {
        var cloud = new CountingCloud { OnAcquire = _ => throw new InvalidOperationException("no capacity") };
        var h = Over(cloud);
        var failure = await Assert.ThrowsAsync<EngineException>(() => Start(h));
        Assert.Contains("no capacity", failure.Message, StringComparison.Ordinal);
        Assert.Empty(cloud.LeaseIds);
        cloud.OnAcquire = null;
        await (await Start(h)).DisposeAsync();
        Assert.Single(cloud.LeaseIds);
        Assert.Equal(2, cloud.Acquired);
    }

    [Fact]
    public async Task Reports_a_release_that_fails_and_leaves_the_engine_free_for_the_next_attempt()
    {
        var cloud = new CountingCloud { OnRelease = _ => throw new InvalidOperationException("stop failed") };
        var h = Over(cloud);
        var session = await Start(h);
        var failure = await Assert.ThrowsAsync<EngineException>(async () => await session.DisposeAsync());
        Assert.Contains("could not release a device: stop failed", failure.Message, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
        cloud.OnRelease = null;
        await (await Start(h)).DisposeAsync();
        Assert.Equal((2, 2), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Bounds_a_release_that_never_answers_and_does_not_report_it_as_a_cancel()
    {
        var cloud = new CountingCloud { OnRelease = token => Task.Delay(Timeout.Infinite, token) };
        var h = Over(cloud, TimeSpan.FromMilliseconds(200));
        var session = await Start(h);
        var failure = await Assert.ThrowsAsync<EngineException>(async () => await session.DisposeAsync());
        Assert.Equal(EngineErrorCodes.EngineFailure, failure.Code);
        Assert.Contains("did not finish within", failure.Message, StringComparison.Ordinal);
        Assert.Contains("may still bill", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bounds_a_release_whose_provider_ignores_its_token()
    {
        var never = new TaskCompletionSource();
        var cloud = new CountingCloud { OnRelease = _ => never.Task };
        var h = Over(cloud, TimeSpan.FromMilliseconds(100));
        var session = await Start(h);
        var started = DateTime.UtcNow;
        var failure = await Assert.ThrowsAsync<EngineException>(async () => await session.DisposeAsync());
        Assert.Contains("did not finish within", failure.Message, StringComparison.Ordinal);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
        never.SetResult();
    }

    [Fact]
    public async Task Refuses_a_second_attempt_while_the_engine_holds_a_lease_and_leases_nothing_for_it()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var first = await Start(h);
        var second = await Assert.ThrowsAsync<EngineException>(() => Start(h));
        Assert.Contains("already holds a hosted device", second.Message, StringComparison.Ordinal);
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        await first.DisposeAsync();
        await (await Start(h)).DisposeAsync();
        Assert.Equal((2, 2), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Rides_the_devices_a_prepare_bound_and_leaves_their_release_to_finish()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var prepared = await h.Engine.PrepareAsync(h.PrepareInfo(1, targetName: "ios-simulator"), CancellationToken.None);
        Assert.Equal(1, cloud.Acquired);
        var session = await h.Engine.StartAsync(new EngineStartOptions { ProjectRoot = Harness.ProjectRoot, Env = Harness.WorkerEnv(prepared) }, CancellationToken.None);
        await session.DisposeAsync();
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        await h.Engine.FinishAsync(Harness.FinishInfo(targetName: "ios-simulator"), CancellationToken.None);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_at_the_end_of_the_process_once_and_not_again_when_the_session_closes()
    {
        var cloud = new CountingCloud();
        var h = Over(cloud);
        var session = (DeviceSession)await Start(h);
        session.Lease!.ReleaseAtExit();
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
        await session.DisposeAsync();
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Names_no_lease_id_or_token_in_a_failure_it_reports()
    {
        var cloud = new CountingCloud { OnRelease = _ => throw new InvalidOperationException("stop failed") };
        var h = Over(cloud);
        var lines = new List<string>();
        var session = await Start(h);
        var failure = await Assert.ThrowsAsync<EngineException>(async () => await session.DisposeAsync());
        lines.Add(failure.Message);
        h.Fake.Respond(DeviceMethods.DevicesBoot, _ => throw new InvalidOperationException("no simulator"));
        cloud.OnRelease = null;
        lines.Add((await Assert.ThrowsAsync<EngineException>(() => Start(h))).Message);
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("lease-", line, StringComparison.Ordinal);
            Assert.DoesNotContain("token-", line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Attaches_to_the_running_app_instead_of_relaunching_it_when_the_provider_cannot_relaunch()
    {
        var h = Harness.Create(options => options with { Provider = new CountingCloud { AttachesToRunningApp = true } });
        var session = (DeviceSession)await Start(h);
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
        var session = (DeviceSession)await Start(h);
        h.Fake.Calls.Clear();
        await session.OpenAsync("", CancellationToken.None);
        var open = Assert.Single(h.Fake.Calls, call => call.Method == DeviceMethods.AppsOpen);
        Assert.Equal(true, open.Args?["relaunch"]?.GetValue<bool>());
        await session.DisposeAsync();
    }
}
