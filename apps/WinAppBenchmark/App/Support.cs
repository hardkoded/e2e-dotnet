// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace WinAppBenchmark.App;

/// <summary>What every scenario shares: the success line, the hint line, deterministic timers, and brushes.</summary>
public static class Support
{
    /// <summary>The terminal element of a scenario whose tree is intact, AutomationId "success-message".</summary>
    public static Element Success(string text) => TextBlock(text).SemiBold().FontSize(20).TextWrapping().AutomationId("success-message");

    /// <summary>The on-screen instruction of a scenario.</summary>
    public static Element Hint(string text) => Body(text).TextWrapping().Foreground("#555555");

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread after <paramref name="milliseconds"/>, the
    /// port of upstream's setTimeout. A cancelled <paramref name="token"/> drops the action.
    /// </summary>
    public static void After(int milliseconds, CancellationToken token, Action action)
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        _ = Task.Delay(milliseconds, token).ContinueWith(
            delay =>
            {
                if (delay.Status == TaskStatus.RanToCompletion)
                {
                    queue.TryEnqueue(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            action();
                        }
                    });
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>A solid brush from a six or eight digit hex color.</summary>
    public static SolidColorBrush Brush(string hex)
    {
        var digits = hex.TrimStart('#');
        var value = Convert.ToUInt32(digits, 16);
        var alpha = digits.Length == 8 ? (byte)(value >> 24) : (byte)0xFF;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
}
