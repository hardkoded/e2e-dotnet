// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Reactor.Input;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>Long-press, swipe left, and double-tap targets in sequence, each gating the next step.</summary>
public sealed class Gestures : Component
{
    private const double SwipeThreshold = -80;
    private const int DoubleTapWindowMilliseconds = 300;
    private static readonly TimeSpan LongPressDuration = TimeSpan.FromMilliseconds(800);

    public override Element Render()
    {
        var (step, setStep) = UseState(0);
        var lastTap = UseRef(DateTime.MinValue);

        // Detects a double tap by comparing timestamps of consecutive presses and advances the flow
        // when two taps land within the allowed window.
        void DoubleTapCandidate()
        {
            var now = DateTime.UtcNow;
            if (now - lastTap.Current <= TimeSpan.FromMilliseconds(DoubleTapWindowMilliseconds) && step == 2)
            {
                setStep(3);
            }

            lastTap.Current = now;
        }

        if (step == 3)
        {
            return VStack(Success("All gestures completed")).Padding(24);
        }

        Element target = step switch
        {
            0 => VStack(12,
                Hint("Long-press the button below (hold ~1 second)"),
                Button("Hold me", null)
                    .OnLongPress(() => setStep(1), LongPressDuration, enableMouseEmulation: true)
                    .AutomationId("long-press-target")),
            1 => VStack(12,
                Hint("Swipe the card below to the left"),
                Border(Body("Swipe me left").AutomationId("swipe-target").Padding(24, 32))
                    .Background(Theme.CardBackground)
                    .WithBorder(Theme.CardStroke)
                    .CornerRadius(12)
                    .OnPan(_ => { }, onEnded: gesture =>
                    {
                        if (gesture.Translation.X < SwipeThreshold)
                        {
                            setStep(2);
                        }
                    }, axis: PanAxis.Horizontal)),
            _ => VStack(12,
                Hint("Double-tap the button below"),
                Button("Double-tap me", DoubleTapCandidate).AutomationId("double-tap-target")),
        };

        return VStack(16,
            Caption($"Step {step + 1} of 3"),
            target)
            .Padding(24);
    }
}
