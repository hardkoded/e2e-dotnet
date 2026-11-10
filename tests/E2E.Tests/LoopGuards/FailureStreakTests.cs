// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.LoopGuards;

/// <summary>
/// Upstream's "failure streak" describe (loop-guards.test.ts). The port keeps each tool result as its text and an error-text flag
/// (upstream's <c>error-text</c> output), so the `it`s read the same messages upstream's read.
/// </summary>
public sealed class FailureStreakTests
{
    private const string Failed = "tap #n6 failed: LOCATOR_NOT_FOUND: node #n6 is not on the current screen (observation b3); it was removed or never existed\n\nCurrent screen (revision b3, 2 nodes):\n#n1 document\n #n2 heading \"X\"";
    private const string Tapped = "Tapped #n6.\n\nScreen unchanged since revision b3 (2 nodes).";
    private const string Conclude = "done";

    private static readonly E2E.GuardToolResult Failure = new(Failed, Failed: true);
    private static readonly E2E.GuardToolResult Success = new(Tapped, Failed: false);

    private static E2E.ModelMessage Result(string toolName, string content, bool errorText = false) => new()
    {
        Role = "tool",
        ToolCallId = toolName,
        Name = toolName,
        Content = content,
        ErrorText = errorText,
    };

    [Fact]
    public void Recognizes_every_failure_shape_a_tool_result_takes_and_nothing_else()
    {
        Assert.True(E2E.LoopGuards.IsFailedResult("tap", Failed));
        Assert.True(E2E.LoopGuards.IsFailedResult("tap", "Action failed: the step budget is spent"));
        Assert.True(E2E.LoopGuards.IsFailedResult("seed", "Tool \"seed\" failed: connection refused"));
        Assert.False(E2E.LoopGuards.IsFailedResult("tap", Tapped));

        // An action's failure leads with the tool and what it was aimed at, never the success it did not have.
        Assert.True(E2E.LoopGuards.IsFailedResult("navigate", "navigate file:///etc/passwd failed: POLICY_DENIED: forbidden URL scheme: file:"));
        Assert.True(E2E.LoopGuards.IsFailedResult("upload", "upload \"voucher.txt\" to #n12006 failed: no file at \"voucher.txt\" under the project root"));
        Assert.True(E2E.LoopGuards.IsFailedResult("back", "back failed: there is no page to go back to"));
        Assert.False(E2E.LoopGuards.IsFailedResult("navigate", "Navigated to https://example.test/failed: x.\n\nScreen changes since revision b3:"));

        // A project tool's own text is its result, however it reads; only a throw, reported by the guard, fails it.
        Assert.False(E2E.LoopGuards.IsFailedResult("ci_status", "build failed: 2 tests red"));
        Assert.True(E2E.LoopGuards.IsFailedResult("ci_status", "Action failed: the step budget is spent"));

        // Text the model typed is not a failure, even when it reads like one.
        Assert.False(E2E.LoopGuards.IsFailedResult("tap", "Typed \"failed: no\" into #n3.\n\nScreen unchanged since revision b3 (2 nodes)."));
        Assert.False(E2E.LoopGuards.IsFailedResult("tap", "Typed \"(x) failed: no\" into #n3.\n\nScreen unchanged since revision b3 (2 nodes)."));
    }

    [Fact]
    public void Recognizes_the_point_verbs_whose_lead_is_the_verb_and_the_point_rather_than_a_sentence()
    {
        const string missed = "nothing the screen lists is at (50, 50)";
        Assert.True(E2E.LoopGuards.IsFailedResult("tap_at", $"tap_at (30, 30) failed: {missed}; this engine taps listed nodes only\n\nScreen unchanged since revision b3 (2 nodes)."));
        Assert.True(E2E.LoopGuards.IsFailedResult("type_at", $"type_at (60, 189) failed: {missed}; type_at needs a control the screen lists."));
        Assert.True(E2E.LoopGuards.IsFailedResult("press_at", $"press_at (60, 189) failed: {missed}; press_at needs a control the screen lists."));
        Assert.True(E2E.LoopGuards.IsFailedResult("select_at", $"select_at (30, 30) failed: {missed}; select_at needs a control the screen lists."));
        Assert.False(E2E.LoopGuards.IsFailedResult("tap", "Tapped the point (300, 60); no listed control is there.\n\nScreen unchanged since revision b3 (2 nodes)."));
        Assert.False(E2E.LoopGuards.IsFailedResult("tap", "Typed into the field at (120, 60) (tapped to focus it; nothing the screen lists is at (200, 100))."));
    }

    [Fact]
    public void Warns_at_three_failures_in_a_row_stops_at_five_and_starts_over_after_a_success()
    {
        var streak = (int count) => Enumerable.Repeat(Failure, count).ToList();
        Assert.Equal((false, null), E2E.LoopGuards.CheckFailureStreak(streak(2)));
        Assert.Equal((false, "the last 3 actions failed in a row"), E2E.LoopGuards.CheckFailureStreak([Success, .. streak(3)]));
        Assert.True(E2E.LoopGuards.CheckFailureStreak(streak(5)).Stop);
        Assert.Equal((false, null), E2E.LoopGuards.CheckFailureStreak([.. streak(4), Success, .. streak(2)]));
    }

