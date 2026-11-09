// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// The masked pixels of an observation (upstream's <c>PixelCapture</c>). <see cref="Pixels"/> carries the image, its scale and the
/// number of regions covered (upstream's <c>pixels</c> and <c>maskedRegionCount</c>, as one <see cref="EngineScreenshot"/>).
/// </summary>
/// <param name="Pixels">The image, with every secure field covered.</param>
/// <param name="MaskingProven">Every secure-field probe completed, independently of semantic capture.</param>
internal sealed record PixelCapture(EngineScreenshot Pixels, bool MaskingProven)
{
    /// <summary>How many regions the masks cover (upstream's <c>maskedRegionCount</c>).</summary>
    public int MaskedRegionCount => Pixels.MaskedRegionCount ?? 0;
}

/// <summary>Masked pixel capture for an observation (upstream's <c>web/src/observe.ts</c>).</summary>
internal static class WebObserve
{
    // The fields the page script marks secure.
    private const string SecureFieldSelector = ReadNode.SecureFieldSelector;

    // The same fields inside the closed shadow roots Playwright's own selectors stop at.
    private const string ClosedSecureFieldSelector = PageScript.ClosedShadowSelectorEngine + "=" + SecureFieldSelector;

    // Budget for one masked screenshot, still capped by the operation timeout. A heavy page that cannot produce a frame promptly must
    // cost the observation a moment, not the whole step: the caller degrades to tree-only input rather than failing.
    private const float PixelCaptureTimeoutMs = 10_000;

    /// <summary>
    /// Two mask locators per frame Playwright can reach, covering every secure field: one through light DOM and open shadow roots, one
    /// through the closed roots the reader also walks. Sweeping every frame rather than only the frames an observation walked makes the
    /// masked set a superset of the observed secure set, the direction that is safe. A frame with no readable document shows nothing to
    /// mask, and probing it would wait for its navigation to commit.
    /// </summary>
    public static async Task<List<ILocator>> SecureFieldMasksAsync(IPage page) =>
        (await FrameDocument.ReadableFramesAsync(page.Frames).ConfigureAwait(false))
            .SelectMany(frame => new[] { frame.Locator(SecureFieldSelector), frame.Locator(ClosedSecureFieldSelector) }).ToList();

    /// <summary>
    /// Captures masked viewport pixels. Secure fields are covered before the image leaves the engine and the covered regions are counted, so
    /// the runner can prove the image is at least as redacted as the tree. <c>Scale = css</c> keeps the image in the CSS pixel space every
    /// observed node rect already uses, which is what lets the runner hit-test a point against the same tree.
    /// </summary>
    public static async Task<PixelCapture> CapturePixelsAsync(IPage page, OperationBudget budget, ViewportSize viewport)
    {
        var masks = await budget.WithinAsync(SecureFieldMasksAsync(page)).ConfigureAwait(false);

        // A frame that detaches mid-sweep contributes nothing to the count, which can only push the observation toward withholding the image.
        var counts = await budget.WithinAsync(Task.WhenAll(masks.Select(CountAsync))).ConfigureAwait(false);

        // The adopted stylesheet hides the caret where Playwright's own option would write an inline style a hydrating framework flags.
        var options = new PageScreenshotOptions
        {
            Type = ScreenshotType.Png,
            Scale = ScreenshotScale.Css,
            Animations = ScreenshotAnimations.Disabled,
            Caret = Microsoft.Playwright.ScreenshotCaret.Initial,
            Timeout = Math.Max(1, Math.Min(budget.PlaywrightTimeout, PixelCaptureTimeoutMs)),
        };
        if (masks.Count > 0)
        {
            options.Mask = masks;
            options.MaskColor = "#000000";
        }

        var png = await ScreenshotCaret.WithHiddenCaretAsync(page, () => budget.WithinAsync(page.ScreenshotAsync(options))).ConfigureAwait(false);

        // The bytes are the authority on their own size. `Scale = css` is asked for precisely so one image pixel is one CSS pixel, but the
        // ratio is measured rather than assumed: if a capture ever comes back at device scale, reporting the viewport instead would displace
        // every coordinate the model reads off it.
        var width = E2E.Pixels.ReadPngSize(png)?.Width ?? viewport.Width;
        return new PixelCapture(
            new EngineScreenshot(png, viewport.Width > 0 ? (double)width / viewport.Width : 1, counts.Sum(count => count ?? 0)),
            masks.Count > 0 && counts.All(count => count is not null));
    }

    // The regions one mask covers, or null when its frame could not say (upstream's Promise.allSettled).
    private static async Task<int?> CountAsync(ILocator mask)
    {
        try
        {
            return await mask.CountAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
