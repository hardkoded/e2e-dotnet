// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Initial loading, a refresh, and a transient toast. The mobile app refreshes on a pull gesture; a
/// desktop has no pull to refresh, so a Refresh button (AutomationId "refresh-button") stands in for
/// it above the scroll view.
/// </summary>
public sealed class AsyncStates : Component
{
    private const int InitialLoadMilliseconds = 2000;
    private const int RefreshMilliseconds = 1200;
    private const int ToastMilliseconds = 2500;

    public override Element Render()
    {
        var (loading, setLoading) = UseState(true);
        var (refreshing, setRefreshing) = UseState(false);
        var (rewardVisible, setRewardVisible) = UseState(false);
        var (toastVisible, setToastVisible) = UseState(false);
        var (claimed, setClaimed) = UseState(false);
        var lifetime = UseRef<CancellationTokenSource?>(null);

        UseEffect(() =>
        {
            var cts = new CancellationTokenSource();
            lifetime.Current = cts;
            After(InitialLoadMilliseconds, cts.Token, () => setLoading(false));
            return () => cts.Cancel();
        }, Array.Empty<object>());

        var token = lifetime.Current?.Token ?? CancellationToken.None;

        // Simulates a refresh fetch that reveals the claim button once the refresh completes.
        void Refresh()
        {
            setRefreshing(true);
            After(RefreshMilliseconds, token, () =>
            {
                setRefreshing(false);
                setRewardVisible(true);
            });
        }

        // Claims the reward: shows a transient toast, then settles into the persistent success state
        // once the toast disappears.
        void Claim()
        {
            setToastVisible(true);
            After(ToastMilliseconds, token, () =>
            {
                setToastVisible(false);
                setClaimed(true);
            });
        }

        if (loading)
        {
            return VStack(12,
                ProgressRing().AutomationId("initial-loading"),
                Hint("Loading…"))
                .Padding(24);
        }

        Element content = claimed
            ? Success("Reward claimed")
            : rewardVisible
                ? Button("Claim reward", Claim).AutomationId("claim-button")
                : Hint("Refresh to reveal your reward");

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            HStack(12,
                Button("Refresh", Refresh).IsEnabled(!refreshing).AutomationId("refresh-button"),
                When(refreshing, () => ProgressRing().AutomationId("refreshing")))
                .Padding(24, 16),
            ScrollView(VStack(12, content).Padding(24)).AutomationName("Reward area").AutomationId("reward-scroll").Grid(row: 1),
            When(toastVisible, () => Border(Body("Claiming reward…").Foreground("#ffffff").Padding(16, 12))
                .Background("#cc222222")
                .CornerRadius(8)
                .HAlign(HorizontalAlignment.Center)
                .VAlign(VerticalAlignment.Bottom)
                .Margin(0, 0, 0, 24)
                .AutomationId("toast")).Grid(row: 1));
    }
}
