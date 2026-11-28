// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>A modifier layer of a layout: what its keys type with no modifier, with Shift, or with Command.</summary>
internal enum Layer
{
    Plain,
    Shift,
    Command,
}

/// <summary>
/// The keyboard layout a shortcut is typed on. A key combo with Command, Control, or Option carries only a key code, and macOS
/// reads its character from the layout, so the code for <c>ControlOrMeta+a</c> must be the one that types <c>a</c> on this Mac's
/// layout: on French AZERTY the key that types <c>a</c> on a US keyboard types <c>q</c>.
/// </summary>
internal static unsafe class Layout
{
    /// <summary><c>kUCKeyActionDown</c>.</summary>
    private const ushort KeyDown = 0;

    /// <summary><c>kUCKeyTranslateNoDeadKeysMask</c>: a dead key answers its own character, not nothing.</summary>
    private const uint NoDeadKeys = 1;

    /// <summary>Every virtual key code a keyboard has.</summary>
    private const ushort Codes = 128;

    // Text Input Sources aborts the process when two threads call it at once, and native calls run on a pool of threads:
    // one read at a time.
    private static readonly Lock s_layout = new();

    private static nint s_layoutDataKey;

    /// <summary>
    /// <c>kTISPropertyUnicodeKeyLayoutData</c>, the key of an input source's layout data: the exported variable when the framework
    /// gives it, else a CFString of the same text, which the constant is equal to.
    /// </summary>
    private static nint LayoutDataKey()
    {
        if (s_layoutDataKey != 0)
        {
            return s_layoutDataKey;
        }

        var carbon = Cf.Load(Native.CarbonPath);
        s_layoutDataKey = NativeLibrary.TryGetExport(carbon, "kTISPropertyUnicodeKeyLayoutData", out var address)
            ? Marshal.ReadIntPtr(address)
            : Cf.CreateString("TISPropertyUnicodeKeyLayoutData");
        return s_layoutDataKey;
    }

    /// <summary>The layer's bit in UCKeyTranslate's modifier state: <c>shiftKey >> 8</c> or <c>cmdKey >> 8</c>.</summary>
    private static uint State(Layer layer) =>
        layer switch
        {
            Layer.Plain => 0,
            Layer.Shift => 2,
            _ => 1,
        };

    /// <summary>
    /// What each key code types on the current ASCII-capable layout, the one macOS matches Command shortcuts against, in one
    /// modifier layer: a layout such as Dvorak-QWERTY Command types other characters with Command held. Null when the layout
    /// cannot be read. A character is a Unicode scalar, as Rust's <c>char</c>.
    /// </summary>
    public static List<(ushort Code, Rune Character)>? Characters(Layer layer)
    {
        lock (s_layout)
        {
            var source = Native.TISCopyCurrentASCIICapableKeyboardLayoutInputSource();
            if (source == 0)
            {
                return null;
            }

            try
            {
                var key = LayoutDataKey();
                var data = Native.TISGetInputSourceProperty(source, key);
                if (data == 0)
                {
                    return null;
                }

                var layout = Native.CFDataGetBytePtr(data);
                var keyboard = (uint)Native.LMGetKbdType();
                var table = new List<(ushort, Rune)>();
                var buffer = stackalloc ushort[4];
                for (ushort code = 0; code < Codes; code++)
                {
                    uint dead = 0;
                    nuint length = 0;
                    var status = Native.UCKeyTranslate(
                        layout,
                        code,
                        KeyDown,
                        State(layer),
                        keyboard,
                        NoDeadKeys,
                        &dead,
                        4,
                        &length,
                        buffer
                    );
                    if (status != 0 || length != 1)
                    {
                        continue;
                    }

                    // A lone surrogate is no character (Rust's char::from_u32 refuses one).
                    if (!char.IsSurrogate((char)buffer[0]))
                    {
                        table.Add((code, new Rune((char)buffer[0])));
                    }
                }

                return table;
            }
            finally
            {
                Native.CFRelease(source);
            }
        }
    }

    /// <summary>
    /// The key code that types <paramref name="character"/> in a layout's table, the lowest when several do. A keypad key is no
    /// match: an app can tell a keypad shortcut apart, and the main keys are what a shortcut names.
    /// </summary>
    public static ushort? CodeFor(Rune character, IEnumerable<(ushort Code, Rune Character)> table)
    {
        ushort? best = null;
        foreach (var (code, typed) in table)
        {
            if (typed.Value == character.Value && !Keypad(code) && (best is null || code < best))
            {
                best = code;
            }
        }

        return best;
    }

    /// <summary>Whether a virtual key code is on the numeric keypad (<c>kVK_ANSI_Keypad*</c>), which every layout maps the same way.</summary>
    private static bool Keypad(ushort code) =>
        code is 65 or 67 or 69 or 71 or 75 or 76 or 78 or (>= 81 and <= 89) or 91 or 92;
}
