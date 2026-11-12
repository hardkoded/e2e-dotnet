// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('eventLine').</summary>
public sealed class EventLineTests
{
    private static readonly Colors Plain = new(false);

    private static Reports.StepEvent Turn(string reasoning) =>
        new() { Kind = Reports.StepEventKind.Model, StartedAt = DateTimeOffset.Parse("2026-01-01T00:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture), DurationMs = 1_200, Status = Reports.StepEventStatus.Passed, Reasoning = reasoning };

    [Fact]
    public void Clips_a_CJK_reasoning_excerpt_to_the_columns_left_on_the_row_not_to_that_many_code_points()
    {
        var line = ListSteps.EventLine(Plain, Turn(new string('日', 40)), 60);
        Assert.Equal($"• Thinking (1.20s) · {new string('日', 19)}…", line);
        Assert.Equal(60, Format.VisibleWidth(line));
    }

    [Fact]
    public void Keeps_an_excerpt_that_fits_the_row_in_columns_whole()
    {
        Assert.Equal($"• Thinking (1.20s) · {new string('日', 19)}", ListSteps.EventLine(Plain, Turn(new string('日', 19)), 60));
    }
}
