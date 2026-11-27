// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Desktop.MacOs;

/// <summary>Where an element is, as far as showing it goes.</summary>
internal enum Placement
{
    /// <summary>In its window and every scroll area it is in.</summary>
    Shown,

    /// <summary>Outside a scroll area it is in, or its window: a scroll into view can bring it back.</summary>
    ScrolledOut,

    /// <summary>In a minimized window: no scroll shows it.</summary>
    Minimized,

    /// <summary>A control with no width or height and nothing inside it: it draws nothing, wherever it is.</summary>
    Collapsed,
}

/// <summary>
/// One window's accessibility tree, read into the raw snapshot shape the node projection reads: a node per element, a ref for every
/// control, and the tree cut where the depth limit, the node budget, or the deadline stops it. Rust's <c>snapshot.rs</c>.
/// </summary>
internal static class Snapshot
{
    /// <summary>The most nodes one snapshot reads; a window with more is read in part (<c>truncated</c>).</summary>
    private const int MaxNodes = 8_000;

    /// <summary>
    /// The most children read under one node; the rest are left out, and the node says so. As many as a snapshot reads in all, so a
    /// long list costs only what the node budget already bounds, and a window cut short is one too large to read.
    /// </summary>
    internal const int MaxChildren = MaxNodes;

    /// <summary>The most ancestors <see cref="PlacementOf"/> walks up from an element to its window.</summary>
    private const int MaxAncestors = 256;

    /// <summary>Every attribute one request reads of a node.</summary>
    private static readonly string[] s_attributes =
    [
        "AXRole",
        "AXSubrole",
        "AXTitle",
        "AXDescription",
        "AXValue",
        "AXIdentifier",
        "AXEnabled",
        "AXFocused",
        "AXSelected",
        "AXExpanded",
        "AXPosition",
        "AXSize",
        "AXPlaceholderValue",
        "AXHelp",
        "AXTitleUIElement",
        "AXVerticalScrollBar",
        "AXHorizontalScrollBar",
        "AXHeadingLevel",
    ];

    /// <summary>What a snapshot is asked for.</summary>
    public sealed record Request(int Pid, uint Window, int MaxDepth, bool Interactive, Instant Deadline);

    /// <summary>What a node's attributes say, read in one request.</summary>
    private sealed record Read
    {
        public required string Role { get; init; }

        public required string AxRole { get; init; }

        public string? Subrole { get; init; }

        public string? Title { get; init; }

        public string? Description { get; init; }

        public AxValue? Value { get; init; }

        public string? Identifier { get; init; }

        public bool? Enabled { get; init; }

        public bool? Focused { get; init; }

        public bool? Selected { get; init; }

        public bool? Expanded { get; init; }

        public Rect? Bounds { get; init; }

        public string? Placeholder { get; init; }

        public string? Help { get; init; }

        public Element? TitleElement { get; init; }

        /// <summary>A vertical scroll bar, listed as an attribute.</summary>
        public bool VerticalBar { get; init; }

        /// <summary>A horizontal scroll bar, listed as an attribute.</summary>
        public bool HorizontalBar { get; init; }

        public int? HeadingLevel { get; init; }
    }

    /// <summary>Whether a failed read leaves a hole: the app did not answer in time (it stalled), so an element that is there was not read. Any other error is the element's own answer (gone, no such value, a SwiftUI element that answers <c>kAXErrorFailure</c>), which a read of it again would give too, so it is left out as before.</summary>
    internal static bool Missed(int error) => error == AxError.CannotComplete;

