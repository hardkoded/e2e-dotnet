// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>Where an event goes.</summary>
internal readonly record struct Route(int? Pid)
{
    /// <summary>Through the HID system, as physical input.</summary>
    public static Route Physical => new(null);

    /// <summary>Into one process's event queue.</summary>
    public static Route Process(int pid) => new(pid);
}

/// <summary>What a pointer does at a point: move there, or click.</summary>
internal readonly record struct PointerKind(bool IsClick, bool Right, long Count)
{
    public static PointerKind Move => new(false, false, 0);

    public static PointerKind Click(bool right, long count) => new(true, right, count);
}

/// <summary>
/// Keyboard and pointer events. A key or text either goes to one process (<c>CGEventPostToPid</c>, which its key window receives while
/// the app stays in the background) or, with physical input, through the HID system like a real keyboard. Pointer events are always
/// physical: a click carries only a point on screen.
/// </summary>
internal static unsafe class Input
{
    /// <summary>How far behind the newest run a leftover cancel mark is dropped.</summary>
    private const uint StaleIds = 4096;

    private const ulong FlagShift = 0x0002_0000;
    private const ulong FlagControl = 0x0004_0000;
    private const ulong FlagAlternate = 0x0008_0000;
    private const ulong FlagCommand = 0x0010_0000;

    private const ulong ShortcutFlags = FlagCommand | FlagControl | FlagAlternate;

    /// <summary><c>kCGEventSourceStateHIDSystemState</c>.</summary>
    private const int HidSystemState = 1;

    /// <summary><c>kCGHIDEventTap</c>.</summary>
    private const uint HidEventTap = 0;

    private const uint MouseMoved = 5;
    private const uint LeftMouseDown = 1;
    private const uint LeftMouseUp = 2;
    private const uint RightMouseDown = 3;
    private const uint RightMouseUp = 4;
    private const uint ButtonLeft = 0;
    private const uint ButtonRight = 1;

    /// <summary><c>kCGMouseEventClickState</c>.</summary>
    private const uint MouseEventClickState = 1;

    /// <summary>The typing runs by the id their caller gave (from <see cref="NextInputId"/>), each marked once it is cancelled.</summary>
    private static readonly Lock s_typingLock = new();

    private static readonly Dictionary<uint, bool> s_typing = [];

    /// <summary>The last input id handed out: one counter for the process, shared by every worker.</summary>
    private static int s_lastId;

    /// <summary>A fresh input id, never 0, wrapping past <see cref="uint.MaxValue"/>.</summary>
    public static uint NextInputId()
    {
        while (true)
        {
            var id = unchecked((uint)Interlocked.Increment(ref s_lastId));
            if (id != 0)
            {
                return id;
            }
        }
    }

    /// <summary>Marks the input run <paramref name="id"/> cancelled: it sends nothing more. A cancel that comes before the run starts holds too.</summary>
    public static void CancelInput(uint id)
    {
        lock (s_typingLock)
        {
            s_typing[id] = true;
        }
    }

    /// <summary>One input run (an action, or the text it types), registered while it lives so <see cref="CancelInput"/> can reach it.</summary>
    public sealed class TypingRun : IDisposable
    {
        private readonly uint? _id;

        private TypingRun(uint? id)
        {
            _id = id;
        }

        public static TypingRun Start(uint? id)
        {
            if (id is { } value)
            {
                lock (s_typingLock)
                {
                    // A cancel that came after its run ended left a mark; ids only grow, so marks far behind this one go. A newer
                    // id stays whatever order the runs start in: its cancel may have come before it started.
                    foreach (
                        var other in s_typing
                            .Keys.Where(other => !(other >= value || value - other < StaleIds))
                            .ToList()
                    )
                    {
                        s_typing.Remove(other);
                    }

                    s_typing.TryAdd(value, false);
                }
            }

            return new TypingRun(id);
        }

        public bool Cancelled()
        {
            if (_id is not { } id)
            {
                return false;
            }

            lock (s_typingLock)
            {
                return s_typing.TryGetValue(id, out var cancelled) && cancelled;
            }
        }

