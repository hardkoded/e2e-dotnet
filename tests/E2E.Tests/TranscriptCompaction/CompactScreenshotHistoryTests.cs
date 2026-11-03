// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TranscriptCompaction.TranscriptMessages;
using Compaction = global::E2E.TranscriptCompaction;

namespace E2E.Tests.TranscriptCompaction;

/// <summary>
/// Upstream's <c>describe('compactScreenshotHistory')</c> (<c>transcript-compaction.test.ts</c>). A screenshot rides a user turn after
/// the tool results, so a stale one loses its image in that turn and keeps its text beside the notice. The opening prompt holds its own.
/// </summary>
public sealed class CompactScreenshotHistoryTests
{
    private static ModelMessage OpeningWithImage { get; } = new()
    {
        Role = "user",
        Content = "Goal: tap the pin\n\nScreen (/):\n- document [ref=e1]",
        Images = [TranscriptMessages.Png],
    };

    [Fact]
    public void Keeps_every_screenshot_while_few_are_stale_so_the_request_prefix_stays_cacheable()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithImage, Screenshot("t1"), Screenshot("t2"), Screenshot("t3")];
        Assert.Same(messages, Compaction.CompactScreenshotHistory(messages));
        Assert.Equal(4, ImageCount(messages));
    }

    [Fact]
    public void Elides_the_older_screenshots_in_one_batch_keeping_the_newest_two_and_every_text()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithImage, Screenshot("t1"), Screenshot("t2"), Screenshot("t3"), Screenshot("t4")];
        var compacted = Compaction.CompactScreenshotHistory(messages);
        Assert.NotSame(messages, compacted);
        Assert.Equal(2, ImageCount(compacted));
        Assert.Equal(
            "Goal: tap the pin\n\nScreen (/):\n- document [ref=e1]\n" + Compaction.ScreenshotElidedNotice,
            compacted[0].Content);
        Assert.Null(compacted[0].Images);
        Assert.Equal("Screenshot attached to the result of t1.\n" + Compaction.ScreenshotElidedNotice, compacted[1].Content);
        Assert.Null(compacted[1].Images);
        Assert.Same(messages[3], compacted[3]);
        Assert.Same(messages[4], compacted[4]);
    }
}
