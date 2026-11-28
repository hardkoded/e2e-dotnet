// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>What <c>act</c> does. Rust's <c>actions::Action</c>; named apart here because <c>Action</c> is a delegate type in .NET.</summary>
internal abstract record DriverAction
{
    public sealed record Click : DriverAction;

    public sealed record DoubleClick : DriverAction;

    public sealed record RightClick : DriverAction;

    public sealed record Focus : DriverAction;

    public sealed record Clear : DriverAction;

    public sealed record Check(bool Wanted) : DriverAction;

    public sealed record ScrollTo : DriverAction;

    public sealed record Hover : DriverAction;

    public sealed record SetValue(string Text) : DriverAction;

    public sealed record SelectAll : DriverAction;

    public sealed record Select(string Label) : DriverAction;

    public sealed record Scroll(string Direction, double Pages) : DriverAction;

    public sealed record Type(string Text, bool Physical) : DriverAction;
}

/// <summary>
/// Actions on the element a ref names, through the accessibility API unless they need the real pointer or keyboard. Each one says
/// whether anything reached the app when it fails, and checks its effect where the element shows it (a box's state, a field's value,
/// a list's choice). Rust's <c>actions/mod.rs</c>, with <c>pointer</c>, <c>scroll</c>, <c>select</c>, <c>typing</c>, and <c>values</c> in
/// the partial files beside it.
/// </summary>
internal static partial class Actions
{
    /// <summary>How long an action that opens a menu or a modal waits for the app to answer before it counts as delivered.</summary>
    private const float BlockingActionSeconds = 0.5f;

    /// <summary>How long an effect is polled for before the action is reported unconfirmed.</summary>
    private static readonly TimeSpan s_effectWait = TimeSpan.FromMilliseconds(1_000);

    /// <summary>How long an action that opens a menu waits for it to show.</summary>
    private static readonly TimeSpan s_menuShowWait = TimeSpan.FromMilliseconds(1_000);

    /// <summary>How often an effect is polled.</summary>
    private static readonly TimeSpan s_poll = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// How long before the deadline a wait that ends in a refusal gives up: the engine cuts the call off at that same deadline, and a
    /// refusal that came after it would read as input that may have reached the app.
    /// </summary>
    private static readonly TimeSpan s_answerMargin = TimeSpan.FromMilliseconds(200);

    /// <summary>How long an action that answered with an error is watched for its element going away, the sign that it landed.</summary>
    private static readonly TimeSpan s_goneWait = TimeSpan.FromMilliseconds(300);

    /// <summary>The action this thread runs: its input run, which a cancel marks, and its deadline.</summary>
    [ThreadStatic]
    private static Running? t_current;

    private sealed record Running(Input.TypingRun Run, Instant Deadline);

    /// <summary>The failure for an AX error of a request that changed nothing.</summary>
    internal static Failure Refused(int error, string what) =>
        error switch
        {
            AxError.InvalidElement => Failure.Refused(Codes.StaleRef, $"{what}: the element no longer exists"),
            AxError.ActionUnsupported or AxError.AttributeUnsupported or AxError.NotImplemented => Failure.Refused(
                Codes.ActionNotSupported,
                $"{what}: {AxError.Describe(error)}"
            ),
            AxError.ApiDisabled => Failure.Refused(Codes.PermDenied, $"{what}: {AxError.Describe(error)}"),
            AxError.IllegalArgument => Failure.Refused(Codes.ActionFailed, $"{what}: {AxError.Describe(error)}"),
            _ => Failure.Unconfirmed(Codes.ActionFailed, $"{what}: {AxError.Describe(error)}"),
        };

    /// <summary>
    /// The failure of an AX attribute write: only a refusal says nothing was sent. Any other answer may follow a write the app took,
    /// whose handler ran (one that replaced the element answers a gone element), so it counts as delivered.
    /// </summary>
    internal static Failure WriteFailed(int error, string what) =>
        Refuses(error) ? Refused(error, what) : Refused(error, what).Delivered();

