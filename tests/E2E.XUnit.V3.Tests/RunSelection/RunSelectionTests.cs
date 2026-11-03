// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Nodes;

namespace E2E.XUnit.V3.Tests.RunSelection;

/// <summary>A fixture for discovery: its tests never run, so no engine starts.</summary>
[Trait("TestCategory", "Checkout")]
public sealed class DiscoveredFixture : E2ETest
{
    [Fact(Skip = "only found by reflection")]
    [Trait("TestCategory", "Slow")]
    [Trait("Owner", "nobody")]
    public void Is_found_by_reflection()
    {
    }

    [Theory(Skip = "only found by reflection")]
    [InlineData(1)]
    [InlineData(2)]
    public void A_theory_is_one_test(int row) => Assert.True(row > 0);

    [Fact(Explicit = true)]
    public void Is_not_judged_when_explicit()
    {
    }
}

/// <summary>A fixture the filters judge: its test is skipped when it runs, by a condition that is never met, so no engine starts.</summary>
public sealed class JudgedFixture : E2ETest
{
    public static bool Never => false;

    [Fact(Skip = "never runs", SkipUnless = nameof(Never))]
    public void Is_judged()
    {
    }
}

/// <summary>A fixture whose test would start a session if the filters did not stop it; it only runs when asked for, and the tests call its setup by hand.</summary>
public sealed class LeftOutFixture : E2ETest
{
    [Fact(Explicit = true)]
    public void Is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream collects tests by running test files): finding the tests of an assembly by reflection, and the test that skips itself
/// when the filters left it out.
/// </summary>
[Collection(nameof(RunSelectionCollection))]
public sealed class RunSelectionTests
{
    private static string KeyOf(string method) => $"{typeof(DiscoveredFixture).FullName}.{method}";

    [Fact]
    public void Finds_the_tests_of_the_assembly_with_their_categories_and_skip_reason()
    {
        var found = E2E.XUnit.V3.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Single(test => test.Key == KeyOf(nameof(DiscoveredFixture.Is_found_by_reflection)));

        Assert.Equal("E2E.XUnit.V3.Tests.RunSelection", found.Namespace);
        Assert.Equal(nameof(DiscoveredFixture), found.TypeName);
        Assert.Equal(["Checkout", "Slow"], found.Tags.Order());
        Assert.Equal("only found by reflection", found.SkipReason);
    }

    [Fact]
    public void Finds_a_theory_once_and_leaves_an_explicit_test_out()
    {
        var methods = E2E.XUnit.V3.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Where(test => test.TypeName == nameof(DiscoveredFixture)).Select(test => test.Title).ToList();

        Assert.Equal([nameof(DiscoveredFixture.Is_found_by_reflection), nameof(DiscoveredFixture.A_theory_is_one_test)], methods);
    }

    [Fact]
    public void Does_not_take_a_conditional_skip_for_an_outright_one()
    {
        var found = E2E.XUnit.V3.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Single(test => test.Key == $"{typeof(JudgedFixture).FullName}.{nameof(JudgedFixture.Is_judged)}");

        Assert.Null(found.SkipReason);
    }

    // The xUnit assertions drop an exception that skips a test, so the skip is read by hand.
    private static string? SkipReasonOf(Type fixture, string method)
    {
        try
        {
            E2E.XUnit.V3.RunSelection.SkipIfLeftOut(fixture, method);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    [Fact]
    public void Skips_a_test_the_filters_left_out_with_their_reason()
    {
        E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string> { [KeyOf("Left_out")] = "login.cs did not change" });
        try
        {
            // xUnit v3 reads this prefix as a skip, and the rest as its reason.
            Assert.Equal("$XunitDynamicSkip$login.cs did not change", SkipReasonOf(typeof(DiscoveredFixture), "Left_out"));
            Assert.Null(SkipReasonOf(typeof(DiscoveredFixture), "Kept"));
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Fact]
    public void Keeps_what_one_run_fixture_left_out_when_another_adds_its_own()
    {
        E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string> { [KeyOf("A")] = "one" });
        try
        {
            E2E.XUnit.V3.RunSelection.Add(new Dictionary<string, string> { [KeyOf("B")] = "two" });

            Assert.Equal("$XunitDynamicSkip$one", SkipReasonOf(typeof(DiscoveredFixture), "A"));
            Assert.Equal("$XunitDynamicSkip$two", SkipReasonOf(typeof(DiscoveredFixture), "B"));
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Fact]
    public async Task Ends_a_test_the_filters_left_out_as_skipped_before_its_session_starts()
    {
        var method = Xunit.TestContext.Current.Test!.TestCase.TestMethodName!;
        E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutFixture).FullName}.{method}"] = "login.cs did not change" });
        try
        {
            // The setup of the test base class is called by hand, as xUnit calls it; no session exists for it to start when the test is left out.
            string? reason = null;
            try
            {
                await new LeftOutFixture().InitializeAsync();
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.Message;
            }

            Assert.Equal("$XunitDynamicSkip$login.cs did not change", reason);
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    private sealed class LeaveOutJudged(string root, string? changedSince) : E2ERunFixture
    {
        private sealed class Judged : ITestFilter
        {
            public string Name => "judged";

            public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) =>
                Task.FromResult(new TestFilterResult { Exclude = tests.Where(test => test.TitlePath[0] == nameof(JudgedFixture)).ToDictionary(test => test.Id, _ => "left out") });
        }

        protected override IReadOnlyList<ITestFilter> Filters => [new Judged()];

        protected override string? ChangedSince => changedSince;

        protected override E2EConfig Config => E2EConfig.Parse("{}", root, _ => null);
    }

    [Fact]
    public async Task The_run_fixture_leaves_out_what_the_filters_name_and_reports_it()
    {
        var dir = FeatureRepo();
        try
        {
            await ((IAsyncLifetime)new LeaveOutJudged(dir, "main")).InitializeAsync();

            Assert.Equal("$XunitDynamicSkip$left out", SkipReasonOf(typeof(JudgedFixture), nameof(JudgedFixture.Is_judged)));
            var run = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, ".e2e", "report.json")))!["run"]!;
            Assert.Equal("judged", (string)run["filters"]![0]!["name"]!);
            var result = Assert.Single(run["results"]!.AsArray())!;
            Assert.Equal("filtered", (string)result["skip"]!["cause"]!);
            Assert.Equal("judged", (string)result["skip"]!["filter"]!);
            Assert.False((bool)result["selected"]!);
            Assert.Equal(1, (int)run["changes"]!["files"]!);
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task The_run_fixture_filters_nothing_without_a_base_ref()
    {
        var dir = FeatureRepo();
        try
        {
            await ((IAsyncLifetime)new LeaveOutJudged(dir, null)).InitializeAsync();

            Assert.Null(SkipReasonOf(typeof(JudgedFixture), nameof(JudgedFixture.Is_judged)));
            Assert.False(File.Exists(Path.Combine(dir, ".e2e", "report.json")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string FeatureRepo()
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
        Assert.Equal(0, process.ExitCode);
    }
}

/// <summary>The tests that change the process's list of left-out tests run one at a time.</summary>
[CollectionDefinition(nameof(RunSelectionCollection), DisableParallelization = true)]
public sealed class RunSelectionCollection;
