// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;
using E2E.Tests.FailureEvidence;
using static E2E.Tests.LastRun.LastRunDocuments;

namespace E2E.Tests.Output;

/// <summary>
/// Upstream's <c>describe('output')</c> (<c>tests/integration/output.test.ts</c>, tester-army/e2e <c>50fa7af</c>), for the two <c>it</c>s about the artifact
/// tree a <c>--last-failed</c> rerun starts from. Both are <c>Partial_</c>: the port has no runner, so the host's selection stands for the rerun's start (the
/// previous report is written by hand, and the rerun's attempt is a session over an engine that masks its screenshots), and the port never wipes the tree
/// for a full run. Not ported here: the other <c>it</c>s of the describe (they need the runner).
/// </summary>
public sealed class OutputTests : IDisposable
{
    private const string Title = "fails on purpose";

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private E2EConfig Project(string output = "out")
    {
        var dir = Directory.CreateTempSubdirectory("e2e-output-").FullName;
        _dirs.Add(dir);
        return E2EConfig.Parse("{\"output\":\"" + output + "\"}", Directory.ResolveLinkTarget(dir, true)?.FullName ?? dir, _ => null);
    }

    private static string Results(E2EConfig config) => Path.Combine(config.Output, "results");

    private static string Segment => AttemptReport.ResultSegmentOf(null, Title);

    // The report of a run whose failed test left one screenshot at the report path.
    private static void WritePrevious(E2EConfig config, string artifact)
    {
        var attempt = Result(("artifacts", new JsonArray(Result(("path", artifact)))));
        Directory.CreateDirectory(config.Output);
        File.WriteAllText(Path.Combine(config.Output, "report.json"), ReportText([Result(("id", "fails"), ("status", "failed"), ("attempts", new JsonArray(attempt)))]));
    }

    private static void Plant(E2EConfig config, string reportPath, string content)
    {
        var file = Path.Combine(Results(config), Path.Combine(reportPath.Split('/')));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static string Read(E2EConfig config, string reportPath) => File.ReadAllText(Path.Combine(Results(config), Path.Combine(reportPath.Split('/'))));

    private static bool Exists(E2EConfig config, string reportPath) => File.Exists(Path.Combine(Results(config), Path.Combine(reportPath.Split('/'))));

    private static Task<HostSelection> RerunAsync(E2EConfig config) =>
        HostTestFilters.ExcludeAsync(config, [], null, [], _ => { }, CancellationToken.None, lastFailed: true);

    /// <summary>Partial: a full run does not clear the tree (the port never wipes it); a rerun clears all but what the report names.</summary>
    [Fact]
    public async Task Partial_Clears_the_artifact_tree_when_a_run_starts_and_reads_last_failed_from_the_output_the_run_names()
    {
        var config = Project();
        var kept = Segment + "/attempt-1/screenshots/failure.png";
        WritePrevious(config, kept);
        Plant(config, kept, "evidence");
        Plant(config, "stale/left-over.txt", "from a run long ago");

        var rerun = await RerunAsync(config);

        Assert.False(Exists(config, "stale/left-over.txt"));
        Assert.False(Directory.Exists(Path.Combine(Results(config), "stale")));
        Assert.True(Exists(config, kept));
        Assert.Equal("rerun-1", rerun.RerunDir);

        // --output over the config: no report there yet, so nothing to rerun from.
        var elsewhere = await Assert.ThrowsAsync<ConfigurationException>(() => RerunAsync(Project("other")));
        Assert.Equal("NO_LAST_RUN", elsewhere.Code);
    }

    /// <summary>Partial: the rerun's attempt is a session and the second and third starts are the host's selection; there is no runner, and no full run to start from an empty tree.</summary>
    [Fact]
    public async Task Partial_Keeps_the_evidence_a_last_failed_rerun_folds_in_byte_for_byte_and_files_the_reruns_own_beside_it()
    {
        var config = Project();
        var first = Segment + "/attempt-1/screenshots/failure.png";
        var inRerun = (int n) => first.Replace("/attempt-1/", $"/rerun-{n}/attempt-1/", StringComparison.Ordinal);
        WritePrevious(config, first);
        Plant(config, first, "the first run's screenshot");
        Plant(config, "planted.txt", "named by no report");

        var rerun = await RerunAsync(config);
        Assert.Equal("rerun-1", rerun.RerunDir);
        Assert.Equal("the first run's screenshot", Read(config, first));
        Assert.False(Exists(config, "planted.txt"));

        // The rerun's attempt fails again and files its evidence beside the first run's.
        await FailAsync(config, rerun.RerunDir);
        Assert.True(Exists(config, inRerun(1)));
        Assert.Equal("the first run's screenshot", Read(config, first));
        Assert.Contains(inRerun(1), File.ReadAllText(Path.Combine(config.Output, "report.json")), StringComparison.Ordinal);

        // The next rerun keeps what the report it reruns names, the first run's evidence no more.
        var again = await RerunAsync(config);
        Assert.Equal("rerun-2", again.RerunDir);
        Assert.True(Exists(config, inRerun(1)));
        Assert.False(Exists(config, first));
    }

    private static async Task FailAsync(E2EConfig config, string? rerunDir)
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new EvidenceEngine("Submit"),
            TestTitle = Title,
            BaseUrl = "http://127.0.0.1:4599",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ProjectRoot = config.ProjectRoot,
            ResultsDirectory = Results(config),
            RerunDir = rerunDir,
            CacheEnabled = false,
            Trace = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target),
        });
        try
        {
            await session.App.OpenAsync("/");
            var error = await Assert.ThrowsAnyAsync<E2EException>(() => session.Screen.GetByRole("button", "Submit now").TapAsync());
            session.Complete(error);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }
}