    /// <summary>
    /// Refuses to send input once the caller gave up on the action: it was cancelled, or its deadline passed. The reads before an input
    /// can take long on a slow app, and input sent after the caller moved on would land unasked.
    /// </summary>
    internal static void Go(string what)
    {
        switch (t_current)
        {
            case { Run: var run } when run.Cancelled():
                throw Failure.Refused(Codes.Timeout, $"{what}: the action was cancelled before its input was sent");
            case { Deadline: var deadline } when Instant.Now >= deadline:
                throw Failure.Refused(Codes.Timeout, $"{what}: the action's time ran out before its input was sent");
        }
    }

    /// <summary>
    /// Whether the caller cancelled the running action. Unlike <see cref="Go"/>, a deadline that passed does not count: cleanup such
    /// as closing a menu runs past it, but never past a cancel, after which the caller has moved on.
    /// </summary>
    internal static bool Cancelled() => t_current is { } current && current.Run.Cancelled();

    /// <summary>
    /// The failure for a read that failed before anything was sent: nothing reached the app. A read the app did not answer in time
    /// is a stall, which a retry may get past.
    /// </summary>
    internal static Failure Unread(int error, string what) =>
        error switch
        {
            AxError.CannotComplete => Failure
                .Refused(Codes.AppUnresponsive, $"{what}: the app did not answer in time")
                .WithDetails(Failure.Retryable),
            AxError.InvalidElement => Failure.Refused(Codes.StaleRef, $"{what}: the element no longer exists"),
            AxError.ApiDisabled => Failure.Refused(Codes.PermDenied, $"{what}: {AxError.Describe(error)}"),
            _ => Failure.Refused(Codes.ActionFailed, $"{what}: {AxError.Describe(error)}"),
        };

    /// <summary>
    /// A read whose only failure that counts is a stall: the app did not answer in time, which says nothing about the answer and
    /// passes up as <see cref="AxError.CannotComplete"/>. Any other failure reads as no answer.
    /// </summary>
    internal static AxResult<T?> Stalled<T>(AxResult<T> read)
        where T : class =>
        read.IsOk ? AxResult<T?>.Ok(read.Value)
        : read.Error == AxError.CannotComplete ? AxResult<T?>.Err(AxError.CannotComplete)
        : AxResult<T?>.Ok(null);

    /// <summary>
    /// The element an attribute names, such as the app's focused element or key window. A read the app did not answer in time fails
    /// as a stall, which a retry may get past, never as focus that moved; any other failure reads as none.
    /// </summary>
    internal static Element? ElementAttribute(Element element, string name, string what)
    {
        var read = Stalled(element.Attribute(name));
        if (!read.IsOk)
        {
            throw Unread(read.Error, what);
        }

        return read.Value?.Element;
    }

    /// <summary>Whether <paramref name="focused"/>, the app's focused window, holds <paramref name="window"/>'s focus (<see cref="Windows.HoldsFocus"/>). A stall fails as one.</summary>
    internal static bool Holds(Element focused, Element window, string what)
    {
        var holds = Windows.HoldsFocus(focused, window);
        return holds.IsOk ? holds.Value : throw Unread(holds.Error, what);
    }

    /// <summary>Whether an attribute can be set. A stall fails, so a busy app does not read as one whose control takes no input; any other failure reads as not settable.</summary>
    private static bool Settable(Element element, string name, string what)
    {
        var read = element.Settable(name);
        if (read.IsOk)
        {
            return read.Value;
        }

        return read.Error == AxError.CannotComplete ? throw Unread(AxError.CannotComplete, what) : false;
    }

    /// <summary>The actions an element takes. A stall, a gone element, or no permission fails; any other failure reads as none.</summary>
    private static List<string> ActionNames(Element element, string what)
    {
        var read = element.Actions();
        if (read.IsOk)
        {
            return read.Value!;
        }

        return read.Error is AxError.CannotComplete or AxError.InvalidElement or AxError.ApiDisabled
            ? throw Unread(read.Error, what)
            : [];
    }

