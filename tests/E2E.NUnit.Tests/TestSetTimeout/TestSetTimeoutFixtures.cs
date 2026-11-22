// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.TestSetTimeout;

/// <summary>
/// A base for the fixtures of <c>test-set-timeout.test.ts</c>: explicit, so a normal run never starts them. The port's clock starts before the engine
/// launches, so every timeout is longer than upstream's (<see cref="TestSetTimeoutTests"/> says by how much).
/// </summary>
public abstract class TestSetTimeoutFixtureBase : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => TestSetTimeoutResults.Of(GetType());
}

/// <summary>Where each fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started.</summary>
internal static class TestSetTimeoutResults
{
    private static readonly string Root = Path.Combine(
        Path.GetTempPath(),
        "e2e-test-set-timeout-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>The <c>results</c> directory of the run of <paramref name="fixture"/>; <c>report.json</c> is its sibling.</summary>
    public static string Of(Type fixture) => Path.Combine(Root, fixture.Name, "results");

    public static string ReportOf(Type fixture) => Path.Combine(Path.GetDirectoryName(Of(fixture))!, "report.json");

    public static void Clear()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>The tests upstream runs with a <c>timeout</c> of 300 ms: here 10 s, past which each body sleeps 12 s.</summary>
[Explicit("Run by TestSetTimeoutTests")]
public sealed class ShortTimeoutFixture : TestSetTimeoutFixtureBase
{
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    [Test]
    public async Task SetTimeout_raises_the_timeout()
    {
        SetTimeout(TimeSpan.FromSeconds(60));
        await Task.Delay(TimeSpan.FromSeconds(12));

        // Past the 10 s the test started with, an unextended timeout cuts this call.
        await App.OpenAsync("/");
    }

    [Test]
    public async Task SetTimeout_fixme_past_the_first_timeout()
    {
        SetTimeout(TimeSpan.FromSeconds(60));
        await Task.Delay(TimeSpan.FromSeconds(12));
        await App.OpenAsync("/");
        Fixme(reason: "not ready");
    }
}

/// <summary><c>describe('raised from beforeEach')</c>: the base class's session starts first, then this <c>[SetUp]</c> raises the timeout.</summary>
[Explicit("Run by TestSetTimeoutTests")]
public sealed class RaisedFromBeforeEachFixture : TestSetTimeoutFixtureBase
{
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    [SetUp]
    public void RaiseTheTimeout() => SetTimeout(TimeSpan.FromSeconds(60));

    [Test]
    public async Task SetTimeout_outlasts_its_own_timeout()
    {
        await Task.Delay(TimeSpan.FromSeconds(12));
        await App.OpenAsync("/");
    }
}

/// <summary>The tests upstream runs with a <c>timeout</c> of 30 000 ms: here 60 s.</summary>
[Explicit("Run by TestSetTimeoutTests")]
public sealed class LongTimeoutFixture : TestSetTimeoutFixtureBase
{
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(60);

    [Test]
    public async Task SetTimeout_lowers_the_timeout()
    {
        SetTimeout(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(12));

        // The timeout cuts the calls the test awaits, as it does without SetTimeout.
        await App.OpenAsync("/about");
    }

    [Test]
    public async Task SetTimeout_below_the_time_spent_times_out_at_once()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        SetTimeout(TimeSpan.FromMilliseconds(100));
        await Task.Delay(TimeSpan.FromSeconds(1));
        await App.OpenAsync("/about");
    }

    [Test]
    public void SetTimeout_refuses_a_timeout_that_is_not_a_positive_integer() => SetTimeout(TimeSpan.Zero);

    [Test]
    public void SetTimeout_refuses_a_fraction() => SetTimeout(TimeSpan.FromMilliseconds(1.5));
}

/// <summary><c>describe('from afterEach')</c>.</summary>
[Explicit("Run by TestSetTimeoutTests")]
public sealed class FromAfterEachFixture : TestSetTimeoutFixtureBase
{
    [TearDown]
    public void RaiseTheTimeout() => SetTimeout(TimeSpan.FromSeconds(60));

    [Test]
    public void SetTimeout_is_refused() { }
}
