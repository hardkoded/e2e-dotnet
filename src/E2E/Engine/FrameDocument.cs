// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>Which frames hold a document script can read (upstream's <c>frame-document.ts</c>).</summary>
internal static class FrameDocument
{
    /// <summary>
    /// True when Playwright can evaluate in the frame without waiting on a navigation.
    /// </summary>
    /// <remarks>
    /// A child frame that has not committed a navigation (its URL is empty, as while its first request is held) shows its initial empty
    /// document. Chromium creates no script context for that document until script in its parent touches it, and Playwright waits for the
    /// navigation to commit instead. Reading <c>contentDocument</c> from the parent is that touch, so the frame is readable from then on. A
    /// parent that cannot read it (a sandboxed frame) means no script could have written into it either: it shows nothing. When the check
    /// itself fails, the frame counts as readable, so the caller's own read of it decides. <paramref name="owner"/> is the frame's element
    /// when the caller already holds it.
    /// </remarks>
    public static async Task<bool> HasReadableDocumentAsync(IFrame frame, IElementHandle? owner = null)
    {
        if (frame.Url != "" || frame.ParentFrame is null)
        {
            return true;
        }

        if (owner is not null)
        {
            return await ParentReadsDocumentAsync(owner).ConfigureAwait(false);
        }

        IElementHandle fetched;
        try
        {
            fetched = await frame.FrameElementAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return true;
        }

        try
        {
            return await ParentReadsDocumentAsync(fetched).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await fetched.DisposeAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }
    }

    /// <summary>The page's frames whose documents script can read; see <see cref="HasReadableDocumentAsync"/>.</summary>
    public static async Task<List<IFrame>> ReadableFramesAsync(IReadOnlyList<IFrame> frames)
    {
        var readable = await Task.WhenAll(frames.Select(frame => HasReadableDocumentAsync(frame))).ConfigureAwait(false);
        return frames.Where((_, index) => readable[index]).ToList();
    }

    /// <summary>Reads the frame's document from its parent, which gives it a script context; true when that read fails.</summary>
    private static async Task<bool> ParentReadsDocumentAsync(IElementHandle owner)
    {
        try
        {
            return await owner.EvaluateAsync<bool>("element => 'contentDocument' in element && element.contentDocument !== null").ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return true;
        }
    }
}