    /// <summary>Every element of an array attribute, however many it holds. A read the app did not answer fails: a list read in part cannot show an option is missing.</summary>
    private static List<Element> All(Element element, string name, string what)
    {
        var counted = element.Count(name);
        int count;
        if (counted.IsOk)
        {
            count = Math.Max(counted.Value, 1);
        }
        else if (counted.Error == AxError.CannotComplete)
        {
            throw Unread(AxError.CannotComplete, what);
        }
        else
        {
            // A count the element does not give: read up to a generous cap instead.
            count = 4096;
        }

        var read = element.Elements(name, count);
        if (read.IsOk)
        {
            return read.Value!;
        }

        return read.Error == AxError.CannotComplete ? throw Unread(AxError.CannotComplete, what) : [];
    }

    /// <summary>The element a ref names, checked to still exist.</summary>
    private static Entry Resolve(string reference)
    {
        var entry = Refs.Get(reference)
            ?? throw Failure.Refused(Codes.StaleRef, $"{reference} names no element of a recent snapshot");
        var role = entry.Element.Attribute("AXRole");
        return role.Error switch
        {
            AxError.InvalidElement => throw Failure.Refused(
                Codes.StaleRef,
                $"{reference}: the element no longer exists"
            ),
            AxError.CannotComplete => throw Failure
                .Refused(Codes.AppUnresponsive, "the app did not answer in time")
                .WithDetails(Failure.Retryable),
            _ => entry,
        };
    }

    /// <summary>Polls <paramref name="check"/> until it holds or the effect wait (bounded by the deadline) ends.</summary>
    internal static bool Until(Instant deadline, Func<bool> check)
    {
        var end = Instant.Min(Instant.After(s_effectWait), deadline);
        while (true)
        {
            if (check())
            {
                return true;
            }

            if (Instant.Now + s_poll >= end)
            {
                return check();
            }

            Thread.Sleep(s_poll);
        }
    }

    /// <summary>
    /// When the watch for an element going away ends: after <c>GONE_WAIT</c>, and in time for the answer to reach the caller before the
    /// action's deadline. An answer that comes after it is cut off, and reads as an action that may have landed even when the app
    /// refused it.
    /// </summary>
    internal static Instant GoneWaitEnd(Instant now, Instant? deadline)
    {
        var end = now + s_goneWait;
        return deadline is { } limit ? Instant.Min(end, limit.CheckedSub(s_answerMargin) ?? limit) : end;
    }

    /// <summary>
    /// Performs an AX action. Once the action is sent, an error answer cannot show that nothing happened, so the element is read again
    /// to tell (<see cref="Answered"/>).
    /// </summary>
    private static void Perform(Element element, string action, string what)
    {
        Go(what);
        element.SetTimeout(BlockingActionSeconds);
        try
        {
            var error = element.Perform(action);
            if (error == AxError.Success)
            {
                return;
            }

            int? then;
            if (error != AxError.AttributeUnsupported && (Refuses(error) || error == AxError.InvalidElement))
            {
                // "No such attribute" is watched too: a SwiftUI alert's button answers it once its press closed the alert.
                then = null;
            }
            else if (error == AxError.CannotComplete)
            {
                // The app answers again or not: one read tells, and another would wait out the same stall.
                then = ErrorOf(element.Attribute("AXRole"));
            }
            else
            {
                // A press that closes what holds its element (a dismissed alert) can leave it readable a moment more.
                var end = GoneWaitEnd(Instant.Now, t_current?.Deadline);
                while (true)
                {
                    then = ErrorOf(element.Attribute("AXRole"));
                    if (then == AxError.InvalidElement || Instant.Now >= end)
                    {
                        break;
                    }

                    Thread.Sleep(s_poll);
                }
            }

            Answered(error, then, what);
        }
        finally
        {
            element.SetTimeout(0.0f);
        }
    }

    /// <summary>The error of a read, or null when it answered.</summary>
    private static int? ErrorOf<T>(AxResult<T> read) => read.IsOk ? null : read.Error;

    /// <summary>Errors with which the app refuses an action before taking it: no such action or attribute, no permission, a bad argument.</summary>
    internal static bool Refuses(int error) =>
        error
            is AxError.ActionUnsupported
                or AxError.AttributeUnsupported
                or AxError.NotImplemented
                or AxError.ApiDisabled
                or AxError.IllegalArgument;

