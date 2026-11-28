// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.FixturePool.FixturePoolSupport;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('test fixtures')</c> (<c>tests/unit/fixture-pool.test.ts</c>).</summary>
public sealed class TestFixturesTests
{
    [Fact]
    public async Task Retains_cleanup_when_the_attempt_aborts_immediately_after_use_provides_a_value()
    {
        using var abort = new CancellationTokenSource();
        var log = new Log();
        var bound = Attempt([Fixture("ready", async (_, use) =>
                {
                    var released = use("ready");
                    // abort() runs its listeners at once; CancelAsync would hand them to another thread and let setup finish first.
                    abort.Cancel();
                    await released;
                    log.Add("teardown:ready");
                }), Logged(log, "later", "later")], ["ready", "later"], new AttemptOptions { Signal = abort.Token });
        var failure = await Assert.ThrowsAsync<ConfigurationException>(() => bound.SetUpAsync());
        Assert.Equal("TEST_SETUP_FAILED", failure.Code);
        var teardowns = bound.Teardowns();
        Assert.Equal(["ready"], teardowns.Select(teardown => teardown.Name));
        await teardowns[0].Run();
        Assert.Equal(["teardown:ready"], log.Lines);
    }

    [Fact]
    public async Task Abandons_setup_on_abort_before_teardown_is_taken_while_keeping_a_provided_fixture_for_cleanup()
    {
        using var abort = new CancellationTokenSource();
        var waiting = new TaskCompletionSource();
        var log = new Log();
        var bound = Attempt([Logged(log, "ready", "ready"), Fixture("slow", async (_, use) =>
                {
                    log.Add("setup:slow");
                    await waiting.Task;
                    await use("slow");
                    log.Add("teardown:slow");
                }), Logged(log, "later", "later")], ["ready", "slow", "later"], new AttemptOptions { Signal = abort.Token });
        var continuation = Task.Run(async () =>
        {
            await bound.SetUpAsync();
            log.Add("body");
        });
        var stopped = Assert.ThrowsAsync<ConfigurationException>(() => continuation);
        await WaitFor(() => Assert.Equal(["setup:ready", "setup:slow"], log.Lines));
        await abort.CancelAsync();
        waiting.SetResult();
        Assert.Equal("TEST_SETUP_FAILED", (await stopped).Code);
        await WaitFor(() => Assert.Equal(["setup:ready", "setup:slow", "teardown:slow"], log.Lines));
        var teardowns = bound.Teardowns();
        Assert.Equal(["ready"], teardowns.Select(teardown => teardown.Name));
        await teardowns[0].Run();
        Assert.Equal("teardown:ready", log.Lines[^1]);
    }

    [Fact]
    public async Task Stops_abandoned_setup_before_later_fixtures_or_the_body_while_releasing_the_late_fixture()
    {
        var log = new Log();
        var waiting = new TaskCompletionSource();
        var bound = Attempt([Fixture("slow", async (_, use) =>
                {
                    log.Add("setup:slow");
                    await waiting.Task;
                    await use("slow");
                    log.Add("teardown:slow");
                }), Logged(log, "later", "later")], ["slow", "later"]);
        var continuation = Task.Run(async () =>
        {
            await bound.SetUpAsync();
            log.Add("body");
        });
        var stopped = Assert.ThrowsAsync<ConfigurationException>(() => continuation);
        await Task.Yield();
        Assert.Empty(bound.Teardowns());
        waiting.SetResult();
        Assert.Equal("TEST_SETUP_FAILED", (await stopped).Code);
        await WaitFor(() => Assert.Equal(["setup:slow", "teardown:slow"], log.Lines));
    }

    [Fact]
    public async Task Tears_down_the_fixtures_set_up_before_a_later_one_throws_last_first_and_says_which_hooks_can_still_run()
    {
        var log = new Log();
        var chain = new[] { Logged(log, "a", "A"), Logged(log, "b", "B"), Fixture("c", (_, _) =>
            {
                log.Add("setup:c");
                throw new InvalidOperationException("c failed");
            }), Logged(log, "d", "D") };
        var hooks = new[] { new[] { "a", "platform" }, ["a", "c"], ["d"] }
            .Select(names => TestHookNaming("afterEach", chain, names))
            .ToArray();
        var (setUp, failed, neverReached) = (hooks[0], hooks[1], hooks[2]);
        var bound = Attempt(chain, ["a", "b", "c", "d"], new AttemptOptions { Hooks = hooks });
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => bound.SetUpAsync());
        Assert.Contains("c failed", failure.Message);
        var teardowns = bound.Teardowns();
        Assert.Equal(["b", "a"], teardowns.Select(teardown => teardown.Name));
        foreach (var teardown in teardowns)
        {
            await teardown.Run();
        }

        Assert.Equal(["setup:a", "setup:b", "setup:c", "teardown:b", "teardown:a"], log.Lines);
        // An afterEach hook that names a fixture whose setup failed, or was never reached, is skipped.
        Assert.Equal([true, false, false], new[] { setUp, failed, neverReached }.Select(bound.Ready));
        Assert.Equal(Obj(("a", "A"), ("platform", "toy")), await Call(bound.Bound, setUp));
    }
}
