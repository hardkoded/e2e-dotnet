// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.RunnerLifecycle;

/// <summary>Where each fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started.</summary>
internal static class RunnerLifecycleResults
{
    private static readonly string Root = Path.Combine(
        Path.GetTempPath(),
        "e2e-runner-lifecycle-" + Guid.NewGuid().ToString("N")
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

/// <summary>The runtime <c>test.fixme</c> tests of upstream's <c>runner-lifecycle.test.ts</c>, as an NUnit body would write them.</summary>
[Explicit("Run by RunnerLifecycleTests")]
public sealed class FixmeFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [Test]
    [Fixme]
    public void Registered() => throw new InvalidOperationException("a [Fixme] body must never run");

    [Test]
    public async Task Conditional()
    {
        await App.OpenAsync("/");
        Fixme(true, "flaky on webkit");
        throw new InvalidOperationException("unreachable after a fixme");
    }

    [Test]
    public void Condition_false_runs_on() => Fixme(false);

    [Test]
    public void Bare() => Fixme();

    [Test]
    public void Blank_reason() => Fixme(reason: "   ");
}

/// <summary><c>describe('hooked')</c>: a <c>[SetUp]</c> marks the test fixme, as upstream's <c>beforeEach</c> does.</summary>
[Explicit("Run by RunnerLifecycleTests")]
public sealed class FixmeFromHookFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [SetUp]
    public void NotYet() => Fixme(reason: "hook says not yet");

    [Test]
    public void Member() => throw new InvalidOperationException("unreachable after a fixme in a [SetUp]");
}

/// <summary>The fixme-beside-a-failure <c>it</c>: a <c>[TearDown]</c> throws after the test marked itself fixme.</summary>
[Explicit("Run by RunnerLifecycleTests")]
public sealed class FixmeBesideFailureFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [TearDown]
    public void Broken() => throw new InvalidOperationException("teardown broke");

    [Test]
    public void Beside_a_failure() => Fixme(reason: "not ready");
}

/// <summary><c>describe('wizard', { serial: true })</c>: the first member marks itself fixme, the second runs.</summary>
[Explicit("Run by RunnerLifecycleTests")]
[Serial]
public sealed class FixmeSerialFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [Test]
    public void A_step_1_is_not_ready() => Fixme(reason: "crashes on step 1");

    [Test]
    public void B_step_2_runs() { }
}

/// <summary>A serial group whose first member passes and whose second is registered with <c>[Fixme]</c>: refused on an unfiltered run too.</summary>
[Explicit("Run by RunnerLifecycleTests")]
[Serial]
public sealed class FixmeAfterPassingSerialMemberFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [Test]
    public void A_passes() { }

    [Test]
    [Fixme]
    public void B_registered_fixme() => throw new InvalidOperationException("a [Fixme] body must never run");
}

/// <summary>A setup test that marks itself fixme: a setup may not skip.</summary>
[Explicit("Run by RunnerLifecycleTests")]
public sealed class FixmeSetupFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [E2ESetup("mark a setup fixme", Saves = ["fixme-setup"])]
    public void Setup_marked_fixme() => Fixme(true, "a setup may not skip");

    [Test]
    [E2ESession("fixme-setup")]
    public void Consumes_the_fixme_setup_session() { }
}
