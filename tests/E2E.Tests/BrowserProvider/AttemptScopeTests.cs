// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// <c>describe('attempt scope')</c> of upstream <c>browser-provider.test.ts</c>. The port has no <c>endAttempt</c> to call
/// after a failed start, so a start that fails gives its lease back itself, and the two tests that call it after a failed
/// start check that nothing is left to release.
/// </summary>
public sealed class AttemptScopeTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Leases_nothing_at_prepare_hands_the_workers_the_slot_count_alone_and_attaches_nothing_at_init()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var dialer = new Dialer();
        var runner = dialer.Engine(cloud);
        var result = await runner.PrepareAsync(Runner.PrepareInfo(2), None);
        Assert.Null(result.Workers);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"slots":2,"leases":[]}"""), JsonNode.Parse(result.Env[Runner.LeasesVariableIn(result.Env.Keys)])));
        Assert.Empty((await runner.PrepareAsync(Runner.PrepareInfo(0), None)).Env);
        runner.Init(Runner.InitInfo(0, Runner.Widen(result.Env)));
        Assert.Empty(cloud.Acquired);
        Assert.Empty(dialer.Endpoints);
        await runner.FinishAsync(Runner.FinishInfo(), None);
        Assert.Empty(cloud.Released);
    }

    [Fact]
    public async Task Leases_a_browser_per_attempt_with_the_attempt_id_attaches_to_it_and_releases_it_at_endAttempt_once()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 2);
        var worker = dialer.Engine(cloud);
        var lines = new List<string>();
        env["BROWSER_TOKEN"] = "t";
        worker.Init(Runner.InitInfo(1, env, lines.Add));
        var first = await worker.StartAsync(Runner.Attempt("a1"), None);
        var request = Assert.Single(cloud.Acquired);
        Assert.Equal((1, 2, "a1"), (request.Slot, request.Slots, request.AttemptId));
        Assert.Equal(env.OrderBy(pair => pair.Key), request.Env.OrderBy(pair => pair.Key));

        // Its progress reaches the reporter through the worker, as a lease from prepare does through the runner.
        Assert.Equal(["toy-cloud: starting"], lines);
        Assert.Equal(["wss://0.example"], dialer.Endpoints);
        await first.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
        Assert.Equal(100, Assert.IsType<ToyLease>(cloud.Released[0]).Secret.Length);
        await first.DisposeAsync();
        Assert.Single(cloud.Released);

        var second = await worker.StartAsync(Runner.Attempt("a2"), None);
        Assert.Equal("a2", cloud.Acquired[^1].AttemptId);
        Assert.Equal(["wss://0.example", "wss://1.example"], dialer.Endpoints);
        await second.DisposeAsync();
        await worker.DisposeAsync();
        Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Trusts_every_lease_to_be_a_fresh_browser_even_one_whose_default_context_id_an_earlier_lease_had()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var dialer = new Dialer(_ => FakeBrowser.Persistent("snapshot-context"));
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        await Runner.RunAttemptAsync(worker, "a1");
        await Runner.RunAttemptAsync(worker, "a2");
        Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds);
        await worker.DisposeAsync();
    }

    /// <summary>Partial: the failed start releases the lease itself, where upstream's <c>endAttempt</c> does.</summary>
    [Fact]
    public async Task Releases_the_lease_at_endAttempt_after_a_start_that_failed_to_attach_PARTIAL_the_failed_start_releases_the_lease_itself()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var dialer = new Dialer { Failure = new PlaywrightException("endpoint refused") };
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var error = await Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a1"), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Single(cloud.Acquired);
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }

    /// <summary>Partial: there is no <c>endAttempt</c> to call, so it checks that nothing was released.</summary>
    [Fact]
    public async Task Fails_the_attempt_start_naming_the_provider_when_the_lease_is_refused_and_endAttempt_has_nothing_to_release_PARTIAL_there_is_no_endAttempt_to_call_after_a_failed_start()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt, failSlot: 0);
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var error = await Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a1"), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not lease a browser: no capacity for slot 0", error.Message, StringComparison.Ordinal);
        Assert.Empty(dialer.Endpoints);
        Assert.Empty(cloud.Released);
    }

    [Fact]
    public async Task Reports_an_acquire_that_gave_up_on_the_aborted_attempt_as_CANCELLED_not_a_provider_failure()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt)
        {
            OnAcquire = async (request, token) =>
            {
                var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var registration = token.Register(() => aborted.SetResult());
                await aborted.Task;
                throw new InvalidOperationException("aborted by the service client");
            },
        };
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(new ToyCloud(BrowserProviderScope.Attempt), 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        using var controller = new CancellationTokenSource();
        var starting = Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a1"), controller.Token));
        await controller.CancelAsync();
        Assert.Equal("CANCELLED", (await starting).Code);
        Assert.Empty(cloud.Released);
    }

    /// <summary>Partial: the engine ends an attempt only by disposing its session, so the case where the attempt ended while the lease was pending drives the lease bookkeeping directly.</summary>
    [Fact]
    public async Task Gives_back_a_lease_that_arrives_after_the_attempt_was_cancelled_or_ended_and_never_commits_it_PARTIAL_the_ended_case_ends_the_lease_bookkeeping_not_an_attempt()
    {
        // Cancelled: the engine's start gets a lease from a provider that ignored the abort.
        {
            var cloud = new ToyCloud(BrowserProviderScope.Attempt);
            var granted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cloud.OnAcquire = async (request, token) =>
            {
                await granted.Task;
                return await cloud.Grant(request);
            };
            var dialer = new Dialer();
            var (_, env) = await Runner.PreparedAsync(new ToyCloud(BrowserProviderScope.Attempt), 1);
            var worker = dialer.Engine(cloud);
            worker.Init(Runner.InitInfo(0, env));
            using var controller = new CancellationTokenSource();
            var starting = Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a1"), controller.Token));
            await controller.CancelAsync();
            granted.SetResult();
            var error = await starting;
            Assert.Equal("CANCELLED", error.Code);
            Assert.Contains("arrived after the attempt ended; released", error.Message, StringComparison.Ordinal);
            Assert.Equal(["lease-0"], cloud.ReleasedIds);
            Assert.Empty(dialer.Endpoints);

            // The next attempt starts clean and leases its own browser.
            await Runner.RunAttemptAsync(worker, "a2");
            Assert.Equal("a2", cloud.Acquired[^1].AttemptId);
            await worker.DisposeAsync();
            Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds);
        }

        // Ended: the engine ends an attempt only by disposing its session, so this case drives the lease bookkeeping the session ends it through.
        {
            var cloud = new ToyCloud(BrowserProviderScope.Attempt);
            var granted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cloud.OnAcquire = async (request, token) =>
            {
                await granted.Task;
                return await cloud.Grant(request);
            };
            var (_, env) = await Runner.PreparedAsync(new ToyCloud(BrowserProviderScope.Attempt), 1);
            var leases = new LeasedBrowsers(cloud);
            leases.Init(Runner.InitInfo(0, env));
            var starting = Assert.ThrowsAsync<EngineException>(() => leases.StartAttemptAsync("a1", None));
            await leases.EndAttemptAsync(None);
            granted.SetResult();
            var error = await starting;
            Assert.Equal("CANCELLED", error.Code);
            Assert.Contains("arrived after the attempt ended; released", error.Message, StringComparison.Ordinal);
            Assert.Equal(["lease-0"], cloud.ReleasedIds);

            await leases.StartAttemptAsync("a2", None);
            Assert.Equal("a2", cloud.Acquired[^1].AttemptId);
            await leases.EndAttemptAsync(None);
            Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds);
        }
    }

    [Fact]
    public async Task Reports_a_release_that_failed_as_a_cleanup_error_once_and_never_holds_the_lease_again()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt, failRelease: true);
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var session = await worker.StartAsync(Runner.Attempt("a1"), None);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.DisposeAsync().AsTask());
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not release browser lease-0: stop failed", error.Message, StringComparison.Ordinal);
        await session.DisposeAsync();
        Assert.Single(cloud.Released);
    }

    /// <summary>Partial: upstream probes with the internal <c>guard</c>; the port reconnects inside the next operation, here <c>OpenAsync</c>.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reattaches_through_the_leases_reconnectEndpoint_after_the_transport_drops_and_through_cdpEndpoint_without_one_PARTIAL_the_probe_is_an_OpenAsync(bool reconnect)
    {
        // One browser identity: recovery must land on the same persistent context.
        var dialer = new Dialer(_ => FakeBrowser.Persistent("same-browser"));
        var cloud = new ToyCloud(BrowserProviderScope.Attempt, reconnect: reconnect);
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var session = await worker.StartAsync(Runner.Attempt("a1"), None);
        await session.OpenAsync("http://app.test/", None);
        dialer.Browsers[0].Drop();
        await session.OpenAsync("http://app.test/", None);
        Assert.Equal(["wss://0.example", reconnect ? "wss://0.example/reconnect" : "wss://0.example"], dialer.Endpoints);

        // Recovery is the same browser: no second lease.
        Assert.Single(cloud.Acquired);
        await session.DisposeAsync();
        await worker.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }
}
