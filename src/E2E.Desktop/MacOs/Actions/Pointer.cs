// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>The sheet on a window that held its focus when a point was taken.</summary>
internal abstract record Sheet
{
    /// <summary>No sheet held the window's focus.</summary>
    public sealed record None : Sheet;

    /// <summary>A sheet, by its key (<see cref="Element.SheetKey"/>).</summary>
    public sealed record Key(string Value) : Sheet;

    /// <summary>A sheet the caller could name no key for: no sheet now is known to be it.</summary>
    public sealed record Unnamed : Sheet;
}

/// <summary>The window a point was taken in, which must still take a click at it.</summary>
internal sealed record PointTarget(int Pid, uint Window, Sheet Sheet, bool Content);

/// <summary>What the pointer lands on at a point.</summary>
internal enum Aim
{
    /// <summary>The element, or content inside it that takes no press of its own.</summary>
    Target,

    /// <summary>A control inside the element (a row's inline button): a click there presses it, not the element.</summary>
    Control,

    /// <summary>Something outside the element.</summary>
    Other,
}

/// <summary>Physical pointer input: the hit test that says where a click lands, and the checks of a point taken in a window.</summary>
internal static partial class Actions
{
    /// <summary>The most steps a walk up from the element under the pointer takes: deeper than any window a snapshot reads.</summary>
    internal const int MaxWalk = 256;

    /// <summary>Roles whose element takes a click of its own: a press, or the text a double-click selects.</summary>
    internal static bool Presses(string axRole) =>
        axRole
            is "AXButton"
                or "AXCheckBox"
                or "AXSwitch"
                or "AXRadioButton"
                or "AXPopUpButton"
                or "AXMenuButton"
                or "AXLink"
                or "AXDisclosureTriangle"
                or "AXIncrementor"
                or "AXSlider"
                or "AXTextField"
                or "AXTextArea"
                or "AXComboBox";

    /// <summary>
    /// Whether an element under the pointer takes a click of its own: by its role, or, for a role that may be decoration or a control
    /// (an image, a group), by the press it offers. The error is a stall.
    /// </summary>
    internal static AxResult<bool> TakesClick(string axRole, Func<AxResult<List<string>>> actions)
    {
        if (Presses(axRole))
        {
            return AxResult<bool>.Ok(true);
        }

        // A role that never takes input (a separator, a splitter) is content. Any other one (a text label, an image, a group a
        // custom control draws itself in) is a control of its own when it offers a press, as the snapshot gives it a ref for one.
        if (Roles.Inert(Roles.Role(axRole, null)))
        {
            return AxResult<bool>.Ok(false);
        }

        var names = actions();
        if (names.IsOk)
        {
            return AxResult<bool>.Ok(names.Value!.Any(name => name is "AXPress" or "AXOpen" or "AXConfirm"));
        }

        return names.Error == AxError.CannotComplete
            ? AxResult<bool>.Err(AxError.CannotComplete)
            : AxResult<bool>.Ok(false);
    }

    /// <summary>One step of the walk up from the element under the pointer: whether it is the element, and else whether it takes a click of its own.</summary>
    internal readonly record struct WalkStep(bool IsElement, bool Clicks);

    /// <summary>
    /// Folds the walk up from the element under the pointer: each step says whether it is the element, and else whether it takes a
    /// click of its own. The walk ends (a null step) at the top, or after <see cref="MaxWalk"/> steps, without reaching the element.
    /// </summary>
    internal static AxResult<Aim> AimAlong(Func<AxResult<WalkStep>?> walk)
    {
        var control = false;
        for (var step = 0; step < MaxWalk; step++)
        {
            var next = walk();
            if (next is not { } result)
            {
                return AxResult<Aim>.Ok(Aim.Other);
            }

            if (!result.IsOk)
            {
                return AxResult<Aim>.Err(result.Error);
            }

            if (result.Value.IsElement)
            {
                return AxResult<Aim>.Ok(control ? Aim.Control : Aim.Target);
            }

            control = control || result.Value.Clicks;
        }

        return AxResult<Aim>.Ok(Aim.Other);
    }