    /// <summary>
    /// What an action's error answer means, from the error and a read of the element after it (<paramref name="then"/>, its error, null
    /// when the element still answers):
    /// <list type="bullet">
    /// <item>refused outright: nothing reached the app;</item>
    /// <item>"no such attribute" with the element gone after: a SwiftUI alert's button answers so once its press closed the alert, so it landed; with the element unread after (the button's handler keeps the app busy), it may have landed;</item>
    /// <item>the element gone as the action was sent: the action may have closed what held it, so it may have landed;</item>
    /// <item>gone after: the action closed what held it (an alert's button, a panel's), so it landed;</item>
    /// <item>a timeout while the app still answers: the action opened a menu or a modal and runs inside the app's handler;</item>
    /// <item>a timeout the app does not answer after: it may never have taken the action, delivered but unverified;</item>
    /// <item>any other error: it may have landed, delivered but unverified. The caller reads the effect where it can.</item>
    /// </list>
    /// </summary>
    internal static void Answered(int error, int? then, string what)
    {
        if (error == AxError.AttributeUnsupported && then == AxError.InvalidElement)
        {
            return;
        }

        if (error == AxError.AttributeUnsupported && then is not null)
        {
            throw Refused(error, what).Delivered();
        }

        if (Refuses(error))
        {
            throw Refused(error, what);
        }

        if (error == AxError.InvalidElement)
        {
            throw Failure.Unconfirmed(Codes.ActionFailed, $"{what}: the element went away as the action was sent");
        }

        if (then == AxError.InvalidElement)
        {
            return;
        }

        if (error == AxError.CannotComplete)
        {
            if (then is null)
            {
                return;
            }

            throw Failure.Unconfirmed(
                Codes.AppUnresponsive,
                $"{what}: the app did not answer the action, or anything after it"
            );
        }

        throw Refused(error, what).Delivered();
    }

    /// <summary>
    /// The element's AX role and its role in the snapshot's vocabulary. A read the app did not answer in time fails as a stall, and a
    /// gone element as a stale ref, so either is retried rather than read as a control without the role; any other failure reads as none.
    /// </summary>
    private static (string AxRole, string Role) RoleOf(Element element, string what)
    {
        var read = element.Attributes(["AXRole", "AXSubrole"]);
        AxValue?[] values;
        if (read.IsOk)
        {
            values = read.Value!;
        }
        else if (read.Error is AxError.CannotComplete or AxError.InvalidElement)
        {
            // A stall is retried, and so is an element gone since it was resolved: the locator finds its replacement.
            throw Unread(read.Error, what);
        }
        else
        {
            values = new AxValue?[2];
        }

        var axRole = values[0]?.Text ?? string.Empty;
        var subrole = values[1]?.Text;
        return (axRole, Roles.Role(axRole, subrole));
    }

    /// <summary>
    /// The state a box shows, read as the snapshot reads it, so a check confirms the state an observation would report: <c>1</c>,
    /// <c>true</c>, and <c>on</c> are checked, <c>0</c> and <c>off</c> unchecked, <c>2</c> and <c>mixed</c> mixed.
    /// </summary>
    private static byte? ToggleState(Element element)
    {
        var read = element.Attribute("AXValue");
        return Snapshot.ParseToggle(read.IsOk ? read.Value : null);
    }

    /// <summary>The text a value attribute holds now.</summary>
    private static string? ValueText(Element element)
    {
        var read = element.Attribute("AXValue");
        return read.IsOk ? read.Value!.Display() : null;
    }

    /// <summary>The element's center on screen.</summary>
    private static (double X, double Y) Center(Element element)
    {
        var (x, y, width, height) = element.Frame()
            ?? throw Failure.Refused(Codes.ActionFailed, "the element has no frame on screen");
        if (width <= 0.0 || height <= 0.0)
        {
            throw Failure.Refused(Codes.ActionFailed, "the element has no area on screen");
        }

        return (x + width / 2.0, y + height / 2.0);
    }

