// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Nodes;
using E2E.TUnit.Tests.EngineContract;
using TUnit.Core.Exceptions;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit.Tests.RunSelection;

/// <summary>A fixture for discovery: its tests never run, so no engine starts.</summary>
[Category("Checkout")]
public sealed class DiscoveredFixture : E2E.TUnit.E2ETest
{
    [Test]
    [Skip("only found by reflection")]
    [Category("Slow")]
    public void Is_found_by_reflection()
    {
    }

    [Test]
    [Skip("only found by reflection")]
    [Arguments(1)]
    [Arguments(2)]
    public async Task A_theory_is_one_test(int row) => await Assert.That(row).IsGreaterThan(0);

    [Test, Explicit]
    public void Is_not_judged_when_explicit()
    {
    }
}

/// <summary>A skip decided when the test runs, by a condition: it is never an outright skip.</summary>
public sealed class SkipWhenRunsAttribute() : SkipAttribute("never runs")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) => Task.FromResult(true);
}

/// <summary>A fixture the filters judge: its test is skipped when it runs, by a condition, so no engine starts.</summary>
public sealed class JudgedFixture : E2E.TUnit.E2ETest
{
    [Test]
    [SkipWhenRuns]
    public void Is_judged()
    {
    }
}

/// <summary>A fixture the filters left out in its own process: the exclusion is made before the class runs, as a run fixture makes it before the first test.</summary>
public sealed class LeftOutByTheFiltersFixture : E2E.TUnit.E2ETest
{
    [Before(HookType.Class)]
    public static void LeaveOut() =>
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutByTheFiltersFixture).FullName}.{nameof(Is_left_out)}"] = "login.cs did not change" });

    [Test, Explicit]
    public void Is_left_out() => Console.Out.WriteLine("body-ran");
}

/// <summary>A fixture whose test would start a session if the filters did not stop it; it only runs when asked for, and the tests call its setup by hand.</summary>
public sealed class LeftOutFixture : E2E.TUnit.E2ETest
{
    [Test, Explicit]
    public void Is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream collects tests by running test files): finding the tests of an assembly by reflection, and the test that skips itself
/// when the filters left it out. The tests change the process's list of left-out tests, so they run one at a time.
/// </summary>
[NotInParallel(nameof(RunSelectionTests))]
public sealed class RunSelectionTests
{
    private static string KeyOf(string method) => $"{typeof(DiscoveredFixture).FullName}.{method}";

    [Test]
    public async Task Finds_the_tests_of_the_assembly_with_their_categories_and_skip_reason()
    {
        var found = E2E.TUnit.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Single(test => test.Key == KeyOf(nameof(DiscoveredFixture.Is_found_by_reflection)));

        await Assert.That(found.Namespace).IsEqualTo("E2E.TUnit.Tests.RunSelection");
        await Assert.That(found.TypeName).IsEqualTo(nameof(DiscoveredFixture));
        await Assert.That(found.Tags.Order().ToArray()).IsEquivalentTo(["Checkout", "Slow"]);
        await Assert.That(found.SkipReason).IsEqualTo("only found by reflection");
    }

    [Test]
    public async Task Finds_a_theory_once_and_leaves_an_explicit_test_out()
    {
        var methods = E2E.TUnit.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Where(test => test.TypeName == nameof(DiscoveredFixture)).Select(test => test.Title).ToList();

        await Assert.That(methods).IsEquivalentTo([nameof(DiscoveredFixture.Is_found_by_reflection), nameof(DiscoveredFixture.A_theory_is_one_test)]);
    }

    [Test]
    public async Task Does_not_take_a_conditional_skip_for_an_outright_one()
    {
        var found = E2E.TUnit.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Single(test => test.Key == $"{typeof(JudgedFixture).FullName}.{nameof(JudgedFixture.Is_judged)}");

        await Assert.That(found.SkipReason).IsNull();
    }

    // The assertions would take the skip for a failure, so the skip is read by hand.
    private static string? SkipReasonOf(Type fixture, string method)
    {
        try
        {
            E2E.TUnit.RunSelection.SkipIfLeftOut(fixture, method);
            return null;
        }
        catch (SkipTestException ex)
        {
            return ex.Message;
        }
    }

