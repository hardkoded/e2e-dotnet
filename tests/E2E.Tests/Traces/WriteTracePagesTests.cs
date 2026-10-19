// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>Upstream's <c>describe('writeTracePages')</c> (<c>tests/unit/traces.test.ts</c>).</summary>
public sealed class WriteTracePagesTests : TracePageKit
{
    private static readonly ReportResult Passing = ReportBuilders.Result(id: new string('f', 12), titlePath: ["opens"], status: ResultStatus.Passed);

    private static readonly ReportResult Interrupted = ReportBuilders.Result(id: new string('9', 12), titlePath: ["cut short"], status: ResultStatus.Interrupted, attempts: [ReportBuilders.Attempt(AttemptStatus.Interrupted)]);

    [Fact]
    public async Task Writes_one_page_per_traced_result_into_its_tests_readable_directory_beside_what_is_there_and_returns_paths_from_the_project_root()
    {
        var root = Project();
        var dir = Path.Combine(root, ".e2e", "results");
        var evidence = Path.Combine(dir, "checkout-applies-the-coupon-1a2b3c4d5e6f7a8b", "attempt-1", "screen-at-failure.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
        await File.WriteAllTextAsync(evidence, "screen");
        var document = ReportBuilders.Document(Passing, Failing, Interrupted);

        var pages = await TracePages.WriteTracePagesAsync(document, new HashSet<string> { Failing.Id }, new WriteTracePagesOptions(root, dir, Path.Combine(root, ".e2e", "cache")));

        Assert.Equal(["checkout-applies-the-coupon-1a2b3c4d5e6f7a8b"], Directory.GetDirectories(dir).Select(Path.GetFileName));
        Assert.Equal(["attempt-1", "trace.md"], Directory.GetFileSystemEntries(Path.Combine(dir, "checkout-applies-the-coupon-1a2b3c4d5e6f7a8b")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal([new KeyValuePair<string, string>(Failing.Id, ".e2e/results/checkout-applies-the-coupon-1a2b3c4d5e6f7a8b/trace.md")], pages);
        var text = await File.ReadAllTextAsync(Path.Combine(dir, "checkout-applies-the-coupon-1a2b3c4d5e6f7a8b", "trace.md"));
        Assert.Contains($"(`.e2e/cache/{Entry}.json`)", text, StringComparison.Ordinal);
        Assert.Contains("## Also failed", text, StringComparison.Ordinal);
        Assert.Contains("**ENGINE_FAILURE** in cleanup: the browser closed early", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_a_cache_directory_outside_the_project_by_its_absolute_path()
    {
        var root = Project();
        var dir = Path.Combine(root, ".e2e", "results");
        var shared = Path.Combine(Project(), "shared");

        await TracePages.WriteTracePagesAsync(ReportBuilders.Document(Failing), new HashSet<string> { Failing.Id }, new WriteTracePagesOptions(root, dir, shared));

        var text = await File.ReadAllTextAsync(Path.Combine(dir, "checkout-applies-the-coupon-1a2b3c4d5e6f7a8b", "trace.md"));
        Assert.Contains($"(`{shared.Replace('\\', '/')}/{Entry}.json`)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tells_a_traced_passing_result_as_its_steps_with_no_failure_to_look_at()
    {
        var opened = ReportBuilders.Result(id: new string('f', 12), titlePath: ["opens"], status: ResultStatus.Passed, attempts: [ReportBuilders.Attempt(AttemptStatus.Passed, steps: [ReportBuilders.Step(0, api: "app.open", label: "/")])]);

        var page = Assert.Single(await PagesOfAsync(opened));

        Assert.Matches(@"\A# ✓ opens\n\n`[^\n]+` · passed · [^\n]+\n\n## Steps\n", page.Text);
        Assert.Contains("1. ✓ `app.open`", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Look at:", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("no error was recorded", page.Text, StringComparison.Ordinal);
    }
}
