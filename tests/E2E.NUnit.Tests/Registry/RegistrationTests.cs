// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.ExpectedFailure;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.Registry;

/// <summary>
/// <c>describe('registration')</c> of upstream's <c>tests/unit/registry.test.ts</c>, for the <c>describe.fail</c> and <c>fail</c> option tests of
/// tester-army/e2e#1925. The port has no collector: a group is a fixture, marked with <c>[DescribeFail]</c> (<c>describe.fail(title, fn)</c>) or
/// <c>[Property("E2E.Fail", reason)]</c> (the <c>fail</c> option), and the host's discovery runs the registry's checks. Each test is <c>Partial_</c>
/// for what that leaves out, named on the test.
/// </summary>
/// <remarks><c>Partial_registers_test_fixme...</c> leaves out the call of <c>test.fixme(title, fn)</c> outside a collection, which has no counterpart: an attribute needs no call.</remarks>
public sealed class RegistrationTests
{
    [Test]
    public void Partial_registers_test_fixme_title_fn_as_fixme_with_the_reason_fixme_and_rejects_it_in_a_serial_group()
    {
        var found = E2E.NUnit.RunSelection.Discover(
            [typeof(FixmeRegistrationFixture)],
            typeof(RegistrationTests).Assembly
        );
        var registered = found.Single();
        Assert.That(registered.Title, Is.EqualTo(nameof(FixmeRegistrationFixture.Not_yet)));
        Assert.That(registered.Fixme, Is.True);
        Assert.That(registered.SkipReason, Is.EqualTo("fixme"));
        var collection = HostTestFilters.Collect(found, [], [], out _);
        Assert.That(collection.Tests.Single().Mode, Is.EqualTo(TestMode.Fixme));

        var serial = E2E.NUnit.RunSelection.Discover(
            [typeof(FixmeInSerialGroupFixture)],
            typeof(RegistrationTests).Assembly
        );
        var error = Assert.Throws<ConfigurationException>(() => HostTestFilters.Collect(serial, [], [], out _))!;
        Assert.That(error!.Code, Is.EqualTo("COLLECTION_ERROR"));
        Assert.That(
            error.Message,
            Is.EqualTo(
                "[Fixme] cannot be used inside a serial group, which skips as one unit; set skip on the serial describe instead"
            )
        );
    }

    /// <remarks>
    /// Partial: <c>describe.fail</c> with tags and keys a spread would drop, and <c>describe.fail</c> given a <c>fail</c> option, have no counterpart: the attribute
    /// takes no option, and a property holds no unknown keys.
    /// </remarks>
    [Test]
    public void Partial_marks_a_group_by_describe_fail_or_the_fail_option_and_checks_both()
    {
        var plain = TestOf(typeof(DescribeFailFixture), nameof(DescribeFailFixture.Still_broken));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(plain), Is.EqualTo("expected to fail"));
        var option = TestOf(typeof(KnownGapsFixture), nameof(KnownGapsFixture.Card_form));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(option), Is.EqualTo("old checkout"));
        var outside = TestOf(typeof(OutsideTheGroupFixture), nameof(OutsideTheGroupFixture.Outside_the_group));
        Assert.That(NUnitSessionTestRunner.ExpectedFailureOf(outside), Is.Null);

        // A fail that is not true, false, or a reason string is a collection error, as upstream's validateFailOption.
        var bad = E2E.NUnit.RunSelection.Discover([typeof(BadFailValueFixture)], typeof(RegistrationTests).Assembly);
        var error = Assert.Throws<ConfigurationException>(() => HostTestFilters.Collect(bad, [], [], out _));
        Assert.That(error!.Code, Is.EqualTo("COLLECTION_ERROR"));
        Assert.That(
            error!.Message,
            Is.EqualTo("describe options: fail must be true, false, or a reason string, got 1")
        );
    }

    /// <remarks>
    /// Partial: <c>describe.fail</c> nested inside a serial group, and a group with <c>fail: false</c> around one, have no counterpart: a nested class is
    /// not a member of its enclosing fixture's serial group, and a property cannot say <c>false</c>. An unmarked group around a serial one stands for the second.
    /// </remarks>
    [Test]
    public void Partial_refuses_fail_on_inside_or_around_a_serial_group()
    {
        const string serialRule =
            "describe option \"fail\" cannot be set on or inside a serial group, which runs as one unit";
        foreach (
            var fixture in new[]
            {
                typeof(MarkedSerialFixture),
                typeof(MarkedMemberSerialFixture),
                typeof(DescribeFailSerialFixture),
            }
        )
        {
            var found = E2E.NUnit.RunSelection.Discover([fixture], typeof(RegistrationTests).Assembly);
            var refused = Assert.Throws<ConfigurationException>(() => HostTestFilters.Collect(found, [], [], out _));
            Assert.That(refused!.Code, Is.EqualTo("COLLECTION_ERROR"));
            Assert.That(refused!.Message, Is.EqualTo(serialRule));
        }

        var around = E2E.NUnit.RunSelection.Discover(
            [typeof(MarkedOuterFixture.SerialInside)],
            typeof(RegistrationTests).Assembly
        );
        var aroundError = Assert.Throws<ConfigurationException>(() => HostTestFilters.Collect(around, [], [], out _));
        Assert.That(aroundError!.Code, Is.EqualTo("COLLECTION_ERROR"));
        Assert.That(
            aroundError.Message,
            Is.EqualTo("a serial group cannot sit inside a group marked fail; its members cannot be expected to fail")
        );

        var unmarked = E2E.NUnit.RunSelection.Discover(
            [typeof(UnmarkedOuterFixture.SerialInside)],
            typeof(RegistrationTests).Assembly
        );
        Assert.That(HostTestFilters.Collect(unmarked, [], [], out _).Tests, Has.Count.EqualTo(1));

        // An unfiltered run does not collect: the member refuses itself with the same error.
        var onSerial = NestedRun.Run(nameof(MarkedSerialFixture.Marked_member));
        Assert.That(onSerial.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(onSerial.Message, Does.Contain("COLLECTION_ERROR").And.Contain(serialRule));

        var inside = NestedRun.Run(nameof(MarkedMemberSerialFixture.Marked_member_of_a_serial_group));
        Assert.That(inside.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(inside.Message, Does.Contain("COLLECTION_ERROR"));

        var aroundRun = NestedRun.Run(
            nameof(MarkedOuterFixture.SerialInside.Member_of_a_serial_group_inside_a_marked_group)
        );
        Assert.That(aroundRun.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(aroundRun.Message, Does.Contain("COLLECTION_ERROR"));
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

/// <summary><c>describe.fail</c> on a serial group: refused.</summary>
[Explicit("Run by RegistrationTests")]
[Serial]
[DescribeFail]
public sealed class DescribeFailSerialFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Member_of_a_describe_fail_serial_group() { }
}

/// <summary>A group whose <c>fail</c> is a number (upstream's <c>{ fail: 1 }</c>).</summary>
[Explicit("Discovered by RegistrationTests, never run")]
[Property(NUnitSessionTestRunner.ExpectedFailureProperty, 1)]
public sealed class BadFailValueFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Bad_fail_value() { }
}

/// <summary>A group with no mark around a serial group: nothing to refuse.</summary>
[Explicit("Discovered by RegistrationTests, never run")]
public sealed class UnmarkedOuterFixture : ExpectedFailureFixtureBase
{
    [Explicit("Discovered by RegistrationTests, never run")]
    [Serial]
    public sealed class SerialInside : ExpectedFailureFixtureBase
    {
        [Test]
        public void Member_of_a_serial_group_inside_an_unmarked_group() { }
    }
}
