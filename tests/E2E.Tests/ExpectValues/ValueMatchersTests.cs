// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text.RegularExpressions;

namespace E2E.Tests.ExpectValues;

/// <summary>
/// <c>describe('value matchers')</c> of upstream's <c>expect-values.test.ts</c>. Not ported: <c>toMatch rejects a predicate instead of
/// matching everything</c> (<c>ToMatch</c> takes a string or a <see cref="Regex"/>, so a predicate cannot be passed), <c>toMatch gives the
/// same answer on every test of a global or sticky regexp</c> (a .NET <see cref="Regex"/> keeps no <c>lastIndex</c>), and <c>compares a
/// non-index key on an array, so "01" is a property and not a hole</c> (a .NET array has no named keys). Upstream's four
/// <c>toMatchSchema</c> its are in <see cref="ToMatchSchemaTests"/>. <c>undefined</c> is <see cref="Expect.Undefined"/>, so the its that tell it from <c>null</c> are full
/// ports (hardkoded/e2e-dotnet#1368). Ported in part (<c>Partial_</c>): the two its that read JavaScript-only values (#1369): sparse arrays,
/// wrapper methods, and <c>new String</c>.
/// </summary>
public sealed class ValueMatchersTests
{
    private sealed record Point(int X, int Y);

    private sealed class Node
    {
        public int A { get; init; }

        public Node? Self { get; set; }
    }

    private sealed class Counted : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            yield return 1;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void ToBe_uses_Object_is()
    {
        Expect.That(1).ToBe(1);
        Expect.That(double.NaN).ToBe(double.NaN);
        FailsWith(() => Expect.That<object>(new object()).ToBe(new object()), "to be");
        FailsWith(() => Expect.That(0.0).ToBe(-0.0), "to be");
    }

    [Fact]
    public void ToEqual_performs_recursive_structural_equality()
    {
        Expect.That(new { a = new object[] { 1, new { b = 2 } } }).ToEqual(new { a = new object[] { 1, new { b = 2 } } });
        Expect.That(new Dictionary<string, int> { ["k"] = 1 }).ToEqual(new Dictionary<string, int> { ["k"] = 1 });
        FailsWith(() => Expect.That(new { a = 1 }).ToEqual(new { a = 2 }), "to equal");
    }

    [Fact]
    public void Truthy_falsy_null_undefined_defined()
    {
        Expect.That(1).ToBeTruthy();
        Expect.That(string.Empty).ToBeFalsy();
        Expect.That<object?>(null).ToBeNull();
        Expect.That(Expect.Undefined).ToBeUndefined();
        Expect.That("x").ToBeDefined();
        Expect.That<object?>(null).ToBeDefined();
        FailsWith(() => Expect.That(Expect.Undefined).ToBeDefined(), "to be defined");
        FailsWith(() => Expect.That<object?>(null).Not.ToBeDefined(), "not to be defined");
    }

    [Fact]
    public void Truthy_falsy_null_undefined_fail_on_the_wrong_value_and_pass_negated()
    {
        FailsWith(() => Expect.That(Expect.Undefined).ToBeTruthy(), "^expected undefined to be truthy");
        FailsWith(() => Expect.That(0).ToBeTruthy(), "^expected 0 to be truthy");
        FailsWith(() => Expect.That(1).Not.ToBeTruthy(), "^expected 1 not to be truthy");
        FailsWith(() => Expect.That(1).ToBeFalsy(), "^expected 1 to be falsy");
        FailsWith(() => Expect.That(string.Empty).Not.ToBeFalsy(), "^expected \"\" not to be falsy");
        FailsWith(() => Expect.That(0).ToBeNull(), "^expected 0 to be null");
        FailsWith(() => Expect.That(Expect.Undefined).ToBeNull(), "^expected undefined to be null");
        FailsWith(() => Expect.That<object?>(null).Not.ToBeNull(), "^expected value not to be null");
        FailsWith(() => Expect.That<object?>(null).ToBeUndefined(), "^expected null to be undefined");
        FailsWith(() => Expect.That(Expect.Undefined).Not.ToBeUndefined(), "^expected value not to be undefined");
        Expect.That(0).Not.ToBeTruthy();
        Expect.That(1).Not.ToBeFalsy();
        Expect.That(Expect.Undefined).Not.ToBeNull();
        Expect.That<object?>(null).Not.ToBeUndefined();
    }

    [Fact]
    public void ToContain_works_on_strings_arrays_sets_and_iterables()
    {
        Expect.That("hello world").ToContain("world");
        Expect.That(new[] { 1, 2, 3 }).ToContain(2);
        Expect.That(new[] { new { a = 1 } }).ToContain(new { a = 1 });
        Expect.That(new HashSet<string> { "x" }).ToContain("x");
        FailsWith(() => Expect.That(new[] { 1 }).ToContain(9), "to contain");
        FailsWith(() => Expect.That(42).ToContain(1), "string or collection");
    }

