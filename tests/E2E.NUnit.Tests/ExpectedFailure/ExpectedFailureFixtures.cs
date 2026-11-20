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
