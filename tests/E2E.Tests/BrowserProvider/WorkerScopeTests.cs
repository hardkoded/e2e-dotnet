// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// <c>describe('worker scope')</c> of upstream <c>browser-provider.test.ts</c>. A port worker is a <see cref="WebEngine"/> whose
/// <c>Init</c> ran, an attempt is a <c>StartAsync</c> and the dispose of its session, and the worker's own dispose is
/// <c>DisposeAsync</c>. Two differences from upstream, both in the port's engine. It keeps no browser connection between
/// attempts, so a dropped browser is seen when the attempt that rode it ends, where upstream sees it when the next one starts.
/// And its start runs on promises that already settled, so the lines two concurrent leases log arrive in the order the code
/// runs them, where upstream's interleave. Not ported: "releases the replacement on dispose even when ending the attempt
/// failed", because the port's <c>DisposeAsync</c> does not end an attempt (the session's dispose does), so it has no
/// <c>endAttempt</c> to fail.
/// </summary>
public sealed class WorkerScopeTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Leases_one_browser_per_slot_at_prepare_hands_the_declared_fields_to_the_workers_and_each_worker_attaches_to_its_own()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var runner = new WebEngine(new WebEngineOptions { Browser = cloud });
        var lines = new List<string>();
        var result = await runner.PrepareAsync(Runner.PrepareInfo(2, lines.Add), None);
        Assert.Equal(2, result.Workers);
        Assert.Equal(
            [(0, 2, (string?)null, "run-1", "web"), (1, 2, null, "run-1", "web")],
            cloud.Acquired.OrderBy(request => request.Slot).Select(request => (request.Slot, request.Slots, request.AttemptId, request.RunId, request.TargetName)));
        Assert.Equal("t", cloud.Acquired[0].Env["BROWSER_TOKEN"]);
        Assert.Single(cloud.Acquired[0].Env);

        // The slots lease at once, so their lines come in no fixed order.
        Assert.Equal("leasing 2 browser(s) from toy-cloud", lines[0]);
        Assert.Equal(
            ["toy-cloud (1 of 2): starting", "toy-cloud (2 of 2): starting", "toy-cloud: leased lease-0", "toy-cloud: leased lease-1"],
            lines.Skip(1).Order(StringComparer.Ordinal));
        var variable = Runner.LeasesVariableIn(result.Env.Keys);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"slots":2,"leases":[{"id":"lease-0","cdpEndpoint":"wss://0.example"},{"id":"lease-1","cdpEndpoint":"wss://1.example"}]}"""),
            JsonNode.Parse(result.Env[variable])));

        // Nothing was attached in the runner: leasing is not connecting.
        Assert.Empty(dialer.Endpoints);

        // A worker reads its slot's lease from the environment and attaches to that endpoint.
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(1, new Dictionary<string, string?> { [variable] = result.Env[variable] }));
        await Runner.RunAttemptAsync(worker, "a1");
        await worker.DisposeAsync();
        Assert.Equal(["wss://1.example"], dialer.Endpoints);

        // The worker acquired nothing of its own, so it released nothing.
        Assert.Equal(2, cloud.Acquired.Count);
        Assert.Empty(cloud.Released);

        // The runner releases what it leased, handing back the provider's own objects.
        var finished = new List<string>();
        await runner.FinishAsync(Runner.FinishInfo(finished.Add), None);
        Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds.Order(StringComparer.Ordinal));
        Assert.Equal(100, Assert.IsType<ToyLease>(cloud.Released[0]).Secret.Length);
        Assert.Equal(["toy-cloud: released 2 browser(s)"], finished);
        await runner.FinishAsync(Runner.FinishInfo(), None);
        Assert.Equal(2, cloud.Released.Count);
    }

    [Fact]
    public async Task Leases_nothing_for_a_target_with_no_slots()
    {
        var cloud = new ToyCloud();
        var runner = new WebEngine(new WebEngineOptions { Browser = cloud });
        var result = await runner.PrepareAsync(Runner.PrepareInfo(0), None);
        Assert.Null(result.Workers);
        Assert.Empty(result.Env);
        Assert.Empty(cloud.Acquired);
        await runner.FinishAsync(Runner.FinishInfo(), None);
        Assert.Empty(cloud.Released);
    }

    [Fact]
    public async Task Holds_the_slots_that_leased_when_another_fails_so_finish_releases_them_and_fails_the_run_before_any_test()
    {
        var cloud = new ToyCloud(failSlot: 1);
        var runner = new WebEngine(new WebEngineOptions { Browser = cloud });
        var error = await Assert.ThrowsAsync<EngineException>(() => runner.PrepareAsync(Runner.PrepareInfo(2), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not lease a browser: no capacity for slot 1", error.Message, StringComparison.Ordinal);
        await runner.FinishAsync(Runner.FinishInfo(), None);
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Treats_an_acquire_that_throws_before_its_first_await_like_any_other_failed_slot()
    {
        var sync = new SyncProvider();
        var runner = new WebEngine(new WebEngineOptions { Browser = sync });
        var error = await Assert.ThrowsAsync<EngineException>(() => runner.PrepareAsync(Runner.PrepareInfo(2), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("token missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_release_failure_once_every_lease_was_tried()
    {
        var cloud = new ToyCloud(failRelease: true);
        var runner = new WebEngine(new WebEngineOptions { Browser = cloud });
        await runner.PrepareAsync(Runner.PrepareInfo(2), None);
        var error = await Assert.ThrowsAsync<EngineException>(() => runner.FinishAsync(Runner.FinishInfo(), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not release a browser: stop failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Rejects_leases_too_large_for_a_worker_environment_by_bytes()
    {
        // Multi-byte characters: the cap is on bytes, not string length.
        var oversized = new InlineProvider("huge", request => Task.FromResult(new BrowserLease { Id = $"l-{request.Slot}", CdpEndpoint = "wss://d.example/" + new string('ż', 9_000) }));
        var runner = new WebEngine(new WebEngineOptions { Browser = oversized });
        var error = await Assert.ThrowsAsync<EngineException>(() => runner.PrepareAsync(Runner.PrepareInfo(1), None));
        Assert.Contains("the worker environment carries at most 16384", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: a lease is typed, so upstream's <c>reconnectEndpoint: 7</c> cannot be written; a blank one is the nearest case.</summary>
    [Fact]
    public async Task Refuses_a_lease_without_an_id_and_a_cdpEndpoint_or_with_a_reconnectEndpoint_that_is_not_one_and_still_releases_nothing_for_it_PARTIAL_a_typed_reconnectEndpoint_is_a_string()
    {
        BrowserLease?[] leases =
        [
            null,
            new BrowserLease(),
            new BrowserLease { Id = "bare" },
            new BrowserLease { CdpEndpoint = "wss://d.example" },
            new BrowserLease { Id = " ", CdpEndpoint = "wss://d.example" },
            new BrowserLease { Id = "x", CdpEndpoint = "wss://d.example", ReconnectEndpoint = " " },
        ];
        foreach (var lease in leases)
        {
            var odd = new InlineProvider("odd", _ => Task.FromResult(lease!));
            var runner = new WebEngine(new WebEngineOptions { Browser = odd });
            var error = await Assert.ThrowsAsync<EngineException>(() => runner.PrepareAsync(Runner.PrepareInfo(1), None));
            Assert.Contains("browser provider \"odd\" returned a lease without an id and a cdpEndpoint", error.Message, StringComparison.Ordinal);
            await runner.FinishAsync(Runner.FinishInfo(), None);
            Assert.Empty(odd.Released);
        }
    }

    [Fact]
    public async Task Fails_init_when_prepare_left_nothing_it_can_read_or_the_slot_is_outside_the_leases()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var variable = Runner.LeasesVariableIn(env.Keys);
        var bare = Assert.Throws<EngineException>(() => dialer.Engine(cloud).Init(Runner.InitInfo(0)));
        Assert.Equal("ENGINE_FAILURE", bare.Code);
        Assert.Contains("browser provider \"toy-cloud\" prepared nothing for target \"web\"", bare.Message, StringComparison.Ordinal);
        foreach (var raw in new[] { "not json", "[]", """{"slots":1,"leases":[{"id":"x"}]}""", """{"slots":-1,"leases":[]}""" })
        {
            var unreadable = Assert.Throws<EngineException>(() => dialer.Engine(cloud).Init(Runner.InitInfo(0, new Dictionary<string, string?> { [variable] = raw })));
            Assert.Contains("prepared nothing for target \"web\"", unreadable.Message, StringComparison.Ordinal);
        }

        var outside = Assert.Throws<EngineException>(() => dialer.Engine(cloud).Init(Runner.InitInfo(1, new Dictionary<string, string?> { [variable] = env[variable] })));
        Assert.Contains("worker slot 1 is outside the 1 browser(s) \"toy-cloud\" leased", outside.Message, StringComparison.Ordinal);
        Assert.Empty(dialer.Endpoints);
    }

    [Fact]
    public async Task Leases_a_replacement_in_the_worker_once_its_browser_dropped_releases_only_what_the_worker_acquired_and_the_rest_on_dispose()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (runner, env) = await Runner.PreparedAsync(cloud, 2);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        await Runner.RunAttemptAsync(worker, "a1");

        // A connected browser is kept across attempts: no new lease.
        Assert.Equal(2, cloud.Acquired.Count);

        // The browser drops while the next attempt rides it.
        var second = await worker.StartAsync(Runner.Attempt("a2"), None);
        dialer.Browsers[1].Drop();
        await second.DisposeAsync();
        Assert.Equal(2, cloud.Acquired.Count);

        var third = await worker.StartAsync(Runner.Attempt("a3"), None);
        Assert.Equal(["wss://0.example", "wss://0.example", "wss://2.example"], dialer.Endpoints);

        // The replacement is asked for the same slot, from the worker, with no attempt id.
        Assert.Equal((0, 2, "run-1", "web"), (cloud.Acquired[^1].Slot, cloud.Acquired[^1].Slots, cloud.Acquired[^1].RunId, cloud.Acquired[^1].TargetName));
        Assert.Null(cloud.Acquired[^1].AttemptId);

        // The dead lease came from prepare: the runner's finish releases it, not the worker.
        Assert.Empty(cloud.Released);
        dialer.Browsers[2].Drop();
        await third.DisposeAsync();

        await using var fourth = await worker.StartAsync(Runner.Attempt("a4"), None);

        // A dead replacement is the worker's own to release.
        Assert.Equal(["lease-2"], cloud.ReleasedIds);
        await fourth.DisposeAsync();
        await worker.DisposeAsync();
        Assert.Equal(["lease-2", "lease-3"], cloud.ReleasedIds);

        await runner.FinishAsync(Runner.FinishInfo(), None);
        Assert.Equal(["lease-2", "lease-3", "lease-0", "lease-1"], cloud.ReleasedIds.Take(4));
    }

    [Fact]
    public async Task Reports_the_progress_a_replacement_lease_logs_through_the_workers_line_to_the_reporter()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var lines = new List<string>();
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env, lines.Add));
        await Runner.RunAttemptAsync(worker, "a1");
        Assert.Empty(lines);
        var second = await worker.StartAsync(Runner.Attempt("a2"), None);
        dialer.Browsers[1].Drop();
        await second.DisposeAsync();
        await Runner.RunAttemptAsync(worker, "a3");
        Assert.Equal(["toy-cloud: starting"], lines);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task Gives_back_a_replacement_that_arrives_after_the_worker_was_disposed_and_never_commits_it()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var first = await worker.StartAsync(Runner.Attempt("a1"), None);
        dialer.Browsers[0].Drop();
        await first.DisposeAsync();
        var granted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = false;
        cloud.OnAcquire = async (request, token) =>
        {
            requested = true;
            await granted.Task;
            return await cloud.Grant(request);
        };
        var starting = Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a2"), None));
        await PollAsync(() => requested);
        await worker.DisposeAsync();
        Assert.Empty(cloud.Released);
        granted.SetResult();
        var error = await starting;
        Assert.Equal("CANCELLED", error.Code);
        await PollAsync(() => cloud.ReleasedIds.SequenceEqual(["lease-1"]));
        Assert.Equal(["wss://0.example"], dialer.Endpoints);
    }

    [Fact]
    public async Task Fails_the_attempt_start_naming_the_provider_when_no_replacement_can_be_leased()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var first = await worker.StartAsync(Runner.Attempt("a1"), None);
        dialer.Browsers[0].Drop();
        await first.DisposeAsync();
        cloud.OnAcquire = (request, token) => throw new InvalidOperationException("quota exhausted");
        var error = await Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(Runner.Attempt("a2"), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not lease a browser: quota exhausted", error.Message, StringComparison.Ordinal);
        await worker.DisposeAsync();
        Assert.Empty(cloud.Released);
    }

    private static async Task PollAsync(Func<bool> condition)
    {
        for (var tries = 0; !condition() && tries < 200; tries++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    /// <summary>An acquire that throws before it returns a task.</summary>
    private sealed class SyncProvider : IBrowserProvider
    {
        public string Name => "sync";

        public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken) =>
            request.Slot == 1
                ? throw new InvalidOperationException("token missing")
                : Task.FromResult(new BrowserLease { Id = $"l-{request.Slot}", CdpEndpoint = "wss://d.example" });

        public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
