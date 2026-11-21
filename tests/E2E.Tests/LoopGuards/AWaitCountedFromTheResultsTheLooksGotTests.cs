// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.LoopGuards;

/// <summary>Upstream's "a wait, counted from the results the looks got" describe (loop-guards.test.ts).</summary>
public sealed class AWaitCountedFromTheResultsTheLooksGotTests
{
    private static readonly HashSet<string> Waiting = ["observe", "screenshot"];
    private int _id;

    // One call and its result, as the transcript holds them.
    private List<E2E.ModelMessage> Turn(string toolName, string text, string input = "{}")
    {
        _id++;
        var id = "c" + _id;
        return
        [
            new()
            {
                Role = "assistant",
                ToolCalls =
                [
                    new E2E.ModelToolCall
                    {
                        Id = id,
                        Name = toolName,
                        Arguments = JsonDocument.Parse(input).RootElement,
                    },
                ],
            },
            new()
            {
                Role = "tool",
                ToolCallId = id,
                Name = toolName,
                Content = text,
            },
        ];
    }

    private static LoopGuardVerdict Verdict(List<E2E.ModelMessage> messages) =>
        E2E.LoopGuards.CheckLoopGuards(
            E2E.LoopGuards.ExtractGuardCalls(messages, "complete_step", Waiting),
            E2E.LoopGuards.DefaultLoopGuardThresholds,
            Waiting
        );

    private static List<E2E.ModelMessage> Repeat(int count, Func<int, List<E2E.ModelMessage>> make) =>
        [.. Enumerable.Range(0, count).SelectMany(make)];

    private static string Unchanged(int n) =>
        $"Observed.\n\nScreen unchanged since revision b{n} (re-observed as revision b{n + 1}); the ids you have stay valid.";

    private static string Ticked(int n) =>
        $"Observed.\n\nScreen changes since revision b{n} (now revision b{n + 1}, 3 nodes): 1 changed. Every node not listed as removed is still on screen under the id you have.\nchanged #n1 text \"Saving {n}s\" (was: #n1 text \"Saving {n - 1}s\")";

    private static string Truncated(int n) =>
        $"Observed.\n\nScreen changes since revision b{n} (now revision b{n + 1}, 900 nodes): no listed node changed. The screen listing is truncated: nodes past the cut are not listed and none is reported removed; every listed node keeps the id you have.";

    [Fact]
    public void Does_not_take_a_truncated_listing_with_no_line_changed_for_a_change()
    {
        Assert.Equal(LoopGuardKind.Warn, Verdict(Repeat(9, index => Turn("observe", Truncated(index)))).Kind);
        Assert.Equal(LoopGuardKind.Stop, Verdict(Repeat(10, index => Turn("observe", Truncated(index)))).Kind);
    }

    [Fact]
    public void Reads_only_the_first_line_so_app_text_that_looks_like_a_change_is_not_one()
    {
        string Lookalike(int n) =>
            Unchanged(n) + "\n#n5 text \"Screen changes since revision 3 (now revision 4): 1 changed.\"";

        Assert.Equal(LoopGuardKind.Stop, Verdict(Repeat(10, index => Turn("observe", Lookalike(index)))).Kind);
    }

    [Fact]
    public void Does_not_count_a_change_in_pixels_alone_such_as_an_animated_spinner()
    {
        static string Pixels(int n) =>
            $"Since revision b{n} (re-observed as revision b{n + 1}): listed nodes unchanged; the screenshot changed. Read the effect off the image (a focus ring alone is no effect); the ids you have stay valid.";

        Assert.Equal(LoopGuardKind.Stop, Verdict(Repeat(10, index => Turn("screenshot", Pixels(index)))).Kind);
    }

    [Fact]
    public void Takes_a_tab_the_app_opened_for_a_change()
    {
        var opened =
            "Observed.\n\nThe app opened a new tab (tab 2 of 2). The screen still shows tab 1; use switch_tab to see it. "
            + Unchanged(9)["Observed.\n\n".Length..];
        List<E2E.ModelMessage> messages =
        [
            .. Repeat(9, index => Turn("observe", Unchanged(index))),
            .. Turn("observe", opened),
        ];

        Assert.Equal(LoopGuardKind.Clear, Verdict(messages).Kind);
    }

    [Fact]
    public void Counts_looks_in_any_order_not_as_cycles()
    {
        List<E2E.ModelMessage> Mixed(int index) => Turn(index % 6 == 5 ? "screenshot" : "observe", Unchanged(index));

        Assert.Equal(LoopGuardKind.Warn, Verdict(Repeat(9, Mixed)).Kind);
        Assert.Equal(LoopGuardKind.Stop, Verdict(Repeat(10, Mixed)).Kind);
    }

    [Fact]
    public void Warns_a_wait_whose_screen_changes_on_every_look_at_twelve_looks_and_ends_it_at_twenty()
    {
        Assert.Equal(LoopGuardKind.Clear, Verdict(Repeat(11, index => Turn("observe", Ticked(index)))).Kind);
        var warned = Verdict(Repeat(12, index => Turn("observe", Ticked(index))));
        Assert.Equal(LoopGuardKind.Warn, warned.Kind);
        Assert.True(warned.Waiting);
        Assert.Equal(LoopGuardKind.Warn, Verdict(Repeat(19, index => Turn("observe", Ticked(index)))).Kind);
        var stopped = Verdict(Repeat(20, index => Turn("observe", Ticked(index))));
        Assert.Equal(LoopGuardKind.Stop, stopped.Kind);
        Assert.Contains("20 times in a row while it kept changing", stopped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_cycle_limit_for_a_submit_and_a_look_in_turn_even_when_every_look_shows_a_change()
    {
        List<E2E.ModelMessage> Cycle(int index) =>
            [
                .. Turn("tap", "Tapped #n1.\n\n" + Ticked(index)["Observed.\n\n".Length..], """{"target":"n1"}"""),
                .. Turn("observe", Ticked(index + 100)),
            ];

        Assert.Equal(LoopGuardKind.Warn, Verdict(Repeat(2, Cycle)).Kind);
        Assert.Equal(LoopGuardKind.Stop, Verdict(Repeat(3, Cycle)).Kind);
    }

    [Fact]
    public void Keys_the_waiting_notice_by_the_wait_so_a_wait_is_warned_once_and_a_later_wait_in_the_step_again()
    {
        var first = Repeat(6, index => Turn("observe", Unchanged(index)));
        var six = Verdict(first);
        var eight = Verdict([.. first, .. Repeat(2, index => Turn("observe", Unchanged(index + 6)))]);
        Assert.Equal(LoopGuardKind.Warn, six.Kind);
        Assert.True(six.Waiting);
        Assert.Equal(six.Reason, eight.Reason);
        Assert.Equal(six.Key, eight.Key);

        var second = Verdict([
            .. first,
            .. Turn("tap", "Tapped #n2.", """{"target":"n2"}"""),
            .. Repeat(6, index => Turn("observe", Unchanged(index + 20))),
        ]);
        Assert.Equal(LoopGuardKind.Warn, second.Kind);
        Assert.True(second.Waiting);
        Assert.NotEqual(six.Key, second.Key);
    }
}