    [Fact]
    public void Partial_Reads_text_results_for_the_failure_shape_counts_a_call_the_SDK_refused_as_failed_and_skips_the_conclusion_tool()
    {
        // What the SDK hands back for an input outside the closed schema and for a tool the step does not offer.
        const string undeclaredField = "failed: Invalid input for tool tap: Type validation failed: Value: {\"target\":\"n6\",\"force\":true}.\nError message: [{\"code\":\"unrecognized_keys\",\"keys\":[\"force\"],\"path\":[],\"message\":\"Unrecognized key: \\\"force\\\"\"}]";
        const string unknownTool = "failed: Model tried to call unavailable tool 'click'. Available tools: observe, tap, type, done.";

        var messages = new List<E2E.ModelMessage>
        {
            new() { Role = "user", Content = "Execute this test step" },
            Result("tap", Failed),
            Result(Conclude, "Action failed: summary too long"),
            Result("tap", undeclaredField, errorText: true),
            Result("click", unknownTool, errorText: true),
            Result("tap", Tapped),
        };
        Assert.Equal(
            new E2E.GuardToolResult[] { Failure, new(undeclaredField, Failed: true), new(unknownTool, Failed: true), Success },
            E2E.LoopGuards.ExtractToolResults(messages, Conclude));
    }

    /// <summary>
    /// .NET-only: the port's own tool names (<c>fill</c>, <c>uncheck</c>, <c>clear</c>) lead their failures with the name they were called by,
    /// so the streak reads them as failures. Upstream has no such names; its failure leads with <c>type</c> and no <c>uncheck</c> or <c>clear</c>.
    /// </summary>
    [Fact]
    public void Counts_a_failed_port_only_tool_toward_the_streak()
    {
        var results = E2E.LoopGuards.ExtractToolResults(
            [Result("fill", "fill #n3 failed: LOCATOR_NOT_FOUND: node #n3 is not on the current screen"), Result("clear", "clear #n4 failed: LOCATOR_NOT_FOUND: node #n4 is gone"), Result("uncheck", "uncheck #n5 failed: LOCATOR_NOT_FOUND: node #n5 is gone")],
            Conclude);
        Assert.All(results, result => Assert.True(result.Failed));
        Assert.Equal((false, "the last 3 actions failed in a row"), E2E.LoopGuards.CheckFailureStreak(results));
    }

    [Fact]
    public void Reads_the_text_beside_a_screenshot_so_the_streak_warns_and_stops_in_pixel_mode_too()
    {
        // The caption ScreenUpdate puts beside a screenshot is part of the result's text, which the streak reads.
        static string WithScreenshot(string text) => text + "\n\nScreenshot attached: 640 by 360 pixels.";
        var streak = (int count) => Enumerable.Range(0, count).Select(_ => Result("tap", WithScreenshot(Failed))).ToList();

        var results = E2E.LoopGuards.ExtractToolResults([Result("tap", WithScreenshot(Tapped)), .. streak(3)], Conclude);
        Assert.Equal(4, results.Count);
        Assert.False(results[0].Failed);
        Assert.StartsWith("tap #n6 failed: ", results[1].Text, StringComparison.Ordinal);
        Assert.True(results[1].Failed);
        Assert.Equal((false, "the last 3 actions failed in a row"), E2E.LoopGuards.CheckFailureStreak(results));
        Assert.True(E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults(streak(5), Conclude)).Stop);
    }

    [Fact]
    public void Counts_a_refused_conclusion_toward_the_streak_three_warn_five_force_the_verdict_and_the_conclusion_tools_own_text_neither_counts_nor_resets()
    {
        // The repeat guard never sees conclusion calls, so a model resending a done the schema turns away has only this bound short of the turn budget.
        const string refusedVerdict = "failed: Invalid input for tool done: Type validation failed: Value: {\"status\":\"passed\",\"summary\":\"done\",\"force\":true}.\nError message: [{\"code\":\"unrecognized_keys\",\"keys\":[\"force\"],\"path\":[],\"message\":\"Unrecognized key: \\\"force\\\"\"}]";
        var refused = (int count) => Enumerable.Range(0, count).Select(_ => Result(Conclude, refusedVerdict, errorText: true)).ToList();

        Assert.Equal(new E2E.GuardToolResult[] { new(refusedVerdict, Failed: true) }, E2E.LoopGuards.ExtractToolResults(refused(1), Conclude));
        Assert.Equal((false, null), E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults(refused(2), Conclude)));
        Assert.Equal((false, "the last 3 actions failed in a row"), E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults(refused(3), Conclude)));
        Assert.True(E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults(refused(5), Conclude)).Stop);

        var sentBack = Result(Conclude, "Rejected: a blocked verdict requires a code naming what blocked you (one of ENVIRONMENT_UNAVAILABLE, CREDENTIALS_REJECTED).");
        Assert.Equal((false, "the last 3 actions failed in a row"), E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults([.. refused(2), sentBack, .. refused(1)], Conclude)));
        Assert.Equal((false, null), E2E.LoopGuards.CheckFailureStreak(E2E.LoopGuards.ExtractToolResults([.. refused(4), Result("tap", Tapped)], Conclude)));
    }
}
