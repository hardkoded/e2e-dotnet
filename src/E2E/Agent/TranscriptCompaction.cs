// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E;

/// <summary>
/// What the model no longer needs to re-read, elided from the transcript (upstream's <c>transcript-compaction.ts</c>). Every full screen and
/// every screenshot the transcript carries is a candidate for elision once newer ones supersede it. Rewriting an earlier message changes
/// the request prefix, and the provider's prompt cache serves only an unchanged prefix, so stale parts are tolerated up to a budget and then
/// elided in one batch rather than one rewrite per turn. Both elisions walk the transcript the same way: oldest message first, across the
/// opening prompt and the tool results alike. Differences: a message here holds one text and its images, where upstream's holds parts, and
/// the pattern of a full screen (<see cref="OverflowShrink.FullScreenPattern"/>) also takes the <c>Screen (</c> heading of the <c>[ref=…]</c>
/// screen a custom executor may print.
/// </summary>
internal static class TranscriptCompaction
{
    /// <summary>How many of the newest full screens stay verbatim in the transcript (upstream's <c>FULL_SCREEN_PRESERVE_COUNT</c>).</summary>
    private const int FullScreenPreserveCount = 1;

    /// <summary>
    /// Bytes of superseded full screens a transcript carries before they are elided. Re-reading a stale screen from the cache costs a tenth
    /// of sending its replacement, so one that still fits under this budget stays verbatim (upstream's <c>KEEP_STALE_SCREEN_BYTES</c>).
    /// </summary>
    internal const int KeepStaleScreenBytes = 32 * 1024;

    /// <summary>How many of the newest screenshots stay in the transcript verbatim (upstream's <c>SCREENSHOT_PRESERVE_COUNT</c>).</summary>
    internal const int ScreenshotPreserveCount = 2;

    /// <summary>
    /// Superseded screenshots tolerated before they are elided in one batch, so the conversation never carries more than five images
    /// (upstream's <c>SCREENSHOT_ELIDE_BATCH</c>). A model that holds a dozen near-identical screenshots loses track of which one is current.
    /// </summary>
    internal const int ScreenshotElideBatch = 3;

    internal const string ScreenshotElidedNotice = "[earlier screenshot elided; the newest screenshots show the current screen]";

    /// <summary>
    /// Elides full screens the transcript no longer needs: every full screen but the newest is reduced to its lead and a notice, once the
    /// stale screens together outgrow <paramref name="keepStaleBytes"/>. Returns the input list unchanged when nothing qualifies.
    /// </summary>
    public static IReadOnlyList<ModelMessage> CompactScreenHistory(IReadOnlyList<ModelMessage> messages, int keepStaleBytes = KeepStaleScreenBytes)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var screens = messages.Where(CarriesScreen).ToList();
        var stale = screens.Count - FullScreenPreserveCount;
        if (stale <= 0 || screens.Take(stale).Sum(message => Encoding.UTF8.GetByteCount(message.Content!)) <= keepStaleBytes)
        {
            return messages;
        }

        return Rewrite(messages, stale, CarriesScreen, message => With(message, OverflowShrink.ElideScreen(message.Content!), message.Images));
    }

    /// <summary>
    /// Elides screenshots the transcript no longer needs: every image but the newest <paramref name="preserve"/> becomes a one-line notice
    /// in its message, once <paramref name="batch"/> stale images have piled up. Returns the input list unchanged when nothing qualifies.
    /// </summary>
    public static IReadOnlyList<ModelMessage> CompactScreenshotHistory(IReadOnlyList<ModelMessage> messages, int preserve = ScreenshotPreserveCount, int batch = ScreenshotElideBatch)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var stale = messages.Sum(ImageCount) - preserve;
        if (stale < batch)
        {
            return messages;
        }

        // Counted in images, as upstream counts parts: a message of several images spends several of the count.
        var remaining = stale;
        var next = new List<ModelMessage>(messages.Count);
        foreach (var message in messages)
        {
            var count = ImageCount(message);
            if (remaining <= 0 || count == 0)
            {
                next.Add(message);
                continue;
            }

            var elide = Math.Min(count, remaining);
            remaining -= elide;
            var kept = message.Images!.Skip(elide).ToList();
            var content = string.Join("\n", new[] { message.Content }.Where(text => !string.IsNullOrEmpty(text)).Append(ScreenshotElidedNotice));
            next.Add(With(message, content, kept.Count == 0 ? null : kept));
        }

        return next;
    }

    private static bool CarriesScreen(ModelMessage message) =>
        message.Role is "user" or "tool" && message.Content is { } text && OverflowShrink.FullScreenPattern().IsMatch(text);

    private static int ImageCount(ModelMessage message) => string.Equals(message.Role, "user", StringComparison.Ordinal) ? message.Images?.Count ?? 0 : 0;

    // Rewrites the oldest `count` messages that qualify and leaves every other message untouched, by identity.
    private static List<ModelMessage> Rewrite(IReadOnlyList<ModelMessage> messages, int count, Func<ModelMessage, bool> qualifies, Func<ModelMessage, ModelMessage> rewrite)
    {
        var remaining = count;
        var next = new List<ModelMessage>(messages.Count);
        foreach (var message in messages)
        {
            if (remaining > 0 && qualifies(message))
            {
                remaining--;
                next.Add(rewrite(message));
            }
            else
            {
                next.Add(message);
            }
        }

        return next;
    }

    private static ModelMessage With(ModelMessage message, string? content, IReadOnlyList<ModelImage>? images) => new()
    {
        Role = message.Role,
        Content = content,
        ToolCalls = message.ToolCalls,
        ToolCallId = message.ToolCallId,
        Name = message.Name,
        ReasoningItems = message.ReasoningItems,
        Images = images,
    };
}
