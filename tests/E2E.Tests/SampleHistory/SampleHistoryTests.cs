// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.SampleHistory;

/// <summary>
/// Upstream's <c>describe('SampleHistory')</c> (<c>tests/unit/sample-history.test.ts</c>): an assertion poll's readings as one event: how
/// many rounds it read, and, when the reading changed or the poll gave up, the readings in order, so a wrong expectation reads
/// differently from a race.
/// </summary>
public sealed partial class SampleHistoryTests
{
    private static E2E.SampleHistory History(string name, Func<string, string>? redact = null) => new(name, redact ?? (text => text));

    [Fact]
    public void Counts_the_reads_of_a_poll_that_passed_at_once_and_tells_no_readings()
    {
        var history = History("expect");
        history.Add("text \"Saved\"");

        var told = history.Event(StepEventStatus.Passed);

        Assert.Equal((StepEventKind.Poll, "expect", StepEventStatus.Passed, 1), (told.Kind, told.Name, told.Status, told.Count));
        Assert.Null(told.Detail);
    }

    [Fact]
    public void Folds_repeated_readings_and_says_when_each_new_one_first_showed()
    {
        var history = History("expect");
        foreach (var value in new[] { "text \"Saving\"", "text \"Saving\"", "text \"Saved\"" })
        {
            history.Add(value);
        }

        var told = history.Event(StepEventStatus.Passed);

        Assert.Equal(3, told.Count);
        Assert.Matches(SavingThenSaved(), told.Detail!);
    }

    [Fact]
    public void Tells_the_one_reading_of_a_poll_that_gave_up()
    {
        var history = History("waitFor");
        for (var index = 0; index < 4; index++)
        {
            history.Add("absent");
        }

        var told = history.Event(StepEventStatus.Failed);

        Assert.Equal(("waitFor", StepEventStatus.Failed, 4, "absent x4"), (told.Name, told.Status, told.Count, told.Detail));
    }

    [Fact]
    public void Keeps_the_first_reading_and_the_latest_ones_when_the_value_kept_changing()
    {
        var history = History("expect");
        for (var index = 0; index < 8; index++)
        {
            history.Add($"count {index}");
        }

        var detail = history.Event(StepEventStatus.Failed).Detail!;

        Assert.StartsWith("count 0 -> … -> ", detail, StringComparison.Ordinal);
        Assert.Contains("count 7", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("count 3", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacts_a_reading_before_it_is_clipped_so_no_cut_leaves_the_head_of_a_secret()
    {
        var history = History("expect", text => text.Replace("eyJsecret-token-value", "[secret]", StringComparison.Ordinal));
        history.Add($"value \"{new string('x', 70)}eyJsecret-token-value\"");

        Assert.Equal($"value \"{new string('x', 70)}[s…", history.Event(StepEventStatus.Failed).Detail);
    }

    [Fact]
    public void Folds_two_long_readings_apart_when_they_differ_only_past_the_clip()
    {
        var history = History("expect");
        var head = new string('y', 100);
        history.Add($"{head}a");
        history.Add($"{head}b");

        Assert.Matches(TwoClippedReadings(), history.Event(StepEventStatus.Failed).Detail!);
    }

    [GeneratedRegex("^text \"Saving\" x2 -> at \\d+ms text \"Saved\"$")]
    private static partial Regex SavingThenSaved();

    [GeneratedRegex("^y+… -> at \\d+ms y+…$")]
    private static partial Regex TwoClippedReadings();
}
