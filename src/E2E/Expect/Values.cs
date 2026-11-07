// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Synchronous matchers over a plain value, from <see cref="Expect.That{T}(T, string?)"/>. A failure is an <c>ASSERTION_FAILED</c>
/// <see cref="TestException"/>. The optional message of the call opens every failure, and survives <see cref="Not"/>.
/// </summary>
/// <remarks>
/// .NET has no <c>undefined</c>: <c>null</c> stands for both <c>null</c> and <c>undefined</c>. Values compare by structure whatever
/// their class: see <see cref="ToEqual(object?)"/>. Upstream's <c>toMatchSchema</c> is not ported (no Standard Schema in .NET).
/// </remarks>
public sealed class ValueExpectation<T>
{
    private readonly T _actual;
    private readonly bool _negated;
    private readonly string? _message;

    internal ValueExpectation(T actual, bool negated, string? message)
    {
        _actual = actual;
        _negated = negated;
        _message = message;
    }

    /// <summary>Inverts the matcher.</summary>
    public ValueExpectation<T> Not => new(_actual, !_negated, _message);

    /// <summary>Compares with JavaScript's <c>Object.is</c>: a number equals itself (<c>NaN</c> included), <c>0</c> is not <c>-0</c>, and an object must be the same instance.</summary>
    public void ToBe(T expected) => Check(
        ValueEquality.ObjectIs(_actual, expected),
        () => $"expected {ValueFormat.Of(_actual)} to be {ValueFormat.Of(expected)}",
        () => $"expected {ValueFormat.Of(_actual)} not to be {ValueFormat.Of(expected)}");

    /// <summary>
    /// Performs recursive structural equality. A class is not compared: a record equals a dictionary of its property names, and a map
    /// (<see cref="IDictionary"/>), a set (<c>ISet&lt;T&gt;</c>), and an array each match by its own rule. Takes <see cref="Expect.Any(Type)"/> and the
    /// other asymmetric matchers anywhere in <paramref name="expected"/>.
    /// </summary>
    public void ToEqual(object? expected) => Check(
        ValueEquality.Equal(_actual, expected),
        () => $"expected {ValueFormat.Of(_actual)} to equal {ValueFormat.Of(expected)}",
        () => $"expected {ValueFormat.Of(_actual)} not to equal {ValueFormat.Of(expected)}");

    /// <summary>Requires every property of <paramref name="expected"/> to match recursively; extra properties on the value are fine, arrays must match element-wise with equal length.</summary>
    public void ToMatchObject(object expected)
    {
        object? actual = _actual;
        if (actual is null || !ValueEquality.IsObject(actual))
        {
            throw Failure($"toMatchObject requires an object, got {ValueFormat.Of(actual)}");
        }

        if (expected is null || !ValueEquality.IsObject(expected) && expected is not AsymmetricMatcher)
        {
            throw Failure($"toMatchObject takes an object to match against, got {ValueFormat.Of(expected)}");
        }

        Check(
            ValueEquality.Subset(actual, expected),
            () => $"expected {ValueFormat.Of(actual)} to match object {ValueFormat.Of(expected)}",
            () => $"expected {ValueFormat.Of(actual)} not to match object {ValueFormat.Of(expected)}");
    }

    /// <summary>Requires a truthy value: not <c>null</c>, <c>false</c>, <c>0</c>, <c>NaN</c>, or an empty string.</summary>
    public void ToBeTruthy() => Check(
        IsTruthy(_actual),
        () => $"expected {ValueFormat.Of(_actual)} to be truthy",
        () => $"expected {ValueFormat.Of(_actual)} not to be truthy");

    /// <summary>Requires a falsy value.</summary>
    public void ToBeFalsy() => Check(
        !IsTruthy(_actual),
        () => $"expected {ValueFormat.Of(_actual)} to be falsy",
        () => $"expected {ValueFormat.Of(_actual)} not to be falsy");

    /// <summary>Requires <c>null</c>.</summary>
    public void ToBeNull() => Check(
        _actual is null,
        () => $"expected {ValueFormat.Of(_actual)} to be null",
        () => "expected value not to be null");

