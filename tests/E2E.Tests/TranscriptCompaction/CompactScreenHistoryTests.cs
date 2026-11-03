// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TranscriptCompaction.TranscriptMessages;
using Compaction = global::E2E.TranscriptCompaction;

namespace E2E.Tests.TranscriptCompaction;

/// <summary>
/// Upstream's <c>describe('compactScreenHistory')</c> (<c>transcript-compaction.test.ts</c>). The port prints a full screen on every
/// result, so "never a change update" is a result that prints no screen. A result with a screenshot attached is a tool message
/// followed by the user turn that holds the image, so "keeps the lead and the image" reads: the screen is elided and the image turn is
/// untouched.
/// </summary>
public sealed class CompactScreenHistoryTests
{
    [Fact]
    public void Leaves_the_transcript_alone_while_only_one_full_screen_is_present()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, ChangeResult("c1"), ChangeResult("c3")];
        Assert.Same(messages, Compaction.CompactScreenHistory(messages));
    }

    [Fact]
    public void Keeps_small_superseded_screens_verbatim_so_the_request_prefix_stays_cacheable()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, ChangeResult("c1"), FullScreenResult("c2"), FullScreenResult("c4")];
        Assert.Same(messages, Compaction.CompactScreenHistory(messages));
        Assert.Same(messages, Compaction.CompactScreenHistory(messages, 1024));
    }

    [Fact]
    public void Elides_the_older_full_screens_in_one_batch_once_they_outgrow_the_budget_and_never_a_change_update()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, ChangeResult("c1"), FullScreenResult("c2"), ChangeResult("c3"), FullScreenResult("c4")];
        Assert.Same(messages, Compaction.CompactScreenHistory(messages, 1024));
        var compacted = Compaction.CompactScreenHistory(messages, 0);
        Assert.NotSame(messages, compacted);
        Assert.Equal("Goal: do the thing\n[earlier screen elided; the newest \"Screen\" describes the screen]", compacted[0].Content);
        // The middle full screen is elided down to its lead; the newest full screen and every change update survive verbatim.
        Assert.Equal("Tapped #e3.\n[earlier screen elided; the newest \"Screen\" describes the screen]", compacted[2].Content);
        Assert.Same(messages[1], compacted[1]);
        Assert.Same(messages[3], compacted[3]);
        Assert.Same(messages[4], compacted[4]);
    }

    [Fact]
    public void Elides_full_screens_that_arrived_with_a_screenshot_keeping_the_lead_and_the_image()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, FullScreenResult("c1"), Screenshot("c1"), FullScreenResult("c2"), Screenshot("c2"), FullScreenResult("c3"), Screenshot("c3")];
        Assert.Same(messages, Compaction.CompactScreenHistory(messages, 1024));
        var compacted = Compaction.CompactScreenHistory(messages, 0);
        Assert.Contains("[earlier screen elided", compacted[0].Content, StringComparison.Ordinal);
        foreach (var index in new[] { 1, 3 })
        {
            Assert.Equal("Tapped #e3.\n[earlier screen elided; the newest \"Screen\" describes the screen]", compacted[index].Content);
        }

        Assert.Same(messages[5], compacted[5]);
        Assert.Equal(3, ImageCount(compacted));
    }

    [Fact]
    public void Keeps_the_notice_that_replaced_a_screenshot_when_the_screen_beside_it_is_elided()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, FullScreenResult("c1"), Screenshot("c1"), FullScreenResult("c2"), Screenshot("c2")];
        var compacted = Compaction.CompactScreenHistory(Compaction.CompactScreenshotHistory(messages, 1, 1), 0);
        Assert.Contains("[earlier screen elided", compacted[1].Content, StringComparison.Ordinal);
        Assert.Contains(Compaction.ScreenshotElidedNotice, compacted[2].Content, StringComparison.Ordinal);
        Assert.Null(compacted[2].Images);
        Assert.Same(messages[3], compacted[3]);
        Assert.Same(messages[4], compacted[4]);
    }

    [Fact]
    public void Shrinks_a_pixel_mode_history_on_the_budget_free_pass_the_overflow_retry_runs_after_the_cache_friendly_pass_kept_it()
    {
        IReadOnlyList<ModelMessage> messages = [OpeningWithScreen, FullScreenResult("c1", 40), Screenshot("c1"), ChangeResult("c2"), FullScreenResult("c3"), FullScreenResult("c4", 40), Screenshot("c4")];
        var kept = Compaction.CompactScreenHistory(messages);
        Assert.Same(messages, kept);
        var shrunk = Compaction.CompactScreenHistory(kept, 0);
        Assert.True(TextChars(shrunk) < TextChars(kept));
        // Text and pixel screens elide alike; the change update and the newest screen survive whole.
        Assert.Contains("[earlier screen elided", shrunk[4].Content, StringComparison.Ordinal);
        Assert.Same(messages[3], shrunk[3]);
        Assert.Same(messages[5], shrunk[5]);
    }
}
