// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>What a <see cref="LaunchArgumentValue"/> holds.</summary>
public enum LaunchArgumentValueKind
{
    /// <summary>A string.</summary>
    String,

    /// <summary>A finite number, an integer below 2^53 in magnitude or a fraction.</summary>
    Number,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A list of strings.</summary>
    List,
}

/// <summary>
/// One value in the object form of <c>app.launchArguments</c> (upstream's <c>LaunchArgumentValue</c>, <c>string | number | boolean | string[]</c>). On iOS
/// a value is spelled as <c>NSUserDefaults</c> reads it: <c>YES</c> or <c>NO</c> for a boolean, a plist array for a list. On Android each value
/// is the <c>am start</c> extra of its type (<c>--ez</c> boolean, <c>--ei</c> or <c>--el</c> integer, <c>--ef</c> float, <c>--es</c> string, <c>--esa</c> list).
/// .NET-only holder: C# has no union type, so the four forms are one struct with an implicit conversion from each.
/// </summary>
public readonly struct LaunchArgumentValue : IEquatable<LaunchArgumentValue>
{
    private readonly string? _text;
    private readonly double _number;
    private readonly bool _flag;
    private readonly IReadOnlyList<string>? _items;

    private LaunchArgumentValue(
        LaunchArgumentValueKind kind,
        string? text,
        double number,
        bool flag,
        IReadOnlyList<string>? items
    )
    {
        Kind = kind;
        _text = text;
        _number = number;
        _flag = flag;
        _items = items;
    }

    /// <summary>Which form this value is.</summary>
    public LaunchArgumentValueKind Kind { get; }

    /// <summary>The string, when <see cref="Kind"/> is <see cref="LaunchArgumentValueKind.String"/>.</summary>
    public string? Text => Kind == LaunchArgumentValueKind.String ? _text : null;

    /// <summary>The number, when <see cref="Kind"/> is <see cref="LaunchArgumentValueKind.Number"/>.</summary>
    public double? Number => Kind == LaunchArgumentValueKind.Number ? _number : null;

    /// <summary>The boolean, when <see cref="Kind"/> is <see cref="LaunchArgumentValueKind.Boolean"/>.</summary>
    public bool? Flag => Kind == LaunchArgumentValueKind.Boolean ? _flag : null;

    /// <summary>The items, when <see cref="Kind"/> is <see cref="LaunchArgumentValueKind.List"/>.</summary>
    public IReadOnlyList<string>? Items => Kind == LaunchArgumentValueKind.List ? _items : null;

    /// <summary>A string value.</summary>
    public static implicit operator LaunchArgumentValue(string value) =>
        new(LaunchArgumentValueKind.String, value ?? throw new ArgumentNullException(nameof(value)), 0, false, null);

    /// <summary>A boolean value.</summary>
    public static implicit operator LaunchArgumentValue(bool value) =>
        new(LaunchArgumentValueKind.Boolean, null, 0, value, null);

    /// <summary>A number value.</summary>
    public static implicit operator LaunchArgumentValue(int value) =>
        new(LaunchArgumentValueKind.Number, null, value, false, null);

    /// <summary>A number value.</summary>
    public static implicit operator LaunchArgumentValue(long value) =>
        new(LaunchArgumentValueKind.Number, null, value, false, null);

    /// <summary>A number value.</summary>
    public static implicit operator LaunchArgumentValue(double value) =>
        new(LaunchArgumentValueKind.Number, null, value, false, null);

    /// <summary>A list value.</summary>
    public static implicit operator LaunchArgumentValue(string[] value) =>
        new(LaunchArgumentValueKind.List, null, 0, false, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>A list value.</summary>
    public static implicit operator LaunchArgumentValue(List<string> value) =>
        new(LaunchArgumentValueKind.List, null, 0, false, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>A list value from any read-only list.</summary>
    public static LaunchArgumentValue FromList(IReadOnlyList<string> items) =>
        new(LaunchArgumentValueKind.List, null, 0, false, items ?? throw new ArgumentNullException(nameof(items)));

    /// <inheritdoc />
    public static bool operator ==(LaunchArgumentValue left, LaunchArgumentValue right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(LaunchArgumentValue left, LaunchArgumentValue right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(LaunchArgumentValue other) =>
        Kind == other.Kind
        && Kind switch
        {
            LaunchArgumentValueKind.String => string.Equals(_text, other._text, StringComparison.Ordinal),
            LaunchArgumentValueKind.Number => _number.Equals(other._number),
            LaunchArgumentValueKind.Boolean => _flag == other._flag,
            _ => _items!.SequenceEqual(other._items!, StringComparer.Ordinal),
        };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LaunchArgumentValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Kind switch
        {
            LaunchArgumentValueKind.String => HashCode.Combine(Kind, _text),
            LaunchArgumentValueKind.Number => HashCode.Combine(Kind, _number),
            LaunchArgumentValueKind.Boolean => HashCode.Combine(Kind, _flag),
            _ => _items!.Aggregate(HashCode.Combine(Kind), (hash, item) => HashCode.Combine(hash, item)),
        };

    /// <summary>The value as JavaScript's <c>String(value)</c> spells it: the text, <c>true</c> or <c>false</c>, the number, or the items joined by commas.</summary>
    public override string ToString() =>
        Kind switch
        {
            LaunchArgumentValueKind.String => _text!,
            LaunchArgumentValueKind.Boolean => _flag ? "true" : "false",
            LaunchArgumentValueKind.Number => FormatNumber(_number),
            _ => string.Join(",", _items!),
        };

    /// <summary>A number as JavaScript's <c>String(number)</c> spells it: no exponent for what <c>double.ToString("R")</c> writes plainly, and <c>1e-7</c> for what it writes as <c>1E-07</c>.</summary>
    internal static string FormatNumber(double number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        var exponent = text.IndexOf('E', StringComparison.Ordinal);
        if (exponent < 0)
        {
            return text;
        }

        var sign = text[exponent + 1] == '-' ? "-" : "+";
        var digits = text[(exponent + 2)..].TrimStart('0');
        return text[..exponent] + "e" + sign + (digits.Length == 0 ? "0" : digits);
    }
}
