// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.ExpectedFailure;

/// <summary>
/// Where each fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started (upstream's <c>runProject</c> returns
/// the run's outcome). The "times out" test runs in the same run as <see cref="ExpectedFailureFixture"/>, as upstream's first <c>it</c> does.
/// </summary>
internal static class ExpectedFailureResults
{
    private static readonly string Root = Path.Combine(
        Path.GetTempPath(),
        "e2e-expected-failure-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>The <c>results</c> directory of the run of <paramref name="fixture"/>; <c>report.json</c> is its sibling.</summary>
    public static string Of(Type fixture) => Path.Combine(Root, RunOf(fixture).Name, "results");

    // The fixtures of one upstream test share a run, as the files of its project do.
    private static Type RunOf(Type fixture) =>
        fixture == typeof(ExpectedFailureTimeoutFixture) ? typeof(ExpectedFailureFixture)
        : fixture == typeof(DescribeFailFixture)
        || fixture == typeof(KnownGapsFixture)
        || fixture == typeof(OutsideTheGroupFixture)
            ? typeof(DescribeFailFixture)
        : fixture == typeof(MarkedLateFixture) ? typeof(MarkedEarlyFixture)
        : fixture == typeof(ExpectedFailureSetupFixture) || fixture == typeof(ExpectedFailureSerialFixture)
            ? typeof(ExpectedFailureSerialFixture)
        : fixture;

    public static string ReportOf(Type fixture) => Path.Combine(Path.GetDirectoryName(Of(fixture))!, "report.json");

    public static void Clear()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

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

    protected override string? ResultsDirectory => ExpectedFailureResults.Of(GetType());

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

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "gap")]
    public async Task Soft_failure_then_skipped()
    {
        await App.OpenAsync("/browser");
        await Expect
            .Soft(Screen.GetByLabel("Loads"))
            .ToHaveTextAsync("not what the page says", timeout: TimeSpan.FromMilliseconds(200));
        Assert.Ignore("not here either");
    }

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "engine gap")]
    public void Engine_failure() =>
        throw new EngineException(EngineErrorCodes.EngineFailure, "tap on display: contents failed");
}

/// <summary>The "times out" test of <see cref="ExpectedFailureFixture"/>, on a fixture of its own for the timeout that cuts its body short.</summary>
[Explicit("Run by TestFailTests")]
public sealed class ExpectedFailureTimeoutFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => ExpectedFailureResults.Of(GetType());

    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(5);

    [Test]
    [Property(NUnitSessionTestRunner.ExpectedFailureProperty, "too slow")]
    public async Task Times_out()
    {
        await App.OpenAsync("/browser");
        await Task.Delay(TimeSpan.FromSeconds(6));
        await App.OpenAsync("/about");
    }
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

    protected override string? ResultsDirectory => ExpectedFailureResults.Of(GetType());
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

/// <summary><c>describe.fail('legacy', ...)</c> of upstream's group test: the property on the fixture marks every test in it, with the default reason.</summary>
[Explicit("Run by TestFailTests")]
[Property(NUnitSessionTestRunner.ExpectedFailureProperty, "")]
public sealed class DescribeFailFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Still_broken() => Assert.That(1, Is.EqualTo(2));

    [Test]
    public void Fixed_since() { }
}

/// <summary><c>describe('known gaps', { fail: 'old checkout' }, ...)</c> of upstream's group test.</summary>
[Explicit("Run by TestFailTests")]
[Property(NUnitSessionTestRunner.ExpectedFailureProperty, "old checkout")]
public sealed class KnownGapsFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Card_form() => throw new InvalidOperationException("the gap");
}

/// <summary>The test outside any group in upstream's group test.</summary>
[Explicit("Run by TestFailTests")]
public sealed class OutsideTheGroupFixture : ExpectedFailureFixtureBase
{
    [Test]
    public void Outside_the_group() { }
}