    private static string? Label(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AxResult<Read> ReadElement(Element element)
    {
        var attributes = element.Attributes(s_attributes);
        if (!attributes.IsOk)
        {
            return AxResult<Read>.Err(attributes.Error);
        }

        var values = attributes.Value!;
        var axRole = values[0]?.Text ?? string.Empty;
        var subrole = values[1]?.Text;
        var bounds =
            values[10] is AxValue.PointValue point && values[11] is AxValue.SizeValue size
                ? new Rect(point.X, point.Y, size.Width, size.Height)
                : (Rect?)null;
        return AxResult<Read>.Ok(
            new Read
            {
                Role = Roles.Role(axRole, subrole),
                AxRole = axRole,
                Subrole = subrole,
                Title = Label(values[2]?.Text),
                Description = Label(values[3]?.Text),
                Value = values[4],
                Identifier = Label(values[5]?.Text),
                Enabled = values[6]?.Boolean,
                Focused = values[7]?.Boolean,
                Selected = values[8]?.Boolean,
                Expanded = values[9]?.Boolean,
                Bounds = bounds,
                Placeholder = Label(values[12]?.Text),
                Help = Label(values[13]?.Text),
                TitleElement = values[14]?.Element,
                // Only an element is a scroll bar: macOS may answer an attribute the area does not support with null.
                VerticalBar = values[15] is AxValue.ElementValue,
                HorizontalBar = values[16] is AxValue.ElementValue,
                HeadingLevel = LevelOf(values[17]),
            }
        );
    }

    /// <summary>A heading level an app answered: a whole number from 1 up. Anything else is no level.</summary>
    internal static int? LevelOf(AxValue? value) =>
        value is AxValue.NumberValue { Value: var number }
        && Math.Truncate(number) == number
        && number >= 1.0
        && number <= int.MaxValue
            ? (int)number
            : null;

    /// <summary>
    /// The text of the element that labels another (<c>AXTitleUIElement</c>). An error for a read the app did not answer in time: a
    /// name it may have had.
    /// </summary>
    internal static AxResult<string?> LabelOf(Element element)
    {
        var read = element.Attributes(["AXValue", "AXTitle"]);
        if (!read.IsOk)
        {
            return read.Error == AxError.CannotComplete
                ? AxResult<string?>.Err(AxError.CannotComplete)
                : AxResult<string?>.Ok(null);
        }

        foreach (var value in read.Value!)
        {
            if (Label(value?.Text) is { } text)
            {
                return AxResult<string?>.Ok(text);
            }
        }

        return AxResult<string?>.Ok(null);
    }

    /// <summary>
    /// The part of the screen a node's children can show in, from the node's own <paramref name="clip"/>. What a scroll area shows is
    /// its own frame: its children outside it are scrolled out of view. An open menu and a popover float above the window, so their
    /// items are not cut by the window's frame.
    /// </summary>
    internal static Rect? InnerClip(string role, Rect? bounds, Rect? clip)
    {
        if (role == "scrollarea" && bounds is { } frame)
        {
            return clip is { } outer ? Intersect(outer, frame) : frame;
        }

        return Floats(role) ? null : clip;
    }

    /// <summary>Roles that float above their window: an open menu and a popover.</summary>
    private static bool Floats(string role) => role is "menu" or "popover";

    /// <summary>Whether a node shows as expanded. A disclosure triangle shows its state as its value, and has no <c>AXExpanded</c>.</summary>
    internal static bool Expanded(string role, bool? expanded, byte? toggle) =>
        expanded == true || (role == "disclosure" && toggle == 1);

    /// <summary>Whether a node shows as selected. A menu reports its highlighted item as selected: that is where the pointer is, not a choice.</summary>
    internal static bool Selected(string role, bool? selected, byte? toggle) =>
        // An AppKit tab (an AXRadioButton with the AXTabButton subrole) says it is the active one by its value, as a radio button
        // says it is checked.
        (selected == true && role != "menuitem") || (role == "tab" && toggle == 1);

    /// <summary>The state a box's value shows: off, on, or mixed, from a number, a boolean, or the words toolkits use.</summary>
    internal static byte? ParseToggle(AxValue? value)
    {
        switch (value)
        {
            case AxValue.NumberValue number:
                // A saturating float-to-integer cast, as Rust's `as u8`.
                return double.IsNaN(number.Value) ? (byte)0 : (byte)Math.Clamp(number.Value, 0.0, 255.0);
            case AxValue.BoolValue flag:
                return flag.Value ? (byte)1 : (byte)0;
            case AxValue.TextValue text:
                return text.Value.Trim().ToLowerInvariant() switch
                {
                    "1" or "true" or "on" or "checked" => (byte)1,
                    "0" or "false" or "off" or "unchecked" => (byte)0,
                    "2" or "mixed" or "indeterminate" => (byte)2,
                    _ => null,
                };
            default:
                return null;
        }
    }

    /// <summary>The overlap of two rectangles, empty when they do not meet.</summary>
    internal static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new Rect(x, y, Math.Max(right - x, 0.0), Math.Max(bottom - y, 0.0));
    }

