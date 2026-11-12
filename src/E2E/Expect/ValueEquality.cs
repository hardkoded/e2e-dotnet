// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>
/// .NET-only: what upstream's <c>values.ts</c> takes from <c>@vitest/expect</c> (<c>equals</c>, <c>iterableEquality</c>,
/// <c>subsetEquality</c>), over .NET values. A number is compared as JavaScript's <c>Object.is</c> does; a <see cref="IDictionary"/> is a
/// <c>Map</c>, an <c>ISet&lt;T&gt;</c> a <c>Set</c>, any other <see cref="IEnumerable"/> an array, and any other object a bag of its public
/// properties and fields, whatever its class.
/// </summary>
internal static class ValueEquality
{
    private enum Kind
    {
        Number,
        Text,
        Value,
        Regex,
        Error,
        Map,
        Set,
        Sequence,
        Bag,
    }

    /// <summary>JavaScript's <c>Object.is</c>: <c>NaN</c> is itself, and <c>0</c> is not <c>-0</c>. A reference type is compared by reference.</summary>
    public static bool ObjectIs(object? actual, object? expected)
    {
        if (actual is null || expected is null)
        {
            return actual is null && expected is null;
        }

        if (actual is double || expected is double || actual is float || expected is float)
        {
            if (!IsNumber(actual.GetType()) || !IsNumber(expected.GetType()))
            {
                return false;
            }

            var left = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
            var right = Convert.ToDouble(expected, CultureInfo.InvariantCulture);
            return double.IsNaN(left) ? double.IsNaN(right) : BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
        }

        if (actual is string || actual.GetType().IsValueType)
        {
            return actual.GetType() == expected.GetType() ? actual.Equals(expected) : IsNumber(actual.GetType()) && IsNumber(expected.GetType()) && CompareIntegral(actual, expected);
        }

        return ReferenceEquals(actual, expected);
    }

    public static bool IsNumber(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(type) is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
            or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal && !type.IsEnum;
    }

    public static bool IsNumber(object? value) => value is not null && IsNumber(value.GetType());

    /// <summary>Reads a member whose getter may throw; a member that cannot be read is absent, as it would be to a plain object.</summary>
    private static bool TryRead(Func<object?> read, out object? value)
    {
        try
        {
            value = read();
            return true;
        }
        catch (Exception ex) when (ex is TargetInvocationException or AmbiguousMatchException or InvalidOperationException or NotSupportedException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>The entries of an <see cref="IDictionary"/>, which a generic dictionary enumerates as key-value pairs and not as <see cref="DictionaryEntry"/> values.</summary>
    public static IEnumerable<DictionaryEntry> Pairs(IDictionary dictionary)
    {
        var entries = dictionary.GetEnumerator();
        while (entries.MoveNext())
        {
            yield return entries.Entry;
        }
    }

    /// <summary>Whether the value has named members to read: not a string, a number, a sequence, or a value such as a date.</summary>
    public static bool HasMembers(object value) => KindOf(value) is Kind.Bag or Kind.Map;

    /// <summary>The members of a bag, or the string keys of a map, in declaration order.</summary>
    public static IEnumerable<KeyValuePair<string, object?>> Members(object value)
    {
        if (value is IDictionary dictionary)
        {
            foreach (var entry in Pairs(dictionary))
            {
                yield return new(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value);
            }

            yield break;
        }

        if (Entries(value) is { } entries)
        {
            foreach (var (key, item) in entries)
            {
                yield return new(Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty, item);
            }

            yield break;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0 && property.CanRead)
            {
                if (TryRead(() => property.GetValue(value), out var read))
                {
                    yield return new(property.Name, read);
                }
            }
        }

        foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return new(field.Name, field.GetValue(value));
        }
    }

