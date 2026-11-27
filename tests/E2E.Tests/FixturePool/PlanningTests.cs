// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FixturePool;

/// <summary>Upstream's <c>describe('planning')</c> (<c>tests/unit/fixture-pool.test.ts</c>); the port has no <c>test.extend</c>, <c>planAttempt</c>, or <c>checkDeclaredFixtures</c>, so only what reads <c>BuiltInFixtures</c> is ported.</summary>
public sealed class PlanningTests
{
    /// <summary>A web-like target, whose attempts are isolated, or a device-like one, whose state outlives them (upstream's <c>toyTarget</c>).</summary>
    internal static BuiltInTarget ToyTarget(string isolation = EngineIsolation.Attempt) =>
        new(
            "toy",
            "toy",
            new Dictionary<string, string>(),
            new ResolvedEngine("toy", new HashSet<string>(), null, isolation, ["gadget"])
        );

    [Fact]
    public void Partial_refuses_a_name_the_target_has_no_fixture_for_with_the_nearest_name_or_the_e2e_way()
    {
        var builtIns = BuiltInFixtures.Of(ToyTarget());
        var available = (string[])
            ["agent", "app", "gadget", "platform", "request", "screen", "services", "session", "workspace"];

        Assert.Equal(
            "target \"toy\" has no \"workspce\" fixture; available: agent, app, gadget, platform, request, screen, services, session, workspace (engine toy contributes gadget); did you mean \"workspace\"?",
            builtIns.Missing("workspce", available)
        );
        Assert.Contains(
            "there is no Playwright page: open the app with app.open()",
            builtIns.Missing("page", available)
        );
        Assert.StartsWith("target \"toy\" has no \"nope\" fixture", builtIns.Missing("nope", available));
    }

    [Fact]
    public void Partial_names_every_built_in_and_how_long_each_lives()
    {
        var attempt = BuiltInFixtures.Of(ToyTarget());
        Assert.Equal(["agent", "app", "screen", "platform", "request", "services", "session", "gadget"], attempt.Names);
        Assert.Equal(
            ["test", "test", "test", "worker", "test", "worker", "test", "test"],
            attempt.Names.Select(name => attempt.Scope(name))
        );
        var worker = BuiltInFixtures.Of(ToyTarget(EngineIsolation.Worker));
        Assert.Equal(
            new[] { "worker", "worker", "worker" },
            new[] { "app", "screen", "gadget" }.Select(name => worker.Scope(name))
        );
        Assert.Null(worker.Scope("workspace"));
    }

    [Fact]
    public void Partial_reads_a_built_in_no_attempt_stands_behind()
    {
        var builtIns = BuiltInFixtures.Of(ToyTarget(), new Dictionary<string, object?> { ["database"] = "db" });
        Assert.Equal("toy", builtIns.Attemptless("platform"));
        Assert.Equal(new Dictionary<string, object?> { ["database"] = "db" }, builtIns.Attemptless("services"));
        Assert.Null(builtIns.Attemptless("app"));
        Assert.False(builtIns.NeedsAttempt("platform"));
        Assert.True(builtIns.NeedsAttempt("app"));
        Assert.True(builtIns.NeedsAttempt("gadget"));
    }

    [Fact]
    public void Partial_takes_the_scope_of_a_fixture_only_another_target_has_from_the_shared_scopes()
    {
        var target = new BuiltInTarget("toy", "toy", new Dictionary<string, string> { ["gizmo"] = "worker" }, null);
        var builtIns = BuiltInFixtures.Of(target);
        Assert.Equal("worker", builtIns.Scope("gizmo"));
        Assert.False(builtIns.NeedsAttempt("gizmo"));
        Assert.Contains("gizmo", builtIns.Names);
    }
}