    /// <summary>Brings the window forward and the app with it, for physical input. A sheet or an alert on it may take focus.</summary>
    internal static void Raise(int pid, Element window)
    {
        var app = Element.Application(pid);

        // Each write moves the focus: none goes once the caller gave up.
        Go("bring the window forward");
        _ = app.SetBool("AXFrontmost", true);
        Go("bring the window forward");
        _ = window.Perform("AXRaise");
        Go("bring the window forward");
        _ = window.SetBool("AXMain", true);
        var deadline = Instant.After(TimeSpan.FromMilliseconds(1_500));
        AxResult<bool> last = AxResult<bool>.Ok(false);
        Until(deadline, () =>
        {
            last = Forward(app, window);
            return last is { IsOk: true, Value: true };
        });
        if (last is { IsOk: true, Value: true })
        {
            return;
        }

        // An app that did not answer in time may have come forward: no proof it did not, and a retry may get past it.
        if (!last.IsOk)
        {
            throw Unread(last.Error, "bring the window forward");
        }

        throw Failure.Refused(Codes.ActionFailed, "the window could not be brought forward");
    }

    /// <summary>Whether the app is frontmost with <paramref name="window"/> holding its focus. A stall fails as <see cref="AxError.CannotComplete"/>.</summary>
    private static AxResult<bool> Forward(Element app, Element window)
    {
        var frontmost = Stalled(app.Attribute("AXFrontmost"));
        if (!frontmost.IsOk)
        {
            return AxResult<bool>.Err(frontmost.Error);
        }

        if (frontmost.Value?.Boolean != true)
        {
            return AxResult<bool>.Ok(false);
        }

        var focusedWindow = Stalled(app.Attribute("AXFocusedWindow"));
        if (!focusedWindow.IsOk)
        {
            return AxResult<bool>.Err(focusedWindow.Error);
        }

        return focusedWindow.Value?.Element is { } focused
            ? Windows.HoldsFocus(focused, window)
            : AxResult<bool>.Ok(false);
    }

    /// <summary>Waits a moment for a menu an action opened to show, so the next read finds it; an action that opens none waits it out and is still done.</summary>
    private static void MenuShown(int pid, int before, Instant deadline) =>
        Until(Instant.Min(deadline, Instant.After(s_menuShowWait)), () => Windows.OpenMenus(pid) > before);

    /// <summary>
    /// Waits within the action's budget for a control to be enabled, as an actionability wait does, and refuses it if it stays
    /// disabled: a disabled AppKit control may still offer AXPress and acknowledge it while ignoring it. A control with no enabled state
    /// goes at once.
    /// </summary>
    private static void Enabled(Element element, Instant deadline, string what)
    {
        var refuseAt = deadline.CheckedSub(s_answerMargin) ?? deadline;
        while (true)
        {
            var read = element.Attribute("AXEnabled");
            if (read.IsOk && read.Value!.Boolean == false)
            {
                if (Instant.Now + s_poll >= refuseAt)
                {
                    throw Failure.Refused(Codes.ActionFailed, "the control is disabled");
                }

                Go(what);
                Thread.Sleep(s_poll);
                continue;
            }

            if (!read.IsOk && read.Error is AxError.CannotComplete or AxError.InvalidElement or AxError.ApiDisabled)
            {
                throw Unread(read.Error, what);
            }

            return;
        }
    }

