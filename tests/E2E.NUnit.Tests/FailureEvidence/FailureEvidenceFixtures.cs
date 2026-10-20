// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.FailureEvidence;

/// <summary>
/// The tests of upstream's <c>failure-evidence.test.ts</c> that need a teardown, as an NUnit body would write them: the
/// <c>afterEach</c> navigates away from the page the test failed on. The fixture is explicit, so a normal run never starts it.
/// </summary>
[Explicit("Run by FailureEvidenceTests")]
public sealed class TeardownFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    /// <summary>Where the fixture's attempts write their evidence.</summary>
    public static string Results { get; } = Path.Combine(Path.GetTempPath(), "e2e-failure-evidence-" + Guid.NewGuid().ToString("N"));

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => Results;

    [TearDown]
    public async Task NavigateAway()
    {
        await App.OpenAsync("/about");
    }

    [Test]
    public async Task Fails_then_tears_down()
    {
        await App.OpenAsync("/browser");
        await Screen.GetByRole("button", "Submit now").TapAsync(new ActionOptions { Timeout = Short });
    }

    [Test]
    public async Task Soft_fails_then_tears_down()
    {
        await App.OpenAsync("/browser");
        await Expect.Soft(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: Short);
    }
}
