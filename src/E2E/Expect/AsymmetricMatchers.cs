// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>
/// A stand-in for a value inside <see cref="ValueExpectation{T}.ToEqual(object?)"/>, <c>ToMatchObject</c>, <c>ToContain</c>, and
/// <c>ToHaveProperty</c>: <see cref="Expect.Any(Type)"/> in place of a number, <see cref="Expect.ObjectContaining(object)"/> in place of
/// a record. The structural matchers ask it whether a value matches instead of comparing.
/// </summary>
public abstract class AsymmetricMatcher
{
    /// <summary>Whether <paramref name="other"/> satisfies the matcher.</summary>
    public abstract bool AsymmetricMatch(object? other);

    /// <summary>The matcher's name, <c>ObjectContaining</c>; a failure message adds the sample.</summary>
    public abstract override string ToString();

    /// <summary>How the matcher reads in a failure: <c>Any&lt;Int32&gt;</c> and <c>Anything</c> describe themselves, the rest print their name and sample.</summary>
    internal virtual string Describe() => ToString();
}

internal sealed class AnyMatcher(Type sample) : AsymmetricMatcher
{
    public override bool AsymmetricMatch(object? other)
    {
        if (sample == typeof(object))
        {
            // Upstream: `typeof other == 'object'`, which holds for null.
            return other is null || !other.GetType().IsValueType && other is not string;
        }

        if (other is null)
        {
            return false;
        }

        if (ValueEquality.IsNumber(sample) && ValueEquality.IsNumber(other.GetType()))
        {
            return true;
        }

        return sample.IsInstanceOfType(other);
    }

    public override string ToString() => "Any";

    internal override string Describe() => "Any<" + sample.Name + ">";
}

internal sealed class AnythingMatcher : AsymmetricMatcher
{
    public override bool AsymmetricMatch(object? other) => other is not null;

    public override string ToString() => "Anything";

    internal override string Describe() => "Anything";
}

internal sealed class ObjectContainingMatcher(object sample) : AsymmetricMatcher
{
    public override bool AsymmetricMatch(object? other)
    {
        if (other is null || !ValueEquality.HasMembers(other))
        {
            return false;
        }

        foreach (var (key, value) in ValueEquality.Members(sample))
        {
            if (!ValueEquality.TryGetMember(other, key, out var actual) || !ValueEquality.Equal(actual, value))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() => "ObjectContaining";

    internal override string Describe() => "ObjectContaining " + ValueFormat.Of(sample);
}

internal sealed class ArrayContainingMatcher(IReadOnlyList<object?> sample) : AsymmetricMatcher
{
    public override bool AsymmetricMatch(object? other)
    {
        if (other is not IEnumerable items || other is string)
        {
            return false;
        }

        var actual = items.Cast<object?>().ToList();
        return sample.All(wanted => actual.Any(item => ValueEquality.Equal(item, wanted)));
    }

    public override string ToString() => "ArrayContaining";

    internal override string Describe() => "ArrayContaining " + ValueFormat.Of(sample);
}

internal sealed class StringContainingMatcher(string sample) : AsymmetricMatcher
{
    public override bool AsymmetricMatch(object? other) => other is string text && text.Contains(sample, StringComparison.Ordinal);

    public override string ToString() => "StringContaining";

    internal override string Describe() => "StringContaining " + ValueFormat.Of(sample);
}

internal sealed class StringMatchingMatcher : AsymmetricMatcher
{
    private readonly Regex _pattern;
    private readonly object _sample;

    public StringMatchingMatcher(string sample)
    {
        _sample = sample;
        _pattern = new Regex(sample, RegexOptions.CultureInvariant);
    }

    public StringMatchingMatcher(Regex sample)
    {
        _sample = sample;
        _pattern = sample;
    }

    public override bool AsymmetricMatch(object? other) => other is string text && _pattern.IsMatch(text);

    public override string ToString() => "StringMatching";

    internal override string Describe() => "StringMatching " + ValueFormat.Of(_sample);
}
