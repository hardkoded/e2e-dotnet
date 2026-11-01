// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E;
using E2E.NUnit.Tests.SkipAfterFailure;
using E2E.NUnit.Tests.Testbed;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.RunSelection;

/// <summary>Two real-model tests that a nested run starts when the filters let them. Needs <c>e2e login github-copilot</c>.</summary>
[Explicit("Run by FilteredRealModelTests")]
[Category("RealModel")]
public sealed class FilteredAgentFixture : ControlInventoryTest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """
        {
          "cache": { "mode": "off" },
          "agents": { "default": { "model": "claude-sonnet-5.5", "provider": "copilot" } }
        }
        """,
        AppContext.BaseDirectory);

    protected override E2EConfig Config => FixtureConfig;

    [Test]
    public async Task Kept_test_picks_a_radio()
    {
        await Agent.ActAsync("pick Express delivery");
        await Expect.That(Screen.GetByLabel("Delivery state")).ToHaveTextAsync("Express");
    }

    [Test]
    public void Left_out_test_would_fail_if_it_started() => Assert.Fail("The filters left this test out, so it must not start.");
}

/// <summary>
/// .NET-only (upstream collects tests by running test files): the filters decide which tests of a real-model run start. One filter leaves
/// one test out; another keeps every test.
/// </summary>
[Category("RealModel")]
public sealed class FilteredRealModelTests
{
    private sealed class LeaveOut(string title) : ITestFilter
    {
        public string Name => "leave-out";

        public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) =>
            Task.FromResult(new TestFilterResult
            {
                Exclude = tests.Where(test => test.TitlePath[^1] == title).ToDictionary(test => test.Id, _ => "left out by the test"),
            });
    }

    private static async Task<IReadOnlyDictionary<string, string>> ExcludeAsync(string dir, params ITestFilter[] filters)
    {
        var found = E2E.NUnit.RunSelection.Discover([typeof(FilteredAgentFixture)], typeof(FilteredAgentFixture).Assembly);
        return await HostTestFilters.ExcludeAsync(E2EConfig.Parse("{}", dir, _ => null), filters, "main", found, _ => { }, CancellationToken.None);
    }

    internal static string FeatureRepo()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-filtered-run-").FullName;
        Git(dir, "init", "--quiet", "--initial-branch=main");
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a\n");
        Git(dir, "add", ".");
        Git(dir, "commit", "--quiet", "-m", "base");
        Git(dir, "checkout", "--quiet", "-b", "feature");
        File.WriteAllText(Path.Combine(dir, "a.txt"), "b\n");
        Git(dir, "commit", "--quiet", "-am", "feature");
        return dir;
    }

    private static void Git(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false" }.Concat(args))
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.Zero, $"git {string.Join(' ', args)}");
    }

    internal static void RemoveRepo(string dir) => Directory.Delete(dir, true);

    [Test]
    public async Task Starts_the_kept_test_and_ignores_the_one_the_filter_left_out()
    {
        var dir = FeatureRepo();
        try
        {
            E2E.NUnit.RunSelection.Set(await ExcludeAsync(dir, new LeaveOut(nameof(FilteredAgentFixture.Left_out_test_would_fail_if_it_started))));

            Assert.That(NestedRun.Run(nameof(FilteredAgentFixture.Kept_test_picks_a_radio)).ResultState.Status, Is.EqualTo(TestStatus.Passed));
            var left = NestedRun.Run(nameof(FilteredAgentFixture.Left_out_test_would_fail_if_it_started));
            Assert.That(left.ResultState.Status, Is.EqualTo(TestStatus.Skipped));
            Assert.That(left.Message, Does.EndWith("left out by the test"));
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
            RemoveRepo(dir);
        }
    }

    [Test]
    public async Task Starts_every_test_when_the_filter_keeps_all()
    {
        var dir = FeatureRepo();
        try
        {
            E2E.NUnit.RunSelection.Set(await ExcludeAsync(dir, new LeaveOut("no such test")));

            Assert.That(NestedRun.Run(nameof(FilteredAgentFixture.Kept_test_picks_a_radio)).ResultState.Status, Is.EqualTo(TestStatus.Passed));

            // Nothing was left out, so the test starts, and it fails by its own assertion.
            Assert.That(NestedRun.Run(nameof(FilteredAgentFixture.Left_out_test_would_fail_if_it_started)).ResultState.Status, Is.EqualTo(TestStatus.Failed));
        }
        finally
        {
            RemoveRepo(dir);
        }
    }
}
