// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.XUnit.V3.Tests.RunnerLifecycle;

/// <summary>Where each fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started.</summary>
internal static class RunnerLifecycleResults
{
    private const string RootVariable = "E2E_RUNNER_LIFECYCLE_ROOT";

    // The nested run is a process of its own: it finds the directory its parent chose through the environment it inherits.
    private static readonly string Root = ChooseRoot();

    private static string ChooseRoot()
    {
        var inherited = Environment.GetEnvironmentVariable(RootVariable);
        if (!string.IsNullOrEmpty(inherited))
        {
            return inherited;
        }

        var root = Path.Combine(Path.GetTempPath(), "e2e-runner-lifecycle-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(RootVariable, root);
        return root;
    }

    /// <summary>The <c>results</c> directory of the run of <paramref name="fixture"/>; <c>report.json</c> is its sibling.</summary>
    public static string Of(Type fixture) => Path.Combine(Root, fixture.Name, "results");

    public static string ReportOf(Type fixture) => Path.Combine(Path.GetDirectoryName(Of(fixture))!, "report.json");

    /// <summary>Chooses the directory, which hands it to a nested process started afterwards. Tests of the class run in parallel, so no test clears it.</summary>
    public static void Prepare() => _ = Root;

    public static void Clear()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>The runtime <c>test.fixme</c> tests of upstream's <c>runner-lifecycle.test.ts</c>, as an xUnit body would write them.</summary>
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

    [Fact(Explicit = true)]
    [Fixme]
    public void Registered() => throw new InvalidOperationException("a [Fixme] body must never run");

    [Fact(Explicit = true)]
    public async Task Conditional()
    {
        await App.OpenAsync("/");
        Fixme(true, "flaky on webkit");
        throw new InvalidOperationException("unreachable after a fixme");
    }

    [Fact(Explicit = true)]
    public void Condition_false_runs_on() => Fixme(false);

    [Fact(Explicit = true)]
    public void Bare() => Fixme();

    [Fact(Explicit = true)]
    public void Blank_reason() => Fixme(reason: "   ");
}

/// <summary><c>test.setup('sign in', ...)</c>: a setup test may not skip, so its <c>Fixme</c> is a test error.</summary>
public sealed class FixmeInSetupFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => RunnerLifecycleResults.Of(GetType());

    [E2ESetup("sign in", Saves = ["admin"], Explicit = true)]
    public async Task SignIn()
    {
        Fixme(true, "a setup may not skip");
        await SaveSessionAsync("admin");
    }

    [Fact(Explicit = true)]
    [E2ESession("admin")]
    public void Consumer() { }
}

/// <summary>
/// The fixme-beside-a-failure <c>it</c>: a soft failure is kept before the test marks itself fixme. Upstream's <c>afterEach</c> throw has no xUnit
/// counterpart the base can see: it reads the verdict of the test and the session, not a throw from a derived <c>DisposeAsync</c>.
/// </summary>
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

    [Fact(Explicit = true)]
    public async Task Beside_a_failure()
    {
        await App.OpenAsync("/browser");
        await Expect
            .Soft(Screen.GetByLabel("Loads"))
            .ToHaveTextAsync("the count", timeout: TimeSpan.FromMilliseconds(200));
        Fixme(reason: "not ready");
    }
}
