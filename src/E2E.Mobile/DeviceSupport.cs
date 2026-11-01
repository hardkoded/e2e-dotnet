// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>A point on the screen, in the device's logical points.</summary>
public readonly record struct Point(double X, double Y);

/// <summary>A box on the screen, in the device's logical points.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>The logical size of the screen, in points.</summary>
public readonly record struct ViewportSize(double Width, double Height);

/// <summary>The screenshot fields a viewport probe reads off the device client's response.</summary>
public sealed record RawScreenshotResult
{
    public string? Path { get; init; }

    public double? Width { get; init; }

    public double? Height { get; init; }

    public double? LogicalWidth { get; init; }

    public double? LogicalHeight { get; init; }

    public double? PixelDensity { get; init; }
}

/// <summary>Shared helpers for the device engine: error constructors, filenames, PNG headers, gestures, the screen location and size.</summary>
public static partial class DeviceSupport
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static EngineException Cancelled(string text) => new(EngineErrorCodes.Cancelled, text, retryable: false);

    public static EngineException InvalidState(string text) => new(EngineErrorCodes.InvalidState, text, retryable: false);

    public static EngineException NotActionable(string text) => new(EngineErrorCodes.NotActionable, text, retryable: false);

    public static EngineException Unsupported(string text) => new(EngineErrorCodes.UnsupportedCapability, text, retryable: false);

    /// <summary>Constrains a caller-supplied artifact label to a safe filename.</summary>
    public static string SanitizeFilename(string name)
    {
        var safe = UnsafeFilenameCharacters().Replace(name, "_");
        safe = safe.Length > 64 ? safe[..64] : safe;
        return safe.Length == 0 ? "artifact" : safe;
    }

    /// <summary>Reads the dimensions out of a PNG's IHDR chunk; null for anything else.</summary>
    public static (int Width, int Height)? ReadPngSize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return null;
        }

        return (ReadBigEndian(data[16..]), ReadBigEndian(data[20..]));
    }

    /// <summary>
    /// The finger gesture that scrolls one rect's content in <paramref name="direction"/>. Scrolling
    /// down reveals what is below, so the finger travels up; the travel is a share of the rect's extent
    /// scaled by momentum, and never leaves the rect. The root scroll sends no share (the device
    /// client's default); these are for a gesture inside one control, and deterministic tests step
    /// through a list with <see cref="Momentum.Slow"/>, a quarter of the box, shorter than any row, so
    /// every row lands fully on screen at some stop. At three quarters the mobile benchmark's scenario
    /// list dragged rows past.
    /// </summary>
    public static (Point From, Point To) SwipeWithin(Rect rect, ScrollDirection direction, Momentum? momentum)
    {
        var ratio = momentum switch
        {
            Momentum.Fast => 0.8,
            Momentum.Slow => 0.25,
            _ => 0.5,
        };
        var centre = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var dx = rect.Width * ratio / 2;
        var dy = rect.Height * ratio / 2;
        return direction switch
        {
            ScrollDirection.Down => (new Point(centre.X, centre.Y + dy), new Point(centre.X, centre.Y - dy)),
            ScrollDirection.Up => (new Point(centre.X, centre.Y - dy), new Point(centre.X, centre.Y + dy)),
            ScrollDirection.Right => (new Point(centre.X + dx, centre.Y), new Point(centre.X - dx, centre.Y)),
            ScrollDirection.Left => (new Point(centre.X - dx, centre.Y), new Point(centre.X + dx, centre.Y)),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
    }

    /// <summary>
    /// The location a device surface reports in a snapshot: the foreground app and the screen shown,
    /// <c>&lt;app&gt; / &lt;screen title&gt;</c>. A simulator has no address bar, so this is an opaque
    /// address the harness shows to the model and the report and anchors trace replay on; it changes
    /// exactly when the app or its screen changes. It is deliberately not a URL: no scheme, no colon,
    /// so the harness never mistakes it for one and applies an origin policy to it. Null when neither
    /// the app nor a title is known.
    /// </summary>
    public static string? ScreenLocation(string? app, string? title)
    {
        var parts = new[] { app, title }
            .Select(part => part is null ? "" : Whitespace().Replace(part, " ").Trim())
            .Where(part => part.Length > 0)
            .ToList();
        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }

    /// <summary>
    /// The device's logical screen size as its screenshot reports it, in points: the logical dimensions
    /// when given, else the pixel dimensions over the density. Null when the response carries neither.
    /// </summary>
    public static ViewportSize? LogicalScreenSize(RawScreenshotResult result)
    {
        ViewportSize? logical =
            result.LogicalWidth is { } logicalWidth && result.LogicalHeight is { } logicalHeight
                ? new ViewportSize(logicalWidth, logicalHeight)
                : result is { Width: { } width, Height: { } height, PixelDensity: > 0 and var density }
                    ? new ViewportSize(width / density, height / density)
                    : null;
        return logical is { Width: > 0, Height: > 0 } ? logical : null;
    }

    private static int ReadBigEndian(ReadOnlySpan<byte> data) =>
        (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFilenameCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>
/// One operation's budget, which the harness's token does not enforce: an input's <c>actionTimeout</c>, or the <c>timeout</c> a
/// locate gets. Every device command the operation sends runs under it: the inputs through <see cref="SendAsync{T}"/>, the
/// snapshots through <see cref="ReadAsync{T}"/>. Upstream's <c>InputBudget</c> in <c>support.ts</c>.
/// </summary>
/// <remarks>
/// <paramref name="track"/> keeps the device command itself in flight, past a timeout that abandons it, until it settles. The
/// callers' own tracking settles with the race, so this is the registration the next attempt waits on. Upstream's <c>track</c>
/// returns the promise it is given; the port's registers the task and the budget keeps its own reference, so it returns nothing.
/// Its <paramref name="time"/> is the .NET stand-in for the fake timers upstream's tests install (<c>vi.useFakeTimers()</c>): the budget and the wait for an answer both run on it, so a test drives them without real time.
/// </remarks>
internal sealed class InputBudget(string label, TimeSpan timeout, Action<Task> track, TimeProvider? time = null)
{
    /// <summary>The least time an input gets to answer once sent, however little budget was left: a tap that lands is then not reported as cut off.</summary>
    private static readonly TimeSpan MinAnswer = TimeSpan.FromMilliseconds(250);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly long _startedAt = (time ?? TimeProvider.System).GetTimestamp();
    private bool _sent;

    /// <summary>
    /// Sends one input with what is left of the budget. With none left it sends nothing: <c>OPERATION_TIMEOUT</c>, or
    /// <c>ACTION_MAY_HAVE_COMMITTED</c> once an earlier input of this operation went out. A gesture that holds
    /// (<paramref name="hold"/>) at least as long as the budget left could only be cut off mid-gesture, so it is refused
    /// unsent. An input that does not answer in time may have landed, so it is <c>ACTION_MAY_HAVE_COMMITTED</c> and the
    /// runner never repeats it.
    /// </summary>
    public async Task<T> SendAsync<T>(Func<Task<T>> dispatch, CancellationToken cancellationToken, TimeSpan? hold = null)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        var remaining = Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw Spent();
        }

        if (hold is { } held && held >= remaining)
        {
            throw new EngineException(
                EngineErrorCodes.NotActionable,
                $"{label} holds for {(long)held.TotalMilliseconds} ms, but only {(long)remaining.TotalMilliseconds} ms of its timeout are left; raise the timeout or shorten the hold",
                retryable: false);
        }

        var pending = dispatch();
        track(pending);

        // An input counts as sent only once its command was dispatched, so one that throws first is not.
        _sent = true;
        try
        {
            return await pending.WaitAsync(remaining > MinAnswer ? remaining : MinAnswer, _time, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!pending.IsCompleted && ex is TimeoutException or OperationCanceledException)
        {
            // The abandoned command may still fail later; nothing is left to read it.
            _ = pending.ContinueWith(static abandoned => abandoned.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            if (ex is TimeoutException)
            {
                throw MaybeCommitted();
            }

            throw;
        }
    }

    /// <summary>The budget left, zero or less once it is spent. Upstream's <c>remainingMs</c>.</summary>
    public TimeSpan Remaining => timeout - _time.GetElapsedTime(_startedAt);

    /// <summary>
    /// Runs one device read with what is left of the budget. With none left it reads nothing. A read that does not answer in time is
    /// <c>OPERATION_TIMEOUT</c>, or <c>ACTION_MAY_HAVE_COMMITTED</c> once an input of this operation went out, so the runner never
    /// repeats that input. Upstream's <c>read</c>.
    /// </summary>
    public async Task<T> ReadAsync<T>(Func<Task<T>> dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        var remaining = Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw Spent();
        }

        var pending = dispatch();
        track(pending);
        try
        {
            return await pending.WaitAsync(remaining, _time, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!pending.IsCompleted && ex is TimeoutException or OperationCanceledException)
        {
            // The abandoned command may still fail later; nothing is left to read it.
            _ = pending.ContinueWith(static abandoned => abandoned.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            if (ex is TimeoutException)
            {
                throw _sent ? MaybeCommitted() : new EngineException(EngineErrorCodes.OperationTimeout, $"{label} timed out before the device answered", retryable: false);
            }

            // A read the caller cancelled is a cancel, as the command itself reports it.
            throw new EngineException(EngineErrorCodes.Cancelled, $"{label} cancelled", retryable: false, ex);
        }
    }

    /// <summary>The failure of a command the spent budget did not send.</summary>
    private EngineException Spent() =>
        _sent ? MaybeCommitted() : new EngineException(EngineErrorCodes.OperationTimeout, $"{label} timed out before it was sent", retryable: false);

    private EngineException MaybeCommitted() =>
        new(EngineErrorCodes.ActionMayHaveCommitted, $"{label} timed out before the device answered; its input may have been dispatched", retryable: false);
}