    [Fact]
    public void ToMatch_accepts_substrings_and_regexps()
    {
        Expect.That("order #42").ToMatch("#42");
        Expect.That("order #42").ToMatch(new Regex(@"#\d+"));
        FailsWith(() => Expect.That("abc").ToMatch(new Regex("xyz")), "to match");
    }

    [Fact]
    public void Numeric_comparisons()
    {
        Expect.That(5).ToBeGreaterThan(4);
        Expect.That(3).ToBeLessThan(4);
        FailsWith(() => Expect.That(1).ToBeGreaterThan(2), "greater");
        FailsWith(() => Expect.That("x").ToBeGreaterThan(2), "requires a number");
    }

    [Fact]
    public void Inclusive_comparisons_accept_the_bound_itself()
    {
        Expect.That(4).ToBeGreaterThanOrEqual(4);
        Expect.That(5).ToBeGreaterThanOrEqual(4);
        Expect.That(4).ToBeLessThanOrEqual(4);
        Expect.That(3).ToBeLessThanOrEqual(4);
        FailsWith(() => Expect.That(3).ToBeGreaterThanOrEqual(4), "greater than or equal");
        FailsWith(() => Expect.That(5).ToBeLessThanOrEqual(4), "less than or equal");
        FailsWith(() => Expect.That(4).Not.ToBeLessThanOrEqual(4), "not to be less than or equal");
    }

    [Fact]
    public void ToBeCloseTo_follows_the_half_unit_rule_of_the_last_kept_digit()
    {
        Expect.That(59.996).ToBeCloseTo(60);
        Expect.That(0.1 + 0.2).ToBeCloseTo(0.3);
        Expect.That(60).ToBeCloseTo(60, 10);
        Expect.That(double.PositiveInfinity).ToBeCloseTo(double.PositiveInfinity);
        Expect.That(59.9).ToBeCloseTo(60, 0);
        FailsWith(() => Expect.That(59.99).ToBeCloseTo(60), @"close to 60 \(2 digits\)");
        FailsWith(() => Expect.That(59.4).ToBeCloseTo(60, 0), "close to");
        FailsWith(() => Expect.That(60).Not.ToBeCloseTo(60), "not to be close");
        FailsWith(() => Expect.That(60).ToBeCloseTo(60, -1), "non-negative integer");
        FailsWith(() => Expect.That("60").ToBeCloseTo(60), "requires a number");
    }

    [Fact]
    public void A_message_opens_the_failure_text_and_survives_negation()
    {
        Expect.That(true, "the user exists through the API").ToBe(true);
        FailsWith(() => Expect.That(false, "the user exists through the API").ToBe(true), "^the user exists through the API: expected false to be true$");
        FailsWith(() => Expect.That(1, "count").Not.ToBe(1), "^count: expected 1 not to be 1$");
    }

    [Fact]
    public void Negation_via_not()
    {
        Expect.That(1).Not.ToBe(2);
        Expect.That(new { a = 1 }).Not.ToEqual(new { a = 2 });
        Expect.That("abc").Not.ToContain("z");
        FailsWith(() => Expect.That(1).Not.ToBe(1), "not to be");
    }

    [Fact]
    public void Double_negation_returns_to_positive()
    {
        Expect.That(1).Not.Not.ToBe(1);
    }

    [Fact]
    public void Partial_ToEqual_ignores_undefined_properties_and_class_types()
    {
        Expect.That(new { a = 1, b = Expect.Undefined }).ToEqual(new { a = 1 });
        Expect.That(new Point(1, 2)).ToEqual(new { X = 1, Y = 2 });
    }

    [Fact]
    public void ToEqual_compares_dates_regexps_errors_sets_maps_and_cycles()
    {
        Expect.That(new DateTime(1, DateTimeKind.Utc)).ToEqual(new DateTime(1, DateTimeKind.Utc));
        FailsWith(() => Expect.That(new DateTime(1, DateTimeKind.Utc)).ToEqual(new DateTime(2, DateTimeKind.Utc)), "to equal");
        Expect.That(new Regex("a", RegexOptions.IgnoreCase)).ToEqual(new Regex("a", RegexOptions.IgnoreCase));
        FailsWith(() => Expect.That(new Regex("a", RegexOptions.IgnoreCase)).ToEqual(new Regex("a")), "to equal");
        Expect.That(new InvalidOperationException("boom")).ToEqual(new InvalidOperationException("boom"));
        FailsWith(() => Expect.That(new InvalidOperationException("boom")).ToEqual(new Exception("boom")), "to equal");
        Expect.That(Identity(new object[] { new { a = 1 } })).ToEqual(Identity(new object[] { new { a = 1 } }));
        Expect.That(ObjectMap(new object[] { new { k = 1 } }, "v")).ToEqual(ObjectMap(new object[] { new { k = 1 } }, "v"));
        FailsWith(() => Expect.That(new Dictionary<string, int> { ["k"] = 1 }).ToEqual(new Dictionary<string, int> { ["k"] = 2 }), "to equal");
        FailsWith(() => Expect.That(0.0).ToEqual(-0.0), "to equal");
        var left = new Node();
        left.Self = left;
        var right = new Node();
        right.Self = right;
        Expect.That(left).ToEqual(right);
    }

