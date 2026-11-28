// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;

namespace E2E.Desktop.MacOs;

/// <summary>
/// Small helpers over CoreFoundation: strings, numbers, booleans, arrays, and the data symbols the framework exports. Stands in
/// for the <c>core-foundation</c> crate the Rust driver uses.
/// </summary>
internal static unsafe class Cf
{
    /// <summary><c>kCFNumberSInt64Type</c>.</summary>
    public const int NumberSInt64 = 4;

    /// <summary><c>kCFNumberFloat64Type</c>.</summary>
    public const int NumberFloat64 = 6;

    private static readonly nint s_coreFoundation = Load(Native.CoreFoundationPath);
    private static readonly nint s_true = ReadExport(s_coreFoundation, "kCFBooleanTrue");
    private static readonly nint s_false = ReadExport(s_coreFoundation, "kCFBooleanFalse");
    private static readonly nint s_arrayCallBacks = NativeLibrary.GetExport(s_coreFoundation, "kCFTypeArrayCallBacks");

    public static nuint StringType { get; } = Native.CFStringGetTypeID();

    public static nuint BooleanType { get; } = Native.CFBooleanGetTypeID();

    public static nuint NumberType { get; } = Native.CFNumberGetTypeID();

    public static nuint ArrayType { get; } = Native.CFArrayGetTypeID();

    public static nuint AxElementType { get; } = Native.AXUIElementGetTypeID();

    public static nuint AxValueType { get; } = Native.AXValueGetTypeID();

    /// <summary>Loads a framework once, so its symbols and classes resolve.</summary>
    public static nint Load(string path) => NativeLibrary.Load(path);

    /// <summary>The value of an exported data symbol that holds an object reference, such as <c>kCFBooleanTrue</c>.</summary>
    public static nint ReadExport(nint library, string symbol) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));

    public static nint Boolean(bool value) => value ? s_true : s_false;

    /// <summary>A new CFString of exactly these UTF-16 units; the caller releases it.</summary>
    public static nint CreateString(string value)
    {
        fixed (char* chars = value)
        {
            return Native.CFStringCreateWithCharacters(0, chars, value.Length);
        }
    }

    /// <summary>The text of a CFString (or an NSString, which is toll-free bridged), in its UTF-16 units.</summary>
    public static string ToManaged(nint cfString)
    {
        var length = (int)Native.CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        fixed (char* chars = buffer)
        {
            Native.CFStringGetCharacters(cfString, new CFRange(0, length), chars);
        }

        return new string(buffer);
    }

    /// <summary>A new CFArray of retained values; the caller releases it.</summary>
    public static nint CreateArray(ReadOnlySpan<nint> values)
    {
        fixed (nint* items = values)
        {
            return Native.CFArrayCreate(0, items, values.Length, s_arrayCallBacks);
        }
    }

    /// <summary>A new CFNumber holding a 64-bit float; the caller releases it.</summary>
    public static nint CreateNumber(double value) => Native.CFNumberCreate(0, NumberFloat64, &value);

    /// <summary>The 64-bit float a CFNumber holds, or null when it cannot be read as one.</summary>
    public static double? NumberToDouble(nint number)
    {
        double value = 0;
        return Native.CFNumberGetValue(number, NumberFloat64, &value) != 0 ? value : null;
    }

    /// <summary>The 64-bit integer a CFNumber holds, or null when it cannot be read as one.</summary>
    public static long? NumberToInt64(nint number)
    {
        long value = 0;
        return Native.CFNumberGetValue(number, NumberSInt64, &value) != 0 ? value : null;
    }

    /// <summary>A UTF-8, NUL-terminated copy of a name, for the Objective-C runtime's lookups.</summary>
    public static byte[] Utf8Z(string value) => Encoding.UTF8.GetBytes(value + "\0");
}
