// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;

namespace E2E;

/// <summary>
/// Whether the built-in agent's step shows pixels. Once a screenshot has gone to the model, every look of the step carries a fresh one, so
/// the model sees what each action did. Upstream keeps this in <c>ScreenPresenter.showingPixels</c> (<c>agent/screen-update.ts</c>); the
/// port's built-in agent prints its screens without the presenter, so the flag has a type of its own (.NET-only name). One per step:
/// the opening prompt and the tools of a step reach the same one through <see cref="For"/>, as upstream's <c>presenterFor</c> does.
/// </summary>
internal sealed class PixelMode
{
    private static readonly ConditionalWeakTable<StepExecutorContext, PixelMode> Steps = [];

    /// <summary>True once a screenshot went to the model in this step.</summary>
    public bool Showing { get; set; }

    /// <summary>The step's pixel mode.</summary>
    public static PixelMode For(StepExecutorContext context) => Steps.GetValue(context, static _ => new PixelMode());

    /// <summary>The line under a screenshot: its pixel size and its scale to the viewport's CSS pixels (upstream's <c>screenshotNote</c>).</summary>
    public static string Note(ExecutorPixels pixels) =>
        "Screenshot attached: " + pixels.Width.ToString(CultureInfo.InvariantCulture) + " by " + pixels.Height.ToString(CultureInfo.InvariantCulture) + " pixels"
        + (pixels.Scale == 1 ? "" : " (" + pixels.Scale.ToString("R", CultureInfo.InvariantCulture) + " per CSS pixel)") + ".";

    /// <summary>Why pixels did not reach the model, and what to do instead (upstream's <c>withheldAdvice</c>).</summary>
    public static string WithheldAdvice(VisionDegradation code) => code switch
    {
        VisionDegradation.MaskingUnproven => "the engine could not prove every secure field on screen masked (MASKING_UNPROVEN). Work from the tree and act on the nodes it lists.",
        _ => "this engine captures no pixels (UNSUPPORTED_CAPABILITY). Work from the tree and act on the nodes it lists.",
    };
}
