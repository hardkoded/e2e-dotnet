// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.FixturePool.FixturePoolSupport;
using Pool = E2E.FixturePool;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('fixtures that extend a built-in')</c> (<c>tests/unit/fixture-pool.test.ts</c>).</summary>
public sealed class FixturesThatExtendABuiltInTests
{
    /// <summary>A fixture named <paramref name="name"/> that names itself, wraps what it received as <c>{ wraps, by }</c>, and logs.</summary>
    private static FixtureDefinition Wrapping(Log log, string name, string by, FixtureOptions? options = null)
    {
        options ??= new FixtureOptions(Scope: null);
        return Fixture(
            name,
            async (fixtures, use) =>
            {
                log.Add($"setup:{by}");
                await use(Obj(("wraps", fixtures[name]), ("by", by)));
                log.Add($"teardown:{by}");
            },
            options with
            {
                Uses = [name, .. options.Uses ?? []],
            }
        );
    }

    [Fact]
    public async Task Hands_the_built_in_to_the_override_under_its_own_name_and_the_override_to_everyone_else()
    {
        var log = new Log();
        var original = new object();
        var seen = new List<object?>();
        var chain = new[]
        {
            Fixture(
                "reader",
                async (fixtures, use) =>
                {
                    seen.Add(fixtures["app"]);
                    await use("R");
                },
                new FixtureOptions(Uses: ["app"])
            ),
            Wrapping(log, "app", "override", new FixtureOptions(Scope: null, Layer: 1)),
        };
        var builtIns = Obj(("app", original), ("platform", "toy"));
        var plan = FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["app", "reader"]), []);
        Assert.Equal(["app", "reader"], PlannedNames(plan));
        Assert.Equal("test", PlannedScope(plan, chain[1]));
        var bound = Attempt(chain, ["app", "reader"], new AttemptOptions { BuiltIns = Pool.AttemptBuiltIns(builtIns) });
        await bound.SetUpAsync();
        var wrapped = Obj(("wraps", original), ("by", "override"));
        Assert.Equal(Obj(("app", wrapped), ("reader", "R")), await bound.Body());
        // A fixture of an earlier extend() that names the built-in gets the override too.
        Assert.Equal([wrapped], seen);
        // The attempt's own built-ins, which the runner reads, are untouched.
        Assert.Same(original, builtIns["app"]);
        foreach (var teardown in bound.Teardowns())
        {
            await teardown.Run();
        }

        Assert.Equal(["setup:override", "teardown:override"], log.Lines);
    }

    [Fact]
    public async Task Sets_the_override_up_only_for_code_that_names_the_built_in()
    {
        var log = new Log();
        var chain = new[] { Wrapping(log, "app", "override") };
        var bound = Attempt(
            chain,
            ["platform"],
            new AttemptOptions { BuiltIns = Pool.AttemptBuiltIns(Obj(("app", "original"), ("platform", "toy"))) }
        );
        await bound.SetUpAsync();
        Assert.Empty(log.Lines);
        Assert.Equal(Obj(("platform", "toy")), await bound.Body());
    }

    [Fact]
    public async Task Chains_overrides_each_receives_the_one_before_it()
    {
        var log = new Log();
        var chain = new[]
        {
            Wrapping(log, "app", "first"),
            Wrapping(log, "app", "second", new FixtureOptions(Scope: null, Layer: 1)),
            Wrapping(log, "app", "third", new FixtureOptions(Scope: FixtureScope.Test, Layer: 2)),
        };
        var bound = Attempt(
            chain,
            ["app"],
            new AttemptOptions { BuiltIns = Pool.AttemptBuiltIns(Obj(("app", "original"), ("platform", "toy"))) }
        );
        await bound.SetUpAsync();
        Assert.Equal(
            Obj(
                (
                    "app",
                    Obj(
                        ("by", "third"),
                        ("wraps", Obj(("by", "second"), ("wraps", Obj(("by", "first"), ("wraps", "original")))))
                    )
                )
            ),
            await bound.Body()
        );
        foreach (var teardown in bound.Teardowns())
        {
            await teardown.Run();
        }

        Assert.Equal(
            ["setup:first", "setup:second", "setup:third", "teardown:third", "teardown:second", "teardown:first"],
            log.Lines
        );
    }

    [Fact]
    public async Task Replaces_the_built_in_for_user_code_when_the_override_does_not_name_it()
    {
        var chain = new[] { Fixture("app", (_, use) => use("mine"), new FixtureOptions(Scope: null)) };
        var bound = Attempt(
            chain,
            ["app"],
            new AttemptOptions { BuiltIns = Pool.AttemptBuiltIns(Obj(("app", "original"), ("platform", "toy"))) }
        );
        await bound.SetUpAsync();
        Assert.Equal(Obj(("app", "mine")), await bound.Body());
    }

    [Fact]
    public async Task Runs_an_override_declared_with_scope_worker_once_per_worker_where_the_engines_state_outlives_a_test()
    {
        var log = new Log();
        var target = ToyTarget(E2E.Engine.EngineIsolation.Worker);
        var pool = new WorkerFixturePool(target);
        var chain = new[] { Wrapping(log, "gadget", "override", new FixtureOptions(Scope: FixtureScope.Worker)) };
        Dictionary<string, object?> GadgetOf(string id) => Obj(("gadget", new Gadget(id)), ("platform", "toy"));
        string Read(Dictionary<string, object?> fixtures) =>
            (string)((ForwardedBuiltIn)((Dictionary<string, object?>)fixtures["gadget"]!)["wraps"]!).Call("Id")!;

        var inHook = Pool.AttemptBuiltIns(GadgetOf("hook"));
        var leaveHook = pool.Enter(inHook);
        var hook = SuiteHook(chain, ["gadget"]);
        var hookPlan = FixturePlanner.PlanSuiteHook(target, hook);
        Assert.Equal(FixtureScope.Worker, PlannedScope(hookPlan, chain[0]));
        Assert.True(hookPlan.UsesEngine);
        var boundHook = Pool.BindFixtures(new BindOptions(hookPlan, inHook, pool));
        await boundHook.SetUpAsync();
        var fromHook = await Call(boundHook, hook);
        Assert.Equal("hook", Read(fromHook));
        leaveHook();

        // One instance for the worker: the next attempt gets it, and what it wraps follows the attempt.
        var inAttempt = Pool.AttemptBuiltIns(GadgetOf("attempt"));
        pool.Enter(inAttempt);
        var bound = Attempt(
            chain,
            ["gadget"],
            new AttemptOptions
            {
                Pool = pool,
                Target = target,
                BuiltIns = inAttempt,
            }
        );
        await bound.SetUpAsync();
        var fromBody = await bound.Body();
        Assert.Same(fromHook["gadget"], fromBody["gadget"]);
        Assert.Equal("attempt", Read(fromBody));
        Assert.Empty(bound.Teardowns());
        Assert.Equal(["setup:override"], log.Lines);
        Assert.Equal([("gadget", true)], pool.Close().Select(teardown => (teardown.Name, teardown.UsesEngine)));
        // On an engine that isolates each attempt, the built-in is a test fixture, which a worker fixture cannot name.
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["gadget"]), []),
            "worker fixture \"gadget\" names \"gadget\", a test fixture on target \"toy\""
        );
    }

    [Fact]
    public async Task Makes_an_override_a_test_fixture_unless_it_declares_a_scope_even_where_the_built_in_is_a_worker_fixture()
    {
        var log = new Log();
        var chain = new[] { Wrapping(log, "gadget", "override") };
        var target = ToyTarget(E2E.Engine.EngineIsolation.Worker);
        Assert.Equal(
            FixtureScope.Test,
            PlannedScope(FixturePlanner.PlanAttempt(target, TestNaming(chain, ["gadget"]), []), chain[0])
        );
        AssertPlanError(
            () => FixturePlanner.PlanSuiteHook(target, SuiteHook(chain, ["gadget"])),
            "beforeAll hook names \"gadget\", a test fixture on target \"toy\""
        );
        var test = new[] { Wrapping(log, "gadget", "override", new FixtureOptions(Scope: FixtureScope.Test)) };
        Assert.Equal(
            FixtureScope.Test,
            PlannedScope(FixturePlanner.PlanAttempt(target, TestNaming(test, ["gadget"]), []), test[0])
        );
        // An override of an earlier fixture of the chain takes no scope from it either.
        var chained = new[]
        {
            Wrapping(new Log(), "app", "first", new FixtureOptions(Scope: FixtureScope.Test)),
            Wrapping(new Log(), "app", "second", new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1)),
        };
        AssertPlanError(
            () => FixturePlanner.CheckDeclaredFixtures(chained, [chained[1]]),
            "worker fixture \"app\" names \"app\", a test fixture"
        );

        var bound = Attempt(
            chain,
            ["gadget"],
            new AttemptOptions
            {
                Target = target,
                BuiltIns = Pool.AttemptBuiltIns(Obj(("gadget", "original"), ("platform", "toy"))),
            }
        );
        await bound.SetUpAsync();
        Assert.Equal(Obj(("gadget", Obj(("wraps", "original"), ("by", "override")))), await bound.Body());
        foreach (var teardown in bound.Teardowns())
        {
            await teardown.Run();
        }

        Assert.Equal(["setup:override", "teardown:override"], log.Lines);
    }

    [Fact]
    public void Lets_a_worker_fixture_name_an_extended_app_only_through_a_worker_scoped_override()
    {
        var chain = new[]
        {
            Wrapping(new Log(), "app", "first", new FixtureOptions(Scope: FixtureScope.Worker)),
            Logged(new Log(), "seeded", "S", new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1, Uses: ["app"])),
        };
        FixturePlanner.CheckDeclaredFixtures(chain, chain);
        Assert.Equal(
            ["app", "seeded"],
            PlannedNames(
                FixturePlanner.PlanAttempt(
                    ToyTarget(E2E.Engine.EngineIsolation.Worker),
                    TestNaming(chain, ["seeded"]),
                    []
                )
            )
        );
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["seeded"]), []),
            "worker fixture \"app\" names \"app\", a test fixture on target \"toy\""
        );
        var testScoped = new[]
        {
            Wrapping(new Log(), "app", "first"),
            Logged(new Log(), "seeded", "S", new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1, Uses: ["app"])),
        };
        AssertPlanError(
            () =>
                FixturePlanner.PlanAttempt(
                    ToyTarget(E2E.Engine.EngineIsolation.Worker),
                    TestNaming(testScoped, ["seeded"]),
                    []
                ),
            "worker fixture \"seeded\" names \"app\", a test fixture on target \"toy\""
        );
    }

    [Fact]
    public async Task Sets_an_auto_fixture_up_through_an_override_that_names_it_and_the_override_only_if_it_declares_auto()
    {
        var log = new Log();
        var named = new[]
        {
            Logged(log, "seed", "base", new FixtureOptions(Scope: null, Auto: true)),
            Wrapping(log, "seed", "override", new FixtureOptions(Scope: null, Auto: null, Layer: 1)),
        };
        Assert.Empty(FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(named, []), []).Order);
        var chain = new[]
        {
            Logged(log, "seed", "base", new FixtureOptions(Scope: null, Auto: true)),
            Wrapping(log, "seed", "override", new FixtureOptions(Scope: null, Auto: true, Layer: 1)),
        };
        var bound = Attempt(chain, []);
        await bound.SetUpAsync();
        Assert.Equal(["setup:seed", "setup:override"], log.Lines);
        foreach (var teardown in bound.Teardowns())
        {
            await teardown.Run();
        }

        Assert.Equal(["setup:seed", "setup:override", "teardown:override", "teardown:seed"], log.Lines);
    }

    [Fact]
    public async Task Does_not_set_up_an_auto_fixture_that_an_override_replaces_without_naming_it()
    {
        var log = new Log();
        var chain = new[]
        {
            Logged(log, "seed", "base", new FixtureOptions(Scope: null, Auto: true)),
            Logged(log, "seed", "replacement", new FixtureOptions(Scope: null, Auto: true, Layer: 1)),
        };
        var bound = Attempt(chain, []);
        await bound.SetUpAsync();
        // One setup, the replacement's: `Logged` logs the fixture's name, which both share.
        Assert.Equal(["setup:seed"], log.Lines);
        Assert.Equal(
            [chain[1]],
            FixturePlanner
                .PlanAttempt(ToyTarget(), TestNaming(chain, []), [])
                .Order.Select(planned => planned.Definition)
        );
    }

    [Fact]
    public void Makes_an_override_of_a_built_in_auto_when_it_declares_auto()
    {
        var eager = new[] { Wrapping(new Log(), "app", "override", new FixtureOptions(Scope: null, Auto: true)) };
        FixturePlanner.CheckDeclaredFixtures(eager, eager);
        Assert.Equal(
            eager,
            FixturePlanner
                .PlanAttempt(ToyTarget(), TestNaming(eager, []), [])
                .Order.Select(planned => planned.Definition)
        );
    }

    [Fact]
    public void Fails_on_a_target_that_lacks_the_built_in_only_when_code_there_names_it()
    {
        var chain = new[] { Wrapping(new Log(), "device", "override") };
        Assert.Empty(FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["app"]), []).Order);
        FixturePlanner.CheckDeclaredFixtures(chain, chain);
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["device"]), []),
            "fixture \"device\" names \"device\": target \"toy\" has no \"device\" fixture; available: agent, app, gadget, platform, request, screen, services, session (engine toy contributes gadget)"
        );
    }
}