    /// <summary>
    /// Where an element is now, by the test the snapshot reads <c>offscreen</c> with: its frame against the window and every scroll
    /// area it is in, up to a menu or popover, which floats above the window. A minimized window shows nothing, whatever its frames say.
    /// An element with no frame has no place to judge, and shows. An error when the app did not answer in time, or
    /// <see cref="AxError.InvalidElement"/> when the element or an ancestor is gone: a gone element shows nowhere.
    /// </summary>
    internal static AxResult<Placement> PlacementOf(Element element)
    {
        // Every frame is read so that a stall fails the placement: a frame the app did not answer is no proof it shows.
        var frameRead = element.ReadFrame();
        if (!frameRead.IsOk)
        {
            return AxResult<Placement>.Err(frameRead.Error);
        }

        var frame = frameRead.Value;
        if (frame is { } size && (size.Width <= 0.0 || size.Height <= 0.0))
        {
            // A count the app did not answer in time is no proof the control holds nothing.
            var counted = element.Count("AXChildren");
            bool empty;
            if (counted.IsOk)
            {
                empty = counted.Value == 0;
            }
            else if (counted.Error is AxError.CannotComplete or AxError.InvalidElement)
            {
                return AxResult<Placement>.Err(counted.Error);
            }
            else
            {
                empty = true;
            }

            if (empty)
            {
                return AxResult<Placement>.Ok(Placement.Collapsed);
            }
        }

        Rect? clip = null;

        // A parent the app did not answer in time may be a scroll area that hides the element: a stall fails, never ends the walk as
        // if the window were reached.
        AxResult<Element?> ParentOf(Element child)
        {
            var read = child.Attribute("AXParent");
            if (read.IsOk && read.Value is AxValue.ElementValue parent)
            {
                return AxResult<Element?>.Ok(parent.Value);
            }

            return !read.IsOk && read.Error is AxError.CannotComplete or AxError.InvalidElement
                ? AxResult<Element?>.Err(read.Error)
                : AxResult<Element?>.Ok(null);
        }

        var first = ParentOf(element);
        if (!first.IsOk)
        {
            return AxResult<Placement>.Err(first.Error);
        }

        var next = first.Value;
        for (var step = 0; step < MaxAncestors; step++)
        {
            if (next is not { } parent)
            {
                break;
            }

            string role;
            var roleRead = parent.Attribute("AXRole");
            if (roleRead.IsOk)
            {
                role = roleRead.Value!.Text ?? string.Empty;
            }
            else if (roleRead.Error is AxError.CannotComplete or AxError.InvalidElement)
            {
                return AxResult<Placement>.Err(roleRead.Error);
            }
            else
            {
                role = string.Empty;
            }

            if (Floats(Roles.Role(role, null)))
            {
                break;
            }

            if (role == "AXWindow")
            {
                var minimized = parent.Attribute("AXMinimized");
                if (minimized is { IsOk: true, Value: AxValue.BoolValue { Value: true } })
                {
                    return AxResult<Placement>.Ok(Placement.Minimized);
                }

                if (!minimized.IsOk && minimized.Error == AxError.CannotComplete)
                {
                    return AxResult<Placement>.Err(AxError.CannotComplete);
                }
            }

            if (role is "AXScrollArea" or "AXWindow")
            {
                var parentFrame = parent.ReadFrame();
                if (!parentFrame.IsOk)
                {
                    return AxResult<Placement>.Err(parentFrame.Error);
                }

                if (parentFrame.Value is { } box)
                {
                    var rect = new Rect(box.X, box.Y, box.Width, box.Height);
                    clip = clip is { } existing ? Intersect(existing, rect) : rect;
                }
            }

            if (role == "AXWindow")
            {
                break;
            }

            var upward = ParentOf(parent);
            if (!upward.IsOk)
            {
                return AxResult<Placement>.Err(upward.Error);
            }

            next = upward.Value;
        }

        var outsideClip =
            frame is { } own && clip is { } limit && Outside(new Rect(own.X, own.Y, own.Width, own.Height), limit);
        return AxResult<Placement>.Ok(outsideClip ? Placement.ScrolledOut : Placement.Shown);
    }

