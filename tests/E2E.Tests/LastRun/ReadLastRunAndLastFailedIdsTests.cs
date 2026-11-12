// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static E2E.Tests.LastRun.LastRunDocuments;
using Last = E2E.Reports.LastRun;

namespace E2E.Tests.LastRun;

/// <summary>
/// Upstream's <c>describe('readLastRun and lastFailedIds')</c> (<c>tests/unit/last-run.test.ts</c>, tester-army/e2e <c>50fa7af</c>). The documents are
/// built as JSON, as upstream's are, and written to a file that <c>ReadLastRunAsync</c> reads.
/// </summary>
public sealed partial class ReadLastRunAndLastFailedIdsTests : IDisposable
{
    private readonly List<string> _roots = [];

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<string[]> ReadLastFailedAsync(string file) => [.. Last.LastFailedIds(await Last.ReadLastRunAsync(file))];

    private static JsonObject Skipped(string id, string cause, string reason) =>
        Result(("id", id), ("status", "skipped"), ("skip", Result(("cause", cause), ("reason", reason))));

    [Fact]
    public async Task Collects_the_ids_of_the_results_that_did_not_pass_or_that_the_run_never_carried_out()
    {
        var ids = await ReadLastFailedAsync(ReportFile(
            _roots,
            ReportText(
            [
                Result(("id", "passed"), ("status", "passed")),
                Result(("id", "flaky"), ("status", "flaky")),
                Result(("id", "failed"), ("status", "failed")),
                Result(("id", "timed-out"), ("status", "timed-out")),
                Result(("id", "interrupted"), ("status", "interrupted")),
                Skipped("explicit", "explicit", "later"),
                Skipped("filtered", "filtered", "tag filter did not match"),
                Skipped("setup-failed", "setup-failed", "setup for session \"user\" failed"),
                Skipped("predecessor", "serial-predecessor-failed", "step 1 failed"),
                Skipped("hook", "hook-failed", "beforeAll failed"),
                Skipped("worker", "infrastructure-unavailable", "worker process exited"),
                Skipped("limit", "failure-limit", "the run stopped at its failure limit"),
                Result(("id", "no-skip"), ("status", "skipped")),
            ])));
        Assert.Equal(new[] { "failed", "timed-out", "interrupted", "setup-failed", "predecessor", "hook", "worker", "limit" }.Select(IdOf), ids);
    }

    [Fact]
    public async Task Is_empty_when_every_result_passed_and_names_a_test_once_however_many_of_its_repeats_failed()
    {
        Assert.Empty(await ReadLastFailedAsync(ReportFile(_roots, ReportText([Result(("id", "a"), ("status", "passed"))]))));
        var repeated = await ReadLastFailedAsync(ReportFile(
            _roots,
            ReportText([Result(("id", "a"), ("status", "passed"), ("repeat", 0)), Result(("id", "a"), ("status", "failed"), ("repeat", 1)), Result(("id", "a"), ("status", "failed"), ("repeat", 2))])));
        Assert.Equal([IdOf("a")], repeated);
    }

    [Fact]
    public async Task Names_every_selected_test_in_the_scope_of_a_failed_suite_hook_and_nothing_outside_it()
    {
        JsonObject[] results =
        [
            Result(("id", "in-scope"), ("titlePath", new JsonArray("teardown", "in-scope")), ("status", "passed")),
            Result(("id", "nested"), ("titlePath", new JsonArray("teardown", "inner", "nested")), ("status", "passed")),
            Result(("id", "prefix-sibling"), ("titlePath", new JsonArray("teardown-other", "prefix-sibling")), ("status", "passed")),
            Result(("id", "separator-sibling"), ("titlePath", new JsonArray("teardown › inner", "separator-sibling")), ("status", "passed")),
            Result(("id", "file-scope"), ("titlePath", new JsonArray("file-scope")), ("status", "passed")),
            Result(("id", "other-file"), ("file", "tests/b.e2e.ts"), ("titlePath", new JsonArray("teardown", "other-file")), ("status", "passed")),
            Result(("id", "other-target"), ("targetId", "mobile"), ("titlePath", new JsonArray("teardown", "other-target")), ("status", "passed")),
            Result(("id", "unselected"), ("titlePath", new JsonArray("teardown", "unselected")), ("status", "skipped"), ("selected", false), ("skip", Result(("cause", "filtered"), ("reason", "grep")))),
        ];
        var scope = Result(("file", "tests/a.e2e.ts"), ("targetId", "web"), ("titlePath", new JsonArray("teardown")));
        var afterAll = Result(("code", "HOOK_FAILED"), ("phase", "afterAll"), ("scopeId", "teardown"), ("scope", scope));
        Assert.Equal(new[] { "in-scope", "nested" }.Select(IdOf), await ReadLastFailedAsync(ReportFile(_roots, ReportText(results, [afterAll]))));

        var fileScope = Result(("code", "HOOK_FAILED"), ("phase", "beforeAll"), ("scopeId", "file"), ("scope", Result(("file", "tests/a.e2e.ts"), ("targetId", "web"), ("titlePath", new JsonArray()))));
        Assert.Equal(
            new[] { "in-scope", "nested", "prefix-sibling", "separator-sibling", "file-scope" }.Select(IdOf),
            await ReadLastFailedAsync(ReportFile(_roots, ReportText(results, [fileScope]))));

        // Control: a run error that is not a suite hook's selects nothing more.
        var cleanup = Result(("code", "CLEANUP_TIMEOUT"), ("phase", "cleanup"));
        Assert.Empty(await ReadLastFailedAsync(ReportFile(_roots, ReportText(results, [cleanup]))));
    }

