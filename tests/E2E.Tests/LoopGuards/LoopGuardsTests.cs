// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace E2E.Tests.LoopGuards;

/// <summary>
/// Upstream's "loop guards" describe (loop-guards.test.ts).
/// </summary>
public sealed class LoopGuardsTests
{
    private static GuardToolCall Call(string name, string input = "{}") => new(name, input);

    private static LoopGuardVerdict Check(IReadOnlyList<GuardToolCall> calls) => E2E.LoopGuards.CheckLoopGuards(calls);

    private static List<GuardToolCall> Repeated(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Call("tap", """{"target":"n1"}"""))];

    [Fact]
    public void Stays_clear_on_varied_work()
    {
        var calls = new[]
        {
            Call("tap", """{"target":"n1"}"""),
            Call("type", """{"target":"n2","value":"a"}"""),
            Call("tap", """{"target":"n3"}"""),
            Call("observe"),
            Call("tap", """{"target":"n4"}"""),
        };

        Assert.Equal(LoopGuardKind.Clear, Check(calls).Kind);
    }

    [Fact]
    public void Warns_on_three_identical_calls_and_stops_on_five()
    {
        Assert.Equal(LoopGuardKind.Clear, Check([Call("observe"), .. Repeated(2)]).Kind);

        var warned = Check([Call("observe"), .. Repeated(3)]);
        Assert.Equal(LoopGuardKind.Warn, warned.Kind);
        Assert.NotNull(warned.Reason);

        var stopped = Check([Call("observe"), .. Repeated(5)]);
        Assert.Equal(LoopGuardKind.Stop, stopped.Kind);
        Assert.Contains("\"tap\"", stopped.Reason, StringComparison.Ordinal);
    }

    private static LoopGuardVerdict Waiting(IReadOnlyList<GuardToolCall> calls, params string[] waiting) =>
        E2E.LoopGuards.CheckLoopGuards(calls, E2E.LoopGuards.DefaultLoopGuardThresholds, new HashSet<string>(waiting));

    [Fact]
    public void Gives_repeated_calls_to_a_waiting_tool_more_room_waiting_on_a_save_in_progress_is_not_a_loop()
    {
        List<GuardToolCall> Observes(int count) =>
            [Call("tap", """{"target":"n1"}"""), .. Enumerable.Range(0, count).Select(_ => Call("observe"))];
        LoopGuardKind Check(int count) => Waiting(Observes(count), "observe").Kind;

        Assert.Equal(LoopGuardKind.Clear, Check(5));
        Assert.Equal(LoopGuardKind.Warn, Check(6));
        Assert.True(Waiting(Observes(6), "observe").Waiting);
        Assert.False(E2E.LoopGuards.CheckLoopGuards(Observes(3)).Waiting);
        Assert.Equal(LoopGuardKind.Stop, Check(10));

        // A tool named observe that the caller did not mark keeps the ordinary thresholds.
        Assert.Equal(LoopGuardKind.Warn, E2E.LoopGuards.CheckLoopGuards(Observes(3)).Kind);
        Assert.Equal(LoopGuardKind.Stop, E2E.LoopGuards.CheckLoopGuards(Observes(5)).Kind);
    }

    [Fact]
    public void Counts_observe_and_screenshot_in_turn_as_one_wait_in_calls_and_keeps_the_cycle_limits_for_anything_else_in_the_cycle()
    {
        List<GuardToolCall> Alternating(int pairs) =>
            [
                Call("tap", """{"target":"n1"}"""),
                .. Enumerable.Range(0, pairs).SelectMany(_ => new[] { Call("observe"), Call("screenshot") }),
            ];
        LoopGuardVerdict Check(int pairs) => Waiting(Alternating(pairs), "observe", "screenshot");

        Assert.Equal(LoopGuardKind.Clear, Check(2).Kind);
        var warned = Check(3);
        Assert.Equal(LoopGuardKind.Warn, warned.Kind);
        Assert.True(warned.Waiting);
        Assert.Equal(LoopGuardKind.Stop, Check(5).Kind);

        // A cycle with an action in it is a circle, whatever else it contains.
        GuardToolCall[] withTap = [Call("observe"), Call("tap", """{"target":"n1"}""")];
        Assert.Equal(LoopGuardKind.Warn, Waiting([.. withTap, .. withTap], "observe", "screenshot").Kind);
        Assert.Equal(LoopGuardKind.Stop, Waiting([.. withTap, .. withTap, .. withTap], "observe", "screenshot").Kind);
    }

    [Fact]
    public void Ends_a_waiting_streak_at_the_observe_that_saw_the_screen_change_so_the_wait_can_lead_on_to_the_next_action()
    {
        var waiting = new HashSet<string> { "observe" };
        static List<E2E.ModelMessage> Turn(string id, string text) =>
            [
                new()
                {
                    Role = "assistant",
                    ToolCalls =
                    [
                        new E2E.ModelToolCall
                        {
                            Id = id,
                            Name = "observe",
                            Arguments = JsonDocument.Parse("{}").RootElement,
                        },
                    ],
                },
                new()
                {
                    Role = "tool",
                    ToolCallId = id,
                    Name = "observe",
                    Content = text,
                },
            ];
        static string Still(int n) =>
            $"Observed.\n\nScreen unchanged since revision b{n} (re-observed as revision b{n + 1}); the ids you have stay valid.";
        LoopGuardKind Verdict(List<E2E.ModelMessage> messages, bool marked = true) =>
            E2E
                .LoopGuards.CheckLoopGuards(
                    E2E.LoopGuards.ExtractGuardCalls(messages, "complete_step", marked ? waiting : null),
                    E2E.LoopGuards.DefaultLoopGuardThresholds,
                    marked ? waiting : null
                )
                .Kind;

        var waits = Enumerable.Range(0, 9).SelectMany(index => Turn($"o{index}", Still(index))).ToList();
        Assert.Equal(LoopGuardKind.Warn, Verdict(waits));
        var saved = new List<E2E.ModelMessage>(waits);
        saved.AddRange(
            Turn(
                "o9",
                "Observed.\n\nScreen changes since revision b9 (now revision b10): 1 changed.\nchanged #n1 button \"Saved\" (was: #n1 button \"Saving\" [disabled])"
            )
        );
        Assert.Equal(LoopGuardKind.Clear, Verdict(saved));

        // Ten unchanged looks still force the verdict, and without the waiting set every observe counts.
        var stuck = new List<E2E.ModelMessage>(waits);
        stuck.AddRange(Turn("o9", Still(9)));
        Assert.Equal(LoopGuardKind.Stop, Verdict(stuck));
        Assert.Equal(LoopGuardKind.Stop, Verdict(saved, marked: false));
    }

    [Fact]
    public void Recognizes_a_waiting_handler_marked_by_another_installed_copy_and_nothing_else()
    {
        var own = E2E.LoopGuards.MarkWaitingTool(
            Tool("own", (_, _) => Task.FromResult(new ToolLoopResult("observed")))
        );
        // Another copy of the assembly marks through the same process-wide slot, not through this copy's state.
        Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>> foreignHandler = (_, _) =>
            Task.FromResult(new ToolLoopResult("observed"));
        var slot =
            (ConditionalWeakTable<object, object>)AppDomain.CurrentDomain.GetData("e2e.agent.waiting-handler.v1")!;
        slot.AddOrUpdate(foreignHandler, foreignHandler);

        var names = E2E.LoopGuards.WaitingToolNames([
            own,
            new ToolLoopTool(Definition("foreign"), foreignHandler),
            Tool("write", (_, _) => Task.FromResult(new ToolLoopResult("wrote"))),
        ]);

        Assert.Equal(["foreign", "own"], names.Order(StringComparer.Ordinal));
    }

    private static ModelTool Definition(string name) =>
        new()
        {
            Name = name,
            Description = name,
            Parameters = JsonDocument.Parse("""{ "type": "object", "properties": {} }""").RootElement,
        };

    private static ToolLoopTool Tool(
        string name,
        Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>> execute
    ) => new(Definition(name), execute);

    [Fact]
    public void Identical_inputs_are_required_alternating_targets_are_not_a_repeat()
    {
        var calls = Enumerable.Range(0, 8).Select(index => Call("tap", $$"""{"target":"n{{index}}"}""")).ToList();

        Assert.Equal(LoopGuardKind.Clear, Check(calls).Kind);
    }

    [Fact]
    public void Detects_a_two_call_cycle_warn_at_two_repetitions_stop_at_three()
    {
        var pair = new[] { Call("tap", """{"target":"a"}"""), Call("tap", """{"target":"b"}""") };

        var warned = Check([.. pair, .. pair]);
        Assert.Equal(LoopGuardKind.Warn, warned.Kind);
        Assert.NotNull(warned.Reason);

        var stopped = Check([.. pair, .. pair, .. pair]);
        Assert.Equal(LoopGuardKind.Stop, stopped.Kind);
        Assert.Contains("2-call sequence", stopped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Detects_a_three_call_cycle_with_an_observe_interleaved()
    {
        var cycle = new[]
        {
            Call("tap", """{"target":"expense"}"""),
            Call("tap", """{"target":"zone"}"""),
            Call("observe"),
        };

        Assert.Equal(LoopGuardKind.Stop, Check([.. cycle, .. cycle, .. cycle]).Kind);
    }

    [Fact]
    public void A_uniform_window_is_owned_by_the_period_1_policy_not_reported_as_a_cycle()
    {
        // Four identical calls: period 2 would call this two cycle repetitions (stop threshold not met),
        // but period 1 warns, the stricter and correct read.
        var verdict = Check(Repeated(4));

        Assert.Equal(LoopGuardKind.Warn, verdict.Kind);
        Assert.Contains("4 times in a row", verdict.Reason, StringComparison.Ordinal);
    }
}
