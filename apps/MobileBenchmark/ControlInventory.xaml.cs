using System.Diagnostics;

namespace MobileBenchmark;

/// <summary>
/// Upstream's Control Inventory scenario, the Gestures section only: a long-press target and a double-tap target that echo the
/// gesture, and the count of taps that arrived with the gap between the last two.
/// </summary>
public partial class ControlInventory : ContentPage
{
    private const int DoubleTapWindowMilliseconds = 300;
    private const int LongPressMilliseconds = 500;

    private readonly Stopwatch _held = new();
    private long _lastTapTicks = long.MinValue;
    private int _tapCount;

    public ControlInventory()
    {
        InitializeComponent();
    }

    private void OnGesturesTabClicked(object? sender, EventArgs e) => GesturesSection.IsVisible = true;

    private void OnHoldMePressed(object? sender, EventArgs e) => _held.Restart();

    private void OnHoldMeReleased(object? sender, EventArgs e) =>
        GestureStatus.Text =
            _held.ElapsedMilliseconds >= LongPressMilliseconds ? "gesture: long press" : "gesture: short press";

    /// <summary>Detects a double tap from consecutive presses and reports the count and the gap, so a pair that missed the window still shows what arrived.</summary>
    private void OnDoubleTapCandidate(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var gap = _tapCount == 0 ? 0 : (int)Stopwatch.GetElapsedTime(_lastTapTicks, now).TotalMilliseconds;
        _lastTapTicks = now;
        _tapCount++;
        TapCount.Text = _tapCount == 1 ? "1 tap" : $"{_tapCount} taps, {gap} ms apart";
        GestureStatus.Text =
            _tapCount > 1 && gap <= DoubleTapWindowMilliseconds ? "gesture: double tap" : "gesture: single tap";
    }
}
