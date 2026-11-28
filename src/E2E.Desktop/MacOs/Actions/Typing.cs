// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>Typing a text key by key into a field, each key only while the keyboard is still where typing began.</summary>
internal static partial class Actions
{
    /// <summary>How often the wait for a typed key reads the field.</summary>
    internal static readonly TimeSpan KeyPoll = TimeSpan.FromMilliseconds(2);

    /// <summary>The longest wait for the app to take a typed key before the next one goes.</summary>
    internal static readonly TimeSpan KeySettle = TimeSpan.FromMilliseconds(250);

    /// <summary>The wait after a typed key when the field's length cannot be read, so its effect cannot be seen.</summary>
    internal static readonly TimeSpan KeySettleUnread = TimeSpan.FromMilliseconds(30);

    private static void TypeText(Entry entry, string text, bool physical, Instant deadline)
    {
        var window = Windows.Find(entry.Pid, entry.Window);

        // Text sent to the app goes to its key window, which must be the one the field is in, as for `press`.
        if (!physical)
        {
            KeyWindow(entry.Pid, window);
        }

        // The app owns focus: typing goes into the field that holds it, which the engine read, and never moves focus back to it (a
        // delete's handler, or a window coming forward, may have moved it on).
        Route route;
        if (physical)
        {
            Raise(entry.Pid, window);
            route = Route.Physical;
        }
        else
        {
            route = Route.Process(entry.Pid);
        }

        // Each key goes only while the window that held the keyboard when typing began still does: a Return that opened another
        // window, a sheet on this one included, or another app must not take the rest of the text with it. A field in a sheet that
        // was there from the start keeps its sheet. Read before the field's own check, so a window a focus handler opens after it
        // shows as a change at the first key, and one it opened before fails that check.
        var typingIn = ElementAttribute(Element.Application(entry.Pid), "AXFocusedWindow", "type");
        if (typingIn is not null && !Holds(typingIn, window, "type"))
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "another window took the keyboard before typing began, so nothing was typed"
            );
        }

        // Coming forward can run a handler that opens a sheet or another window, and the app can move focus on: the keys would then
        // go there. The field itself must hold the app's focus before the first key.
        if (
            !Element.Same(ElementAttribute(Element.Application(entry.Pid), "AXFocusedUIElement", "type"), entry.Element)
        )
        {
            throw Failure.Refused(
                Codes.ActionFailed,
                "the field lost keyboard focus before typing began, so nothing was typed"
            );
        }

        // What an element holds, in characters, as the app answers it: a change says the app took the key sent last.
        static double? Length(Element element)
        {
            var read = element.Attribute("AXNumberOfCharacters");
            return read is { IsOk: true, Value: AxValue.NumberValue count } ? count.Value : null;
        }

        Element? FocusedElement() => Element.Application(entry.Pid).ElementAttribute("AXFocusedUIElement");

        // The same read for a check a key waits on: a stall fails it, as a retry may get past it, never as focus that moved.
        Element? FocusedChecked() => ElementAttribute(Element.Application(entry.Pid), "AXFocusedUIElement", "type");

        // The element that held the keyboard when the last key went, and its length then; unset before the first key. It is the
        // field typing began in until a Tab, or a code field's next box, takes the keys within the window.
        (Element? Target, double? Before)? sentTo = null;

        // Set once a key showed no change in its whole wait: a field that drops or caps characters would spend the wait on every
        // key after it, so the rest get the short one.
        var quiet = false;
        void Still()
        {
            // A key posted to the app is in its queue, not yet handled: wait until the element it went to shows it, or focus moved,
            // so a window the key opened (a handler on the text, a Return) holds the keyboard by the time the check below reads it.
            var last = sentTo;
            if (last is { } sent)
            {
                var settle = Instant.Min(deadline, Instant.After(quiet ? KeySettleUnread : KeySettle));
                if (sent.Target is { } sentElement && sent.Before is { } count)
                {
                    // A fine poll of its own: the app takes a key within a millisecond or two, and `until`'s coarse one would add
                    // its whole interval to every key.
                    bool Taken() => Length(sentElement) != count || !Element.Same(FocusedElement(), sentElement);
                    var seen = Taken();
                    while (!seen && Instant.Now < settle)
                    {
                        Thread.Sleep(KeyPoll);
                        seen = Taken();
                    }

                    if (!seen)
                    {
                        quiet = true;
                    }
                }
                else
                {
                    var remaining = settle.Remaining;
                    Thread.Sleep(KeySettleUnread < remaining ? KeySettleUnread : remaining);
                }
            }

            // The element the key will reach, read before the checks below, so nothing reads the app between them and the key: focus
            // that moves while they run is caught by them, not followed. The first key goes only to the field. Past the first key, an
            // unanswered read only shortens the wait for the next key's effect.
            var target = last is null ? FocusedChecked() : FocusedElement();
            var held = target is null ? (double?)null : Length(target);
            if (last is null && !Element.Same(target, entry.Element))
            {
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "the field lost keyboard focus before typing began, so nothing was typed"
                );
            }

            var focused = ElementAttribute(Element.Application(entry.Pid), "AXFocusedWindow", "type");
            bool holding;
            if (typingIn is not null)
            {
                holding = Element.Same(focused, typingIn);
            }
            else
            {
                holding = focused is not null && Holds(focused, window, "type");
            }

            var forward = !physical || Frontmost(entry.Pid);
            if (holding && forward)
            {
                if (last is null)
                {
                    // The first key goes only to the field, read once more: the reads above wait on the app, and a handler that moved
                    // focus to another field of the window meanwhile would take the key.
                    if (!Element.Same(FocusedChecked(), entry.Element))
                    {
                        throw Failure.Refused(
                            Codes.ActionFailed,
                            "the field lost keyboard focus before typing began, so nothing was typed"
                        );
                    }

                    // That read waits on the app too, and a physical key goes to whatever app is frontmost: the frontmost check is
                    // the last read before it, as for `press`, so another app that came forward meanwhile is still caught.
                    if (physical && !Frontmost(entry.Pid))
                    {
                        throw Failure.Refused(
                            Codes.ActionFailed,
                            "another app came forward before typing began, so nothing was typed"
                        );
                    }
                }

                sentTo = (target, held);
            }
            else
            {
                throw Failure.Refused(
                    Codes.ActionFailed,
                    "typing stopped: the field's window lost the keyboard to another window or app"
                );
            }
        }

        var current = t_current ?? throw Failure.Refused(Codes.Internal, "type ran outside an action");
        Input.TypeText(text, route, current.Run, deadline, Still);
    }
}
