// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ComputedStyle;

/// <summary>
/// Upstream's <c>describe('computed style properties')</c> (<c>packages/e2e/tests/unit/computed-style.test.ts</c>, tester-army/e2e#1633).
/// </summary>
public sealed class ComputedStylePropertiesTests
{
    public static TheoryData<string?[]?> Refused() => new()
    {
        { null },
        { [] },
        { ["content"] },
        { ["background-image"] },
        { ["--token"] },
        { ["COLOR"] },
        { ["color", "content"] },
        { [null] },
    };

    /// <summary>
    /// Partial: upstream's cases that are no list (<c>{}</c>, <c>'color'</c>, <c>null</c> inside a typed list is the hole <c>Array(1)</c>) cannot be
    /// passed to a <see cref="IReadOnlyList{T}"/> of strings; the null list and the null item stand in for them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Refused))]
    public void Partial_Refuses_a_request_outside_the_allow_list_before_engine_access(string?[]? properties)
    {
        var error = Assert.ThrowsAny<E2EException>(() => E2E.Engine.ComputedStyles.ValidateComputedStyleProperties(properties!));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    [Fact]
    public void Copies_and_deduplicates_the_property_list()
    {
        string[] properties = [.. Enumerable.Repeat("color", 11), "opacity"];

        var checkedList = E2E.Engine.ComputedStyles.ValidateComputedStyleProperties(properties);

        Assert.Equal(["color", "opacity"], checkedList);
        Assert.NotSame(properties, checkedList);
    }
}
