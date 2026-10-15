// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.LoopGuards;

/// <summary>
/// Upstream's "checkLoopGuards thresholds" and "failure streak" describes are not ported. The port fixes the thresholds
/// and takes no caller override. Its failure-streak guard is its own, and it reads no tool result text: it counts every
/// failed action and empty turn of the act (COMPATIBILITY, Ported).
/// </summary>
public sealed class LoopGuardsTests
{
    private static (string Name, string Input) Call(string name, string input = "{}") => (name, input);

    private static (bool Stop, string? Reason) Check(IReadOnlyList<(string Name, string Input)> calls) => E2E.LoopGuards.CheckLoopGuards(calls);

    private static List<(string Name, string Input)> Repeated(int count) =>
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

        Assert.Equal((false, null), Check(calls));
    }

    [Fact]
    public void Warns_on_three_identical_calls_and_stops_on_five()
    {
        Assert.Equal((false, null), Check([Call("observe"), .. Repeated(2)]));

        var warned = Check([Call("observe"), .. Repeated(3)]);
        Assert.False(warned.Stop);
        Assert.NotNull(warned.Reason);

        var stopped = Check([Call("observe"), .. Repeated(5)]);
        Assert.True(stopped.Stop);
        Assert.Contains("\"tap\"", stopped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Identical_inputs_are_required_alternating_targets_are_not_a_repeat()
    {
        var calls = Enumerable.Range(0, 8).Select(index => Call("tap", $$"""{"target":"n{{index}}"}""")).ToList();

        Assert.Equal((false, null), Check(calls));
    }

    [Fact]
    public void Detects_a_two_call_cycle_warn_at_two_repetitions_stop_at_three()
    {
        var pair = new[] { Call("tap", """{"target":"a"}"""), Call("tap", """{"target":"b"}""") };

        var warned = Check([.. pair, .. pair]);
        Assert.False(warned.Stop);
        Assert.NotNull(warned.Reason);

        var stopped = Check([.. pair, .. pair, .. pair]);
        Assert.True(stopped.Stop);
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

        Assert.True(Check([.. cycle, .. cycle, .. cycle]).Stop);
    }

    [Fact]
    public void A_uniform_window_is_owned_by_the_period_1_policy_not_reported_as_a_cycle()
    {
        // Four identical calls: period 2 would call this two cycle repetitions (stop threshold not met),
        // but period 1 warns, the stricter and correct read.
        var verdict = Check(Repeated(4));

        Assert.False(verdict.Stop);
        Assert.Contains("4 times in a row", verdict.Reason, StringComparison.Ordinal);
    }
}
