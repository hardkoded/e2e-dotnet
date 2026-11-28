// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.FixturePool.FixturePoolSupport;
using Pool = E2E.FixturePool;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('worker fixtures')</c> of <c>tests/unit/fixture-pool.test.ts</c>.</summary>
public sealed class WorkerFixturesTests
{
    [Fact]
    public async Task Sets_one_instance_up_for_every_attempt_of_the_worker_that_names_it_and_tears_it_down_when_the_pool_closes()
    {
        var log = new Log();
        var target = ToyTarget();
        var pool = new WorkerFixturePool(target);
        var chain = new[]
        {
            Logged(log, "database", Obj(("rows", 1)), new FixtureOptions(Scope: FixtureScope.Worker)),
            Logged(log, "unused", "U", new FixtureOptions(Scope: FixtureScope.Worker)),
            Logged(log, "page", "P"),
        };
        var database = chain[0];
        foreach (var _ in new[] { 1, 2 })
        {
            var bound = Attempt(chain, ["database", "page"], new AttemptOptions { Pool = pool, Target = target });
            await bound.SetUpAsync();
            Assert.Equal(Obj(("database", Obj(("rows", 1))), ("page", "P")), await bound.Body());
            foreach (var teardown in bound.Teardowns())
            {
                await teardown.Run();
            }
        }

        Assert.Equal(["setup:database", "setup:page", "teardown:page", "setup:page", "teardown:page"], log.Lines);
        var teardowns = pool.Close();
        Assert.Equal([("database", false)], teardowns.Select(teardown => (teardown.Name, teardown.UsesEngine)));
        foreach (var teardown in teardowns)
        {
            await teardown.Run();
        }

        Assert.Equal("teardown:database", log.Lines[^1]);
        Assert.Equal("database", database.Name);
    }

    [Fact]
    public async Task Tries_a_failed_setup_again_for_the_next_attempt_that_needs_it()
    {
        var target = ToyTarget();
        var pool = new WorkerFixturePool(target);
        var tries = 0;
        var flaky = Fixture(
            "flaky",
            async (_, use) =>
            {
                tries += 1;
                if (tries == 1)
                {
                    throw new InvalidOperationException("first start failed");
                }

                await use(tries);
            },
            new FixtureOptions(Scope: FixtureScope.Worker)
        );
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Attempt([flaky], ["flaky"], new AttemptOptions { Pool = pool, Target = target }).SetUpAsync()
        );
        Assert.Contains("first start failed", failure.Message);
        var second = Attempt([flaky], ["flaky"], new AttemptOptions { Pool = pool, Target = target });
        await second.SetUpAsync();
        Assert.Equal(Obj(("flaky", 2)), await second.Body());
    }

    [Fact]
    public async Task Hands_a_worker_fixture_the_worker_fixtures_it_names()
    {
        var target = ToyTarget();
        var seen = new List<string>();
        var chain = new[]
        {
            Logged(new Log(), "page", "P"),
            Logged(new Log(), "server", "S", new FixtureOptions(Scope: FixtureScope.Worker)),
            Fixture(
                "client",
                async (fixtures, use) =>
                {
                    seen.Add($"{fixtures["platform"]}:{fixtures["server"]}");
                    await use("C");
                },
                new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1, Uses: ["server", "platform"])
            ),
        };
        await Attempt(chain, ["client"], new AttemptOptions { Target = target }).SetUpAsync();
        Assert.Equal(["toy:S"], seen);
    }

    [Fact]
    public async Task Forwards_a_device_like_built_in_to_whichever_attempt_is_running_and_marks_the_teardown_as_needing_the_engine()
    {
        var target = ToyTarget(E2E.Engine.EngineIsolation.Worker);
        var pool = new WorkerFixturePool(target);
        ForwardedBuiltIn? held = null;
        var keeper = Fixture(
            "keeper",
            async (fixtures, use) =>
            {
                held = (ForwardedBuiltIn)fixtures["gadget"]!;
                await use("kept");
            },
            new FixtureOptions(Scope: FixtureScope.Worker, Uses: ["gadget"])
        );
        Dictionary<string, object?> GadgetOf(string id) => Obj(("gadget", new Gadget(id)), ("platform", "toy"));
        var first = Pool.AttemptBuiltIns(GadgetOf("first"));
        var leave = pool.Enter(first);
        await Attempt(
                [keeper],
                ["keeper"],
                new AttemptOptions
                {
                    Pool = pool,
                    Target = target,
                    BuiltIns = first,
                }
            )
            .SetUpAsync();
        Assert.Equal("first", held?.Call("Id"));
        leave();
        pool.Enter(Pool.AttemptBuiltIns(GadgetOf("second")));
        Assert.Equal("second", held?.Call("Id"));
        Assert.Equal([("keeper", true)], pool.Close().Select(teardown => (teardown.Name, teardown.UsesEngine)));
    }
}