    /// <summary>Whether <paramref name="inner"/> lies wholly outside <paramref name="outer"/>.</summary>
    internal static bool Outside(Rect inner, Rect outer)
    {
        // A node with no size has no place to judge: an open menu reports a zero frame while its items have real ones.
        if (inner.Width <= 0.0 || inner.Height <= 0.0)
        {
            return false;
        }

        return outer.Width <= 0.0
            || outer.Height <= 0.0
            || inner.X + inner.Width <= outer.X
            || inner.X >= outer.X + outer.Width
            || inner.Y + inner.Height <= outer.Y
            || inner.Y >= outer.Y + outer.Height;
    }

    /// <summary>
    /// The name a row or cell takes from the text inside it, as an accessible name comes from content: every text, in reading order,
    /// joined by spaces.
    /// </summary>
    internal static string? ContentName(IEnumerable<string> texts) =>
        Label(string.Join(" ", texts.Select(text => text.Trim()).Where(text => text.Length > 0)));

    /// <summary>The title-bar buttons a window names as its own; null when it names none, so the walk falls back to their subrole.</summary>
    private static List<Element>? WindowChrome(Element window)
    {
        var buttons = new List<Element>();
        foreach (var name in new[] { "AXCloseButton", "AXMinimizeButton", "AXZoomButton", "AXFullScreenButton" })
        {
            if (window.ElementAttribute(name) is { } button)
            {
                buttons.Add(button);
            }
        }

        return buttons.Count == 0 ? null : buttons;
    }

    /// <summary>Reads one window of a process.</summary>
    public static RawSnapshot Take(Request request)
    {
        var window = Windows.Find(request.Pid, request.Window);
        var app = Element.Application(request.Pid);
        Element? focused = null;
        var focusUnknown = false;
        var focusRead = app.Attribute("AXFocusedUIElement");
        if (focusRead.IsOk)
        {
            focused = focusRead.Value!.Element;
        }
        else if (focusRead.Error == AxError.CannotComplete)
        {
            focusUnknown = true;
        }

        var windowBounds = window.Frame() is { } frame
            ? new Rect(frame.X, frame.Y, frame.Width, frame.Height)
            : (Rect?)null;
        var title = window.Text("AXTitle");
        var walk = new Walk
        {
            Pid = request.Pid,
            Window = request.Window,
            SnapshotId = Refs.SnapshotId(),
            MaxDepth = Math.Max(request.MaxDepth, 1),
            Interactive = request.Interactive,
            Deadline = request.Deadline,
            Focused = focused,
            FocusUnknown = focusUnknown,
            Chrome = WindowChrome(window),
            Complete = !focusUnknown,
        };

        // The window must answer: one that does not is gone, or its app is.
        var role = window.Attribute("AXRole");
        if (!role.IsOk)
        {
            switch (role.Error)
            {
                case AxError.InvalidElement:
                    throw Failure.Refused(Codes.WindowNotFound, $"window {request.Window} closed");
                case AxError.CannotComplete:
                    throw Failure
                        .Refused(Codes.AppUnresponsive, "the app did not answer in time")
                        .WithDetails(Failure.Retryable);
                case AxError.ApiDisabled:
                    throw Failure.Refused(Codes.PermDenied, "this process has no Accessibility permission");
            }
        }

        var tree = walk.Node(window, 1, true, windowBounds);

        // Accessibility permission taken away during the walk answers every read with an error the walk takes for an element's own
        // answer, and leaves it out: the tree would read as complete, and empty where it was not read.
        if (!Element.Trusted())
        {
            throw Failure.Refused(
                Codes.PermDenied,
                "this process lost its Accessibility permission while it read the window"
            );
        }

        if (tree is null)
        {
            throw Failure.Refused(Codes.WindowNotFound, $"window {request.Window} could not be read");
        }

        Refs.InsertAll(walk.Refs);
        return new RawSnapshot
        {
            SnapshotId = walk.SnapshotId,
            Complete = walk.Complete,
            TimedOut = walk.TimedOut ? true : null,
            RootUnread = walk.RootUnread ? true : null,
            Truncated = walk.Truncated,
            Tree = tree,
            Window = new SnapshotWindow(request.Window.ToString(CultureInfo.InvariantCulture), title),
        };
    }

