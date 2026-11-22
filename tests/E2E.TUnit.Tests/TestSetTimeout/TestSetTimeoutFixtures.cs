// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.TUnit.Tests.TestSetTimeout;

/// <summary>Where the fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started.</summary>
internal static class TestSetTimeoutResults
{
    private const string RootVariable = "E2E_TEST_SET_TIMEOUT_ROOT";

    // The nested run is a process of its own: it finds the directory its parent chose through the environment it inherits.
    private static readonly string Root = ChooseRoot();

    private static string ChooseRoot()
    {
        var inherited = Environment.GetEnvironmentVariable(RootVariable);
        if (!string.IsNullOrEmpty(inherited))
        {
            return inherited;
        }

        var root = Path.Combine(Path.GetTempPath(), "e2e-test-set-timeout-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(RootVariable, root);
        return root;
    }

    /// <summary>The <c>results</c> directory of the run of <paramref name="fixture"/>; <c>report.json</c> is its sibling.</summary>
    public static string Of(Type fixture) => Path.Combine(Root, fixture.Name, "results");

    public static string ReportOf(Type fixture) => Path.Combine(Path.GetDirectoryName(Of(fixture))!, "report.json");

    /// <summary>Chooses the directory, which hands it to a nested process started afterwards.</summary>
    public static void Prepare() => _ = Root;

    public static void Clear()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>The tests upstream runs with a <c>timeout</c> of 300 ms: here 10 s, past which the body sleeps 12 s.</summary>
public sealed class ShortTimeoutFixture : E2E.TUnit.E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => TestSetTimeoutResults.Of(GetType());

    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    [Test, Explicit]
    public async Task SetTimeout_fixme_past_the_first_timeout()
    {
        SetTimeout(TimeSpan.FromSeconds(60));
        await Task.Delay(TimeSpan.FromSeconds(12));
        await App.OpenAsync("/");
        Fixme(reason: "not ready");
    }
}
