// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Desktop.MacOs;

/// <summary>A control's state and value: a box checked or unchecked, a stepper stepped, a field's text set or selected.</summary>
internal static partial class Actions
{
    /// <summary>The states a box can show: off, on, and mixed.</summary>
    internal const int BoxStates = 3;

    /// <summary>The most presses a stepper gets to reach a value.</summary>
    internal const int MaxSteps = 500;

    /// <summary>
    /// Whether an uncheck asks a selected radio button to clear itself, which only selecting another does: a press would run its
    /// handler and leave it selected.
    /// </summary>
    internal static bool ClearsOnlyByAnother(string role, bool wanted, byte? shown) =>
        role == "radiobutton" && !wanted && shown == 1;

    private static void Check(Element element, bool wanted, Instant deadline)
    {
        // A press on anything but a box would run its handler (a submit, a delete) before the check could fail.
        var (_, role) = RoleOf(element, "check");
        if (!Roles.Toggleable(role))
        {
            throw Failure.Refused(Codes.ActionNotSupported, $"a {role} has no checked state to set");
        }

        var target = (byte)(wanted ? 1 : 0);
        var what = wanted ? "check" : "uncheck";
        byte? Read()
        {
            var value = element.Attribute("AXValue");
            if (!value.IsOk && value.Error is AxError.CannotComplete or AxError.InvalidElement)
            {
                throw Unread(value.Error, what);
            }

            return Snapshot.ParseToggle(value.IsOk ? value.Value : null);
        }

        var shown = Read();
        if (shown != target)
        {
            // A disabled AppKit box acknowledges a press and ignores it: wait for it to be enabled, then read it again, as the app
            // may have set it meanwhile. A box that already shows the state asked for is left alone, disabled or not.
            Enabled(element, deadline, what);
            shown = Read();
        }

        // A box whose state cannot be read may already show the one asked for: a press would turn it the other way. Both refusals
        // come before the scroll below, as a refusal says nothing reached the app.
        if (shown is null)
        {
            throw Failure.Refused(
                Codes.ActionNotSupported,
                $"the {role} shows no checked state the engine can read, so pressing it could undo the one asked for"
            );
        }

        if (ClearsOnlyByAnother(role, wanted, shown))
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "a selected radio button is cleared only by selecting another one; nothing was pressed"
            );
        }

        // A box a scroll area holds out of view is pressed all the same through accessibility, out of the user's reach.
        var scrolled = shown != target && Reveal(element, deadline, what);
        if (scrolled)
        {
            // The list moved: the app may have set the box as it came into view, and a press now could undo that.
            shown = AfterScroll(true, Read);
            if (shown == target)
            {
                return;
            }

            if (shown is null)
            {
                throw Failure.Unconfirmed(
                    Codes.ActionFailed,
                    $"the {role}'s state could not be read once it was scrolled into view, so it was not pressed"
                );
            }
        }

        var reached = AfterScroll(scrolled, () => PressToState(
                target,
                shown,
                () => Perform(element, "AXPress", what),
                () =>
                {
                    var before = shown;
                    Until(deadline, () => ToggleState(element) != before);
                    shown = ToggleState(element);
                    return shown;
                }
            ));
        if (!reached)
        {
            throw Failure.Unconfirmed(Codes.ActionFailed, $"the box is still not {(wanted ? "checked" : "unchecked")}");
        }
    }

    /// <summary>
    /// Presses a box until it shows <paramref name="target"/>, at most once per state it can show: an AppKit box goes mixed, on, off,
    /// so unchecking a mixed box takes two presses. <paramref name="settle"/> waits for the state a press led to. It stops early when the
    /// state cannot be read or a press changed nothing, so a box that does not answer is not pressed again and again. A failed first
    /// press sent nothing; a failed later one follows a press that landed. A press that answered with an error but may have landed is
    /// judged by the state it led to: a SwiftUI control in a sheet answers its first press so.
    /// </summary>
    internal static bool PressToState(byte target, byte? shown, Action press, Func<byte?> settle)
    {
        if (shown == target)
        {
            return true;
        }

        var before = shown;
        for (var attempt = 0; attempt < BoxStates; attempt++)
        {
            Failure? pressed = null;
            try
            {
                press();
            }
            catch (Failure failure)
            {
                pressed = attempt == 0 ? failure : failure.Delivered();
            }

            if (pressed is { Delivery: Delivery.NotDelivered })
            {
                throw pressed;
            }

            var now = settle();
            if (now == target)
            {
                return true;
            }

            if (now is null || now == before)
            {
                return pressed is null ? false : throw pressed;
            }

            before = now;
        }

        return false;
    }

    /// <summary>
    /// Whether two stepper values are the same number: a step of 0.1 sums to 0.30000000000000004, not 0.3. <paramref name="step"/>, once
    /// a press showed it, bounds the tolerance to half a step, so no rounding allowance hides a whole step.
    /// </summary>
    internal static bool Same(double a, double b, double? step)
    {
        // Whole numbers a float holds exactly compare exactly: a step of 1 apart is a step, however large the value (a Unix time in
        // microseconds). Others compare up to a few hundred float epsilons of the larger, the error of summing fractional steps such
        // as 0.1.
        const double exact = 9_007_199_254_740_992.0;
        if (Math.Truncate(a) == a && Math.Truncate(b) == b && Math.Abs(a) <= exact && Math.Abs(b) <= exact)
        {
            return a == b;
        }

        var rounding = 2.220446049250313e-16 * 256.0 * Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1.0);
        return Math.Abs(a - b) <= (step is { } size ? Math.Min(rounding, Math.Abs(size) / 2.0) : rounding);
    }

    /// <summary>
    /// Steps a value toward <paramref name="target"/>, one increment or decrement at a time, until it shows it. It stops when the value
    /// cannot be read or a step changed nothing. When a step goes past the target (the step does not divide the distance), it steps
    /// back to the value it started from, so a value the stepper cannot take leaves it as it was. It never steps past
    /// <paramref name="deadline"/>: the steps sent by then stay sent, delivered but unverified. A failed first step sent nothing; a
    /// failed later one follows steps that landed. A step that answered with an error but may have landed is judged by the value it led
    /// to. A start within rounding of the target, but not equal to it, may be the target summed by earlier steps or a value a small step
    /// away from it: one step tells which, and when it was the target, a step back puts it as it was. Its failures name no value: the
    /// target may be a secret, and the ledger that redacts one does not match it re-parsed as a number (<c>0042</c> as <c>42</c>), nor
    /// the stepper's values a step from it.
    /// </summary>
    internal static bool StepTo(double target, double? shown, Instant deadline, Action<bool> step, Func<double?> settle)
    {
        if (shown is not { } start)
        {
            return false;
        }

        if (start == target)
        {
            return true;
        }

        var probing = Same(start, target, null);
        double? size = null;
        var now = start;
        for (var attempt = 0; attempt < MaxSteps; attempt++)
        {
            if (!(probing && attempt == 0) && Same(now, target, size))
            {
                return true;
            }

            if (Instant.Now >= deadline)
            {
                var timeout = Failure.Refused(Codes.Timeout, "the stepper ran out of time before it reached the value");
                throw attempt == 0 ? timeout : timeout.Delivered();
            }

            var up = now < target;
            Failure? stepped = null;
            try
            {
                step(up);
            }
            catch (Failure failure)
            {
                stepped = attempt == 0 ? failure : failure.Delivered();
            }

            if (stepped is { Delivery: Delivery.NotDelivered })
            {
                throw stepped;
            }

            if (settle() is not { } next)
            {
                return stepped is null ? false : throw stepped;
            }

            // A press that changed nothing leaves the very number it found: any difference is a step, however small.
            if (next == now)
            {
                return stepped is null ? false : throw stepped;
            }

            // A step that went the other way wrapped around the stepper's range: stepping on would only cycle through it.
            if ((up && next < now) || (!up && next > now))
            {
                throw Failure.Unconfirmed(
                    Codes.ActionFailed,
                    "the stepper went the other way on a step toward the value: it wraps, and cannot reach the value"
                );
            }

            size = Math.Abs(next - now);
            var startWasTarget = probing && attempt == 0 && Same(start, target, size);
            if (startWasTarget || (!Same(next, target, size) && ((up && next > target) || (!up && next < target))))
            {
                // Put back the value it had before the fill, a step at a time: a fill the stepper cannot take must not leave it
                // changed. As many steps back as went forward, and none more once a step changes nothing.
                var value = next;
                for (var back = 0; back <= attempt; back++)
                {
                    if (Same(value, start, size) || StepFails(step, !up))
                    {
                        break;
                    }

                    if (settle() is { } returned && !Same(returned, value, size))
                    {
                        value = returned;
                    }
                    else
                    {
                        break;
                    }
                }

                if (startWasTarget)
                {
                    return Same(value, start, size)
                        ? true
                        : throw Failure.Unconfirmed(
                            Codes.ActionFailed,
                            "the stepper already showed about the value; a step to tell it from the value left it changed"
                        );
                }

                var failure = Failure.Unconfirmed(Codes.ActionFailed, "the stepper's step goes past the value");
                throw Same(value, start, size)
                    ? failure.WithMessage($"{failure.Message}; it was stepped back to where it started")
                    : failure.WithMessage($"{failure.Message}, and could not be stepped back to where it started");
            }

            now = next;
        }

        return Same(now, target, size);
    }

    private static bool StepFails(Action<bool> step, bool up)
    {
        try
        {
            step(up);
            return false;
        }
        catch (Failure)
        {
            return true;
        }
    }

    /// <summary>
    /// The number a stepper shows, from its <c>AXValue</c> read. A read the app did not answer in time, or of an element that is gone,
    /// is no proof the stepper shows no number: it fails before anything is sent, a stall as one a retry may get past. A value that is
    /// no number is null.
    /// </summary>
    internal static double? ShownNumber(AxResult<AxValue> read, string what)
    {
        if (read.IsOk)
        {
            return (read.Value as AxValue.NumberValue)?.Value;
        }

        return read.Error is AxError.CannotComplete or AxError.InvalidElement ? throw Unread(read.Error, what) : null;
    }

    /// <summary>Whether a stepper's target lies outside the range it reports: below <paramref name="min"/> or above <paramref name="max"/>. A bound it does not report limits nothing.</summary>
    internal static bool Outside(double target, double? min, double? max) =>
        (min is { } low && target < low) || (max is { } high && target > high);

    /// <summary>The number a text names, when it names a finite one: <c>NaN</c> or <c>inf</c> is no value a control can take.</summary>
    internal static double? Finite(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        && double.IsFinite(number)
            ? number
            : null;

    private static void SetValue(Element element, string text, Instant deadline)
    {
        // A disabled field takes no value: wait for it to be enabled, as a fill's actionability wait does.
        Enabled(element, deadline, "set value");
        var (axRole, role) = RoleOf(element, "set value");
        var secure = axRole == "AXSecureTextField" || element.Text("AXSubrole") == "AXSecureTextField";
        if (!Settable(element, "AXValue", "set value"))
        {
            // A stepper whose value cannot be written still steps toward a number.
            var names = ActionNames(element, "set value");
            var steps = names.Any(name => name is "AXIncrement" or "AXDecrement");
            var current = steps ? ShownNumber(element.Attribute("AXValue"), "set value") : null;
            if (current is { } first && Finite(text) is { } target)
            {
                // A value outside the range the stepper reports is one no step reaches: refused before any press.
                double? Bound(string name) =>
                    element.Attribute(name) is { IsOk: true, Value: AxValue.NumberValue number } ? number.Value : null;
                if (Outside(target, Bound("AXMinValue"), Bound("AXMaxValue")))
                {
                    throw Failure.Refused(Codes.ActionFailed, $"the value is outside the range of this {role}");
                }

                double? Number() =>
                    element.Attribute("AXValue") is { IsOk: true, Value: AxValue.NumberValue number }
                        ? number.Value
                        : null;

                // The number read above, not a second read, which a stall would turn into no number and a fill that stops short
                // without a step.
                double? shown = first;
                var reached = StepTo(
                    target,
                    shown,
                    deadline,
                    up => Perform(element, up ? "AXIncrement" : "AXDecrement", "step the value"),
                    () =>
                    {
                        var before = shown;
                        Until(deadline, () => Number() != before);
                        shown = Number();
                        return shown;
                    }
                );
                if (!reached)
                {
                    throw Failure.Unconfirmed(Codes.ActionFailed, $"the {role} stopped short of the value");
                }

                return;
            }

            throw Failure.Refused(Codes.ActionNotSupported, $"the value of this {role} cannot be set");
        }

        // Whether the value is a number (a slider's), read before anything is sent: a read the app did not answer in time is no proof
        // it is text, and writing text to a number fails.
        bool numeric;
        var valueRead = element.Attribute("AXValue");
        if (valueRead.IsOk)
        {
            numeric = valueRead.Value is AxValue.NumberValue;
        }
        else if (valueRead.Error == AxError.CannotComplete)
        {
            throw Unread(AxError.CannotComplete, "set value");
        }
        else
        {
            numeric = false;
        }

        // A field that holds focus commits its text the way typing does; one that cannot take focus still takes the value. Focus moved
        // by this call has reached the app (its handlers may have run), so a later failure is no longer "nothing sent". A focus write
        // that failed after the app took it (a focus handler that replaced the field) may still have run its handlers: only a refusal
        // shows it did not.
        var movedFocus = false;
        if (
            Roles.TakesText(role)
            && Settable(element, "AXFocused", "set value")
            && element.Boolean("AXFocused") != true
        )
        {
            Go("set value");
            var focusWrite = element.SetBool("AXFocused", true);
            movedFocus = focusWrite == AxError.Success || !Refuses(focusWrite);
        }

        // The reads above can take long on a slow app; the caller may have given up since.
        try
        {
            Go("set value");
        }
        catch (Failure failure) when (movedFocus)
        {
            throw failure.Delivered();
        }

        var number = numeric ? Finite(text) : null;
        var written = number is { } parsed ? element.SetNumber("AXValue", parsed) : element.SetText("AXValue", text);

        // A write the app answered with anything but a refusal may have run its change handler (one that replaced the field answers a
        // gone element): only a refusal says nothing was sent.
        if (written != AxError.Success)
        {
            throw movedFocus ? Refused(written, "set value").Delivered() : WriteFailed(written, "set value");
        }

        if (secure)
        {
            // A secure field shows no value, but counts its characters: the count says the text landed whole (one a formatter cut
            // short does not), with no secret read. A field that counts none is taken at the write's word.
            (bool IsOk, int? Count) Count() => SecureCount(element.Attribute("AXNumberOfCharacters"));
            var wanted = text.Length;
            if (Count() is (true, null) || Until(deadline, () => Count() is (true, { } count) && count == wanted))
            {
                return;
            }

            throw Failure.Unconfirmed(Codes.ActionFailed, "the secure field did not take the whole text set");
        }

        var expected = numeric ? (Finite(text) is { } formatted ? AxFormat.Number(formatted) : text) : text;

        // An empty AppKit text field may answer no value at all, which reads as empty, as the projection reads it.
        bool Shown()
        {
            var read = element.Attribute("AXValue");
            if (read.IsOk)
            {
                return read.Value!.Display() == expected;
            }

            return read.Error == AxError.NoValue && Roles.TakesText(role) && expected.Length == 0;
        }

        if (!Until(deadline, Shown))
        {
            throw Failure.Unconfirmed(Codes.ActionFailed, "the value did not change to the text set");
        }
    }

    /// <summary>
    /// What a secure field's character count answers: <c>(true, null)</c> for a field that counts none, which is taken at the write's
    /// word. Not ok for a read the app did not answer in time, or for a field gone since the write (a change handler that replaced
    /// it): neither is proof the field counts none, so the fill is not confirmed by it, as a plain field's is not.
    /// </summary>
    internal static (bool IsOk, int? Count) SecureCount(AxResult<AxValue> answer)
    {
        if (answer.IsOk)
        {
            return answer.Value is AxValue.NumberValue count ? (true, (int)count.Value) : (true, null);
        }

        return answer.Error is AxError.CannotComplete or AxError.InvalidElement ? (false, null) : (true, null);
    }

    /// <summary>
    /// Selects all the text of a field through accessibility. A select-all key (Cmd+A) sent to an app in the background goes to its
    /// menu bar, not the field, so the delete after it would remove one character.
    /// </summary>
    private static void SelectAll(Element element, Instant deadline)
    {
        if (!Settable(element, "AXSelectedTextRange", "select all"))
        {
            throw Failure.Refused(Codes.ActionNotSupported, "select all: the field's selection cannot be set");
        }

        // A secure field shows no value, but still counts its characters. A field with neither cannot be shown to be selected whole:
        // a range of 0 would read as done and leave its text in place.
        int length;
        var counted = element.Attribute("AXNumberOfCharacters");
        if (counted.IsOk && counted.Value is AxValue.NumberValue count && count.Value >= 0.0)
        {
            length = (int)count.Value;
        }
        else if (!counted.IsOk && counted.Error == AxError.CannotComplete)
        {
            throw Unread(AxError.CannotComplete, "select all");
        }
        else if (ValueText(element) is { } valueText)
        {
            length = valueText.Length;
        }
        else
        {
            throw Failure.Refused(Codes.ActionNotSupported, "select all: the field's length cannot be read");
        }

        Go("select all");
        var written = element.SetRange("AXSelectedTextRange", 0, length);
        if (written != AxError.Success)
        {
            throw WriteFailed(written, "select all");
        }

        if (
            !Until(deadline, () =>
                element.Attribute("AXSelectedTextRange") is { IsOk: true, Value: AxValue.RangeValue { Start: 0 } range }
                && range.Length == length
            )
        )
        {
            throw Failure.Unconfirmed(Codes.ActionFailed, "select all: the field's text did not become selected");
        }
    }
}
