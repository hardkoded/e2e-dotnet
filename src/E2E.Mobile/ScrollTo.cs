// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>What the drag loop of <see cref="ScrollTo.DragToEdgeAsync"/> needs from the device. Upstream's <c>EdgeDevice</c>.</summary>
internal interface IEdgeDevice
{
    /// <summary>Sends <paramref name="count"/> drags toward the edge, back to back.</summary>
    Task DragAsync(int count);

    /// <summary>What the region shows after the content rests, or null for a read that left nodes out.</summary>
    Task<string?> ReadAsync();

    /// <summary>What is left of the input budget.</summary>
    TimeSpan Remaining { get; }
}

/// <summary>
/// <see cref="LocatorAction.ScrollTo"/> on a device, apart from the session: the loop that drags toward an edge until a drag moves nothing, and
/// what a container shows between drags. Upstream's <c>mobile/src/scroll-to.ts</c>.
/// </summary>
internal static class ScrollTo
{
    /// <summary>Most drags sent between two reads: a read costs more than a drag.</summary>
    private const int MaxEdgeDrags = 3;

    /// <summary>
    /// Roles left out of what a container shows: a spinner (a pull-to-refresh indicator, a loading row) changes on its own, so counting it would
    /// read a container at its edge as still moving.
    /// </summary>
    private static readonly HashSet<string> RestlessRoles = new(StringComparer.Ordinal) { "progressbar", "status" };

    /// <summary>
    /// The direction that moves content toward <paramref name="edge"/>, for <see cref="DeviceSupport.SwipeWithin"/>: to reach the bottom the
    /// content scrolls down, so the finger travels up. Without momentum it runs from a quarter in from one side of the region to a quarter in
    /// from the other, released without a hold so the content keeps going; the quarter keeps the start off a sticky footer or a tab bar. Upstream's
    /// <c>EDGE_DIRECTION</c>.
    /// </summary>
    internal static ScrollDirection EdgeDirection(ScrollEdge edge) =>
        edge switch
        {
            ScrollEdge.Top => ScrollDirection.Up,
            ScrollEdge.Bottom => ScrollDirection.Down,
            ScrollEdge.Left => ScrollDirection.Left,
            ScrollEdge.Right => ScrollDirection.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null),
        };

    /// <summary>
    /// Drags toward an edge until a drag leaves what the region shows unchanged, starting from <paramref name="before"/>, the first read. The
    /// first pass is one drag, since a container already at its edge needs no more; one that moves gets up to three per read. No pass starts that
    /// the budget cannot finish, estimated from the last read and the last drags' time per drag, since the drag count grows between passes. A read
    /// that left nodes out never reads as unchanged. Upstream's <c>dragToEdge</c>.
    /// </summary>
    internal static async Task DragToEdgeAsync(IEdgeDevice device, string? before, TimeProvider clock)
    {
        var last = before;
        var drags = 0;
        var read = TimeSpan.Zero;
        var drag = TimeSpan.Zero;
        for (var passes = 0; ; passes++)
        {
            var count = Math.Min(passes + 1, MaxEdgeDrags);
            if (passes > 0 && device.Remaining < read + (drag * count))
            {
                var reason = last is null
                    ? $"the screen was read with nodes left out (agent-device truncated it) after {drags.ToString(CultureInfo.InvariantCulture)} drags, so the edge could not be confirmed"
                    : $"the content was still moving after {drags.ToString(CultureInfo.InvariantCulture)} drags when the budget ran out";
                throw new EngineException(EngineErrorCodes.OperationTimeout, reason, retryable: false);
            }

            var dragStarted = clock.GetTimestamp();
            await device.DragAsync(count).ConfigureAwait(false);
            drags += count;
            var readStarted = clock.GetTimestamp();
            drag = clock.GetElapsedTime(dragStarted, readStarted) / count;
            var after = await device.ReadAsync().ConfigureAwait(false);
            read = clock.GetElapsedTime(readStarted);
            if (after is not null && after == last)
            {
                return;
            }

            last = after;
        }
    }

    /// <summary>
    /// What a region of the screen shows, as one comparable string: every node whose box meets the region, by role, name, text, value, test id
    /// (pages of a gallery can differ in nothing else), and box rounded to whole points. Two equal signatures around a drag mean the drag moved
    /// nothing, so the container is at its edge. Upstream's <c>regionSignature</c>.
    /// </summary>
    internal static string RegionSignature(IReadOnlyList<ProjectedNode> index, Rect region)
    {
        var lines = new List<string>();
        foreach (var projected in index)
        {
            var node = projected.Node;
            if (node.Rect is not { } rect || (node.Role is not null && RestlessRoles.Contains(node.Role)))
            {
                continue;
            }

            if (rect.X >= region.X + region.Width || rect.X + rect.Width <= region.X)
            {
                continue;
            }

            if (rect.Y >= region.Y + region.Height || rect.Y + rect.Height <= region.Y)
            {
                continue;
            }

            var box = string.Join(
                ',',
                new[] { rect.X, rect.Y, rect.Width, rect.Height }.Select(value =>
                    Math.Round(value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
                )
            );
            lines.Add(
                string.Join(
                    '\0',
                    node.Role ?? "",
                    node.Name ?? "",
                    node.Text ?? "",
                    node.Value ?? "",
                    node.TestId ?? "",
                    box
                )
            );
        }

        return string.Join('\n', lines);
    }

    /// <summary>The part of <paramref name="rect"/> on a screen of <paramref name="size"/>; a box wholly off it has no area. Upstream's <c>clipTo</c>.</summary>
    internal static Rect ClipTo(Rect rect, ViewportSize size)
    {
        var x = Math.Max(0, rect.X);
        var y = Math.Max(0, rect.Y);
        return new Rect(
            x,
            y,
            Math.Max(0, Math.Min(size.Width, rect.X + rect.Width) - x),
            Math.Max(0, Math.Min(size.Height, rect.Y + rect.Height) - y)
        );
    }

    /// <summary>A region cut off above <paramref name="top"/> (the soft keyboard's edge), so a drag never starts on a key. Upstream's <c>above</c>.</summary>
    internal static Rect Above(Rect region, double? top) =>
        top is not { } edge || edge >= region.Y + region.Height
            ? region
            : region with
            {
                Height = Math.Max(0, edge - region.Y),
            };
}