    /// <summary>
    /// Brings an element a scroll area holds out of view into view (<c>AXScrollToVisible</c>), and waits for it to show. An element
    /// already on screen gets nothing; one in a minimized window, one with no size, or one that cannot be scrolled, is refused, as an
    /// action on it would reach a control the user cannot. True when it scrolled the list.
    /// </summary>
    private static bool Reveal(Element element, Instant deadline, string what)
    {
        var placement = Snapshot.PlacementOf(element);
        if (!placement.IsOk)
        {
            throw Unread(placement.Error, what);
        }

        switch (placement.Value)
        {
            case Placement.Shown:
                return false;
            case Placement.Minimized:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "the element's window is minimized, so the user cannot reach it; restore the window first"
                );
            case Placement.Collapsed:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "the element has no size on screen, so the user cannot reach it"
                );
        }

        if (!ActionNames(element, what).Contains("AXScrollToVisible"))
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "the element is off screen and cannot be scrolled into view; scroll its list first"
            );
        }

        Perform(element, "AXScrollToVisible", "scroll into view");

        // A list can replace a row's element as it scrolls the row in: the element is gone then, and no wait shows it.
        var gone = false;
        var shown = Until(deadline, () =>
        {
            var now = Snapshot.PlacementOf(element);
            if (now is { IsOk: true, Value: Placement.Shown })
            {
                return true;
            }

            if (!now.IsOk && now.Error == AxError.InvalidElement)
            {
                gone = true;
                return true;
            }

            return false;
        });
        if (gone)
        {
            throw Failure.Unconfirmed(Codes.StaleRef, "the list replaced the element as it scrolled it into view");
        }

        return shown
            ? true
            : throw Failure.Unconfirmed(
                Codes.ActionFailed,
                "the element is still off screen after it was scrolled into view"
            );
    }

    /// <summary>
    /// What an action did once <see cref="Reveal"/> scrolled its list: the scroll moved the app's content, so a failure after it is no
    /// longer "nothing sent".
    /// </summary>
    internal static T AfterScroll<T>(bool scrolled, Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Failure failure) when (scrolled)
        {
            throw failure.Delivered();
        }
    }

    internal static void AfterScroll(bool scrolled, Action work) => AfterScroll(scrolled, () =>
        {
            work();
            return 0;
        });

    private static void Click(Entry entry, Instant deadline)
    {
        var element = entry.Element;
        Enabled(element, deadline, "click");

        // The engine checked the node shows before the call; the wait above can outlast that, as the window minimizes or a list
        // scrolls meanwhile.
        var scrolled = Reveal(element, deadline, "click");
        AfterScroll(scrolled, () => ClickShown(entry, deadline));
    }

    /// <summary><see cref="Click"/> on an element that shows.</summary>
    private static void ClickShown(Entry entry, Instant deadline)
    {
        var element = entry.Element;
        var (axRole, role) = RoleOf(element, "click");

        // A tap on a text field puts the keyboard there. AppKit's password field also offers AXConfirm, which the action choice below
        // would send in its place, leaving the keyboard in the field before.
        if (role == "textfield" && Settable(element, "AXFocused", "click"))
        {
            Focus(element, deadline);
            return;
        }

        var names = ActionNames(element, "click");
        var action = new[] { "AXPress", "AXOpen", "AXConfirm" }.FirstOrDefault(name => names.Contains(name));
        if (action is not null)
        {
            // Only a press that opens a menu waits for it, so only it counts the open menus, a read of every window there is.
            int? before = axRole is "AXPopUpButton" or "AXMenuButton" ? Windows.OpenMenus(entry.Pid) : null;
            Perform(element, action, "click");
            if (before is { } count)
            {
                MenuShown(entry.Pid, count, deadline);
            }

            return;
        }

        // An NSTableView's row is selected through its table (`AXSelectedRows`), and a cell in a row through that row: neither
        // takes a press, and the row's own `AXSelected` may not be writable.
        Element? row = role switch
        {
            "row" or "treeitem" => element,
            "cell" => element.ElementAttribute("AXParent") is { } parent
            && parent.Text("AXRole") is "AXRow" or "AXOutlineRow"
                ? parent
                : null,
            _ => null,
        };
        if (row is not null)
        {
            var list = row.ElementAttribute("AXParent");
            if (ChooseRow(list, row, deadline, "the row did not become selected"))
            {
                return;
            }
        }

        if (role is "cell" or "option" or "tab" && Settable(element, "AXSelected", "click"))
        {
            Go("select");
            var written = element.SetBool("AXSelected", true);
            if (written != AxError.Success)
            {
                throw WriteFailed(written, "select");
            }

            if (Until(deadline, () => element.Boolean("AXSelected") == true))
            {
                return;
            }

            throw Failure.Unconfirmed(Codes.ActionFailed, "the element did not become selected");
        }

        if (Roles.TakesText(role) && Settable(element, "AXFocused", "click"))
        {
            Focus(element, deadline);
            return;
        }

        throw Failure.Refused(Codes.ActionNotSupported, $"this {role} takes no click through accessibility");
    }

    private static void Focus(Element element, Instant deadline)
    {
        // The app's focused element is the one source, as the snapshot reads it: a table's cell reports the table's focus as its own
        // `AXFocused`.
        AxResult<bool> Holds()
        {
            if (element.Pid() is { } pid)
            {
                var read = Stalled(Element.Application(pid).Attribute("AXFocusedUIElement"));
                return read.IsOk
                    ? AxResult<bool>.Ok(Element.Same(read.Value?.Element, element))
                    : AxResult<bool>.Err(read.Error);
            }

            var focused = Stalled(element.Attribute("AXFocused"));
            return focused.IsOk ? AxResult<bool>.Ok(focused.Value?.Boolean == true) : AxResult<bool>.Err(focused.Error);
        }

        // A stall before the write is no proof the element lacks focus: nothing is sent, and a retry may get past it.
        var before = Holds();
        if (!before.IsOk)
        {
            throw Unread(before.Error, "focus");
        }

        if (before.Value)
        {
            return;
        }

        Go("focus");

        // A focus write that failed after the app took it (a focus handler that replaced the element) may have run its handlers:
        // only a refusal shows nothing was sent, as for `set_value`.
        var written = element.SetBool("AXFocused", true);
        if (written != AxError.Success)
        {
            throw WriteFailed(written, "focus");
        }

        var last = AxResult<bool>.Ok(false);
        Until(deadline, () =>
        {
            last = Holds();
            return last is { IsOk: true, Value: true };
        });
        if (last is { IsOk: true, Value: true })
        {
            return;
        }

        throw !last.IsOk
            ? Failure.Unconfirmed(
                Codes.AppUnresponsive,
                "focus: the app did not answer in time after the element was focused"
            )
            : Failure.Unconfirmed(Codes.ActionFailed, "focus: the element did not take keyboard focus");
    }

    /// <summary>
    /// Runs <paramref name="work"/> as the action <paramref name="inputId"/> names: until it returns, every input it sends first checks
    /// that the action was not cancelled and is still in time (<see cref="Go"/>).
    /// </summary>
    internal static T Guarded<T>(uint? inputId, Instant deadline, Func<T> work)
    {
        using var run = Input.TypingRun.Start(inputId);
        t_current = new Running(run, deadline);
        try
        {
            return work();
        }
        finally
        {
            t_current = null;
        }
    }

    /// <summary>Runs one action on the element <paramref name="reference"/> names.</summary>
    public static void Act(string reference, DriverAction action, uint? inputId, Instant deadline) =>
        Guarded(inputId, deadline, () =>
        {
            Run(reference, action, deadline);
            return 0;
        });

    private static void Run(string reference, DriverAction action, Instant deadline)
    {
        var entry = Resolve(reference);
        var element = entry.Element;
        switch (action)
        {
            case DriverAction.Click:
                Click(entry, deadline);
                break;

            // A press on a disabled control is acknowledged and ignored: each wait for it to be enabled, as a tap does. A hover
            // presses nothing, so it goes to a disabled control too.
            case DriverAction.DoubleClick:
                Enabled(element, deadline, "double click");
                Physical(entry, PointerKind.Click(false, 2));
                break;
            case DriverAction.Hover:
                Physical(entry, PointerKind.Move);
                break;
            case DriverAction.RightClick:
            {
                Enabled(element, deadline, "right click");

                // Refused before the scroll below, which would move the list for nothing.
                if (!ActionNames(element, "right click").Contains("AXShowMenu"))
                {
                    throw Failure.Refused(Codes.ActionNotSupported, "this element has no context menu");
                }

                var scrolled = Reveal(element, deadline, "right click");
                var before = Windows.OpenMenus(entry.Pid);
                AfterScroll(scrolled, () => Perform(element, "AXShowMenu", "right click"));
                MenuShown(entry.Pid, before, deadline);
                break;
            }

            case DriverAction.Focus:
                Focus(element, deadline);
                break;
            case DriverAction.Clear:
                SetValue(element, string.Empty, deadline);
                break;
            case DriverAction.Check check:
                Check(element, check.Wanted, deadline);
                break;
            case DriverAction.ScrollTo:
                Reveal(element, deadline, "scroll into view");
                break;
            case DriverAction.SetValue set:
                SetValue(element, set.Text, deadline);
                break;
            case DriverAction.SelectAll:
                SelectAll(element, deadline);
                break;
            case DriverAction.Select select:
                Select(entry, select.Label, deadline);
                break;
            case DriverAction.Scroll scroll:
                Scroll(element, scroll.Direction, scroll.Pages);
                break;
            case DriverAction.Type type:
                TypeText(entry, type.Text, type.Physical, deadline);
                break;
        }
    }

    /// <summary>
    /// Presses a key combo into a window of <paramref name="pid"/>. Without physical input the key goes to the process, which hands it
    /// to its key window: the window must be that one. With it, the window comes forward first. No key goes out once
    /// <paramref name="inputId"/> was cancelled or <paramref name="deadline"/> passed. With <paramref name="focused"/>, the key goes
    /// only while the element that ref names holds the app's focus: a window that came forward can open a sheet that takes it, and a
    /// locator's key must reach its own control.
    /// </summary>
    public static void Press(
        int pid,
        uint window,
        string combo,
        bool physical,
        string? focused,
        uint? inputId,
        Instant deadline
    )
    {
        var parsed = Input.ParseCombo(combo);
        Guarded(inputId, deadline, () =>
        {
            var element = Windows.Find(pid, window);
            Route route;
            if (physical)
            {
                Raise(pid, element);
                route = Route.Physical;
            }
            else
            {
                KeyWindow(pid, element);
                route = Route.Process(pid);
            }

            Entry? control = null;
            if (focused is not null)
            {
                control = Refs.Get(focused)
                    ?? throw Failure.Refused(Codes.StaleRef, $"{focused} names no element of a recent snapshot");
            }

            // A key sent as physical input goes to whatever app is frontmost: the user may have switched apps since the raise,
            // while the app still names its own focused control. The window check waits on the app, so it comes first.
            if (physical)
            {
                KeyWindow(pid, element);
            }

            // The control a locator's key is for must hold the app's focus, read after the window check: a sheet that opened
            // during it took the focus, and its default button would take the key.
            if (
                control is not null
                && !Element.Same(
                    ElementAttribute(Element.Application(pid), "AXFocusedUIElement", "press"),
                    control.Element
                )
            )
            {
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "the control lost keyboard focus before the key was sent, so nothing was sent"
                );
            }

            // The frontmost check is the last read before a physical key, so a switch to another app is still caught.
            if (physical && !Frontmost(pid))
            {
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "another app came forward before the key was sent, so nothing was sent"
                );
            }

            Go("press");
            try
            {
                Input.Press(parsed, route);
            }
            catch (Failure failure)
            {
                throw failure.Delivered();
            }

            return 0;
        });
    }

    /// <summary>Whether the app is the frontmost one. A read the app did not answer in time is a stall.</summary>
    private static bool Frontmost(int pid)
    {
        var read = Element.Application(pid).Attribute("AXFrontmost");
        if (read.IsOk)
        {
            return read.Value!.Boolean == true;
        }

        return read.Error == AxError.CannotComplete
            ? throw Unread(read.Error, "read whether the app is frontmost")
            : false;
    }

    /// <summary>
    /// Refuses unless <paramref name="window"/> is the app's key window, or holds the focused sheet or alert: a key or text sent to the
    /// app goes to its key window, and would land in another one.
    /// </summary>
    private static void KeyWindow(int pid, Element window)
    {
        if (
            ElementAttribute(Element.Application(pid), "AXFocusedWindow", "read the app's key window") is { } focused
            && Holds(focused, window, "read the app's key window")
        )
        {
            return;
        }

        throw Failure
            .Refused(
                Codes.ActionFailed,
                "the window is not the app's key window, so a key sent to the app would land elsewhere"
            )
            .WithDetail("cause", "exact_window_focus_unconfirmed");
    }

    /// <summary>
    /// Brings a window of <paramref name="pid"/> forward, only while <paramref name="inputId"/> is not cancelled and
    /// <paramref name="deadline"/> has not passed: finding the window can wait on the app, and the caller may have moved on by then.
    /// </summary>
    public static void FocusWindow(int pid, uint window, uint? inputId, Instant deadline) =>
        Guarded(inputId, deadline, () =>
        {
            // A call given up on before it began reads nothing either.
            Go("bring the window forward");
            var element = Windows.Find(pid, window);
            Raise(pid, element);
            return 0;
        });
}
