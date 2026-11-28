// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

#pragma warning disable CS0618 // The three tests run in the order upstream's file declares them; NUnit has no other way to order them.

namespace E2E.NUnit.Tests.TestStep;

/// <summary>
/// A base for the fixtures of <c>tests/integration/test-step.test.ts</c>: explicit, so a normal run never starts them. The port's clock starts before the
/// engine launches, so every timeout is longer than upstream's (<see cref="TestStepInARunTests"/> says by how much).
/// </summary>
public abstract class TestStepFixtureBase : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    protected override string? ResultsDirectory => TestStepResults.Of(GetType());
}

/// <summary>Where each fixture's attempts and <c>report.json</c> go, so a test reads the report of the run it started.</summary>
internal static class TestStepResults
{
    private static readonly string Root = Path.Combine(
        Path.GetTempPath(),
        "e2e-test-step-" + Guid.NewGuid().ToString("N")
    );

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

/// <summary>Runs tests of this assembly through a runner of its own, as <c>NestedRun</c> does one, and keeps what each result says.</summary>
internal static class NestedTestStepRun
{
    public static IReadOnlyList<ITestResult> Run(params string[] testNames)
    {
        return Task.Run(() =>
            {
                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(typeof(NestedTestStepRun).Assembly, new Dictionary<string, object>());
                var names = string.Concat(testNames.Select(name => $"<name>{name}</name>"));
                var result = runner.Run(TestListener.NULL, TestFilter.FromXml($"<filter><or>{names}</or></filter>"));
                return Leaves(result).ToList();
            })
            .GetAwaiter()
            .GetResult();
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) =>
        result.HasChildren ? result.Children.SelectMany(Leaves) : [result];
}

/// <summary>The suite's first four tests: a <c>[SetUp]</c> step opens the app.</summary>
[Explicit("Run by TestStepInARunTests")]
public sealed class StepInARunFixture : TestStepFixtureBase
{
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    // Upstream's beforeEach.
    [SetUp]
    public Task OpenTheApp() => Step("Open the app", async () => await App.OpenAsync("/"));

    [Test]
    public async Task Reads_the_heading()
    {
        var text = await Step("Read the heading", async () => await Step("Find it", async () =>
            {
                await Screen.GetByRole("heading").First().WaitForAsync();
                return "found";
            }));
        if (text != "found")
        {
            throw new InvalidOperationException("the step did not return what its body did");
        }
    }

    [Test]
    public async Task Taps_a_missing_button() =>
        await Step("Log in as test user", async () =>
            await Screen
                .GetByRole("button", "No such button")
                .TapAsync(new TapOptions { Timeout = TimeSpan.FromMilliseconds(200) })
        );

    [Test]
    public async Task Skips_from_inside_a_step() => await Step("Check the platform", () =>
        {
            Fixme(reason: "not on web");
            return Task.CompletedTask;
        });

    [Test]
    public async Task Hangs_in_a_step() => await Step("Wait for nothing", () => new TaskCompletionSource().Task);
}

/// <summary><c>const TIMEOUTS</c> of the suite: the first test, with a call running in the step when the timeout cuts it.</summary>
[Explicit("Run by TestStepAroundATestTimeoutTests")]
public sealed class StepAroundATestTimeoutFixture : TestStepFixtureBase
{
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    [Test, Order(1)]
    public async Task Times_out_in_a_call()
    {
        await App.OpenAsync("/");
        await Step("Wait for a missing button", async () =>
            await Screen
                .GetByRole("button", "No such button")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = TimeSpan.FromSeconds(60) })
        );
    }

    [Test, Order(2)]
    public async Task Times_out_then_steps_late()
    {
        await App.OpenAsync("/");
        await Task.Delay(TimeSpan.FromSeconds(12));
        await Step("Late", () => Task.Delay(TimeSpan.FromMilliseconds(200)));
    }

    [Test, Order(3)]
    public async Task Runs_next()
    {
        await App.OpenAsync("/");
        await Screen.GetByRole("heading").First().WaitForAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}
