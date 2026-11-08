// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.RegularExpressions;

namespace E2E.Tests.ExpectValues;

/// <summary>
/// <c>describe('asymmetric matchers')</c> of upstream's <c>expect-values.test.ts</c>. A failure names a .NET type, so upstream's
/// <c>Any&lt;Number&gt;</c> reads <c>Any&lt;Int32&gt;</c> here. <c>undefined</c> is <see cref="Expect.Undefined"/> (hardkoded/e2e-dotnet#1368). Ported in part
/// (<c>Partial_</c>): <c>expect.any matches classes and primitives</c> has no <c>Symbol</c> (#1369).
/// </summary>
public sealed class AsymmetricMatchersTests
{
    [Fact]
    public void Partial_Expect_any_matches_classes_and_primitives()
    {
        Expect.That(1).ToEqual(Expect.Any(typeof(int)));
        Expect.That("x").ToEqual(Expect.Any(typeof(string)));
        Expect.That(true).ToEqual(Expect.Any(typeof(bool)));
        Expect.That(new BigInteger(1)).ToEqual(Expect.Any(typeof(BigInteger)));
        Expect.That<Action>(() => { }).ToEqual(Expect.Any(typeof(Delegate)));
        Expect.That(new object()).ToEqual(Expect.Any(typeof(object)));
        Expect.That(Array.Empty<int>()).ToEqual(Expect.Any(typeof(Array)));
        Expect.That(DateTime.UtcNow).ToEqual(Expect.Any(typeof(DateTime)));
        Expect.That<object?>(null).ToEqual(Expect.Any(typeof(object)));
        FailsWith(() => Expect.That("1").ToEqual(Expect.Any(typeof(int))), "to equal Any<Int32>$");
    }

    [Fact]
    public void Expect_anything_matches_everything_but_null_and_undefined()
    {
        Expect.That(0).ToEqual(Expect.Anything());
        Expect.That(string.Empty).ToEqual(Expect.Anything());
        Expect.That<object?>(null).Not.ToEqual(Expect.Anything());
        FailsWith(() => Expect.That(Expect.Undefined).ToEqual(Expect.Anything()), "to equal Anything$");
    }

    [Fact]
    public void ObjectContaining_ArrayContaining_StringContaining_StringMatching()
    {
        Expect.That(new { a = 1, b = 2 }).ToEqual(Expect.ObjectContaining(new { a = 1 }));
        Expect.That(new { a = new { b = 1 } }).ToEqual(Expect.ObjectContaining(new { a = new { b = 1 } }));
        FailsWith(() => Expect.That(new { a = 1 }).ToEqual(Expect.ObjectContaining(new { b = 1 })), "ObjectContaining");
        FailsWith(() => Expect.That(new { a = new { b = 1, c = 2 } }).ToEqual(Expect.ObjectContaining(new { a = new { b = 1 } })), "ObjectContaining");
        Expect.That(new[] { 3, 1, 2 }).ToEqual(Expect.ArrayContaining([1, 2]));
        Expect.That(new[] { new { a = 1 }, new { a = 2 } }).ToEqual(Expect.ArrayContaining([new { a = 2 }]));
        FailsWith(() => Expect.That(new[] { 1 }).ToEqual(Expect.ArrayContaining([1, 2])), "ArrayContaining");
        Expect.That("hello world").ToEqual(Expect.StringContaining("world"));
        FailsWith(() => Expect.That("hello").ToEqual(Expect.StringContaining("world")), "StringContaining \"world\"");
        Expect.That("order #42").ToEqual(Expect.StringMatching(new Regex(@"#\d+")));
        Expect.That("order #42").ToEqual(Expect.StringMatching("#42"));
        FailsWith(() => Expect.That(42).ToEqual(Expect.StringMatching(new Regex("42"))), "StringMatching");
    }

    [Fact]
    public void Are_honoured_inside_every_structural_matcher()
    {
        var payload = new { id = "usr_1", createdAt = DateTime.UtcNow, tags = new[] { "a", "b" }, profile = new { name = "Ada", age = 36 } };
        Expect.That(payload).ToEqual(new
        {
            id = Expect.StringMatching(new Regex("^usr_")),
            createdAt = Expect.Any(typeof(DateTime)),
            tags = Expect.ArrayContaining(["b"]),
            profile = Expect.ObjectContaining(new { name = Expect.Any(typeof(string)) }),
        });
        Expect.That(payload).ToMatchObject(new { profile = new { age = Expect.Any(typeof(int)) }, tags = new object[] { Expect.Any(typeof(string)), "b" } });
        Expect.That(new[] { new { id = 1, ok = true }, new { id = 2, ok = false } }).ToContain(Expect.ObjectContaining(new { id = 2 }));
        Expect.That(new[] { "alpha", "beta" }).ToContain(Expect.StringContaining("bet"));
        Expect.That(payload).ToHaveProperty("profile.name", Expect.Any(typeof(string)));
        Expect.That(payload).ToHaveProperty("tags", Expect.ArrayContaining(["a"]));
        FailsWith(
            () => Expect.That(payload).ToEqual(new { id = Expect.Any(typeof(int)), createdAt = Expect.Anything(), tags = new[] { "a", "b" }, profile = Expect.Anything() }),
            "to equal");
        FailsWith(() => Expect.That(payload).ToMatchObject(new { profile = new { age = Expect.Any(typeof(string)) } }), "to match object");
        FailsWith(() => Expect.That(new[] { new { id = 1 } }).ToContain(Expect.ObjectContaining(new { id = 2 })), "to contain");
        FailsWith(() => Expect.That(payload).ToHaveProperty("profile.age", Expect.Any(typeof(string))), "to equal Any<String>, got 36");
        FailsWith(() => Expect.That(payload).Not.ToMatchObject(new { id = Expect.Any(typeof(string)) }), "not to match object");
    }

    [Fact]
    public void A_matcher_on_a_string_ToContain_is_refused_like_any_non_string()
    {
        FailsWith(() => Expect.That("abc").ToContain(Expect.StringContaining("a")), "requires a string");
    }

    private static void FailsWith(Action action, string pattern)
    {
        var error = Assert.Throws<TestException>(action);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(pattern, error.Message);
    }
}
