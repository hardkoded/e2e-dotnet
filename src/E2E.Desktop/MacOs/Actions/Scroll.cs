// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>Scrolling a list or scroll area by its scroll bar, or by the page actions an element takes.</summary>
internal static partial class Actions
{
    /// <summary>About how many of a scroll bar's increments make a page.</summary>
    internal const double LinesPerPage = 8.0;

    /// <summary>A scroll area's scroll bar for one axis. A SwiftUI <c>List</c> lists it only as a child, with no <c>AXVerticalScrollBar</c>.</summary>
    public static Element? ScrollBar(Element element, bool vertical)
    {
        if (element.ElementAttribute(vertical ? "AXVerticalScrollBar" : "AXHorizontalScrollBar") is { } bar)
        {
            return bar;
        }

        var orientation = vertical ? "AXVerticalOrientation" : "AXHorizontalOrientation";
        var children = element.Elements("AXChildren", 16);
        if (!children.IsOk)
        {
            return null;
        }

        return children.Value!.FirstOrDefault(child =>
            child.Text("AXRole") == "AXScrollBar"
            && (
                child.Text("AXOrientation") is { } shown
                    ? shown == orientation
                    // No orientation: a vertical bar is taller than it is wide.
                    : child.Frame() is { } frame && (frame.Height > frame.Width) == vertical
            )
        );
    }

    /// <summary>Scrolls a scroll area by moving its scroll bar, <paramref name="pages"/> of its visible size.</summary>
    private static void Scroll(Element element, string direction, double pages)
    {
        var vertical = direction is "up" or "down";

        // A node that scrolls itself along the axis is the one moved; one that does not, a list or table, scrolls through the scroll
        // area that holds it. The enclosing pane comes last, so a swipe on a nested list never moves the pane around it.
        var (_, role) = RoleOf(element, "scroll");
        var own = ScrollsItself(
            role,
            ScrollBar(element, vertical) is not null,
            ActionNames(element, "scroll"),
            direction
        );
        var target = own ? null : EnclosingScrollArea(element, vertical);
        try
        {
            ScrollBy(target ?? element, direction, pages);
        }
        catch (Failure failure) when (failure is { Code: Codes.ActionNotSupported, Delivery: Delivery.NotDelivered })
        {
            // An element that takes none of the ways to scroll: say so, not which attribute it lacks. Like a window with nothing to
            // scroll, it is not actionable, not a capability the engine lacks.
            throw Failure.Refused(Codes.ActionFailed, $"nothing here scrolls {direction}");
        }
    }

    /// <summary>
    /// Whether a node scrolls itself along <paramref name="direction"/>, by the rule the snapshot offers <c>Scroll</c> with: a scroll
    /// bar for the axis, or, for any role but a scroll area, the page action for the direction. SwiftUI gives every scroll area the page
    /// actions, also one whose content fits or that scrolls only along the other axis, and the action then moves nothing while it
    /// answers success.
    /// </summary>
    internal static bool ScrollsItself(string role, bool bar, IReadOnlyList<string> actions, string direction) =>
        bar || (role != "scrollarea" && actions.Contains(PageAction(direction)));

    /// <summary>The nearest scroll area above <paramref name="element"/>, a few levels up, with a scroll bar for the axis.</summary>
    internal static Element? EnclosingScrollArea(Element element, bool vertical)
    {
        var current = element.ElementAttribute("AXParent");
        for (var level = 0; level < 4; level++)
        {
            if (current is null)
            {
                return null;
            }

            if (current.Text("AXRole") == "AXScrollArea" && ScrollBar(current, vertical) is not null)
            {
                return current;
            }

            current = current.ElementAttribute("AXParent");
        }

        return null;
    }

    /// <summary>The accessibility action that scrolls an element a page in <paramref name="direction"/>.</summary>
    private static string PageAction(string direction) =>
        direction switch
        {
            "down" => "AXScrollDownByPage",
            "up" => "AXScrollUpByPage",
            "right" => "AXScrollRightByPage",
            _ => "AXScrollLeftByPage",
        };

    /// <summary>Performs a scroll action <paramref name="count"/> times. Each goes through the cancel check; a failure after the first follows a scroll that landed.</summary>
    internal static void Repeat(Element element, string action, int count)
    {
        for (var sent = 0; sent < count; sent++)
        {
            try
            {
                Perform(element, action, "scroll");
            }
            catch (Failure failure) when (sent != 0)
            {
                throw failure.Delivered();
            }
        }
    }

    internal static void ScrollBy(Element element, string direction, double pages)
    {
        var vertical = direction is "up" or "down";
        var forward = direction is "down" or "right";
        if (ScrollBar(element, vertical) is { } bar)
        {
            // A position the app did not answer is no position: one step from the top could move the view the wrong way.
            double? current = null;
            var position = bar.Attribute("AXValue");
            if (position.IsOk)
            {
                current = (position.Value as AxValue.NumberValue)?.Value;
            }
            else if (position.Error == AxError.CannotComplete)
            {
                throw Unread(AxError.CannotComplete, "scroll");
            }

            var (_, _, width, height) = element.Frame() ?? (0.0, 0.0, 0.0, 0.0);
            var visible = vertical ? height : width;
            var contents = element.ElementAttribute("AXContents")
                ?? (
                    element.Elements("AXChildren", 16) is { IsOk: true } listed
                        ? listed.Value!.FirstOrDefault(child => child.Text("AXRole") != "AXScrollBar")
                        : null
                );
            var content = contents?.Frame() is { } frame ? (vertical ? frame.Height : frame.Width) : 0.0;
            var step = content > visible && visible > 0.0 ? pages * visible / (content - visible) : pages * 0.25;
            if (current is { } value && Settable(bar, "AXValue", "scroll"))
            {
                var next = Math.Clamp(value + (forward ? step : -step), 0.0, 1.0);
                Go("scroll");
                var written = bar.SetNumber("AXValue", next);
                if (written != AxError.Success)
                {
                    throw WriteFailed(written, "scroll");
                }

                return;
            }

            // A step is about a line: as many as make the pages asked for, so a fast swipe goes further than a slow one.
            Repeat(
                bar,
                forward ? "AXIncrement" : "AXDecrement",
                Math.Max((int)Math.Round(pages * LinesPerPage, MidpointRounding.AwayFromZero), 1)
            );
            return;
        }

        var names = ActionNames(element, "scroll");
        var (_, role) = RoleOf(element, "scroll");
        if (ScrollsItself(role, false, names, direction))
        {
            Repeat(element, PageAction(direction), Math.Max((int)Math.Round(pages, MidpointRounding.AwayFromZero), 1));
            return;
        }

        throw Failure.Refused(Codes.ActionFailed, $"nothing here scrolls {direction}");
    }
}
