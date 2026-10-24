// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// An off-screen target under sticky chrome. The agreement content is a plain (non-virtualized)
/// scroll view, so every section, including the "Accept terms" button roughly three screens down, is
/// present in the UI Automation tree as an off-screen node from the first snapshot. A sticky footer
/// overlaps the bottom of the scroll area, so bringing the accept button into view is not enough: a
/// tap must land it clear of the footer, and the footer's Continue button stays a decoy until the
/// terms are accepted.
/// </summary>
public sealed class StickyChrome : Component
{
    private const int SectionCount = 14;

    public override Element Render()
    {
        var (accepted, setAccepted) = UseState(false);
        var (decoyTapped, setDecoyTapped) = UseState(false);
        var (done, setDone) = UseState(false);

        if (done)
        {
            return VStack(Success("Terms accepted")).Padding(24);
        }

        var sections = Enumerable.Range(1, SectionCount).Select(section => VStack(4,
                BodyStrong($"Section {section}"),
                Body($"Deterministic agreement paragraph {section} of {SectionCount}. Nothing in this section is actionable; it only adds scroll distance between the top of the screen and the accept button.").TextWrapping())
            .WithKey(section.ToString()));

        var content = ScrollView(VStack(16, [
            Hint("Read the agreement, tap Accept terms at the bottom, then Continue."),
            .. sections,
            Button(accepted ? "Terms accepted ✓" : "Accept terms", () => setAccepted(true)).AutomationId("accept-terms"),
        ]).Padding(24, 16, 24, 96));

        var footer = VStack(8,
                When(decoyTapped && !accepted, () => Body("Accept the terms at the bottom first").Foreground("#b00020")),
                Button("Continue", () =>
                {
                    if (accepted)
                    {
                        setDone(true);
                    }
                    else
                    {
                        setDecoyTapped(true);
                    }
                }).Opacity(accepted ? 1 : 0.5).HAlign(HorizontalAlignment.Stretch).AutomationId("continue-button"))
            .Padding(16)
            .Background(Theme.SolidBackground)
            .VAlign(VerticalAlignment.Bottom);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            content,
            footer);
    }
}
