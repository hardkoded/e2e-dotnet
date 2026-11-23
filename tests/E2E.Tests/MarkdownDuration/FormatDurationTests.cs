// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.MarkdownDuration;

/// <summary>Upstream: packages/e2e/tests/unit/markdown-duration.test.ts, describe('formatDuration').</summary>
public sealed class FormatDurationTests
{
    [Fact]
    public void Rounds_before_choosing_the_unit_so_the_value_carries_over()
    {
        Assert.Equal("250ms", MarkdownText.FormatDuration(250));
        Assert.Equal("999ms", MarkdownText.FormatDuration(999.4));
        Assert.Equal("1.0s", MarkdownText.FormatDuration(999.6));
        Assert.Equal("12.3s", MarkdownText.FormatDuration(12_340));
        Assert.Equal("59.9s", MarkdownText.FormatDuration(59_949));
        Assert.Equal("1m 0s", MarkdownText.FormatDuration(59_960));
        Assert.Equal("1m 59s", MarkdownText.FormatDuration(119_400));
        Assert.Equal("2m 0s", MarkdownText.FormatDuration(119_600));
        Assert.Equal("2m 5s", MarkdownText.FormatDuration(125_000));
    }
}
