// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>The helpers upstream's <c>unit/traces.test.ts</c> declares at the top of the file: a temporary project, and the pages a set of results writes.</summary>
public abstract class TracePageKit : IDisposable
{
    internal const string Entry = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    internal string Project()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-traces-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _dirs.Add(root);
        return root;
    }

    /// <summary>The failing checkout result that most of the tests in the file read.</summary>
    internal static ReportResult Failing { get; } = ReportBuilders.Result(
        id: "1a2b3c4d5e6f7a8b9c0d",
        file: "tests/checkout.e2e.ts",
        titlePath: ["checkout", "applies the coupon"],
        status: ResultStatus.Failed,
        attempts:
        [
            ReportBuilders.Attempt(
                status: AttemptStatus.Failed,
                error: ReportBuilders.Error("ASSERTION_FAILED", "expect.toHaveText failed"),
                secondaryErrors: [ReportBuilders.Error("ENGINE_FAILURE", "the browser closed early", "cleanup")],
                appLog: [new AppLogRecord(AppLogSource.Network, AppLogLevel.Error, "POST http://127.0.0.1:4100/api/coupon 500 Internal Server Error", At(2), 0)],
                steps:
                [
                    ReportBuilders.Step(
                        index: 0,
                        kind: StepKind.Agent,
                        api: "agent.act",
                        label: "add the coupon",
                        cache: new CacheInfo { Mode = "agent-concluded", Reason = "target-not-found", ReplayedActions = 1, TotalActions = 3, Entry = Entry, Detail = "at action 2 of 3, tap button \"Apply\"" },
                        cacheWrite: CacheWrite.Saved,
                        events: [ReportBuilders.Event(StepEventKind.Engine, At(1), 12, name: "tap", detail: "tap button \"Coupon\"")]),
                    ReportBuilders.Step(
                        index: 1,
                        kind: StepKind.Assertion,
                        api: "expect.toHaveText",
                        label: "getByRole(\"status\")",
                        status: StepStatus.Failed,
                        events: [ReportBuilders.Event(StepEventKind.Poll, At(3), 5000, StepEventStatus.Failed, "expect", 50, detail: "text \"$10\" (1 match) x50")]),
                ]),
        ]);

    /// <summary>A moment of the report's first second, <paramref name="second"/> seconds in.</summary>
    internal static DateTimeOffset At(int second) => ReportBuilders.ReportAt.AddSeconds(second);

    /// <summary>Writes the pages of <paramref name="results"/> into a fresh project and returns each page's directory name and text.</summary>
    internal async Task<List<(string Name, string Text)>> PagesOfAsync(params ReportResult[] results)
    {
        var root = Project();
        var dir = Path.Combine(root, ".e2e", "results");
        await TracePages.WriteTracePagesAsync(
            ReportBuilders.Document(results),
            results.Select(result => result.Id).ToHashSet(StringComparer.Ordinal),
            new WriteTracePagesOptions(root, dir, Path.Combine(root, ".e2e", "cache")));
        return [.. Directory.GetDirectories(dir).Select(name => (Path.GetFileName(name), File.ReadAllText(Path.Combine(name, "trace.md"))))];
    }

    /// <summary>The lines a page lists under its step <paramref name="index"/>, without their list markers.</summary>
    internal static List<string> StepLines(string text, int index)
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith(index + ". ", StringComparison.Ordinal));
        List<string> under = [];
        foreach (var line in lines.Skip(start + 1))
        {
            if (!line.StartsWith("   - ", StringComparison.Ordinal))
            {
                break;
            }

            under.Add(line[5..]);
        }

        return under;
    }
}