    private sealed class Walk
    {
        public required int Pid { get; init; }

        public required uint Window { get; init; }

        public required string SnapshotId { get; init; }

        public required int MaxDepth { get; init; }

        public required bool Interactive { get; init; }

        public required Instant Deadline { get; init; }

        public Element? Focused { get; init; }

        /// <summary>
        /// The app did not answer which element holds its focus in time: no node reads as focused, and the read is incomplete, never
        /// filled in from each element's own <c>AXFocused</c> (a table's cells report the table's focus).
        /// </summary>
        public bool FocusUnknown { get; init; }

        /// <summary>The window's own title-bar buttons, as it names them (<c>AXCloseButton</c>, ...); null when it names none.</summary>
        public List<Element>? Chrome { get; init; }

        public bool Complete { get; set; }

        /// <summary>Whether the deadline stopped the read.</summary>
        public bool TimedOut { get; private set; }

        /// <summary>Whether the app did not answer for the window itself.</summary>
        public bool RootUnread { get; private set; }

        public bool Truncated { get; private set; }

        public List<(string Reference, Entry Entry)> Refs { get; } = [];

        private int _nodes;

        /// <summary>How many reads of an element that still exists failed; a parent whose subtree lost one is truncated.</summary>
        private int _lost;

        /// <summary>
        /// The text of every text node read so far, in reading order, pruned ones too: a row or cell with no name of its own takes it
        /// from the text inside it.
        /// </summary>
        private readonly List<string> _content = [];

        private bool Expired => Instant.Now >= Deadline;

        /// <summary>Notes a read of an element that still exists and did not answer: the snapshot is no longer complete.</summary>
        private void Lose()
        {
            Complete = false;
            _lost++;
        }

        /// <summary>Whether an attribute can be set. A stall leaves a hole in the read: a field the app did not answer about is not read-only.</summary>
        private bool Settable(Element element, string name)
        {
            var read = element.Settable(name);
            if (read.IsOk)
            {
                return read.Value;
            }

            if (Missed(read.Error))
            {
                Lose();
            }

            return false;
        }

