// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace WinAppBenchmark.E2ETests.Fixtures;

/// <summary>
/// The suite's base class, the port of upstream's <c>tests/fixtures.ts</c>: <see cref="WinAppTest"/> plus
/// <see cref="OpenScenarioAsync"/>. The credential <c>benchmark</c> is the scenario's hardcoded account,
/// declared here as upstream declares it in <c>e2e.config.ts</c>.
/// </summary>
public abstract class BenchmarkTest : WinAppTest
{
    /// <summary>Every agent step includes model round trips on top of window time, and the long lists take dozens of actions: upstream gives the agentic suite 300 seconds.</summary>
    protected override TimeSpan StepTimeout => TimeSpan.FromMinutes(5);

    /// <summary>The scenario's hardcoded account, which <c>Credentials.User("benchmark")</c> reads.</summary>
    [OneTimeSetUp]
    public void DeclareTheBenchmarkAccount()
    {
        Environment.SetEnvironmentVariable("E2E_USER_BENCHMARK_USERNAME", Environment.GetEnvironmentVariable("E2E_USER_BENCHMARK_USERNAME") ?? "tester@tester.army");
        Environment.SetEnvironmentVariable("E2E_USER_BENCHMARK_PASSWORD", Environment.GetEnvironmentVariable("E2E_USER_BENCHMARK_PASSWORD") ?? "benchmark123");
    }

    /// <summary>
    /// Launches the app fresh, on its home list, and opens one scenario from it. The list is two columns of
    /// compact rows with every scenario on screen, so a scenario is one tap away and never scrolled to. A row
    /// is found by its name, "&lt;name&gt;. " and the description. A scenario is up once its row has left the
    /// tree and its own header shows the route name; Bottom Tabs, the one route without a header, shows its
    /// home tab instead.
    /// </summary>
    protected async Task OpenScenarioAsync(string name)
    {
        await App.OpenAsync();
        var row = Screen.GetByLabel($"{name}. ", exact: false);
        await Expect.That(row).ToBeVisibleAsync();
        await row.TapAsync();
        var shown = name == "Bottom Tabs" ? Screen.GetByTestId("home-tab-content") : Screen.GetByTestId("screen-title");
        await Expect.That(shown).ToBeVisibleAsync();
        await Expect.That(row).ToBeHiddenAsync();
    }

    /// <summary>
    /// Scrolls the screen down until <paramref name="target"/> is visible, the port of <c>screen.scrollUntilVisible</c>: the locator API has
    /// no such call, so the window session swipes the largest scrollable control, one screenful at a time.
    /// </summary>
    protected async Task ScrollUntilVisibleAsync(Locator target, int maxSwipes = 60)
    {
        for (var swipe = 0; swipe < maxSwipes; swipe++)
        {
            if (await target.IsVisibleAsync())
            {
                return;
            }

            await Session.SwipeAsync(ScrollDirection.Down, CancellationToken.None);
        }

        Assert.Fail($"{target} did not become visible after {maxSwipes} swipes");
    }

    /// <summary>The node with this test id in a fresh observation, for the window session calls that take a node.</summary>
    protected async Task<SemanticNode> NodeAsync(string testId)
    {
        // A screen that just changed shows its new controls a moment later.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var observation = await Session.ObserveAsync(CancellationToken.None);
            if (Flatten(observation.Roots).FirstOrDefault(node => node.TestId == testId) is { } found)
            {
                return found;
            }

            await Task.Delay(250);
        }

        throw new AssertionException($"no node with test id {testId} on the screen");
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));
}
