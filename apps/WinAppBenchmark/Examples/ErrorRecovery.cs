// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Deterministic error-and-recovery loop: the first load always fails with a retryable error banner,
/// the retry always succeeds, and finishing the flow requires confirming a destructive action through
/// a modal.
/// </summary>
public sealed class ErrorRecovery : Component
{
    private const int LoadMilliseconds = 1500;

    private enum Phase { Loading, Error, Loaded }

    public override Element Render()
    {
        var (phase, setPhase) = UseState(Phase.Loading);
        var (confirmVisible, setConfirmVisible) = UseState(false);
        var (deleted, setDeleted) = UseState(false);
        var attempts = UseRef(0);
        var pending = UseRef<CancellationTokenSource?>(null);

        UseEffect(() =>
        {
            var cts = new CancellationTokenSource();
            pending.Current = cts;
            After(LoadMilliseconds, cts.Token, () =>
            {
                attempts.Current += 1;
                setPhase(Phase.Error);
            });
            return () => cts.Cancel();
        }, Array.Empty<object>());

        // Retries the failed load; the second attempt deterministically succeeds.
        void Retry()
        {
            setPhase(Phase.Loading);
            After(LoadMilliseconds, pending.Current?.Token ?? CancellationToken.None, () =>
            {
                attempts.Current += 1;
                setPhase(attempts.Current >= 2 ? Phase.Loaded : Phase.Error);
            });
        }

        if (phase == Phase.Loading)
        {
            return VStack(12,
                ProgressRing().AutomationId("loading-indicator"),
                Hint("Loading drafts…"))
                .Padding(24);
        }

        if (phase == Phase.Error)
        {
            return VStack(12,
                InfoBar("Network error", "could not load drafts").Error().AutomationId("error-banner"),
                Button("Retry", Retry).AutomationId("retry-button"))
                .Padding(24);
        }

        var drafts = deleted
            ? VStack(Success("Draft deleted"))
            : VStack(12,
                Subtitle("Drafts"),
                Body("Untitled draft — “Quarterly numbers…”"),
                Button("Delete draft", () => setConfirmVisible(true)).AutomationId("delete-draft"));

        var confirm = ScrollViewer(VStack(12,
                Subtitle("Delete this draft?"),
                Hint("This action cannot be undone."),
                HStack(8,
                    Button("Cancel", () => setConfirmVisible(false)).AutomationId("confirm-cancel"),
                    Button("Delete", () =>
                    {
                        setConfirmVisible(false);
                        setDeleted(true);
                    }).AutomationId("confirm-delete")))
                .Padding(20))
            .Background(Theme.CardBackground)
            .CornerRadius(12)
            .VAlign(VerticalAlignment.Center)
            .HAlign(HorizontalAlignment.Center);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            drafts.Padding(24),
            When(confirmVisible, () => Border(confirm).Background("#80000000")));
    }
}
