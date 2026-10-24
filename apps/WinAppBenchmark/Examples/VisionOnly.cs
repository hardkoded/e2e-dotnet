// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Automation.Peers;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Simulates a canvas-rendered app (Flutter, game engines, custom renderers): the shapes have no UI
/// Automation peers and every text is in the raw view only, so an observation returns nothing
/// actionable. The agent has to read the on-screen instruction and tap the shapes in the right order
/// purely from pixels.
/// </summary>
public sealed class VisionOnly : Component
{
    private enum Shape { Circle, Square, Diamond }

    private static readonly Shape[] Sequence = [Shape.Square, Shape.Circle, Shape.Diamond];

    public override Element Render()
    {
        var (progress, setProgress) = UseState(0);
        var (failed, setFailed) = UseState(false);

        // Advances the sequence when the tapped shape matches the next expected one; any wrong tap
        // resets progress and shows a visual error.
        void Tap(Shape shape)
        {
            if (progress < Sequence.Length && shape == Sequence[progress])
            {
                setFailed(false);
                setProgress(progress + 1);
                return;
            }

            setFailed(true);
            setProgress(0);
        }

        var done = progress >= Sequence.Length;
        if (done)
        {
            return VStack(TextBlock("Sequence complete").SemiBold().FontSize(20).AccessibilityView(AccessibilityView.Raw)).Padding(24);
        }

        return VStack(24,
            Body("Tap: square, then circle, then diamond").AccessibilityView(AccessibilityView.Raw),
            Body($"Progress: {progress}/{Sequence.Length}{(failed ? " — wrong shape, start over" : "")}").AccessibilityView(AccessibilityView.Raw),
            HStack(32,
                Ellipse().Fill(Brush("#1565c0")).Size(72, 72).OnTapped((_, _) => Tap(Shape.Circle)),
                Rectangle().Fill(Brush("#6a1b9a")).Size(56, 56)
                    .Set(rectangle => rectangle.CenterPoint = new System.Numerics.Vector3(28, 28, 0))
                    .Rotation(45)
                    .Margin(8)
                    .OnTapped((_, _) => Tap(Shape.Diamond)),
                Rectangle().Fill(Brush("#2e7d32")).Size(72, 72).OnTapped((_, _) => Tap(Shape.Square))))
            .Padding(24);
    }
}