        public void Dispose()
        {
            if (_id is { } id)
            {
                lock (s_typingLock)
                {
                    s_typing.Remove(id);
                }
            }
        }
    }

    /// <summary>One key of a combo, its virtual key code (ANSI layout) and the character it types, if any.</summary>
    internal readonly record struct Key(ushort Code, Rune? Character);

    /// <summary>A parsed combo: its key and modifier flags.</summary>
    internal readonly record struct Combo(Key Key, ulong Flags)
    {
        public bool HasShift => (Flags & FlagShift) != 0;
    }

    private static ushort? NamedKey(string name) =>
        name switch
        {
            "return" or "enter" => 36,
            "tab" => 48,
            "space" => 49,
            "backspace" or "delete" => 51,
            "escape" or "esc" => 53,
            "forwarddelete" => 117,
            "home" => 115,
            "end" => 119,
            "pageup" => 116,
            "pagedown" => 121,
            "left" => 123,
            "right" => 124,
            "down" => 125,
            "up" => 126,
            "f1" => 122,
            "f2" => 120,
            "f3" => 99,
            "f4" => 118,
            "f5" => 96,
            "f6" => 97,
            "f7" => 98,
            "f8" => 100,
            "f9" => 101,
            "f10" => 109,
            "f11" => 103,
            "f12" => 111,
            _ => null,
        };

    private static ushort? CharacterKey(Rune character) =>
        character.Value switch
        {
            'a' => 0,
            's' => 1,
            'd' => 2,
            'f' => 3,
            'h' => 4,
            'g' => 5,
            'z' => 6,
            'x' => 7,
            'c' => 8,
            'v' => 9,
            'b' => 11,
            'q' => 12,
            'w' => 13,
            'e' => 14,
            'r' => 15,
            'y' => 16,
            't' => 17,
            '1' => 18,
            '2' => 19,
            '3' => 20,
            '4' => 21,
            '6' => 22,
            '5' => 23,
            '=' => 24,
            '9' => 25,
            '7' => 26,
            '-' => 27,
            '8' => 28,
            '0' => 29,
            ']' => 30,
            'o' => 31,
            'u' => 32,
            '[' => 33,
            'i' => 34,
            'p' => 35,
            'l' => 37,
            'j' => 38,
            '\'' => 39,
            'k' => 40,
            ';' => 41,
            '\\' => 42,
            ',' => 43,
            '/' => 44,
            'n' => 45,
            'm' => 46,
            '.' => 47,
            '`' => 50,
            _ => null,
        };

    /// <summary>What a key types with Shift held, on the US layout the key codes follow.</summary>
    private static Rune? Shifted(Rune character)
    {
        if (character.Value is >= 'a' and <= 'z')
        {
            return new Rune(character.Value - 'a' + 'A');
        }

        return character.Value switch
        {
            '1' => new Rune('!'),
            '2' => new Rune('@'),
            '3' => new Rune('#'),
            '4' => new Rune('$'),
            '5' => new Rune('%'),
            '6' => new Rune('^'),
            '7' => new Rune('&'),
            '8' => new Rune('*'),
            '9' => new Rune('('),
            '0' => new Rune(')'),
            '-' => new Rune('_'),
            '=' => new Rune('+'),
            '[' => new Rune('{'),
            ']' => new Rune('}'),
            '\\' => new Rune('|'),
            ';' => new Rune(':'),
            '\'' => new Rune('"'),
            ',' => new Rune('<'),
            '.' => new Rune('>'),
            '/' => new Rune('?'),
            '`' => new Rune('~'),
            _ => null,
        };
    }

    /// <summary>Lowercases an ASCII letter, as <c>to_ascii_lowercase</c> does, and leaves any other character alone.</summary>
    private static Rune AsciiLower(Rune character) =>
        character.Value is >= 'A' and <= 'Z' ? new Rune(character.Value - 'A' + 'a') : character;