    /// <summary>Requires <c>null</c>: .NET has no <c>undefined</c> apart from it.</summary>
    public void ToBeUndefined() => Check(
        _actual is null,
        () => $"expected {ValueFormat.Of(_actual)} to be undefined",
        () => "expected value not to be undefined");

    /// <summary>Requires any value but <c>null</c>: .NET has no <c>undefined</c> apart from it.</summary>
    public void ToBeDefined() => Check(
        _actual is not null,
        () => $"expected {ValueFormat.Of(_actual)} to be defined",
        () => $"expected {ValueFormat.Of(_actual)} not to be defined");

    /// <summary>Requires a <c>length</c> equal to <paramref name="expected"/>: a string, an array, a collection, or anything with a numeric <c>Length</c> or <c>Count</c>.</summary>
    public void ToHaveLength(int expected)
    {
        object? actual = _actual;
        var length = LengthOf(actual) ?? throw Failure($"toHaveLength requires a value with a numeric length, got {ValueFormat.Of(actual)}");
        Check(
            length == expected,
            () => $"expected {ValueFormat.Of(actual)} to have length {expected}, got {length}",
            () => $"expected {ValueFormat.Of(actual)} not to have length {expected}");
    }

    /// <summary>Requires the property at the dotted <paramref name="path"/> to exist. A key that holds a dot needs the list form.</summary>
    public void ToHaveProperty(string path) => HaveProperty(KeysOf(path), path, expected: null);

    /// <summary>Requires the property at the dotted <paramref name="path"/> to equal <paramref name="expected"/> by <see cref="ToEqual(object?)"/> rules.</summary>
    public void ToHaveProperty(string path, object? expected) => HaveProperty(KeysOf(path), path, [expected]);

    /// <summary>Requires the property at <paramref name="path"/>, one key per element, to exist.</summary>
    public void ToHaveProperty(IReadOnlyList<object> path) => HaveProperty(KeysOf(path), path, expected: null);

    /// <summary>Requires the property at <paramref name="path"/>, one key per element, to equal <paramref name="expected"/> by <see cref="ToEqual(object?)"/> rules.</summary>
    public void ToHaveProperty(IReadOnlyList<object> path, object? expected) => HaveProperty(KeysOf(path), path, [expected]);

    /// <summary>Requires string or collection containment. A collection is searched with <see cref="ToEqual(object?)"/> rules.</summary>
    public void ToContain(object? expected)
    {
        object? actual = _actual;
        bool contains;
        if (actual is string text)
        {
            if (expected is not string part)
            {
                throw Failure($"toContain on a string requires a string, got {ValueFormat.Of(expected)}");
            }

            contains = text.Contains(part, StringComparison.Ordinal);
        }
        else if (actual is IEnumerable items)
        {
            contains = items.Cast<object?>().Any(item => ValueEquality.Equal(item, expected));
        }
        else
        {
            throw Failure($"toContain requires a string or collection, got {ValueFormat.Of(actual)}");
        }

        Check(
            contains,
            () => $"expected {ValueFormat.Of(actual)} to contain {ValueFormat.Of(expected)}",
            () => $"expected {ValueFormat.Of(actual)} not to contain {ValueFormat.Of(expected)}");
    }

    /// <summary>Requires the string to contain <paramref name="expected"/>.</summary>
    public void ToMatch(string expected)
    {
        var actual = TextOf("toMatch");
        Check(
            actual.Contains(expected, StringComparison.Ordinal),
            () => $"expected {ValueFormat.Of(actual)} to match {ValueFormat.Of(expected)}",
            () => $"expected {ValueFormat.Of(actual)} not to match {ValueFormat.Of(expected)}");
    }

    /// <summary>Requires the string to match <paramref name="expected"/>.</summary>
    public void ToMatch(Regex expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var actual = TextOf("toMatch");
        Check(
            expected.IsMatch(actual),
            () => $"expected {ValueFormat.Of(actual)} to match {ValueFormat.Of(expected)}",
            () => $"expected {ValueFormat.Of(actual)} not to match {ValueFormat.Of(expected)}");
    }