    /// <summary>
    /// Reads <paramref name="key"/> of <paramref name="holder"/> as <c>toHaveProperty</c> walks a path: a map by its key, an array by its
    /// index, anything else by a public property or field (the exact name first, then the name in any case). A string is read through its
    /// members, as Jest reads a primitive through its wrapper.
    /// </summary>
    public static bool TryGetMember(object? holder, string key, out object? value)
    {
        value = null;
        if (holder is null)
        {
            return false;
        }

        if (holder is IDictionary dictionary)
        {
            foreach (var entry in Pairs(dictionary))
            {
                if (Convert.ToString(entry.Key, CultureInfo.InvariantCulture) == key)
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }

        if (holder is IList list && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            if (index >= list.Count)
            {
                return false;
            }

            value = list[index];
            return true;
        }

        if (holder is string text && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var position))
        {
            if (position >= text.Length)
            {
                return false;
            }

            value = text[position].ToString();
            return true;
        }

        if (Entries(holder) is { } entries)
        {
            foreach (var (entryKey, entryValue) in entries)
            {
                if (Convert.ToString(entryKey, CultureInfo.InvariantCulture) == key)
                {
                    value = entryValue;
                    return true;
                }
            }

            return false;
        }

        var type = holder.GetType();
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var comparison in new[] { BindingFlags.Default, BindingFlags.IgnoreCase })
        {
            try
            {
                var property = type.GetProperty(key, Flags | comparison);
                if (property is { CanRead: true } && property.GetIndexParameters().Length == 0)
                {
                    return TryRead(() => property.GetValue(holder), out value);
                }

                var field = type.GetField(key, Flags | comparison);
                if (field is not null)
                {
                    value = field.GetValue(holder);
                    return true;
                }
            }
            catch (AmbiguousMatchException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Recursive structural equality: <c>toEqual</c>.</summary>
    public static bool Equal(object? actual, object? expected) => Equal(actual, expected, subset: false, []);

    /// <summary>
    /// Whether <paramref name="actual"/> holds every member of <paramref name="expected"/>, recursively; arrays match element-wise with
    /// equal length: <c>toMatchObject</c>.
    /// </summary>
    public static bool Subset(object? actual, object? expected) => Equal(actual, expected, subset: true, []);

    private static bool Equal(object? actual, object? expected, bool subset, List<(object Actual, object Expected)> path)
    {
        if (expected is AsymmetricMatcher expectedMatcher)
        {
            return expectedMatcher.AsymmetricMatch(actual);
        }

        if (actual is AsymmetricMatcher actualMatcher)
        {
            return actualMatcher.AsymmetricMatch(expected);
        }

        if (actual is null || expected is null)
        {
            return actual is null && expected is null;
        }

        if (actual is UndefinedValue || expected is UndefinedValue)
        {
            return ReferenceEquals(actual, expected);
        }

        if (ReferenceEquals(actual, expected) && !IsNumber(actual))
        {
            return true;
        }

        foreach (var (seenActual, seenExpected) in path)
        {
            if (ReferenceEquals(seenActual, actual) && ReferenceEquals(seenExpected, expected))
            {
                return true;
            }
        }

        var kind = KindOf(actual);
        var expectedKind = KindOf(expected);
        if (kind is Kind.Map && expectedKind is Kind.Bag || kind is Kind.Bag && expectedKind is Kind.Map)
        {
            kind = expectedKind = Kind.Bag;
        }

        if (subset && kind is Kind.Map && expectedKind is Kind.Map && HasStringKeys(actual) && HasStringKeys(expected))
        {
            // A map of names is an object to toMatchObject: the expected one may name fewer members.
            kind = expectedKind = Kind.Bag;
        }

        if (kind != expectedKind)
        {
            return false;
        }

        switch (kind)
        {
            case Kind.Number:
                return NumbersEqual(actual, expected);
            case Kind.Text:
                return actual.Equals(expected);
            case Kind.Value:
                return actual.GetType() == expected.GetType() && actual.Equals(expected);
            case Kind.Regex:
                return actual is Regex left && expected is Regex right && left.ToString() == right.ToString() && left.Options == right.Options;
            case Kind.Error:
                return actual.GetType() == expected.GetType() && ((Exception)actual).Message == ((Exception)expected).Message;
        }

        path.Add((actual, expected));
        try
        {
            return kind switch
            {
                Kind.Map => MapsEqual(actual, expected, subset, path),
                Kind.Set => SetsEqual(actual, expected, path),
                Kind.Sequence => SequencesEqual(actual, expected, subset, path),
                _ => BagsEqual(actual, expected, subset, path),
            };
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
        }
    }

    private static bool NumbersEqual(object actual, object expected)
    {
        if (actual is double or float || expected is double or float)
        {
            return ObjectIs(Convert.ToDouble(actual, CultureInfo.InvariantCulture), Convert.ToDouble(expected, CultureInfo.InvariantCulture));
        }

        return CompareIntegral(actual, expected);
    }

    private static bool CompareIntegral(object actual, object expected) =>
        Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == Convert.ToDecimal(expected, CultureInfo.InvariantCulture);

    private static bool SequencesEqual(object actual, object expected, bool subset, List<(object Actual, object Expected)> path)
    {
        var left = ((IEnumerable)actual).Cast<object?>().ToList();
        var right = ((IEnumerable)expected).Cast<object?>().ToList();
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!Equal(left[i], right[i], subset, path))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SetsEqual(object actual, object expected, List<(object Actual, object Expected)> path)
    {
        var left = ((IEnumerable)actual).Cast<object?>().ToList();
        var right = ((IEnumerable)expected).Cast<object?>().ToList();
        // Jest's Set rule: every actual entry needs some structurally equal expected entry, and one expected entry may serve several.
        return left.Count == right.Count && left.All(item => right.Any(candidate => Equal(item, candidate, subset: false, path)));
    }

    private static bool MapsEqual(object actual, object expected, bool subset, List<(object Actual, object Expected)> path)
    {
        var left = Entries(actual)!;
        var right = Entries(expected)!;
        return left.Count == right.Count
            && left.All(entry => right.Any(candidate => Equal(entry.Key, candidate.Key, subset: false, path) && Equal(entry.Value, candidate.Value, subset, path)));
    }

    private static bool BagsEqual(object actual, object expected, bool subset, List<(object Actual, object Expected)> path)
    {
        var left = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in Members(actual))
        {
            left[member.Key] = member.Value;
        }

        var right = Members(expected).ToList();
        if (!subset)
        {
            // Jest's toEqual: a member that holds undefined is the same as a member that is absent.
            if (left.Values.Any(value => value is UndefinedValue))
            {
                left = left.Where(member => member.Value is not UndefinedValue).ToDictionary(member => member.Key, member => member.Value, StringComparer.Ordinal);
            }

            right.RemoveAll(member => member.Value is UndefinedValue);
        }

        if (!subset && left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, wanted) in right)
        {
            if (!left.TryGetValue(key, out var found))
            {
                return false;
            }

            if (subset && wanted is not null && wanted is not AsymmetricMatcher && IsContainer(wanted))
            {
                // Jest's rule at a cycle: once the expected object comes round again, the pair is compared for full equality.
                var cycle = path.Any(pair => ReferenceEquals(pair.Expected, wanted));
                if (!Equal(found, wanted, subset: !cycle, cycle ? [] : path))
                {
                    return false;
                }

                continue;
            }

            if (!Equal(found, wanted, subset, path))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasStringKeys(object value) => Entries(value)!.All(entry => entry.Key is string);

    /// <summary>Whether the value is an object to <c>toMatchObject</c>: a bag, a map, a set, or an array; not a string, a number, or a date.</summary>
    public static bool IsObject(object value) => IsContainer(value);

    private static bool IsContainer(object value) => KindOf(value) is Kind.Bag or Kind.Map or Kind.Set or Kind.Sequence;

    private static Kind KindOf(object value)
    {
        var type = value.GetType();
        if (IsNumber(type))
        {
            return Kind.Number;
        }

        if (value is string or char)
        {
            return Kind.Text;
        }

        if (value is UndefinedValue)
        {
            // Not an object: IsObject and HasMembers must not read the sentinel's members.
            return Kind.Value;
        }

        if (value is Regex)
        {
            return Kind.Regex;
        }

        if (value is Exception)
        {
            return Kind.Error;
        }

        if (type.IsPrimitive || type.IsEnum || type.IsValueType && type.Namespace is not null && type.Namespace.StartsWith("System", StringComparison.Ordinal) && !type.Name.StartsWith("ValueTuple", StringComparison.Ordinal) && !type.Name.StartsWith("KeyValuePair", StringComparison.Ordinal) || value is Uri or Type or Delegate)
        {
            return Kind.Value;
        }

        if (value is IDictionary || Entries(value) is not null)
        {
            return Kind.Map;
        }

        if (type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ISet<>) || contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)))
        {
            return Kind.Set;
        }

        return value is IEnumerable ? Kind.Sequence : Kind.Bag;
    }

    /// <summary>The entries of a map: a <see cref="IDictionary"/>, or anything enumerating <see cref="KeyValuePair{TKey, TValue}"/>.</summary>
    private static List<KeyValuePair<object?, object?>>? Entries(object value)
    {
        if (value is string)
        {
            return null;
        }

        if (value is IDictionary dictionary)
        {
            return [.. Pairs(dictionary).Select(entry => new KeyValuePair<object?, object?>(entry.Key, entry.Value))];
        }

        var pair = value.GetType().GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>) && contract.GetGenericArguments()[0] is { IsGenericType: true } item && item.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
        if (pair is null)
        {
            return null;
        }

        var entries = new List<KeyValuePair<object?, object?>>();
        foreach (var item in (IEnumerable)value)
        {
            var type = item.GetType();
            entries.Add(new(type.GetProperty("Key")!.GetValue(item), type.GetProperty("Value")!.GetValue(item)));
        }

        return entries;
    }
}
