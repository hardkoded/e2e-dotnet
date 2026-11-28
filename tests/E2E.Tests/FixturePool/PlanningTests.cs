// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.FixturePool.FixturePoolSupport;
using Pool = E2E.FixturePool;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('planning')</c> (<c>tests/unit/fixture-pool.test.ts</c>): which fixtures a piece of test code needs, and in what order.</summary>
public sealed class PlanningTests
{
    [Fact]
    public async Task Sets_up_only_the_fixtures_the_test_names()
    {
        var log = new Log();
        var bound = Attempt([Logged(log, "a", "A"), Logged(log, "b", "B"), Logged(log, "c", "C")], ["b"]);
        await bound.SetUpAsync();
        Assert.Equal(["setup:b"], log.Lines);
        // The body is handed the fixtures it names, and nothing else.
        Assert.Equal(Obj(("b", "B")), await bound.Body());
    }

    [Fact]
    public async Task Hands_a_function_a_built_in_when_it_destructures_it_not_before()
    {
        var reads = new List<string>();
        var builtIns = new Dictionary<string, Func<object?>>
        {
            ["app"] = () =>
            {
                reads.Add("app");
                return "the app";
            },
            ["platform"] = () => "toy",
        };
        var chain = new[] { Logged(new Log(), "a", "A") };
        var test = TestNaming(chain, ["app", "a"], _ => Task.CompletedTask);
        var bound = Pool.BindFixtures(
            new BindOptions(
                FixturePlanner.PlanAttempt(ToyTarget(), test, []),
                Pool.AttemptBuiltIns(builtIns),
                new WorkerFixturePool(ToyTarget())
            )
        );
        await bound.SetUpAsync();
        await bound.CallAsync(test);
        Assert.Empty(reads);
        var reader = TestNaming(chain, ["app", "a"], fixtures =>
        {
            reads.Add($"body:{fixtures["app"]}:{fixtures["a"]}");
            return Task.CompletedTask;
        });
        var again = Pool.BindFixtures(
            new BindOptions(
                FixturePlanner.PlanAttempt(ToyTarget(), reader, []),
                Pool.AttemptBuiltIns(builtIns),
                new WorkerFixturePool(ToyTarget())
            )
        );
        await again.SetUpAsync();
        await again.CallAsync(reader);
        Assert.Equal(["app", "body:the app:A"], reads);
    }

    [Fact]
    public async Task Sets_up_nothing_for_a_test_that_names_no_fixture()
    {
        var log = new Log();
        var bound = Attempt(
            [Logged(log, "a", "A"), Logged(log, "database", "D", new FixtureOptions(Scope: FixtureScope.Worker))],
            []
        );
        await bound.SetUpAsync();
        Assert.Empty(log.Lines);
        Assert.Empty(bound.Teardowns());
    }

    [Fact]
    public async Task Sets_up_what_a_fixture_names_before_it_through_every_fixture_in_between()
    {
        var log = new Log();
        var seen = new List<object?>();
        var chain = new[]
        {
            Logged(log, "unused", "U"),
            Logged(log, "server", "S", new FixtureOptions(Scope: FixtureScope.Worker)),
            Logged(log, "client", "C", new FixtureOptions(Layer: 1, Uses: ["server"])),
            Fixture(
                "page",
                async (fixtures, use) =>
                {
                    log.Add("setup:page");
                    seen.Add(fixtures["client"]);
                    seen.Add(fixtures["platform"]);
                    await use("P");
                },
                new FixtureOptions(Layer: 2, Uses: ["client", "platform"])
            ),
        };
        var bound = Attempt(chain, ["page"]);
        await bound.SetUpAsync();
        Assert.Equal(["setup:server", "setup:client", "setup:page"], log.Lines);
        Assert.Equal(["C", "toy"], seen);
        // The worker's `server` is the pool's to tear down.
        Assert.Equal(["page", "client"], bound.Teardowns().Select(teardown => teardown.Name));
    }