        /// <summary>The actions a control takes, in the raw vocabulary, and the axes it scrolls along when it takes <c>Scroll</c>.</summary>
        private (List<string> Actions, List<string> Axes) ReadActions(Element element, Read read)
        {
            var output = new List<string>();
            var role = read.Role;
            List<string> names;
            if (Roles.Inert(role))
            {
                names = [];
            }
            else
            {
                var listed = element.Actions();
                if (listed.IsOk)
                {
                    names = listed.Value!;
                }
                else
                {
                    if (Missed(listed.Error))
                    {
                        Lose();
                    }

                    names = [];
                }
            }

            bool Has(string name) => names.Contains(name);
            void Push(string action)
            {
                if (!output.Contains(action))
                {
                    output.Add(action);
                }
            }

            if (
                Has("AXPress")
                || Has("AXOpen")
                || Has("AXConfirm")
                || (role is "row" or "cell" or "treeitem" or "option" or "tab" && Settable(element, "AXSelected"))
            )
            {
                Push("Click");
                if (Roles.Toggleable(role))
                {
                    Push("Toggle");
                }
            }

            if (Has("AXShowMenu") && role is not ("window" or "sheet" or "dialog" or "application"))
            {
                Push("RightClick");
            }

            if (Has("AXScrollToVisible"))
            {
                Push("ScrollTo");
            }

            // SwiftUI gives every scroll area the page actions, also one whose content fits: only a scroll bar says a scroll area
            // scrolls. A viewport swipe goes to the first node offered Scroll, so one that cannot scroll must not be.
            bool vertical;
            bool horizontal;
            if (role == "scrollarea")
            {
                vertical = read.VerticalBar || Actions.ScrollBar(element, true) is not null;
                horizontal = read.HorizontalBar || Actions.ScrollBar(element, false) is not null;
            }
            else
            {
                vertical = Has("AXScrollDownByPage") || Has("AXScrollUpByPage");
                horizontal = Has("AXScrollLeftByPage") || Has("AXScrollRightByPage");
            }

            // A viewport swipe goes to the first node that scrolls along its axis: a carousel does not take a swipe down.
            var axes = new List<string>();
            if (vertical)
            {
                axes.Add("vertical");
            }

            if (horizontal)
            {
                axes.Add("horizontal");
            }

            if (axes.Count > 0)
            {
                Push("Scroll");
            }

            var valueSettable = Roles.BearsValue(role) && Settable(element, "AXValue");

            // A stepper steps toward a number only when it shows one.
            var steps = (Has("AXIncrement") || Has("AXDecrement")) && read.Value is AxValue.NumberValue;
            if (steps || valueSettable)
            {
                Push("SetValue");
            }

            if ((role == "combobox" && (valueSettable || read.AxRole == "AXPopUpButton")) || Roles.Collection(role))
            {
                Push("Select");
            }

            if ((Roles.Interactive(role) || Roles.Collection(role)) && Settable(element, "AXFocused"))
            {
                Push("SetFocus");
            }

            // An AppKit password field takes keystrokes but restricts AXSelectedText: a value it lets be set says it takes text.
            var secure = read.Subrole == "AXSecureTextField" || read.AxRole == "AXSecureTextField";
            if (Roles.TakesText(role) && ((secure && valueSettable) || Settable(element, "AXSelectedText")))
            {
                Push("TypeText");
            }

            if (
                (Roles.Expandable(role) && Settable(element, "AXExpanded"))
                || (Has("AXPress") && Roles.Expandable(role))
            )
            {
                Push("Expand");
                Push("Collapse");
            }

            return (output, axes);
        }

        /// <summary>The children of an element, at most <see cref="MaxChildren"/>, and how many it has.</summary>
        private (List<Element> Children, int Count) Children(Element element, string role)
        {
            if (Roles.Large(role))
            {
                var counted = element.Count("AXChildren");
                int count;
                if (counted.IsOk)
                {
                    count = counted.Value;
                }
                else
                {
                    if (Missed(counted.Error))
                    {
                        Lose();
                    }

                    count = 0;
                }

                List<Element> children;
                if (count == 0)
                {
                    children = [];
                }
                else
                {
                    var listed = element.Elements("AXChildren", MaxChildren);
                    if (listed.IsOk)
                    {
                        children = listed.Value!;
                    }
                    else
                    {
                        if (Missed(listed.Error))
                        {
                            Lose();
                        }

                        children = [];
                    }
                }

                return (children, count);
            }

            var read = element.Attribute("AXChildren");
            List<Element> items;
            if (read.IsOk && read.Value is AxValue.ArrayValue array)
            {
                items = array.Items.OfType<AxValue.ElementValue>().Select(item => item.Value).ToList();
            }
            else
            {
                if (!read.IsOk && Missed(read.Error))
                {
                    Lose();
                }

                items = [];
            }

            return (items.Take(MaxChildren).ToList(), items.Count);
        }

