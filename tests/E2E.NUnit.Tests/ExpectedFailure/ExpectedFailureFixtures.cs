// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.ExpectedFailure;

/// <summary>
/// The tests of upstream's <c>expected-failure.test.ts</c> (<c>describe('test.fail')</c>), as an NUnit body would write them: the mark is the
/// property <c>E2E.Fail</c> on the test, or set in its body. The fixture is explicit, so a normal run never starts it.
/// </summary>
[Explicit("Run by TestFailTests")]
public sealed class ExpectedFailureFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "expected to fail")]
    public async Task Registered_fails()
    {
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Loads")).ToHaveTextAsync("not what the page says");
    }

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "known gap")]
    public void Option_fails() => throw new InvalidOperationException("the gap");

    [Test]
    public async Task Runtime_fails()
    {
        global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Properties.Set(
            NUnitSessionTestRunner.ExpectedFailureProperty,
            "gap at runtime"
        );
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Loads")).ToHaveTextAsync("not what the page says");
    }

    [Test]
    public async Task Condition_false_runs_as_normal()
    {
        await App.OpenAsync("/browser");
    }

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap is fixed")]
    public async Task Option_passes()
    {
        await App.OpenAsync("/browser");
    }

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
    public void Marked_then_skipped() => Assert.Ignore("not here");
}

/// <summary>A base for the fixtures of the later tests of <c>describe('test.fail')</c>: the explicit fixture of <see cref="ExpectedFailureFixture"/>, with no page.</summary>
public abstract class ExpectedFailureFixtureBase : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;
}

/// <summary><c>describe('marked early')</c> of upstream's hook test: the mark comes from a <c>beforeEach</c> hook, a <c>[SetUp]</c> here.</summary>
[Explicit("Run by TestFailTests")]
public sealed class MarkedEarlyFixture : ExpectedFailureFixtureBase
{
    [SetUp]
    public void MarkedInBeforeEach() => Fail(true, "marked in beforeEach");

    [Test]
    public void Fails_after_a_beforeEach_mark() => Assert.That(1, Is.EqualTo(2));
}

/// <summary><c>describe('marked late')</c> of upstream's hook test: the mark comes from an <c>afterEach</c> hook, a <c>[TearDown]</c> here.</summary>
[Explicit("Run by TestFailTests")]
public sealed class MarkedLateFixture : ExpectedFailureFixtureBase
{
    [TearDown]
    public void TooLate() => Fail(true, "too late");

    [Test]
    public void Fails_before_an_afterEach_mark() => Assert.That(1, Is.EqualTo(2));
}

/// <summary>The tests of upstream's retry test. <c>[Retry(2)]</c> is NUnit's retry policy: it reruns a failed attempt, and a marked failure passes.</summary>
[Explicit("Run by TestFailTests")]
public sealed class ExpectedFailureRetriesFixture : ExpectedFailureFixtureBase
{
    public static int ExpectedFailureRuns;

    public static int FailRuns;

    [Test]
    [Retry(2)]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
    public void Expected_failure()
    {
        ExpectedFailureRuns++;
        Assert.That(1, Is.EqualTo(2));
    }

    [Test]
    [Retry(2)]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
    public void Unexpected_pass() { }

    [Test]
    [Retry(2)]
    public void Fails_before_the_mark()
    {
        if (++FailRuns == 1)
        {
            Assert.Fail("first attempt fails unmarked"); // NUnit retries an assertion failure, not an exception
        }

        Fail(true, "gap from the second attempt");
        Assert.That(1, Is.EqualTo(2));
    }
}

/// <summary>The setup test of upstream's refusal test: a setup may not fail on purpose.</summary>
[Explicit("Run by TestFailTests")]
public sealed class ExpectedFailureSetupFixture : ExpectedFailureFixtureBase
{
    [E2ESetup("sign in", Saves = ["admin"])]
    public async Task SignIn()
    {
        Fail(true, "a setup may not fail on purpose");
        await SaveSessionAsync("admin");
    }
}

/// <summary>The serial group of upstream's refusal test: a member may not fail on purpose.</summary>
[Explicit("Run by TestFailTests")]
[Serial]
public sealed class ExpectedFailureSerialFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Step_1() => Fail(true, "a member may not fail on purpose");
}