    [Fact]
    public async Task Orders_the_fixtures_of_one_extend_call_by_what_they_name_whatever_order_they_are_written_in()
    {
        var log = new Log();
        var chain = new[]
        {
            Logged(log, "member", "M", new FixtureOptions(Uses: ["owner"])),
            Logged(log, "owner", "O"),
            Logged(log, "other", "X"),
        };
        await Attempt(chain, ["member", "other"]).SetUpAsync();
        Assert.Equal(["setup:owner", "setup:member", "setup:other"], log.Lines);
    }

    [Fact]
    public async Task Sets_up_a_fixture_written_later_before_the_one_that_names_it_after_an_engine_fixture()
    {
        var log = new Log();
        var seen = new List<object?>();
        FixtureDefinition Reading(string name, string[] uses) =>
            Fixture(
                name,
                async (fixtures, use) =>
                {
                    log.Add($"setup:{name}");
                    seen.Add(fixtures.TryGetValue("owner", out var owner) ? owner : null);
                    await use(name);
                },
                new FixtureOptions(Uses: uses)
            );
        var chain = new[]
        {
            Reading("afterBuiltIn", ["app", "owner"]),
            Reading("afterEngineFixture", ["gadget", "owner"]),
            Reading("afterEngineUser", ["afterBuiltIn", "late"]),
            Logged(log, "owner", "O"),
            Logged(log, "late", "L"),
        };
        var names = new[] { "afterBuiltIn", "afterEngineFixture", "afterEngineUser" };
        var plan = FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, names), []);
        Assert.Equal(["owner", "afterBuiltIn", "afterEngineFixture", "late", "afterEngineUser"], PlannedNames(plan));
        Assert.True(plan.UsesEngine);
        await Attempt(
                chain,
                names,
                new AttemptOptions
                {
                    BuiltIns = Pool.AttemptBuiltIns(
                        Obj(("app", new object()), ("gadget", new object()), ("platform", "toy"))
                    ),
                }
            )
            .SetUpAsync();
        Assert.Equal(
            ["setup:owner", "setup:afterBuiltIn", "setup:afterEngineFixture", "setup:late", "setup:afterEngineUser"],
            log.Lines
        );
        Assert.Equal(["O", "O", null], seen);
    }

    [Fact]
    public async Task Resolves_the_attempt_as_one_unit_what_its_beforeEach_and_afterEach_hooks_name_is_set_up_with_what_the_body_names()
    {
        var log = new Log();
        var chain = new[]
        {
            Logged(log, "a", "A"),
            Logged(log, "b", "B"),
            Logged(log, "c", "C"),
            Logged(log, "d", "D"),
        };
        var bound = Attempt(
            chain,
            ["b"],
            new AttemptOptions
            {
                Hooks = [TestHookNaming("beforeEach", chain, ["a"]), TestHookNaming("afterEach", chain, ["c"])],
            }
        );
        await bound.SetUpAsync();
        Assert.Equal(["setup:a", "setup:b", "setup:c"], log.Lines);
    }

    [Fact]
    public async Task Sets_an_auto_fixture_up_for_every_test_and_its_worker_scoped_kind_once_per_worker()
    {
        var log = new Log();
        var target = ToyTarget();
        var pool = new WorkerFixturePool(target);
        var chain = new[]
        {
            Logged(log, "install", null, new FixtureOptions(Scope: FixtureScope.Worker, Auto: true)),
            Logged(log, "needed", "N"),
            Logged(log, "watch", null, new FixtureOptions(Auto: true, Layer: 1, Uses: ["needed"])),
            Logged(log, "idle", "I", new FixtureOptions(Layer: 1)),
        };
        foreach (var _ in new[] { 1, 2 })
        {
            var bound = Attempt(chain, [], new AttemptOptions { Pool = pool, Target = target });
            await bound.SetUpAsync();
            foreach (var teardown in bound.Teardowns())
            {
                await teardown.Run();
            }
        }

        Assert.Equal(
            [
                "setup:install",
                "setup:needed",
                "setup:watch",
                "teardown:watch",
                "teardown:needed",
                "setup:needed",
                "setup:watch",
                "teardown:watch",
                "teardown:needed",
            ],
            log.Lines
        );
    }

    [Fact]
    public void Names_the_cycle_when_fixtures_need_each_other()
    {
        var chain = new[]
        {
            Logged(new Log(), "a", "A", new FixtureOptions(Uses: ["b"])),
            Logged(new Log(), "b", "B", new FixtureOptions(Uses: ["c"])),
            Logged(new Log(), "c", "C", new FixtureOptions(Uses: ["a"])),
            Logged(new Log(), "d", "D", new FixtureOptions(Uses: ["d"])),
        };
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["b"]), []),
            "fixtures depend on each other in a cycle: \"b\" -> \"c\" -> \"a\" -> \"b\""
        );
        // A fixture that names itself extends the one before it, and here there is none.
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["d"]), []),
            "fixture \"d\" names \"d\": target \"toy\" has no \"d\" fixture; available: a, agent, app, b, c, gadget, platform, request, screen, services, session"
        );
        // Found where the fixtures are declared too, before any target is known.
        AssertPlanError(
            () => FixturePlanner.CheckDeclaredFixtures(chain, chain),
            "cycle: \"a\" -> \"b\" -> \"c\" -> \"a\""
        );
        // A cycle nothing names is not this test's.
        Assert.Equal(
            ["e"],
            PlannedNames(
                FixturePlanner.PlanAttempt(
                    ToyTarget(),
                    TestNaming([.. chain, Logged(new Log(), "e", "E", new FixtureOptions(Layer: 1))], ["e"]),
                    []
                )
            )
        );
    }

    [Fact]
    public void Refuses_a_name_the_target_has_no_fixture_for_with_the_nearest_name_or_the_e2e_way()
    {
        var chain = new[] { Logged(new Log(), "workspace", "W") };
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["workspce"]), []),
            "test \"suite > case\" names \"workspce\": target \"toy\" has no \"workspce\" fixture; available: agent, app, gadget, platform, request, screen, services, session, workspace (engine toy contributes gadget); did you mean \"workspace\"?"
        );
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["page"]), []),
            "there is no Playwright page: open the app with app.open()"
        );
        AssertPlanError(
            () =>
                FixturePlanner.PlanAttempt(
                    ToyTarget(),
                    TestNaming([], []),
                    [TestHookNaming("beforeEach", chain, ["workspace"])]
                ),
            "beforeEach hook names \"workspace\": target \"toy\" has no \"workspace\" fixture"
        );
        AssertPlanError(
            () =>
                FixturePlanner.PlanAttempt(
                    ToyTarget(),
                    TestNaming([Logged(new Log(), "a", "A", new FixtureOptions(Uses: ["nope"]))], ["a"]),
                    []
                ),
            "fixture \"a\" names \"nope\": target \"toy\" has no \"nope\" fixture"
        );
    }

    [Fact]
    public void Refuses_a_fixture_that_names_one_a_later_extend_declares()
    {
        var chain = new[]
        {
            Logged(new Log(), "early", "E", new FixtureOptions(Uses: ["late"])),
            Logged(new Log(), "late", "L", new FixtureOptions(Layer: 1)),
        };
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["early"]), []),
            "fixture \"early\" names \"late\", which a later test.extend() declares"
        );
    }

    [Fact]
    public void Refuses_a_worker_fixture_that_names_a_test_fixture_where_it_is_declared_when_the_scope_is_known_by_then()
    {
        var page = Logged(new Log(), "page", "P");
        var cache = Logged(
            new Log(),
            "cache",
            "C",
            new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1, Uses: ["page"])
        );
        AssertPlanError(
            () => FixturePlanner.CheckDeclaredFixtures([page, cache], [cache]),
            "worker fixture \"cache\" names \"page\", a test fixture: beforeAll, afterAll, and worker fixtures see worker fixtures only"
        );
        var session = Logged(new Log(), "token", "T", new FixtureOptions(Scope: FixtureScope.Worker, Uses: ["agent"]));
        AssertPlanError(
            () => FixturePlanner.CheckDeclaredFixtures([session], [session]),
            "worker fixture \"token\" names \"agent\", a test fixture"
        );
        // `app` is a test fixture on one engine and a worker fixture on another: decided per target.
        var opener = Logged(
            new Log(),
            "opener",
            "O",
            new FixtureOptions(Scope: FixtureScope.Worker, Uses: ["app", "gadget"])
        );
        FixturePlanner.CheckDeclaredFixtures([opener], [opener]);
        AssertPlanError(
            () => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming([opener], ["opener"]), []),
            "worker fixture \"opener\" names \"app\", a test fixture on target \"toy\""
        );
        Assert.Equal(
            ["opener"],
            PlannedNames(
                FixturePlanner.PlanAttempt(ToyTarget(EngineIsolationWorker), TestNaming([opener], ["opener"]), [])
            )
        );
    }

    [Fact]
    public void Carries_where_the_offending_code_was_declared()
    {
        var chain = new[] { Logged(new Log(), "a", "A", new FixtureOptions(Uses: ["nope"])) };
        static Internal.SourceLocation? DeclaredAt(Action plan)
        {
            try
            {
                plan();
            }
            catch (FixtureUseException cause)
            {
                return cause.DeclaredAt;
            }

            return null;
        }

        Assert.Equal(
            new Internal.SourceLocation("/repo/tests/support.ts", 7, 1),
            DeclaredAt(() => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["a"]), []))
        );
        Assert.Equal(
            new Internal.SourceLocation("/repo/tests/a.e2e.ts", 3, 1),
            DeclaredAt(() => FixturePlanner.PlanAttempt(ToyTarget(), TestNaming(chain, ["nope"]), []))
        );
        Assert.Equal(
            new Internal.SourceLocation("/repo/tests/a.e2e.ts", 9, 1),
            DeclaredAt(() => FixturePlanner.PlanSuiteHook(ToyTarget(), SuiteHook(chain, ["app"])))
        );
    }

    [Fact]
    public void Says_whether_the_code_reaches_the_engine_itself_or_through_a_fixture_it_names()
    {
        var target = ToyTarget(EngineIsolationWorker);
        var chain = new[]
        {
            Logged(new Log(), "database", "D", new FixtureOptions(Scope: FixtureScope.Worker, Uses: ["platform"])),
            Logged(new Log(), "keeper", "K", new FixtureOptions(Scope: FixtureScope.Worker, Uses: ["gadget"])),
            Logged(
                new Log(),
                "holder",
                "H",
                new FixtureOptions(Scope: FixtureScope.Worker, Layer: 1, Uses: ["keeper"])
            ),
        };
        bool UsesEngine(params string[] names) =>
            FixturePlanner.PlanSuiteHook(target, SuiteHook(chain, names)).UsesEngine;
        Assert.False(UsesEngine());
        Assert.False(UsesEngine("platform", "database"));
        Assert.True(UsesEngine("app"));
        Assert.True(UsesEngine("gadget"));
        Assert.True(UsesEngine("keeper"));
        Assert.True(UsesEngine("holder"));
        var plan = FixturePlanner.PlanSuiteHook(target, SuiteHook(chain, ["database", "holder"]));
        Assert.Equal(
            [("database", false), ("keeper", true), ("holder", true)],
            plan.Order.Select(planned => (planned.Definition.Name, planned.UsesEngine))
        );
        // An auto worker fixture that drives the device makes every suite hook of its chain need the engine.
        Assert.True(
            FixturePlanner
                .PlanSuiteHook(
                    target,
                    SuiteHook(
                        [
                            Logged(
                                new Log(),
                                "install",
                                null,
                                new FixtureOptions(Scope: FixtureScope.Worker, Auto: true, Uses: ["gadget"])
                            ),
                        ],
                        []
                    )
                )
                .UsesEngine
        );
    }

    private const string EngineIsolationWorker = E2E.Engine.EngineIsolation.Worker;
}