        private static Read RootFallback() => new() { Role = "window", AxRole = "AXWindow" };

        /// <summary>
        /// Reads one node and the levels under it. Null for a node an interactive read leaves out, or one that is gone.
        /// <paramref name="clip"/> is the part of the screen the node can show in: the window, cut by every scroll area above it.
        /// </summary>
        public RawNode? Node(Element element, int level, bool root, Rect? clip)
        {
            if (_nodes >= MaxNodes)
            {
                Truncated = true;
                return null;
            }

            _nodes++;
            Read read;
            var readResult = ReadElement(element);
            if (readResult.IsOk)
            {
                read = readResult.Value!;
            }
            else
            {
                var error = readResult.Error;
                if (!root && Missed(error))
                {
                    // Still there, but the app did not answer: the read has a hole.
                    Lose();
                    return null;
                }

                if (!root)
                {
                    // Gone since its parent listed it, or an element that refuses reads: nothing a later read would find.
                    return null;
                }

                if (error == AxError.InvalidElement)
                {
                    // The window closed after it was found: no tree, rather than an empty one that reads as complete.
                    return null;
                }

                if (Missed(error))
                {
                    Lose();
                    RootUnread = true;
                }

                read = RootFallback();
            }

            // The window's own title-bar buttons are window chrome, never the app's controls: the window names them (`chrome`), so a
            // button of the app's content with the same subrole (a tab's close button) stays. A window that names none falls back to
            // the subrole.
            var isChrome = Chrome is not null
                ? Chrome.Contains(element)
                : Roles.WindowControlName(read.Subrole) is not null;
            if (!root && isChrome)
            {
                return null;
            }

            var role = root && read.Role == "unknown" ? "window" : read.Role;
            var secure = read.Subrole == "AXSecureTextField" || read.AxRole == "AXSecureTextField";
            var (actions, scrollAxes) = ReadActions(element, read);

            // The name a control takes from its label element: a stall reading it leaves the name unknown, a hole in the read, so a
            // locator cannot take the control's absence by that name as proof.
            string? labelled = null;
            if (read.TitleElement is { } titleElement)
            {
                var label = LabelOf(titleElement);
                if (label.IsOk)
                {
                    labelled = label.Value;
                }
                else
                {
                    Lose();
                }
            }

            var textValue = secure ? null : read.Value?.Display();
            var name = read.Title
                ?? labelled
                ?? read.Description
                ?? (Roles.Text(role) ? Label(textValue) : null)
                ?? (Roles.TakesText(role) ? read.Placeholder : null);
            var description = read.Description is { } described && described != name ? described : null;

            var states = new List<string>();

            // The app's focused element is the one source: a table's cells report the table's focus as their own.
            bool focusedState;
            if (Focused is not null)
            {
                focusedState = Focused.Equals(element);
            }
            else if (FocusUnknown)
            {
                focusedState = false;
            }
            else
            {
                focusedState = read.Focused == true;
            }

            if (focusedState)
            {
                states.Add("focused");
            }

            if (read.Enabled == false)
            {
                states.Add("disabled");
            }

            if (secure)
            {
                states.Add("secure");
            }

            var toggle = ParseToggle(read.Value);
            if (Expanded(role, read.Expanded, toggle))
            {
                states.Add("expanded");
            }

            if (Roles.Toggleable(role))
            {
                switch (toggle)
                {
                    case 1:
                        states.Add("checked");
                        break;
                    case 2:
                        states.Add("indeterminate");
                        break;
                }
            }

            if (Selected(role, read.Selected, toggle))
            {
                states.Add("selected");
            }

            if (role == "button" && toggle == 1 && read.Subrole == "AXToggleButton")
            {
                states.Add("pressed");
            }

            if (role is "textfield" or "combobox" && !actions.Contains("SetValue"))
            {
                states.Add("readonly");
            }

            // A menu or a popover floats above the window, so the window's frame does not hide it.
            if (!root && !Floats(role) && read.Bounds is { } bounds && clip is { } limit && Outside(bounds, limit))
            {
                states.Add("offscreen");
            }

            // A table's row gets a ref whatever it offers: an NSTableView selects it through the table's AXSelectedRows, which a tap
            // uses, not an action of the row. So does an element whose one action scrolls it into view.
            string? refId = null;
            if (
                !root
                && (
                    Roles.Interactive(role)
                    || role == "row"
                    || actions.Any(action =>
                        action
                            is "Click"
                                or "SetValue"
                                or "Select"
                                or "Scroll"
                                or "RightClick"
                                or "TypeText"
                                or "Expand"
                                or "ScrollTo"
                    )
                )
            )
            {
                refId = $"@{SnapshotId}:e{Refs.Count + 1}";
                Refs.Add((refId, new Entry(element, Pid, Window)));
            }

            var subtreeTruncated = false;
            int? childrenCount = null;
            var children = new List<RawNode>();
            var inner = InnerClip(role, read.Bounds, clip);
            var contentFrom = _content.Count;
            if (role == "scrollbar")
            {
                // A scroll bar's arrows and page areas are its parts, not controls of their own.
            }
            else if (level >= MaxDepth)
            {
                var counted = element.Count("AXChildren");
                var count = 0;
                if (counted.IsOk)
                {
                    count = counted.Value;
                }
                else if (Missed(counted.Error))
                {
                    // A stalled count could hide children past the depth limit: the read has a hole.
                    Lose();
                    subtreeTruncated = true;
                }

                if (count > 0)
                {
                    childrenCount = count;
                }
            }
            else if (Expired)
            {
                Complete = false;
                TimedOut = true;
                var counted = element.Count("AXChildren");
                subtreeTruncated = counted.IsOk && counted.Value > 0;
            }
            else
            {
                var lost = _lost;
                var (listed, count) = Children(element, role);
                if (count > listed.Count)
                {
                    subtreeTruncated = true;
                }

                foreach (var child in listed)
                {
                    if (Expired)
                    {
                        Complete = false;
                        TimedOut = true;
                        subtreeTruncated = true;
                        break;
                    }

                    if (_nodes >= MaxNodes)
                    {
                        Truncated = true;
                        subtreeTruncated = true;
                        break;
                    }

                    if (Node(child, level + 1, false, inner) is { } node)
                    {
                        children.Add(node);
                    }
                }

                if (_lost > lost)
                {
                    subtreeTruncated = true;
                }
            }

            if (name is null && Roles.NamedByContent(role))
            {
                name = ContentName(_content.Skip(contentFrom));
            }

            if (Roles.Text(role) && name is not null)
            {
                _content.Add(name);
            }

            if (
                Interactive
                && !root
                && refId is null
                && children.Count == 0
                && !subtreeTruncated
                && childrenCount is null
                && !states.Contains("secure")
            )
            {
                return null;
            }

            return new RawNode
            {
                RefId = refId,
                Role = role,
                Name = name,
                Value = textValue,
                Description = description,
                Hint = read.Help,
                Level = role == "heading" ? read.HeadingLevel : null,
                Placeholder = read.Placeholder,
                NativeId = read.Identifier is { } identifier ? new NativeId("ax_identifier", identifier) : null,
                States = states.Count == 0 ? null : states,
                AvailableActions = actions.Count == 0 ? null : actions,
                ScrollAxes = scrollAxes.Count == 0 ? null : scrollAxes,
                Bounds = read.Bounds,
                SubtreeTruncated = subtreeTruncated ? true : null,
                ChildrenCount = childrenCount,
                Children = children.Count == 0 ? null : children,
            };
        }
    }
}
