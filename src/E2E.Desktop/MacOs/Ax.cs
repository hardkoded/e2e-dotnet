// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Desktop.MacOs;

/// <summary>The accessibility API's error codes (<c>AXError</c>), and a readable name for each.</summary>
internal static class AxError
{
    public const int Success = 0;
    public const int Failure = -25200;
    public const int IllegalArgument = -25201;
    public const int InvalidElement = -25202;
    public const int CannotComplete = -25204;
    public const int AttributeUnsupported = -25205;
    public const int ActionUnsupported = -25206;
    public const int NotImplemented = -25208;
    public const int ApiDisabled = -25211;
    public const int NoValue = -25212;

    /// <summary>A readable name for an AX error, for messages.</summary>
    public static string Describe(int error) =>
        error switch
        {
            Failure => "the app's accessibility answered with a failure",
            IllegalArgument => "the app refused the value",
            InvalidElement => "the element no longer exists",
            CannotComplete => "the app did not answer in time",
            AttributeUnsupported => "the element has no such attribute",
            ActionUnsupported => "the element does not take that action",
            NotImplemented => "the app does not implement accessibility for it",
            ApiDisabled => "accessibility is not allowed for this process",
            NoValue => "the attribute has no value",
            _ => "the accessibility request failed",
        };
}