    /// <summary>Requires a numeric lower bound.</summary>
    public void ToBeGreaterThan(double expected) => Compare("toBeGreaterThan", expected, actual => actual > expected, "greater than");

    /// <summary>Requires a numeric lower bound, inclusive.</summary>
    public void ToBeGreaterThanOrEqual(double expected) => Compare("toBeGreaterThanOrEqual", expected, actual => actual >= expected, "greater than or equal to");

    /// <summary>Requires a numeric upper bound.</summary>
    public void ToBeLessThan(double expected) => Compare("toBeLessThan", expected, actual => actual < expected, "less than");

    /// <summary>Requires a numeric upper bound, inclusive.</summary>
    public void ToBeLessThanOrEqual(double expected) => Compare("toBeLessThanOrEqual", expected, actual => actual <= expected, "less than or equal to");

    /// <summary>
    /// Jest's rule: the difference must be under half a unit of the last kept digit, <c>10 ** -digits / 2</c>, so <c>ToBeCloseTo(60)</c>
    /// accepts 59.996 and rejects 59.99. Infinite values pass only when they are the same infinity.
    /// </summary>
    public void ToBeCloseTo(double expected, int digits = 2)
    {
        var actual = NumberOf("toBeCloseTo");
        if (digits < 0)
        {
            throw Failure($"toBeCloseTo digits must be a non-negative integer, got {digits.ToString(CultureInfo.InvariantCulture)}");
        }

        var close = actual == expected
            || double.IsFinite(actual) && double.IsFinite(expected) && Math.Abs(actual - expected) < Math.Pow(10, -digits) / 2;
        Check(
            close,
            () => $"expected {ValueFormat.Number(actual)} to be close to {ValueFormat.Number(expected)} ({digits} digits)",
            () => $"expected {ValueFormat.Number(actual)} not to be close to {ValueFormat.Number(expected)} ({digits} digits)");
    }

    private void HaveProperty(IReadOnlyList<string> keys, object path, object?[]? expected)
    {
        if (keys.Count == 0 || keys.Any(key => key.Length == 0))
        {
            throw Failure($"toHaveProperty takes a dotted path or an array of keys, got {ValueFormat.Of(path)}");
        }

        var label = string.Join('.', keys);
        object? actual = _actual;
        var present = true;
        var found = actual;
        foreach (var key in keys)
        {
            if (!ValueEquality.TryGetMember(found, key, out found))
            {
                present = false;
                break;
            }
        }

        if (expected is null)
        {
            Check(
                present,
                () => $"expected {ValueFormat.Of(actual)} to have property \"{label}\"",
                () => $"expected {ValueFormat.Of(actual)} not to have property \"{label}\"");
            return;
        }

        var value = expected[0];
        Check(
            present && ValueEquality.Equal(found, value),
            () => present
                ? $"expected property \"{label}\" of {ValueFormat.Of(actual)} to equal {ValueFormat.Of(value)}, got {ValueFormat.Of(found)}"
                : $"expected {ValueFormat.Of(actual)} to have property \"{label}\" equal to {ValueFormat.Of(value)}",
            () => $"expected property \"{label}\" of {ValueFormat.Of(actual)} not to equal {ValueFormat.Of(value)}");
    }

    /// <summary>The one body of the four ordering matchers: a number on the left, a phrase for the message.</summary>
    private void Compare(string name, double expected, Func<double, bool> holds, string phrase)
    {
        var actual = NumberOf(name);
        Check(
            holds(actual),
            () => $"expected {ValueFormat.Number(actual)} to be {phrase} {ValueFormat.Number(expected)}",
            () => $"expected {ValueFormat.Number(actual)} not to be {phrase} {ValueFormat.Number(expected)}");
    }