    [Fact]
    public void Matches_Set_and_Map_entries_by_structure_in_any_order_with_Jests_rules()
    {
        Expect.That(Identity(new object[] { new { x = 1 }, new { y = 2 } })).ToEqual(Identity(new object[] { new { y = 2 }, new { x = 1 } }));
        Expect.That(ObjectMap(new object[] { new { k = 1 }, new { k = 2 } }, "a", "b")).ToEqual(ObjectMap(new object[] { new { k = 2 }, new { k = 1 } }, "b", "a"));
        FailsWith(() => Expect.That(ObjectMap(new object[] { new { k = 1 } }, "a")).ToEqual(ObjectMap(new object[] { new { k = 1 } }, "b")), "to equal");
        FailsWith(
            () => Expect.That(ObjectMap(new object[] { new { k = 1 }, new { k = 1 } }, "a", "b")).ToEqual(ObjectMap(new object[] { new { k = 1 }, new { k = 2 } }, "a", "b")),
            "to equal");

        // Jest's Set rule: every actual entry needs some structurally equal expected entry of the same size, and one expected entry may
        // serve several. Two distinct { x: 1 } are therefore satisfied by a single { x: 1 }.
        Expect.That(Identity(new object[] { new { x = 1 }, new { x = 1 } })).ToEqual(Identity(new object[] { new { x = 1 }, new { y = 2 } }));
        FailsWith(() => Expect.That(Identity(new object[] { new { x = 1 }, new { x = 1 } })).ToEqual(Identity(new object[] { new { y = 2 }, new { z = 3 } })), "to equal");
    }

    [Fact]
    public void Fails_rather_than_throwing_when_only_one_side_is_iterable()
    {
        FailsWith(() => Expect.That(new HashSet<int> { 1 }).ToEqual(new object()), "to equal");
        FailsWith(() => Expect.That<object>(new Counted()).ToEqual(new object()), "to equal");
        FailsWith(() => Expect.That<object>(new object()).ToEqual(new Counted()), "to equal");
    }

    [Fact]
    public void ToMatchObject_terminates_on_a_self_referencing_expected_object_or_array()
    {
        var actual = new Node { A = 1 };
        actual.Self = actual;
        var same = new Node { A = 1 };
        same.Self = same;
        Expect.That(actual).ToMatchObject(same);
        var list = new List<object>();
        list.Add(list);
        var expectedList = new List<object>();
        expectedList.Add(expectedList);
        Expect.That(list).ToMatchObject(expectedList);

        // Jest's rule at a cycle: the subset walk stops and the pair is compared for full equality, so an actual with a property the
        // expected lacks no longer matches once the reference comes round again.
        var expected = new Dictionary<string, object?>();
        expected["Self"] = expected;
        FailsWith(() => Expect.That(actual).ToMatchObject(expected), "to match object");
        var other = new Dictionary<string, object?> { ["b"] = 2 };
        other["Self"] = other;
        FailsWith(() => Expect.That(actual).ToMatchObject(new Dictionary<string, object?> { ["Self"] = other }), "to match object");
    }

    [Fact]
    public void ToMatchObject_matches_a_subset_recursively_arrays_element_wise()
    {
        var order = new
        {
            id = 7,
            customer = new { name = "Ada", email = "ada@example.test" },
            lines = new[] { new { sku = "a", qty = 1 }, new { sku = "b", qty = 2 } },
        };
        Expect.That(order).ToMatchObject(new { id = 7 });
        Expect.That(order).ToMatchObject(new { customer = new { name = "Ada" } });
        Expect.That(order).ToMatchObject(new { lines = new[] { new { sku = "a" }, new { sku = "b" } } });
        Expect.That(new[] { new { a = 1, b = 2 } }).ToMatchObject(new[] { new { a = 1 } });
        FailsWith(() => Expect.That(order).ToMatchObject(new { lines = new[] { new { sku = "a" } } }), "to match object");
        FailsWith(() => Expect.That(order).ToMatchObject(new { customer = new { name = "Bob" } }), "to match object");
        FailsWith(() => Expect.That(order).ToMatchObject(new { missing = 1 }), "to match object");
        FailsWith(() => Expect.That(order).Not.ToMatchObject(new { id = 7 }), "not to match object");
        Expect.That(order).Not.ToMatchObject(new { id = 8 });
        FailsWith(() => Expect.That<object>(7).ToMatchObject(new object()), "requires an object");
        FailsWith(() => Expect.That(new object()).ToMatchObject(null!), "takes an object");
    }