/// <summary>What a call to the accessibility API answered: a value, or the AX error that took its place. Rust's <c>Result&lt;T, AXError&gt;</c>.</summary>
internal readonly struct AxResult<T>
{
    private AxResult(T? value, int error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    /// <summary>The AX error, or <see cref="AxError.Success"/> when the call answered a value.</summary>
    public int Error { get; }

    public bool IsOk => Error == AxError.Success;

    public static AxResult<T> Ok(T value) => new(value, AxError.Success);

    public static AxResult<T> Err(int error) => new(default, error);

    /// <summary>The value when the call answered one.</summary>
    public bool TryGet(out T value)
    {
        value = Value!;
        return IsOk;
    }
}

/// <summary>One attribute's value, decoded. Rust's <c>ax::Value</c>.</summary>
internal abstract record AxValue
{
    /// <summary>The text, when this is one.</summary>
    public string? Text => (this as TextValue)?.Value;

    /// <summary>The boolean a flag shows: a boolean, or a number that is not zero.</summary>
    public bool? Boolean =>
        this switch
        {
            BoolValue value => value.Value,
            NumberValue value => value.Value != 0.0,
            _ => null,
        };

    public Element? Element => (this as ElementValue)?.Value;

    /// <summary>The value as the text a node shows: a string as it is, a number in its shortest form.</summary>
    public string? Display() =>
        this switch
        {
            TextValue value => value.Value,
            NumberValue value => AxFormat.Number(value.Value),
            BoolValue value => value.Value ? "1" : "0",
            _ => null,
        };

    public sealed record TextValue(string Value) : AxValue;

    public sealed record NumberValue(double Value) : AxValue;

    public sealed record BoolValue(bool Value) : AxValue;

    public sealed record ElementValue(Element Value) : AxValue;

    public sealed record ArrayValue(IReadOnlyList<AxValue> Items) : AxValue;

    public sealed record PointValue(double X, double Y) : AxValue;

    public sealed record SizeValue(double Width, double Height) : AxValue;

    /// <summary>A text range: its start and length, in UTF-16 units.</summary>
    public sealed record RangeValue(long Start, long Length) : AxValue;

    public sealed record OtherValue : AxValue;
}

/// <summary>How a number reads as the text a node shows.</summary>
internal static class AxFormat
{
    /// <summary>A number as a node shows it: <c>1</c>, not <c>1.0</c>, and never in an exponent form, as Rust prints a float.</summary>
    public static string Number(double number)
    {
        if (double.IsNaN(number))
        {
            return "NaN";
        }

        if (double.IsInfinity(number))
        {
            return number > 0 ? "inf" : "-inf";
        }

        if (Math.Abs(number - Math.Truncate(number)) == 0.0 && Math.Abs(number) < 1e15)
        {
            return ((long)number).ToString(CultureInfo.InvariantCulture);
        }

        return Shortest(number);
    }

    /// <summary>The shortest text that reads back as the same double, in plain decimal notation (Rust's <c>{}</c> for an <c>f64</c>).</summary>
    private static string Shortest(double number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = text.IndexOfAny(['E', 'e']);
        if (exponentAt < 0)
        {
            return text;
        }

        var mantissa = text[..exponentAt];
        var exponent = int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var negative = mantissa.StartsWith('-');
        if (negative)
        {
            mantissa = mantissa[1..];
        }

        var point = mantissa.IndexOf('.');
        var digits = point < 0 ? mantissa : mantissa.Remove(point, 1);
        var integerDigits = (point < 0 ? mantissa.Length : point) + exponent;
        string plain;
        if (integerDigits <= 0)
        {
            plain = "0." + new string('0', -integerDigits) + digits;
        }
        else if (integerDigits >= digits.Length)
        {
            plain = digits + new string('0', integerDigits - digits.Length);
        }
        else
        {
            plain = digits[..integerDigits] + "." + digits[integerDigits..];
        }

        return negative ? "-" + plain : plain;
    }
}

/// <summary>
/// An owned accessibility element: an <c>AXUIElementRef</c>, released when the last reference to this object is collected. An
/// <c>AXUIElementRef</c> is an immutable token naming an element in another process, and CoreFoundation's retain and release are
/// thread safe, so an element is shared across threads. Rust's <c>ax::Element</c>; where Rust clones, the C# port shares the object.
/// </summary>
internal sealed unsafe class Element : IEquatable<Element>
{
    private const uint ValueCgPoint = 1;
    private const uint ValueCgSize = 2;
    private const uint ValueCfRange = 4;
    private const uint ValueAxError = 5;

    private readonly nint _ref;

    /// <summary>Takes ownership of one retain of <paramref name="reference"/>.</summary>
    private Element(nint reference)
    {
        _ref = reference;
    }

    ~Element()
    {
        if (_ref != 0)
        {
            Native.CFRelease(_ref);
        }
    }

    /// <summary>Whether this process may use the accessibility API.</summary>
    public static bool Trusted() => Native.AXIsProcessTrusted() != 0;

    /// <summary>
    /// Sets how long every request to another app may take before it fails with <see cref="AxError.CannotComplete"/>. Set once for the
    /// process: a request to an app that hangs then ends in this time, and the reader's own deadline decides what to do next.
    /// </summary>
    public static void SetGlobalTimeout(float seconds)
    {
        var system = Native.AXUIElementCreateSystemWide();
        if (system != 0)
        {
            Native.AXUIElementSetMessagingTimeout(system, seconds);
            Native.CFRelease(system);
        }
    }

    /// <summary>The element for an app's process.</summary>
    public static Element Application(int pid) => new(Native.AXUIElementCreateApplication(pid));

    /// <summary>The system-wide element: what it finds at a point is whatever is on top there, of any app.</summary>
    public static Element SystemWide() => new(Native.AXUIElementCreateSystemWide());

    /// <summary>A key naming a sheet or window as a string the engine can hand back: its window server id, else <c>ax-</c> and the accessibility element's hash.</summary>
    public string SheetKey() =>
        WindowId() is { } id
            ? id.ToString(CultureInfo.InvariantCulture)
            : "ax-" + ((ulong)Native.CFHash(_ref)).ToString("x", CultureInfo.InvariantCulture);

    public bool Equals(Element? other) => other is not null && Native.CFEqual(_ref, other._ref) != 0;

    public override bool Equals(object? obj) => obj is Element other && Equals(other);

    public override int GetHashCode() => ((nuint)Native.CFHash(_ref)).GetHashCode();

    /// <summary>Equality of two optional elements.</summary>
    public static bool Same(Element? left, Element? right) => left is null ? right is null : left.Equals(right);

    /// <summary>The values of a batch read, from what each slot decoded to. A batch that succeeds can still carry an error in one slot: a gone element fails the whole read, and so does a stall, since a value the app did not answer in time is not a value it lacks. Any other error is an attribute with no value.</summary>
    internal static AxResult<AxValue?[]> Slots(IReadOnlyList<AxResult<AxValue>> decoded)
    {
        var slots = new AxValue?[decoded.Count];
        for (var index = 0; index < decoded.Count; index++)
        {
            var slot = decoded[index];
            if (slot.IsOk)
            {
                slots[index] = slot.Value;
            }
            else if (slot.Error is AxError.InvalidElement or AxError.CannotComplete)
            {
                return AxResult<AxValue?[]>.Err(slot.Error);
            }
        }

        return AxResult<AxValue?[]>.Ok(slots);
    }

    /// <summary>Decodes one CoreFoundation value. An error carries an AX error a multiple-attribute read put in the value's place.</summary>
    private static AxResult<AxValue> Decode(nint value)
    {
        if (value == 0)
        {
            return AxResult<AxValue>.Err(AxError.NoValue);
        }

        var kind = Native.CFGetTypeID(value);
        if (kind == Cf.StringType)
        {
            return AxResult<AxValue>.Ok(new AxValue.TextValue(Cf.ToManaged(value)));
        }

        if (kind == Cf.BooleanType)
        {
            return AxResult<AxValue>.Ok(new AxValue.BoolValue(Native.CFBooleanGetValue(value) != 0));
        }

        if (kind == Cf.NumberType)
        {
            return AxResult<AxValue>.Ok(
                Cf.NumberToDouble(value) is { } number ? new AxValue.NumberValue(number) : new AxValue.OtherValue()
            );
        }

        if (kind == Cf.AxElementType)
        {
            Native.CFRetain(value);
            return AxResult<AxValue>.Ok(new AxValue.ElementValue(new Element(value)));
        }

        if (kind == Cf.ArrayType)
        {
            var count = (int)Native.CFArrayGetCount(value);
            var items = new List<AxValue>(Math.Max(count, 0));
            for (var index = 0; index < count; index++)
            {
                var item = Decode(Native.CFArrayGetValueAtIndex(value, index));
                if (item.IsOk)
                {
                    items.Add(item.Value!);
                }
            }

            return AxResult<AxValue>.Ok(new AxValue.ArrayValue(items));
        }

        if (kind == Cf.AxValueType)
        {
            switch (Native.AXValueGetType(value))
            {
                case ValueCgPoint:
                {
                    var point = default(CGPoint);
                    Native.AXValueGetValue(value, ValueCgPoint, &point);
                    return AxResult<AxValue>.Ok(new AxValue.PointValue(point.X, point.Y));
                }

                case ValueCgSize:
                {
                    var size = default(CGSize);
                    Native.AXValueGetValue(value, ValueCgSize, &size);
                    return AxResult<AxValue>.Ok(new AxValue.SizeValue(size.Width, size.Height));
                }

                case ValueCfRange:
                {
                    var range = new CFRange(0, 0);
                    Native.AXValueGetValue(value, ValueCfRange, &range);
                    return AxResult<AxValue>.Ok(
                        new AxValue.RangeValue(Math.Max(range.Location, 0), Math.Max(range.Length, 0))
                    );
                }

                case ValueAxError:
                {
                    var error = 0;
                    Native.AXValueGetValue(value, ValueAxError, &error);
                    return AxResult<AxValue>.Err(error);
                }

                default:
                    return AxResult<AxValue>.Ok(new AxValue.OtherValue());
            }
        }

        return AxResult<AxValue>.Ok(new AxValue.OtherValue());
    }

    /// <summary>One attribute. The error is the AX error: <see cref="AxError.NoValue"/> or <see cref="AxError.AttributeUnsupported"/> when it has none.</summary>
    public AxResult<AxValue> Attribute(string name)
    {
        var key = Cf.CreateString(name);
        try
        {
            nint value = 0;
            var error = Native.AXUIElementCopyAttributeValue(_ref, key, &value);
            if (error != AxError.Success)
            {
                return AxResult<AxValue>.Err(error);
            }

            // Success with no value out: nothing to decode or release.
            if (value == 0)
            {
                return AxResult<AxValue>.Err(AxError.NoValue);
            }

            try
            {
                return Decode(value);
            }
            finally
            {
                Native.CFRelease(value);
            }
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    /// <summary>
    /// Several attributes in one request, each null when it has no value. An error when the whole request failed, the element is
    /// gone, or the app did not answer one of them in time.
    /// </summary>
    public AxResult<AxValue?[]> Attributes(IReadOnlyList<string> names)
    {
        var keys = new nint[names.Count];
        for (var index = 0; index < names.Count; index++)
        {
            keys[index] = Cf.CreateString(names[index]);
        }

        var array = Cf.CreateArray(keys);
        try
        {
            nint values = 0;
            var error = Native.AXUIElementCopyMultipleAttributeValues(_ref, array, 0, &values);
            if (error != AxError.Success)
            {
                return AxResult<AxValue?[]>.Err(error);
            }

            if (values == 0)
            {
                return AxResult<AxValue?[]>.Ok(new AxValue?[names.Count]);
            }

            try
            {
                var count = (int)Native.CFArrayGetCount(values);
                var decoded = new List<AxResult<AxValue>>(names.Count);
                for (var index = 0; index < names.Count; index++)
                {
                    decoded.Add(
                        index < count
                            ? Decode(Native.CFArrayGetValueAtIndex(values, index))
                            : AxResult<AxValue>.Err(AxError.NoValue)
                    );
                }

                return Slots(decoded);
            }
            finally
            {
                Native.CFRelease(values);
            }
        }
        finally
        {
            Native.CFRelease(array);
            foreach (var key in keys)
            {
                Native.CFRelease(key);
            }
        }
    }

    /// <summary>How many values an array attribute holds.</summary>
    public AxResult<int> Count(string name)
    {
        var key = Cf.CreateString(name);
        try
        {
            nint count = 0;
            var error = Native.AXUIElementGetAttributeValueCount(_ref, key, &count);
            return error != AxError.Success ? AxResult<int>.Err(error) : AxResult<int>.Ok((int)Math.Max(count, 0));
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    /// <summary>Up to <paramref name="max"/> elements of an array attribute, from the first.</summary>
    public AxResult<List<Element>> Elements(string name, int max)
    {
        var key = Cf.CreateString(name);
        try
        {
            nint values = 0;
            var error = Native.AXUIElementCopyAttributeValues(_ref, key, 0, max, &values);
            if (error != AxError.Success)
            {
                return AxResult<List<Element>>.Err(error);
            }

            if (values == 0)
            {
                return AxResult<List<Element>>.Ok([]);
            }

            try
            {
                var decoded = Decode(values);
                var elements = new List<Element>();
                if (decoded.IsOk && decoded.Value is AxValue.ArrayValue array)
                {
                    foreach (var item in array.Items)
                    {
                        if (item is AxValue.ElementValue element)
                        {
                            elements.Add(element.Value);
                        }
                    }
                }

                return AxResult<List<Element>>.Ok(elements);
            }
            finally
            {
                Native.CFRelease(values);
            }
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    public string? Text(string name) => Attribute(name) is { IsOk: true, Value: { } value } ? value.Text : null;

    public bool? Boolean(string name) => Attribute(name) is { IsOk: true, Value: { } value } ? value.Boolean : null;

    public Element? ElementAttribute(string name) =>
        Attribute(name) is { IsOk: true, Value: { } value } ? value.Element : null;

    /// <summary>The element's frame in screen points, top-left origin.</summary>
    public (double X, double Y, double Width, double Height)? Frame()
    {
        var read = ReadFrame();
        return read.IsOk ? read.Value : null;
    }

    /// <summary>
    /// <see cref="Frame"/>, failing with <see cref="AxError.CannotComplete"/> when the app did not answer in time, and with
    /// <see cref="AxError.InvalidElement"/> when the element is gone: neither is proof the element has no frame. Any other error reads as none.
    /// </summary>
    public AxResult<(double X, double Y, double Width, double Height)?> ReadFrame()
    {
        var read = Attributes(["AXPosition", "AXSize"]);
        if (!read.IsOk)
        {
            return read.Error is AxError.CannotComplete or AxError.InvalidElement
                ? AxResult<(double, double, double, double)?>.Err(read.Error)
                : AxResult<(double, double, double, double)?>.Ok(null);
        }

        var values = read.Value!;
        return AxResult<(double, double, double, double)?>.Ok(
            values[0] is AxValue.PointValue point && values[1] is AxValue.SizeValue size
                ? (point.X, point.Y, size.Width, size.Height)
                : null
        );
    }

    public AxResult<bool> Settable(string name)
    {
        var key = Cf.CreateString(name);
        try
        {
            byte settable = 0;
            var error = Native.AXUIElementIsAttributeSettable(_ref, key, &settable);
            return error != AxError.Success ? AxResult<bool>.Err(error) : AxResult<bool>.Ok(settable != 0);
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    public int SetText(string name, string value)
    {
        var text = Cf.CreateString(value);
        try
        {
            return Set(name, text);
        }
        finally
        {
            Native.CFRelease(text);
        }
    }

    public int SetBool(string name, bool value) => Set(name, Cf.Boolean(value));

    public int SetNumber(string name, double value)
    {
        var number = Cf.CreateNumber(value);
        try
        {
            return Set(name, number);
        }
        finally
        {
            Native.CFRelease(number);
        }
    }

    /// <summary>Sets a text range attribute, such as <c>AXSelectedTextRange</c>, in UTF-16 units.</summary>
    public int SetRange(string name, long location, long length)
    {
        var range = new CFRange((nint)location, (nint)length);
        var value = Native.AXValueCreate(ValueCfRange, &range);
        if (value == 0)
        {
            return AxError.Failure;
        }

        try
        {
            return Set(name, value);
        }
        finally
        {
            Native.CFRelease(value);
        }
    }

    public int SetElements(string name, IReadOnlyList<Element> values)
    {
        var references = new nint[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            references[index] = values[index]._ref;
        }

        var array = Cf.CreateArray(references);
        try
        {
            return Set(name, array);
        }
        finally
        {
            Native.CFRelease(array);
            GC.KeepAlive(values);
        }
    }

    private int Set(string name, nint value)
    {
        var key = Cf.CreateString(name);
        try
        {
            return Native.AXUIElementSetAttributeValue(_ref, key, value);
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    public int Perform(string action)
    {
        var key = Cf.CreateString(action);
        try
        {
            return Native.AXUIElementPerformAction(_ref, key);
        }
        finally
        {
            Native.CFRelease(key);
        }
    }

    public AxResult<List<string>> Actions()
    {
        nint names = 0;
        var error = Native.AXUIElementCopyActionNames(_ref, &names);
        if (error != AxError.Success)
        {
            return AxResult<List<string>>.Err(error);
        }

        if (names == 0)
        {
            return AxResult<List<string>>.Ok([]);
        }

        try
        {
            var decoded = Decode(names);
            var texts = new List<string>();
            if (decoded.IsOk && decoded.Value is AxValue.ArrayValue array)
            {
                foreach (var item in array.Items)
                {
                    if (item is AxValue.TextValue text)
                    {
                        texts.Add(text.Value);
                    }
                }
            }

            return AxResult<List<string>>.Ok(texts);
        }
        finally
        {
            Native.CFRelease(names);
        }
    }

    /// <summary>Sets this element's own messaging timeout, for a request expected to wait (an action that opens a menu).</summary>
    public void SetTimeout(float seconds) => Native.AXUIElementSetMessagingTimeout(_ref, seconds);

    /// <summary>The id of the process the element belongs to.</summary>
    public int? Pid()
    {
        var pid = 0;
        return Native.AXUIElementGetPid(_ref, &pid) == AxError.Success ? pid : null;
    }

    /// <summary>The window server's id of a window element.</summary>
    public uint? WindowId()
    {
        uint id = 0;
        var error = Native._AXUIElementGetWindow(_ref, &id);
        return error == AxError.Success && id != 0 ? id : null;
    }

    /// <summary>
    /// The element at a point on screen, of this app, or of any app on the system-wide element: null when none is there, an error
    /// when the app did not answer.
    /// </summary>
    public AxResult<Element?> ElementAt(double x, double y)
    {
        nint element = 0;
        var error = Native.AXUIElementCopyElementAtPosition(_ref, (float)x, (float)y, &element);
        return error switch
        {
            AxError.Success when element != 0 => AxResult<Element?>.Ok(new Element(element)),
            AxError.Success or AxError.NoValue => AxResult<Element?>.Ok(null),
            _ => AxResult<Element?>.Err(error),
        };
    }
}
