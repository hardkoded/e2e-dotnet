// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests;

/// <summary>A throwaway project directory: where a run keeps its cache and results. Upstream's <c>FixtureProject</c> (<c>tests/helpers/run-project.ts</c>).</summary>
internal sealed class FixtureProject(string dir)
{
    public string Dir { get; } = dir;

    public void Cleanup()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}

/// <summary>What <see cref="RunProject.RunExistingAsync"/> runs. Upstream's <c>RunProjectOptions</c>, with the tests in memory (a test file's source is a closure) and the engine a config file cannot hold.</summary>
internal sealed record RunProjectOptions
{
    /// <summary>The fixture's tests: upstream's fixture test file, as bodies over the session.</summary>
    public required IReadOnlyList<InMemoryTest> Tests { get; init; }

    /// <summary>The engine every test runs on.</summary>
    public required IEngine Engine { get; init; }

    /// <summary>Overrides of the fixture config, as the config's JSON.</summary>
    public string? Config { get; init; }

    /// <summary>The agents given in code, by name.</summary>
    public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }

    /// <summary>The app's URL; the run resolves against it as the fixture app's.</summary>
    public string BaseUrl { get; init; } = "https://counter.test";

    /// <summary>Upstream's <c>runOptions.env</c>: the run environment, in place of the fixture's (<c>CI</c> unset).</summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>Upstream's <c>runOptions.noCache</c>.</summary>
    public bool NoCache { get; init; }

    /// <summary>Upstream's <c>runOptions.strictCache</c>.</summary>
    public bool StrictCache { get; init; }
}

/// <summary>Creates throwaway fixture projects and runs them through the runner. Upstream's <c>tests/helpers/run-project.ts</c>.</summary>
internal static class RunProject
{
    /// <summary>The virtual file the fixture's tests are registered in.</summary>
    public const string SuiteFile = "tests/act.e2e";

    public static FixtureProject CreateProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "e2e-run-project", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return new FixtureProject(dir);
    }

    /// <summary>
    /// Runs an existing project directory. Repeated runs share on-disk state, which is what cache tests need: the second run must see what the
    /// first wrote. The run's environment is the fixture's (outside CI) unless <see cref="RunProjectOptions.Env"/> sets one.
    /// </summary>
    public static Task<RunOutcome> RunExistingAsync(FixtureProject project, RunProjectOptions options)
    {
        var config = options.Config is { } extra
            ? extra
            : "{}";
        return Runner.RunAsync(new RunOptions
        {
            Cwd = project.Dir,
            RawConfig = ConfigWithTarget(config, options.BaseUrl),
            Tests = new InMemoryTests(SuiteFile, options.Tests),
            Env = options.Env ?? new Dictionary<string, string?> { ["CI"] = "" },
            NoCache = options.NoCache,
            StrictCache = options.StrictCache,
            Engine = options.Engine,
            Agents = options.Agents,
        });
    }

    /// <summary>
    /// Finds one result by test title. A miss names the titles the run did produce and its run-level errors, since an empty run usually means
    /// collection failed and the error says which. Upstream's <c>resultByTitle</c>.
    /// </summary>
    public static E2E.Reports.ReportResult ResultByTitle(RunOutcome outcome, string title)
    {
        return outcome.Report.Run.Results.FirstOrDefault(candidate => candidate.TitlePath.SequenceEqual([title]))
            ?? throw new InvalidOperationException($"no result titled \"{title}\"; got: {string.Join(", ", outcome.Report.Run.Results.Select(result => string.Join(' ', result.TitlePath)))}; run errors: {string.Join("; ", outcome.Report.Run.Errors.Select(error => error.Message))}");
    }

    // The fixture config's target is the web target on the fixture app, as upstream's defaultTargets is.
    private static string ConfigWithTarget(string json, string baseUrl)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        if (!node.ContainsKey("targets") && !node.ContainsKey("target"))
        {
            node["targets"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = "web",
                ["app"] = new System.Text.Json.Nodes.JsonObject { ["url"] = baseUrl },
            });
        }

        return node.ToJsonString();
    }
}