    /// <summary>
    /// What the pointer lands on at a point on screen, relative to <paramref name="element"/>, asked of the whole system: a floating
    /// panel of another app over the element is what a click there reaches. An error when the app did not answer.
    /// </summary>
    internal static AxResult<Aim> AimAt(Element element, double x, double y)
    {
        var at = Element.SystemWide().ElementAt(x, y);
        if (!at.IsOk)
        {
            return AxResult<Aim>.Err(at.Error);
        }

        var next = at.Value;
        return AimAlong(() =>
        {
            var hit = next;
            next = null;
            if (hit is null)
            {
                return null;
            }

            if (hit.Equals(element))
            {
                return AxResult<WalkStep>.Ok(new WalkStep(true, false));
            }

            string axRole;
            var roleRead = hit.Attribute("AXRole");
            if (roleRead.IsOk)
            {
                axRole = roleRead.Value!.Text ?? string.Empty;
            }
            else if (roleRead.Error == AxError.CannotComplete)
            {
                return AxResult<WalkStep>.Err(AxError.CannotComplete);
            }
            else
            {
                axRole = string.Empty;
            }

            var clicks = TakesClick(axRole, hit.Actions);
            if (!clicks.IsOk)
            {
                return AxResult<WalkStep>.Err(clicks.Error);
            }

            var parent = hit.Attribute("AXParent");
            if (parent.IsOk && parent.Value is AxValue.ElementValue parentElement)
            {
                next = parentElement.Value;
            }
            else if (!parent.IsOk && parent.Error == AxError.CannotComplete)
            {
                return AxResult<WalkStep>.Err(AxError.CannotComplete);
            }

            return AxResult<WalkStep>.Ok(new WalkStep(false, clicks.Value));
        });
    }