    private double NumberOf(string name)
    {
        object? actual = _actual;
        return ValueEquality.IsNumber(actual)
            ? Convert.ToDouble(actual, CultureInfo.InvariantCulture)
            : throw Failure($"{name} requires a number, got {ValueFormat.Of(actual)}");
    }

    private string TextOf(string name)
    {
        object? actual = _actual;
        return actual as string ?? throw Failure($"{name} requires a string, got {ValueFormat.Of(actual)}");
    }

    /// <summary>Messages are thunks: formatting large values is paid only on failure.</summary>
    private void Check(bool condition, Func<string> positive, Func<string> negative)
    {
        if (_negated)
        {
            if (condition)
            {
                throw Failure(negative());
            }

            return;
        }

        if (!condition)
        {
            throw Failure(positive());
        }
    }

    private TestException Failure(string detail) => new("ASSERTION_FAILED", _message is null ? detail : _message + ": " + detail);

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool flag => flag,
        string text => text.Length > 0,
        char character => character != '\0',
        _ when ValueEquality.IsNumber(value) => Convert.ToDouble(value, CultureInfo.InvariantCulture) is var number && number != 0 && !double.IsNaN(number),
        _ => true,
    };

    private static int? LengthOf(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return text.Length;
            case Array array:
                return array.Length;
            case IDictionary when ValueEquality.TryGetMember(value, "length", out var named) && named is int declared:
                return declared;
            case ICollection collection:
                return collection.Count;
        }

        foreach (var name in new[] { "length", "count" })
        {
            if (ValueEquality.TryGetMember(value, name, out var found) && found is int number)
            {
                return number;
            }
        }

        return null;
    }

    private static string[] KeysOf(string path) => path.Split('.');

    private static string[] KeysOf(IReadOnlyList<object> path) => [.. path.Select(key => Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty)];
}

/// <summary>.NET-only: <c>format</c> of upstream's <c>values.ts</c>, which reads a value the way <c>JSON.stringify</c> does.</summary>
internal static class ValueFormat
{
    public static string Of(object? value) => Of(value, []);

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Of(object? value, List<object> seen)
    {
        switch (value)
        {
            case null:
                return "null";
            case string text:
                return JsonQuote.Of(text);
            case char character:
                return JsonQuote.Of(character.ToString());
            case bool flag:
                return flag ? "true" : "false";
            case Regex pattern:
                return "/" + pattern + "/";
            case AsymmetricMatcher matcher:
                return matcher.Describe();
            case DateTime moment:
                return JsonQuote.Of(moment.ToString("o", CultureInfo.InvariantCulture));
            case DateTimeOffset moment:
                return JsonQuote.Of(moment.ToString("o", CultureInfo.InvariantCulture));
            case Exception error:
                return JsonQuote.Of(error.GetType().Name + ": " + error.Message);
            case Delegate callback:
                return "[Function " + callback.Method.Name + "]";
            case Type type:
                return type.Name;
            case IFormattable formattable when value.GetType().IsPrimitive || value is decimal:
                return formattable.ToString(value is double or float ? "R" : null, CultureInfo.InvariantCulture);
        }

        if (value.GetType().IsEnum)
        {
            return JsonQuote.Of(value.ToString() ?? string.Empty);
        }

        if (seen.Any(item => ReferenceEquals(item, value)))
        {
            return "[Circular]";
        }

        seen.Add(value);
        try
        {
            if (value is IDictionary dictionary)
            {
                var entries = ValueEquality.Pairs(dictionary).Select(entry => JsonQuote.Of(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty) + ":" + Of(entry.Value, seen));
                return "{" + string.Join(',', entries) + "}";
            }

            if (value is IEnumerable items)
            {
                return "[" + string.Join(',', items.Cast<object?>().Select(item => Of(item, seen))) + "]";
            }

            var members = ValueEquality.Members(value).Select(member => JsonQuote.Of(member.Key) + ":" + Of(member.Value, seen));
            return "{" + string.Join(',', members) + "}";
        }
        finally
        {
            seen.RemoveAt(seen.Count - 1);
        }
    }
}