    [Fact]
    public void ToHaveLength_reads_a_length()
    {
        Expect.That("abc").ToHaveLength(3);
        Expect.That(new[] { 1, 2 }).ToHaveLength(2);
        Expect.That(new Dictionary<string, int> { ["length"] = 4 }).ToHaveLength(4);
        Expect.That(new[] { 1 }).Not.ToHaveLength(2);
        FailsWith(() => Expect.That(new[] { 1, 2 }).ToHaveLength(3), @"^expected \[1,2\] to have length 3, got 2$");
        FailsWith(() => Expect.That("ab").Not.ToHaveLength(2), "not to have length 2");
        FailsWith(() => Expect.That(42).ToHaveLength(2), "numeric length");
        FailsWith(() => Expect.That(new object()).ToHaveLength(0), "numeric length");
    }

    [Fact]
    public void ToHaveProperty_walks_a_dotted_or_array_path_optionally_checking_the_value()
    {
        var user = new
        {
            name = "Ada",
            address = new Dictionary<string, object> { ["city"] = "London", ["zip.code"] = "N1" },
            tags = new[] { "a", "b" },
            none = (string?)null,
        };
        Expect.That(user).ToHaveProperty("name");
        Expect.That(user).ToHaveProperty("address.city");
        Expect.That(user).ToHaveProperty("address.city", "London");
        Expect.That(user).ToHaveProperty(["address", "zip.code"], "N1");
        Expect.That(user).ToHaveProperty(["tags", 1], "b");
        Expect.That(user).ToHaveProperty("tags.0", "a");
        Expect.That(user).ToHaveProperty("none");
        Expect.That(user).ToHaveProperty("none", null);
        Expect.That(user).ToHaveProperty("address", new Dictionary<string, object> { ["city"] = "London", ["zip.code"] = "N1" });
        Expect.That(user).Not.ToHaveProperty("age");
        Expect.That(user).Not.ToHaveProperty("name", "Bob");
        FailsWith(() => Expect.That(user).ToHaveProperty("address.street"), "to have property \"address.street\"$");
        FailsWith(() => Expect.That(user).ToHaveProperty("name", "Bob"), "expected property \"name\" of .* to equal \"Bob\", got \"Ada\"");
        FailsWith(() => Expect.That(user).ToHaveProperty("age", 3), "to have property \"age\" equal to 3");
        FailsWith(() => Expect.That(user).Not.ToHaveProperty("name"), "not to have property \"name\"");
        FailsWith(() => Expect.That(user).Not.ToHaveProperty("name", "Ada"), "not to equal \"Ada\"");
        FailsWith(() => Expect.That(user).ToHaveProperty(string.Empty), "dotted path");
        FailsWith(() => Expect.That<object?>(null).ToHaveProperty("a"), "to have property \"a\"");
    }

    [Fact]
    public void Partial_ToHaveProperty_reads_a_primitive_on_the_path_through_its_wrapper_as_Jest_does()
    {
        var tag = new { label = "abc", count = 3, flag = true, none = (string?)null, missing = Expect.Undefined };
        Expect.That(tag).ToHaveProperty("label.length", 3);
        Expect.That(tag).ToHaveProperty(["label", 0], "a");
        Expect.That(tag).ToHaveProperty("label.2", "c");
        Expect.That(tag).Not.ToHaveProperty("label.3");
        Expect.That(tag).Not.ToHaveProperty("label.length", 4);
        Expect.That("abc").ToHaveProperty("length", 3);
        Expect.That(tag).ToHaveProperty("none", null);
        Expect.That(tag).Not.ToHaveProperty("none.length");
        Expect.That(tag).Not.ToHaveProperty("missing.length");
        FailsWith(() => Expect.That(tag).ToHaveProperty("label.length", 4), "expected property \"label.length\" of .* to equal 4, got 3");
        FailsWith(() => Expect.That(tag).ToHaveProperty("none.length"), "to have property \"none.length\"$");
        FailsWith(() => Expect.That(Expect.Undefined).ToHaveProperty("length"), "to have property \"length\"");
    }

    private static HashSet<object> Identity(object[] items) => new(items, ReferenceEqualityComparer.Instance);

    private static Dictionary<object, string> ObjectMap(object[] keys, params string[] values)
    {
        var map = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < keys.Length; i++)
        {
            map[keys[i]] = values.Length == 1 ? values[0] : values[i];
        }

        return map;
    }

    private static void FailsWith(Action action, string pattern)
    {
        var error = Assert.Throws<TestException>(action);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(pattern, error.Message);
    }
}