    [Fact]
    public async Task Names_every_selected_test_for_a_failed_suite_hook_whose_scope_the_report_does_not_say()
    {
        JsonObject[] results =
        [
            Result(("id", "a"), ("status", "passed")),
            Result(("id", "b"), ("file", "tests/b.e2e.ts"), ("status", "passed")),
            Result(("id", "unselected"), ("status", "skipped"), ("selected", false), ("skip", Result(("cause", "filtered"), ("reason", "grep")))),

            // A report older than `selected` does not say it.
            Result(("id", "legacy"), ("status", "passed"), ("selected", null)),
        ];
        var unscoped = Result(("code", "HOOK_FAILED"), ("phase", "afterAll"), ("scopeId", "file"));
        Assert.Equal(new[] { "a", "b", "legacy" }.Select(IdOf), await ReadLastFailedAsync(ReportFile(_roots, ReportText(results, [unscoped]))));
    }

    [Fact]
    public async Task Is_NO_LAST_RUN_when_no_report_exists_with_the_path_and_the_way_out()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-last-run-").FullName;
        _roots.Add(dir);
        var missing = Path.Combine(dir, "report.json");
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Last.ReadLastRunAsync(missing));
        Assert.Equal("NO_LAST_RUN", error.Code);
        Assert.Equal($"--last-failed needs the report of a previous run, and none is at {missing}; run once without the flag first", error.Message);
    }

    [Fact]
    public async Task Is_NO_LAST_RUN_when_the_file_is_not_JSON_or_not_a_report_1_document()
    {
        var notJson = await Assert.ThrowsAsync<ConfigurationException>(() => Last.ReadLastRunAsync(ReportFile(_roots, "{")));
        Assert.Equal("NO_LAST_RUN", notJson.Code);
        Assert.Matches(NoReport(), notJson.Message);

        string[] contents =
        [
            "null",
            "[]",
            """{"schemaVersion":"report-2","run":{"results":[]}}""",
            """{"schemaVersion":"report-1","run":{}}""",
            """{"schemaVersion":"report-1","run":{"results":[null]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a"}]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a","status":"skipped","skip":"later"}]}}""",
            """{"schemaVersion":"report-1","run":{"results":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[{"phase":1}],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[{"phase":"afterAll","scope":{"file":"a.e2e.ts","targetId":"web"}}],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a","status":"passed","file":"a.e2e.ts","titlePath":["a"]}],"errors":[],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a","status":"passed","file":"a.e2e.ts","titlePath":["a"],"attempts":[{}]}],"errors":[],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a","status":"passed","file":"a.e2e.ts","titlePath":["a"],"attempts":[{"artifacts":[null]}]}],"errors":[],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[{"id":"a","status":"passed","file":"a.e2e.ts","titlePath":["a"],"attempts":[{"artifacts":[{"path":1}]}]}],"errors":[],"serialGroups":[]}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[],"serialGroups":[{"id":"g"}]}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[],"serialGroups":[],"carried":{"results":[{"id":"a"}],"errors":[],"serialGroups":[]}}}""",
            """{"schemaVersion":"report-1","run":{"results":[],"errors":[],"serialGroups":[],"carried":{"results":[]}}}""",
        ];
        foreach (var content in contents)
        {
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => Last.ReadLastRunAsync(ReportFile(_roots, content)));
            Assert.True(error.Code == "NO_LAST_RUN", content);
            Assert.True(SomethingElse().IsMatch(error.Message), content);
        }
    }

    [GeneratedRegex(@"\A--last-failed found no report in .*report\.json: ", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NoReport();

    [GeneratedRegex(@"\A--last-failed needs a report-1 document at .*report\.json, which holds something else; run once without the flag to write one\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SomethingElse();
}
