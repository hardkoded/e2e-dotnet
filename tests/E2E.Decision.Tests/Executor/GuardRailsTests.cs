// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('guard rails')</c> of <c>executor.test.ts</c>.</summary>
public sealed class GuardRailsTests
{
    private const string Stalled = "Three actions in a row failed or changed nothing on screen.";

    [Fact]
    public async Task Blocks_three_actions_with_no_page_change()
    {
        var (model, _) = ScriptedDecision(Script(_ => "tap"));
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED");
        Assert.Equal(3, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Blocks_three_failed_actions_while_a_timer_ticks()
    {
        var (model, _) = ScriptedDecision(Script(_ => "tap"));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        fixture.Actions.Fail = (verb, _) =>
            verb == "tap" ? new AgentException("APP_UNREACHABLE", "not hittable") : null;
        var seconds = 20;
        fixture.ObserveWith = _ =>
        {
            seconds++;
            return Look([
                new ExecutorNode
                {
                    Id = "clock",
                    Role = "text",
                    Name = $"0:{seconds}",
                },
                .. Buttons,
            ]);
        };

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked);
        Assert.Equal(Stalled, verdict.Summary);
        Assert.Equal(3, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Counts_a_scroll_that_brings_other_nodes_into_view_as_progress()
    {
        var (model, _) = ScriptedDecision(Script(call => call < 4 ? "scroll_down" : "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var capture = 0;
        fixture.ObserveWith = _ =>
        {
            var offset = Math.Min(capture, 4) * 200;
            capture++;
            return Look(
                [
                    .. Enumerable
                        .Range(0, 10)
                        .Select(index => new ExecutorNode
                        {
                            Id = $"r{index}",
                            Role = "button",
                            Name = $"Row {index}",
                            Rect = new BoundingBox(0, (index * 200) + 50 - offset, 100, 100),
                        }),
                ],
                "#r0 button"
            );
        };

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(4, fixture.Actions.Count("scroll"));
    }

    [Fact]
    public async Task Blocks_three_scrolls_that_move_nothing_as_at_the_bottom_of_the_page()
    {
        var (model, _) = ScriptedDecision(Script(_ => "scroll_down"));
        var fixture = Context(
            new ContextOptions
            {
                Tree =
                [
                    new ExecutorNode
                    {
                        Id = "last",
                        Role = "button",
                        Name = "Back to top",
                        Rect = new BoundingBox(0, 400, 100, 40),
                    },
                ],
            }
        );

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked);
        Assert.Equal(Stalled, verdict.Summary);
        Assert.Equal(3, fixture.Actions.Count("scroll"));
    }

    [Fact]
    public async Task Never_trips_the_guard_when_pages_keep_changing()
    {
        var operation = InTurn(["tap", "tap", "tap", "tap"], "done");
        var (model, _) = ScriptedDecision(Script(_ => operation()));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var n = 0;
        IReadOnlyList<ExecutorNode> first =
        [
            new()
            {
                Id = "a",
                Role = "button",
                Name = "Page A",
            },
        ];
        IReadOnlyList<ExecutorNode> second =
        [
            new()
            {
                Id = "a",
                Role = "button",
                Name = "Page B",
            },
        ];
        fixture.ObserveWith = _ =>
        {
            n++;
            return Look(n % 2 == 0 ? first : second, "#a button");
        };

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.True(fixture.Actions.Count("tap") > 3);
    }

    [Fact]
    public async Task Sends_failing_actions_back_as_history_and_rethrows_hard_stops()
    {
        // Calls alternate operation and target: two taps take four calls before done.
        var (model, _) = ScriptedDecision(Script(call => call < 4 ? "tap" : "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        fixture.Actions.Fail = (verb, nth) =>
            verb == "tap" && nth == 0 ? new AgentException("POLICY_DENIED", "denied") : null;

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(2, fixture.Actions.Count("tap"));
    }

    [Theory]
    [InlineData("STEP_TIMEOUT")]
    [InlineData("CANCELLED")]
    public async Task Rethrows_STEP_TIMEOUT_and_CANCELLED(string code)
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) => Pick(id == "operation" ? "tap" : keys.FirstOrDefault() ?? "")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        fixture.Actions.Fail = (verb, nth) => verb == "tap" && nth == 0 ? new AgentException(code, "stop") : null;

        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(model, fixture));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Never_makes_the_call_that_would_exceed_the_budget()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => Pick(id == "operation" ? "tap" : keys.FirstOrDefault() ?? "")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons, MaxModelCalls = 1 });

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED");
        Assert.Single(requests);
        Assert.Single(fixture.Usage);
    }

    [Fact]
    public async Task Records_failed_calls_too()
    {
        var (model, _) = ScriptedDecision((_, _, _) => Pick("tap"), throws: new InvalidOperationException("boom"));
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(model, fixture));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Single(fixture.Usage);
    }
}
