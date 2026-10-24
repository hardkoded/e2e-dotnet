// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Xaml.Automation.Peers;
using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Deliberately hostile accessibility: every text and field is in the UI Automation raw view only,
/// the buttons are bare shapes with no peers, no labels, and no automation ids. The observation is
/// useless here and the agent must operate from vision and raw coordinates.
/// </summary>
public sealed class BrokenAccessibility : Component
{
    private const string SecretCode = "7291";

    private enum Step { Pick, Code, Done }

    public override Element Render()
    {
        var (step, setStep) = UseState(Step.Pick);
        var (code, setCode) = UseState("");
        var (wrongPick, setWrongPick) = UseState(false);

        return step switch
        {
            Step.Pick => VStack(24,
                Body("Tap the green circle to continue").AccessibilityView(AccessibilityView.Raw),
                When(wrongPick, () => Body("Wrong shape, try again").Foreground("#b00020").AccessibilityView(AccessibilityView.Raw)),
                HStack(24,
                    Ellipse().Fill(Brush("#c62828")).Size(64, 64).OnTapped((_, _) => setWrongPick(true)),
                    Rectangle().Fill(Brush("#2e7d32")).Size(64, 64).OnTapped((_, _) => setWrongPick(true)),
                    Ellipse().Fill(Brush("#2e7d32")).Size(64, 64).OnTapped((_, _) =>
                    {
                        setWrongPick(false);
                        setStep(Step.Code);
                    }),
                    Ellipse().Fill(Brush("#1565c0")).Size(64, 64).OnTapped((_, _) => setWrongPick(true))))
                .Padding(24),
            Step.Code => VStack(24,
                Body($"Enter the code {SecretCode} and press the arrow").AccessibilityView(AccessibilityView.Raw),
                HStack(16,
                    TextBox(code, setCode).MaxLength(4).Width(120).AccessibilityView(AccessibilityView.Raw),
                    Grid(
                        columns: [GridSize.Px(56)],
                        rows: [GridSize.Px(56)],
                        Ellipse().Fill(Brush(code == SecretCode ? "#2e7d32" : "#9e9e9e")).Size(56, 56),
                        TextBlock("→").FontSize(24).Foreground("#ffffff").HAlign(HorizontalAlignment.Center).VAlign(VerticalAlignment.Center).AccessibilityView(AccessibilityView.Raw))
                    .OnTapped((_, _) =>
                    {
                        if (code == SecretCode)
                        {
                            setStep(Step.Done);
                        }
                    })))
                .Padding(24),
            _ => VStack(TextBlock("Access granted").SemiBold().FontSize(20).AccessibilityView(AccessibilityView.Raw)).Padding(24),
        };
    }
}
