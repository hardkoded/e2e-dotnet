// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.FixturePool.FixturePoolSupport;
using Pool = E2E.FixturePool;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('suite hook fixtures')</c> (<c>tests/unit/fixture-pool.test.ts</c>).</summary>
public sealed class SuiteHookFixturesTests
{
    private static Exception NoAttempt(string name) =>
        new InvalidOperationException($"\"{name}\" has no attempt behind it");

    /// <summary>Binds a <c>beforeAll</c> hook that names <paramref name="names"/>; <paramref name="builtIns"/> stands in for its attempt, and without one it has none.</summary>
    private static (BoundFixtures Bound, Func<Task<Dictionary<string, object?>>> Hook) BindHook(
        BuiltInTarget target,
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<string> names,
        Dictionary<string, object?>? builtIns = null
    )
    {
        var hook = SuiteHook(chain, names);
        var bound = Pool.BindFixtures(
            new BindOptions(
                FixturePlanner.PlanSuiteHook(target, hook),
                builtIns is null
                    ? Pool.AttemptlessBuiltIns(target, new Dictionary<string, object?>(), NoAttempt)
                    : Pool.AttemptBuiltIns(builtIns),
                new WorkerFixturePool(target)
            )
        );
        return (bound, () => Call(bound, hook));
    }

    [Fact]
    public async Task Sets_up_the_worker_fixtures_the_hook_names_and_the_auto_worker_fixtures_and_no_test_fixture()
    {
        var log = new Log();
        var target = ToyTarget();
        var chain = new[]
        {
            Logged(log, "page", "P"),
            Logged(log, "watch", null, new FixtureOptions(Auto: true)),
            Logged(log, "install", null, new FixtureOptions(Scope: FixtureScope.Worker, Auto: true)),
            Logged(log, "database", "D", new FixtureOptions(Scope: FixtureScope.Worker)),
            Logged(log, "unused", "U", new FixtureOptions(Scope: FixtureScope.Worker)),
        };
        var (bound, hook) = BindHook(target, chain, ["database", "platform"]);
        await bound.SetUpAsync();
        Assert.Equal(["setup:install", "setup:database"], log.Lines);
        // With no attempt behind it, `platform` is still there: the target alone decides it.
        Assert.Equal(Obj(("database", "D"), ("platform", "toy")), await hook());
    }

    [Fact]
    public void Refuses_a_test_fixture_by_scope_before_anything_is_set_up()
    {
        var target = ToyTarget();
        var chain = new[]
        {
            Logged(new Log(), "page", "P"),
            Logged(new Log(), "database", "D", new FixtureOptions(Scope: FixtureScope.Worker)),
        };
        foreach (var name in new[] { "page", "gadget", "screen", "agent" })
        {
            AssertPlanError(
                () => FixturePlanner.PlanSuiteHook(target, SuiteHook(chain, ["database", name])),
                $"beforeAll hook names \"{name}\", a test fixture on target \"toy\": beforeAll, afterAll, and worker fixtures see worker fixtures only"
            );
        }

        AssertPlanError(
            () => FixturePlanner.PlanSuiteHook(target, SuiteHook(chain, ["nope"])),
            "beforeAll hook names \"nope\": target \"toy\" has no \"nope\" fixture"
        );
    }

    [Fact]
    public async Task Sees_the_engine_fixtures_app_and_screen_on_an_engine_whose_state_outlives_an_attempt()
    {
        var target = ToyTarget(EngineIsolation.Worker);
        var builtIns = Obj(
            ("gadget", "G"),
            ("app", "A"),
            ("screen", "S"),
            ("platform", "toy"),
            ("agent", "never read")
        );
        Assert.Equal(
            Obj(("gadget", "G"), ("app", "A"), ("screen", "S")),
            await BindHook(target, [], ["gadget", "app", "screen"], builtIns).Hook()
        );
        AssertPlanError(
            () => FixturePlanner.PlanSuiteHook(target, SuiteHook([], ["agent"])),
            "beforeAll hook names \"agent\", a test fixture on target \"toy\""
        );
    }

    [Fact]
    public async Task Fails_a_hook_that_names_an_engine_fixture_with_no_attempt_behind_it_with_the_reason_as_it_destructures_it()
    {
        var target = ToyTarget(EngineIsolation.Worker);
        var (bound, hook) = BindHook(target, [], ["platform", "gadget"]);
        await bound.SetUpAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(hook);
        Assert.Contains("\"gadget\" has no attempt behind it", failure.Message);
    }
}
