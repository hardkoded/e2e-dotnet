// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.RunEvents;

/// <summary>.NET-only (upstream's runner owns the run's start, plan, and errors): what a run fixture tells the process's events.</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class HostRunTests
{
    private static DiscoveredTest Found(string typeName, string title, string? skip = null, string @namespace = "App.Tests") =>
        new($"{@namespace}.{typeName}.{title}", @namespace, typeName, title, [], skip);

    private static List<RunEventFact> Listen(out IDisposable listening)
    {
        var facts = new List<RunEventFact>();
        listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", runEvent =>
        {
            lock (facts)
            {
                facts.Add(runEvent.Fact);
            }
        }));
        return facts;
    }

    [Fact]
    public void Plans_the_tests_the_run_will_start_per_class_without_the_ignored_and_the_left_out()
    {
        var facts = Listen(out var listening);
        using (listening)
        {
            HostRun.Planned(
                "web",
                [
                    Found("CartTests", "Adds_an_item"),
                    Found("CartTests", "Removes_an_item"),
                    Found("CartTests", "Is_left_out"),
                    Found("LoginTests", "Is_ignored", skip: "not yet"),
                    Found("LoginTests", "Signs_in"),
                    Found("Outer+Inner", "Nests", @namespace: "App.Tests"),
                ],
                new Dictionary<string, string> { ["App.Tests.CartTests.Is_left_out"] = "did not change" });
        }

        var plan = Assert.IsType<PlanEvent>(Assert.Single(facts));
        Assert.Equal(4, plan.Total);
        Assert.Equal(
            [("App.Tests.CartTests", "web", 2), ("App.Tests.LoginTests", "web", 1), ("App.Tests.Outer+Inner", "web", 1)],
            plan.Files);
    }

    [Fact]
    public void Tells_the_step_that_finds_the_tests_with_how_long_it_took()
    {
        var facts = Listen(out var listening);
        using (listening)
        {
            Assert.Equal(7, HostRun.Collect(() => 7));
        }

        Assert.Equal(2, facts.Count);
        Assert.Equal(new SetupEvent(new CollectSetupStep(), SetupState.Started), facts[0]);
        var finished = Assert.IsType<SetupEvent>(facts[1]);
        Assert.Equal((new CollectSetupStep(), SetupState.Finished, null), (finished.Step, finished.State, finished.Outcome));
    }

    [Fact]
    public void Tells_the_run_start_with_its_targets_and_where_its_artifacts_go()
    {
        var facts = Listen(out var listening);
        var root = Directory.CreateTempSubdirectory("e2e-host-run-").FullName;
        try
        {
            using (listening)
            {
                HostRun.Started(E2EConfig.Default(root));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var started = Assert.IsType<RunStartedEvent>(Assert.Single(facts));
        Assert.Equal((AttemptReport.RunId, root), (started.RunId, started.ProjectRoot));
        Assert.Equal(Path.Combine(root, ".e2e", "results"), started.ArtifactsRoot);
        Assert.NotEmpty(started.Targets);
        Assert.Null(started.Agents);
    }

    [Fact]
    public void Tells_a_failure_that_stopped_the_run_as_a_run_error_without_hiding_it()
    {
        var facts = Listen(out var listening);
        var root = Directory.CreateTempSubdirectory("e2e-host-run-").FullName;
        try
        {
            using (listening)
            {
                HostRun.Failed(E2EConfig.Default(root), new ConfigurationException("INVALID_CONFIG", "the filter is wrong"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var error = Assert.IsType<RunErrorEvent>(Assert.Single(facts)).Error;
        Assert.Equal(("INVALID_CONFIG", "the filter is wrong", ErrorCategory.Configuration), (error.Code, error.Message, error.Category));
    }
}