    /// <summary>
    /// Whether the pointer may go where <paramref name="aim"/> says it lands: <paramref name="clicks"/> refuses a control inside the
    /// element, which a hover does not press. A hit test the app did not answer is a stall, never a covering element.
    /// </summary>
    internal static void Aimed(AxResult<Aim> aim, bool clicks, string when)
    {
        if (!aim.IsOk)
        {
            throw Unread(aim.Error, "find the element under the pointer");
        }

        switch (aim.Value)
        {
            case Aim.Target:
                return;
            case Aim.Control when !clicks:
                return;
            case Aim.Control:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    $"{when}, a control inside this element is under the pointer, so the click would press it"
                );
            default:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    $"{when}, another element is under the pointer, so no click was sent there"
                );
        }
    }

    /// <summary>Physical pointer input at the element's center, once its window is forward and nothing covers it.</summary>
    internal static void Physical(Entry entry, PointerKind action)
    {
        var window = Windows.Find(entry.Pid, entry.Window);
        Go("pointer");
        Raise(entry.Pid, window);
        var (x, y) = Center(entry.Element);
        var clicks = action.IsClick;
        Aimed(AimAt(entry.Element, x, y), clicks, "before the move");

        // Raising the window and the hit test can take a second on a slow app: the caller may have given up meanwhile. The pointer's
        // move can reveal a control over the element (a row's inline button): the first click checks the point again. A
        // double-click's second click follows at once, as part of the same gesture.
        Input.Pointer(x, y, action, click =>
        {
            if (click == 1)
            {
                StillAimed(() => AimAt(entry.Element, x, y));
            }
            else
            {
                Go("pointer");
            }
        });
    }

    /// <summary>
    /// Whether a click may go: the caller still wants it, the point still lands on the element, and the caller still wants it after
    /// that check, which waits on the app.
    /// </summary>
    internal static void StillAimed(Func<AxResult<Aim>> aim)
    {
        Go("pointer");
        Aimed(aim(), true, "after the move");
        Go("pointer");
    }

    /// <summary>
    /// Whether a click at a point taken in a window may go: the app is frontmost, the window holds its focus, and the sheet on it
    /// (<paramref name="now"/>) is the one that was there when the point was taken (<paramref name="then"/>): none opened, none closed,
    /// and none took another's place. A sheet with no key is never known to be the same one.
    /// </summary>
    internal static void TakesPoint(bool frontmost, bool holds, Sheet now, Sheet then)
    {
        if (!frontmost)
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "another app came forward over the window the point was taken in, so no click was sent"
            );
        }

        if (!holds)
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "another window took focus from the one the point was taken in, so no click was sent"
            );
        }

        string message;
        switch (now, then)
        {
            case (Sheet.None, Sheet.None):
                return;
            case (_, Sheet.None):
                message = "a sheet opened over the window the point was taken in, so no click was sent";
                break;
            case (Sheet.None, _):
                message = "the sheet the point was taken on closed, so no click was sent to what was under it";
                break;
            case (Sheet.Key(var nowKey), Sheet.Key(var thenKey)) when nowKey == thenKey:
                return;
            default:
                message =
                    "another sheet took the place of the one the point was taken on, or it cannot be told apart from one, so no click was sent";
                break;
        }

        throw Failure.Refused(Codes.ActionFailed, message);
    }

    /// <summary>
    /// <see cref="TakesPoint"/> for a window of the target's app, read now, and the point on top of that window, not of another app's
    /// or window's over it. A read the app did not answer in time is a stall.
    /// </summary>
    internal static void WindowTakesPoint(PointTarget target, double x, double y)
    {
        var window = Windows.Find(target.Pid, target.Window);
        var app = Element.Application(target.Pid);
        var frontmost = Frontmost(target.Pid);
        Element? focused = null;
        var read = app.Attribute("AXFocusedWindow");
        if (read.IsOk)
        {
            focused = read.Value!.Element;
        }
        else if (read.Error == AxError.CannotComplete)
        {
            throw Unread(read.Error, "read the app's focused window");
        }

        var holds = focused is not null && Holds(focused, window, "read the app's focused window");
        Sheet now =
            focused is not null && !focused.Equals(window) ? new Sheet.Key(focused.SheetKey()) : new Sheet.None();
        TakesPoint(frontmost, holds, now, target.Sheet);
        PointLands(AimAt(window, x, y), target.Content);
    }

    /// <summary>
    /// For a hover: the point is on the window it was taken in, not on another app's or window's over it, which the move would reach.
    /// A hover presses nothing, so a control under the point, focus, and sheets are not checked.
    /// </summary>
    internal static void WindowOnPoint(PointTarget target, double x, double y)
    {
        var window = Windows.Find(target.Pid, target.Window);
        var aim = AimAt(window, x, y);
        if (!aim.IsOk)
        {
            throw Unread(aim.Error, "find the element under the pointer");
        }

        if (aim.Value == Aim.Other)
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "another window or app is on top at the point, so the pointer was not moved"
            );
        }
    }

    /// <summary>
    /// Whether a click at a point may go where the hit test (relative to the window) says it lands. A tap at a point names no
    /// element, so any control of the window may be there, unless the point was taken on <paramref name="content"/>, a node that takes
    /// no click of its own: a control there now (one coming forward revealed) would take the click.
    /// </summary>
    internal static void PointLands(AxResult<Aim> aim, bool content)
    {
        if (!aim.IsOk)
        {
            throw Unread(aim.Error, "find the element under the pointer");
        }

        switch (aim.Value)
        {
            case Aim.Target:
                return;
            case Aim.Control when !content:
                return;
            case Aim.Control:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "a control is under the pointer at the node the point was taken on (over it, or one it is in), so no click was sent"
                );
            default:
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "another window or app is on top at the point, so no click was sent"
                );
        }
    }

    /// <summary>
    /// Physical pointer input at a point on screen, sent only while <paramref name="inputId"/> is not cancelled and
    /// <paramref name="deadline"/> has not passed. With <paramref name="target"/> (the app, the window the point was taken in, the
    /// sheet on it then, and whether the point is on a node that takes no click of its own), the first click goes only while that
    /// window still takes it, checked after the move.
    /// </summary>
    public static void PointerAt(double x, double y, string kind, uint? inputId, Instant deadline, PointTarget? target)
    {
        var action = kind switch
        {
            "hover" => PointerKind.Move,
            "tap" => PointerKind.Click(false, 1),
            "doubleTap" => PointerKind.Click(false, 2),
            "secondaryTap" => PointerKind.Click(true, 1),
            _ => throw Failure.Refused(Codes.ActionNotSupported, $"no pointer action {kind}"),
        };
        Guarded(inputId, deadline, () =>
        {
            // A point another window or app already covers is refused before the move, which would reach that window: nothing is
            // sent. The check after the move is for one that opens meanwhile, before a click. A hover presses nothing, so it is
            // checked only for a window or app over the point, which the move alone would reach.
            if (target is not null)
            {
                Go("pointer");
                if (action.IsClick)
                {
                    WindowTakesPoint(target, x, y);
                }
                else
                {
                    WindowOnPoint(target, x, y);
                }
            }

            Input.Pointer(x, y, action, click =>
            {
                Go("pointer");

                // A sheet or window that opened during the move would take the click: the first one checks, and the caller
                // once more after that check, which waits on the app.
                if (target is not null && click == 1)
                {
                    WindowTakesPoint(target, x, y);
                    Go("pointer");
                }
            });
            return 0;
        });
    }
}