    [Test]
    public async Task Skips_a_test_the_filters_left_out_with_their_reason()
    {
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [KeyOf("Left_out")] = "login.cs did not change" });
        try
        {
            await Assert.That(SkipReasonOf(typeof(DiscoveredFixture), "Left_out")).IsEqualTo("login.cs did not change");
            await Assert.That(SkipReasonOf(typeof(DiscoveredFixture), "Kept")).IsNull();
        }
        finally
        {
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public async Task Keeps_what_one_run_fixture_left_out_when_another_adds_its_own()
    {
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [KeyOf("A")] = "one" });
        try
        {
            E2E.TUnit.RunSelection.Add(new Dictionary<string, string> { [KeyOf("B")] = "two" });

            await Assert.That(SkipReasonOf(typeof(DiscoveredFixture), "A")).IsEqualTo("one");
            await Assert.That(SkipReasonOf(typeof(DiscoveredFixture), "B")).IsEqualTo("two");
        }
        finally
        {
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public async Task Ends_a_test_the_filters_left_out_as_skipped_before_its_session_starts(CancellationToken cancellationToken)
    {
        var context = TUnitContext.Current!;
        var method = context.Metadata.TestDetails.MethodName;
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutFixture).FullName}.{method}"] = "login.cs did not change" });
        try
        {
            // The start hook of the test base class is called by hand, as TUnit calls it; no session exists for it to start when the test is left out.
            string? reason = null;
            try
            {
                await new LeftOutFixture().StartE2ESessionAsync(context, cancellationToken);
            }
            catch (SkipTestException ex)
            {
                reason = ex.Message;
            }

            await Assert.That(reason).IsEqualTo("login.cs did not change");
        }
        finally
        {
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public async Task TUnit_skips_a_test_the_filters_left_out_without_running_its_body()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(LeftOutByTheFiltersFixture));

        await Assert.That(results["Is_left_out"].Result).IsEqualTo("Skipped");
        await Assert.That(output).DoesNotContain("body-ran");
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

    [Test]
    public async Task The_run_fixture_leaves_out_what_the_filters_name_and_reports_it(CancellationToken cancellationToken)
    {
        var dir = FeatureRepo();
        try
        {
            await new LeaveOutJudged(dir, "main").InitializeAsync(typeof(RunSelectionTests).Assembly, cancellationToken);

            await Assert.That(SkipReasonOf(typeof(JudgedFixture), nameof(JudgedFixture.Is_judged))).IsEqualTo("left out");
            var run = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, ".e2e", "report.json")))!["run"]!;
            await Assert.That((string)run["filters"]![0]!["name"]!).IsEqualTo("judged");
            var result = run["results"]!.AsArray().Single()!;
            await Assert.That((string)result["skip"]!["cause"]!).IsEqualTo("filtered");
            await Assert.That((string)result["skip"]!["filter"]!).IsEqualTo("judged");
            await Assert.That((bool)result["selected"]!).IsFalse();
            await Assert.That((int)run["changes"]!["files"]!).IsEqualTo(1);
        }
        finally
        {
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
            DeleteRepo(dir);
        }
    }

    [Test]
    public async Task The_run_fixture_filters_nothing_without_a_base_ref(CancellationToken cancellationToken)
    {
        var dir = FeatureRepo();
        try
        {
            await new LeaveOutJudged(dir, null).InitializeAsync(typeof(RunSelectionTests).Assembly, cancellationToken);

            await Assert.That(SkipReasonOf(typeof(JudgedFixture), nameof(JudgedFixture.Is_judged))).IsNull();
            await Assert.That(File.Exists(Path.Combine(dir, ".e2e", "report.json"))).IsFalse();
        }
        finally
        {
            DeleteRepo(dir);
        }
    }

    [Test]
    public async Task A_project_with_no_run_fixture_runs_none_of_the_hook()
    {
        // The hook of a library runs for every project that references it, so it is opt-in: only a public class derived from E2ERunFixture
        // with a public parameterless constructor takes part. LeaveOutJudged has constructor arguments, so it does not.
        await Assert.That(E2ERunFixture.DeclaredIn(typeof(E2ERunFixture).Assembly)).IsEmpty();
        await Assert.That(E2ERunFixture.DeclaredIn(typeof(RunSelectionTests).Assembly).Select(fixture => fixture.GetType())).IsEquivalentTo([typeof(RunHookProbe)]);
    }

    [Test]
    public async Task The_hook_starts_the_run_fixture_a_project_declares()
    {
        var (_, output) = await NestedSessionRun.RunAsync(typeof(RunHookProbeTests));

        await Assert.That(output).Contains("run-fixture-started");
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

    // Git writes its objects read-only, which a recursive delete on Windows refuses.
    private static void DeleteRepo(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(dir, true);
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
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} exited with {process.ExitCode}");
        }
    }
}

/// <summary>The one run fixture this project declares. It does nothing but note, in a nested run, that the hook started it.</summary>
public sealed class RunHookProbe : E2ERunFixture
{
    protected override E2EConfig Config
    {
        get
        {
            if (Environment.GetEnvironmentVariable(NestedSessionRun.ExitFileVariable) is { Length: > 0 } file)
            {
                File.AppendAllText(file, "run-fixture-started" + Environment.NewLine);
            }

            return E2EConfig.Parse("{}", AppContext.BaseDirectory, _ => null);
        }
    }
}

/// <summary>A test with no session, for the nested run that checks the run fixture starts.</summary>
public sealed class RunHookProbeTests
{
    [Test, Explicit]
    public Task Runs() => Task.CompletedTask;
}
