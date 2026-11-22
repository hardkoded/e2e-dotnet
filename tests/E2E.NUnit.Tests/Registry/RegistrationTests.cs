// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.ExpectedFailure;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.Registry;

/// <summary>
/// <c>describe('registration')</c> of upstream's <c>tests/unit/registry.test.ts</c>, for the <c>describe.fail</c> and <c>fail</c> option tests of
/// tester-army/e2e#1925. The port has no collector: a group is a fixture, marked with <c>[Property("E2E.Fail", reason)]</c>. Each test is <c>Partial_</c>
/// for what that leaves out: <c>describe.fail(title, fn)</c> and its registration checks (a non-boolean, non-string <c>fail</c> is not expressible in a
/// property), and the <c>COLLECTION_ERROR</c> (the port refuses the serial case as the test's own <c>INVALID_ARGUMENT</c> when it runs).
/// </summary>
public sealed class RegistrationTests
{
    [Test]
    public void Partial_marks_a_group_by_describe_fail_or_the_fail_option_and_checks_both()
    {
        var plain = TestOf(typeof(DescribeFailFixture), nameof(DescribeFailFixture.Still_broken));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(plain), Is.EqualTo("expected to fail"));
        var option = TestOf(typeof(KnownGapsFixture), nameof(KnownGapsFixture.Card_form));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(option), Is.EqualTo("old checkout"));
        var outside = TestOf(typeof(OutsideTheGroupFixture), nameof(OutsideTheGroupFixture.Outside_the_group));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(outside), Is.Null);
    }

    [Test]
    public void Partial_refuses_fail_on_inside_or_around_a_serial_group()
    {
        var onSerial = NestedRun.Run(nameof(MarkedSerialFixture.Marked_member));
        Assert.That(onSerial.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(
            onSerial.Message,
            Does.Contain("INVALID_ARGUMENT").And.Contain("cannot be set on or inside a serial group")
        );

        var inside = NestedRun.Run(nameof(MarkedMemberSerialFixture.Marked_member_of_a_serial_group));
        Assert.That(inside.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(inside.Message, Does.Contain("INVALID_ARGUMENT"));

        var around = NestedRun.Run(
            nameof(MarkedOuterFixture.SerialInside.Member_of_a_serial_group_inside_a_marked_group)
        );
        Assert.That(around.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(around.Message, Does.Contain("INVALID_ARGUMENT"));
    }

    [Test]
    public void Partial_an_inner_group_s_fail_reason_wins_over_an_outer_one_and_fail_false_does_not_unmark()
    {
        var inner = TestOf(typeof(MarkedOuterFixture.Inner), nameof(MarkedOuterFixture.Inner.Inner_test));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(inner), Is.EqualTo("inner reason"));
        var blank = TestOf(typeof(MarkedOuterFixture.Blank), nameof(MarkedOuterFixture.Blank.Blank_test));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(blank), Is.EqualTo("outer reason"));
        var unmarked = TestOf(typeof(MarkedOuterFixture.Unmarked), nameof(MarkedOuterFixture.Unmarked.Unmarked_test));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(unmarked), Is.EqualTo("outer reason"));
    }

    private static ITest TestOf(Type fixture, string method)
    {
        var builder = new global::NUnit.Framework.Api.DefaultTestAssemblyBuilder();
        var root = builder.Build(typeof(RegistrationTests).Assembly, new Dictionary<string, object>());
        return Find(root, fixture, method)
            ?? throw new InvalidOperationException("no test " + fixture.Name + "." + method);
    }

    private static ITest? Find(ITest each, Type fixture, string method) =>
        !each.IsSuite && each.TypeInfo?.Type == fixture && each.MethodName == method
            ? each
            : each.Tests.Select(child => Find(child, fixture, method)).FirstOrDefault(found => found is not null);
}

/// <summary>A serial group marked fail: refused.</summary>
[Explicit("Run by RegistrationTests")]
[Serial]
[Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
public sealed class MarkedSerialFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Marked_member() { }
}

/// <summary>A marked test inside a serial group: refused.</summary>
[Explicit("Run by RegistrationTests")]
[Serial]
public sealed class MarkedMemberSerialFixture : ExpectedFailureFixtureBase
{
    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
    public void Marked_member_of_a_serial_group() { }
}

/// <summary>A group marked fail with an outer reason, around groups that mark again, mark blank, or give nothing, and around a serial group.</summary>
[Explicit("Run by RegistrationTests")]
[Property(NUnitSessionTestRunner.ExpectedFailureProperty, "outer reason")]
public sealed class MarkedOuterFixture : ExpectedFailureFixtureBase
{
    [Explicit("Run by RegistrationTests")]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "inner reason")]
    public sealed class Inner : ExpectedFailureFixtureBase
    {
        [Test]
        public void Inner_test() { }
    }

    [Explicit("Run by RegistrationTests")]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, " ")]
    public sealed class Blank : ExpectedFailureFixtureBase
    {
        [Test]
        public void Blank_test() { }
    }

    [Explicit("Run by RegistrationTests")]
    public sealed class Unmarked : ExpectedFailureFixtureBase
    {
        [Test]
        public void Unmarked_test() { }
    }

    [Explicit("Run by RegistrationTests")]
    [Serial]
    public sealed class SerialInside : ExpectedFailureFixtureBase
    {
        [Test]
        public void Member_of_a_serial_group_inside_a_marked_group() { }
    }
}