    private static string AsciiLower(string text) => string.Create(text.Length, text, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                span[index] = source[index] is >= 'A' and <= 'Z' ? (char)(source[index] - 'A' + 'a') : source[index];
            }
        });

    /// <summary>Parses <c>cmd+shift+a</c>: modifiers, then one key. A lone capital letter adds Shift.</summary>
    public static Combo ParseCombo(string combo)
    {
        var parts = combo.Split('+');
        var last = parts[^1];
        ulong flags = 0;
        foreach (var modifier in parts[..^1])
        {
            flags |= AsciiLower(modifier) switch
            {
                "cmd" or "command" or "meta" => FlagCommand,
                "shift" => FlagShift,
                "ctrl" or "control" => FlagControl,
                "alt" or "option" => FlagAlternate,
                var other => throw Failure.Refused(Codes.InvalidArgs, $"\"{other}\" is not a modifier"),
            };
        }

        var characters = last.EnumerateRunes().Take(2).ToList();
        Key key;
        if (characters.Count == 1)
        {
            var character = characters[0];
            if (character.Value is >= 'A' and <= 'Z')
            {
                flags |= FlagShift;
            }

            var lower = AsciiLower(character);

            // Checked on the character, before any key code: on another layout the US key for q types something else.
            if (lower.Value == 'q' && (flags & FlagCommand) != 0)
            {
                throw Failure.Refused(Codes.PolicyDenied, "Command-Q quits the app; end it with closeApp");
            }

            var us = CharacterKey(lower)
                ?? throw Failure.Refused(Codes.ActionNotSupported, $"\"{character}\" has no key of its own");

            // A shortcut carries only its key code, and macOS reads its character from the layout: the code must be the key that
            // types the character on this Mac's layout, in its Command layer for a Command shortcut. Text and Shift keys carry
            // their characters, as typed.
            var shortcut = (flags & ShortcutFlags) != 0;
            ushort code;
            if (shortcut)
            {
                var layer = (flags & FlagCommand) != 0 ? Layer.Command : Layer.Plain;
                var table = Layout.Characters(layer)
                    ?? throw Failure.Refused(
                        Codes.ActionFailed,
                        "the keyboard layout could not be read, so the shortcut's key is unknown"
                    );
                code = Layout.CodeFor(lower, table)
                    ?? throw Failure.Refused(
                        Codes.ActionNotSupported,
                        $"no key types \"{lower}\" without Shift on this Mac's keyboard layout (on AZERTY, the digits need Shift)"
                    );
            }
            else
            {
                code = us;
            }

            // With Shift the key types its shifted character; one with none on the US layout is left to the app to work out.
            var typed = (flags & FlagShift) != 0 ? Shifted(lower) : lower;
            key = new Key(code, typed);
        }
        else
        {
            var name = AsciiLower(last);
            var code = NamedKey(name) ?? throw Failure.Refused(Codes.ActionNotSupported, $"\"{last}\" is not a key");
            key = new Key(code, name == "space" ? new Rune(' ') : null);
        }

        return new Combo(key, flags);
    }

    /// <summary>An event source over the HID system's state, released when disposed.</summary>
    private sealed class EventSource : IDisposable
    {
        private readonly nint _source;

        public EventSource()
        {
            _source = Native.CGEventSourceCreate(HidSystemState);
            if (_source == 0)
            {
                throw Failure.Refused(Codes.Internal, "could not create an event source");
            }
        }

        public nint Handle => _source;

        public void Dispose() => Native.CFRelease(_source);
    }

    /// <summary>A <c>CGEvent</c>, released when disposed.</summary>
    private sealed class CgEvent(nint handle) : IDisposable
    {
        public nint Handle { get; } = handle;

        public void SetFlags(ulong flags) => Native.CGEventSetFlags(Handle, flags);

        public void SetString(ReadOnlySpan<char> units)
        {
            fixed (char* chars = units)
            {
                Native.CGEventKeyboardSetUnicodeString(Handle, (nuint)units.Length, chars);
            }
        }

        public void Post(Route route)
        {
            if (route.Pid is { } pid)
            {
                Native.CGEventPostToPid(pid, Handle);
            }
            else
            {
                Native.CGEventPost(HidEventTap, Handle);
            }
        }

        public void Dispose() => Native.CFRelease(Handle);
    }

    private static CgEvent? Keyboard(EventSource source, ushort code, bool down)
    {
        var handle = Native.CGEventCreateKeyboardEvent(source.Handle, code, down ? (byte)1 : (byte)0);
        return handle == 0 ? null : new CgEvent(handle);
    }

    /// <summary>Presses and releases one combo.</summary>
    public static void Press(Combo combo, Route route)
    {
        using var source = new EventSource();
        var plain = (combo.Flags & ShortcutFlags) == 0;
        foreach (var down in new[] { true, false })
        {
            using var created = Keyboard(source, combo.Key.Code, down)
                ?? throw Failure.Refused(Codes.Internal, "could not create a key event");
            created.SetFlags(combo.Flags);
            if (plain && combo.Key.Character is { } character)
            {
                created.SetString(character.ToString());
            }

            created.Post(route);
            Thread.Sleep(4);
        }
    }

    /// <summary>One key event's worth of text to type: one character (a surrogate pair stays whole), or Return or Tab.</summary>
    internal abstract record Piece
    {
        public sealed record Text(string Units) : Piece;

        public sealed record Return : Piece;

        public sealed record Tab : Piece;
    }

    /// <summary>
    /// Cuts text into key events, one per character as a keyboard sends them: a handler that reads each key (a field that checks every
    /// digit, or moves focus after one) sees every character. A surrogate pair is one character, so it is never split; a line break is
    /// Return (<c>\r\n</c> is one) and a tab is Tab.
    /// </summary>
    internal static List<Piece> Pieces(string text)
    {
        var output = new List<Piece>();
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            switch (character)
            {
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    output.Add(new Piece.Return());
                    break;
                case '\n':
                    output.Add(new Piece.Return());
                    break;
                case '\t':
                    output.Add(new Piece.Tab());
                    break;
                default:
                    if (
                        char.IsHighSurrogate(character)
                        && index + 1 < text.Length
                        && char.IsLowSurrogate(text[index + 1])
                    )
                    {
                        output.Add(new Piece.Text(text.Substring(index, 2)));
                        index++;
                    }
                    else
                    {
                        output.Add(new Piece.Text(character.ToString()));
                    }

                    break;
            }
        }

        return output;
    }

    /// <summary>
    /// Types text as keystrokes that carry their characters, whatever the keyboard layout (<see cref="Pieces"/>). Before each event it
    /// checks <paramref name="run"/> and <paramref name="deadline"/>: a cancelled run, or one out of time, sends nothing more, so keys
    /// never keep going after the caller gave up, when another app may have taken the keyboard. <paramref name="still"/> is asked before
    /// each key too: a Return can open another window or app, and the keys after it must not follow the keyboard there.
    /// </summary>
    public static void TypeText(string text, Route route, TypingRun run, Instant deadline, Action still)
    {
        using var source = new EventSource();

        // The key that types each character on this Mac's layout, and Shift when it takes Shift, so a control that reads the key
        // code sees the key a person would press; a character no key types keeps code 0, and its text says what it is.
        var plain = Layout.Characters(Layer.Plain) ?? [];
        var shifted = Layout.Characters(Layer.Shift) ?? [];

        // A stop before the first key sent nothing; after it, the keys sent stay sent.
        var sent = false;
        Failure Stopped(Failure failure, bool wasSent) =>
            wasSent
                ? failure.WithMessage($"{failure.Message}; the text sent before then stays sent").Delivered()
                : failure;

        foreach (var piece in Pieces(text))
        {
            // The focus check reads the app and can take a while: the cancel and the deadline are checked after it, right before
            // the key goes.
            try
            {
                still();
            }
            catch (Failure failure)
            {
                throw Stopped(failure, sent);
            }

            if (run.Cancelled())
            {
                throw Stopped(Failure.Refused(Codes.Timeout, "typing was cancelled"), sent);
            }

            if (Instant.Now >= deadline)
            {
                throw Stopped(Failure.Refused(Codes.Timeout, "typing ran out of time"), sent);
            }

            if (piece is Piece.Return or Piece.Tab)
            {
                var code = piece is Piece.Tab ? (ushort)48 : (ushort)36;
                try
                {
                    Press(new Combo(new Key(code, null), 0), route);
                }
                catch (Failure failure)
                {
                    throw Stopped(failure, sent);
                }

                sent = true;
                continue;
            }

            var units = ((Piece.Text)piece).Units;
            sent = true;
            var (keyCode, shift) = KeyCode(units, plain, shifted);
            foreach (var down in new[] { true, false })
            {
                using var created = Keyboard(source, keyCode, down)
                    ?? throw Failure.Unconfirmed(Codes.Internal, "could not create a key event");

                // Only the Shift the character takes: a key the user holds would otherwise ride along on an HID-state event and
                // turn text into a shortcut.
                created.SetFlags(shift ? FlagShift : 0);
                created.SetString(units);
                created.Post(route);
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// The key a typed character takes on a layout, and whether it takes Shift: the key that types it with no modifier, else the one
    /// that types it with Shift, else code 0 with no Shift.
    /// </summary>
    internal static (ushort Code, bool Shift) KeyCode(
        string units,
        IReadOnlyList<(ushort Code, Rune Character)> plain,
        IReadOnlyList<(ushort Code, Rune Character)> shifted
    )
    {
        var runes = units.EnumerateRunes().Take(2).ToList();
        if (runes.Count != 1)
        {
            return ((ushort)0, false);
        }

        if (Layout.CodeFor(runes[0], plain) is { } code)
        {
            return (code, false);
        }

        return Layout.CodeFor(runes[0], shifted) is { } shiftedCode ? (shiftedCode, true) : ((ushort)0, false);
    }

    /// <summary>
    /// Moves the real pointer to a point on screen, and clicks there. <paramref name="proceed"/> is asked before the move (with 0) and
    /// before each click (with its number, from 1), so nothing more is sent once the caller gave up; a failure after the move may have
    /// reached the app.
    /// </summary>
    public static void Pointer(double x, double y, PointerKind action, Action<long> proceed)
    {
        using var source = new EventSource();
        var point = new CGPoint(x, y);

        CgEvent Make(uint kind, uint button)
        {
            var handle = Native.CGEventCreateMouseEvent(source.Handle, kind, point, button);
            return handle == 0
                ? throw Failure.Refused(Codes.Internal, "could not create a pointer event")
                : new CgEvent(handle);
        }

        using (var moved = Make(MouseMoved, ButtonLeft))
        {
            proceed(0);
            moved.Post(Route.Physical);
        }

        Thread.Sleep(30);
        if (!action.IsClick)
        {
            return;
        }

        var (down, up, button) = action.Right
            ? (RightMouseDown, RightMouseUp, ButtonRight)
            : (LeftMouseDown, LeftMouseUp, ButtonLeft);
        for (long click = 1; click <= action.Count; click++)
        {
            CgEvent? pressed = null;
            CgEvent? released = null;
            try
            {
                var pressedHandle = Native.CGEventCreateMouseEvent(source.Handle, down, point, button);
                var releasedHandle = Native.CGEventCreateMouseEvent(source.Handle, up, point, button);
                pressed = pressedHandle == 0 ? null : new CgEvent(pressedHandle);
                released = releasedHandle == 0 ? null : new CgEvent(releasedHandle);
                if (pressed is null || released is null)
                {
                    throw Failure.Unconfirmed(Codes.Internal, "could not create a pointer event");
                }

                // Each click checks the caller still wants it; a button pressed is always released.
                try
                {
                    proceed(click);
                }
                catch (Failure failure)
                {
                    throw failure.Delivered();
                }

                foreach (var created in new[] { pressed, released })
                {
                    Native.CGEventSetIntegerValueField(created.Handle, MouseEventClickState, click);
                    created.Post(Route.Physical);
                    Thread.Sleep(15);
                }
            }
            finally
            {
                pressed?.Dispose();
                released?.Dispose();
            }
        }
    }
}
