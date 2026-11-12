// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Reports;
using static E2E.Tests.LastRun.LastRunDocuments;
using Last = E2E.Reports.LastRun;

namespace E2E.Tests.LastRun;

/// <summary>
/// Upstream's <c>describe('carryForward')</c> (<c>tests/unit/last-run.test.ts</c>, tester-army/e2e <c>50fa7af</c>). Not ported: <c>carries a serial member's group,
/// where its attempts live</c> (the port has no serial groups). <c>names every artifact file a report points at, its carried ones included</c> is
/// <c>Partial_</c>: it leaves out the serial group's video.
/// </summary>
public sealed class CarryForwardTests : IDisposable
{
    private static readonly JsonObject Scope = Result(("file", "tests/a.e2e.ts"), ("targetId", "web"), ("titlePath", new JsonArray("teardown")));
    private static readonly JsonObject AfterAll = Result(("code", "HOOK_FAILED"), ("phase", "afterAll"), ("scopeId", "teardown"), ("scope", Scope));

    private readonly List<string> _roots = [];

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A report with the JSON it was read from.</summary>
    private sealed record Read(Report Report, JsonObject Document);

    private static Read Of(JsonObject document) => new(Typed(document), document);

    private static JsonObject With(JsonObject row, params (string Name, JsonNode? Value)[] fields)
    {
        var copy = (JsonObject)row.DeepClone();
        foreach (var (name, value) in fields)
        {
            copy[name] = value?.DeepClone();
        }

        return copy;
    }

    private static JsonObject Failed(string id, params (string Name, JsonNode? Value)[] extra)
    {
        var artifact = Result(("path", $"web/{id}/attempt-0/failure.png"));
        var attempt = Result(("artifacts", new JsonArray(artifact)));
        return With(Result(("id", id), ("status", "failed"), ("attempts", new JsonArray(attempt))), extra);
    }

    private static JsonObject Passed(string id, params (string Name, JsonNode? Value)[] extra)
    {
        var attempt = Result(("artifacts", new JsonArray()));
        return With(Result(("id", id), ("status", "passed"), ("attempts", new JsonArray(attempt))), extra);
    }

    private static JsonObject InScope(Func<string, (string Name, JsonNode? Value)[], JsonObject> status) => status("in-scope", [("titlePath", new JsonArray("teardown", "in-scope"))]);

    private static Read DocumentOf(JsonObject[] results, JsonObject[]? errors = null, JsonObject? run = null) => Of(Document(results, errors, run));

    /// <summary><c>CarryForward</c> with the rerun having collected the tests its report has rows for, unless <paramref name="collected"/> says more, and failed to collect <paramref name="uncollected"/>.</summary>
    private static ReportCarried? Carry(Read lastRun, Read current, string[]? collected = null, string[]? uncollected = null) =>
        Last.CarryForward(
            lastRun.Report,
            current.Report,
            new RerunCollection(
                new HashSet<string>([.. current.Report.Run.Results.Select(result => result.TestId), .. collected ?? []], StringComparer.Ordinal),
                new HashSet<string>(uncollected ?? [], StringComparer.Ordinal)));

    private static string[] CarriedIds(ReportCarried? carried) => [.. carried!.Results.Select(result => result.TestId)];

    [Fact]
    public void Carries_what_the_run_before_owed_and_another_filter_left_out_as_that_run_reported_it()
    {
        var before = DocumentOf([Failed("broken"), Result(("id", "limit"), ("status", "skipped"), ("skip", Result(("cause", "failure-limit"), ("reason", "stopped")))), Passed("fine")]);
        var carried = Carry(before, DocumentOf([Passed("broken"), LeftOut("limit"), LeftOut("fine", "did not fail in the last run")]));
        Assert.Equal(["limit"], CarriedIds(carried));
        Assert.Same(before.Report.Run.Results[1], carried!.Results[0]);
        Assert.Empty(carried.Errors);

        // What the rerun ran is its own result now, passed or not; nothing owed left out is nothing carried.
        Assert.Null(Carry(before, DocumentOf([Failed("broken"), Passed("limit"), LeftOut("fine")])));
    }

    [Fact]
    public void Keeps_a_test_owed_across_consecutive_reruns_until_one_runs_it_and_the_next_last_failed_selects_it()
    {
        var first = DocumentOf([Failed("a"), Failed("b")]);
        var carriedOnce = Carry(first, DocumentOf([Passed("a"), LeftOut("b")]));
        Assert.Equal(["b"], CarriedIds(carriedOnce));
        var second = DocumentOf([Passed("a"), LeftOut("b")], [], Result(("carried", new PreviousRun(first.Report, first.Document).Carried(carriedOnce!))));
        Assert.Equal([IdOf("b")], Last.LastFailedIds(second.Report));

        // Left out again: still the first run's row, not the filtered one in between.
        var again = Carry(second, DocumentOf([LeftOut("a", "did not fail in the last run"), LeftOut("b")]));
        Assert.Single(again!.Results);
        Assert.Equivalent(first.Report.Run.Results[1], again.Results[0], strict: true);

        // Run again, it is the rerun's own result.
        Assert.Null(Carry(second, DocumentOf([LeftOut("a"), Passed("b")])));

        // A test the rerun no longer has cannot run again.
        Assert.Null(Carry(second, DocumentOf([LeftOut("a")])));
    }

    [Fact]
    public void Carries_a_suite_hook_failure_while_its_scope_holds_a_carried_test_and_drops_it_once_the_scope_ran_again()
    {
        var before = DocumentOf([InScope(Passed), Failed("elsewhere", ("file", "tests/b.e2e.ts"))], [AfterAll, Result(("code", "CLEANUP_TIMEOUT"), ("phase", "cleanup"))]);
        var narrowed = Carry(before, DocumentOf([LeftOut("in-scope"), Passed("elsewhere", ("file", "tests/b.e2e.ts"))]));
        Assert.Equal(["in-scope"], CarriedIds(narrowed));
        Assert.Single(narrowed!.Errors);
        Assert.Same(before.Report.Run.Errors[0], narrowed.Errors[0]);

        // The scope ran again and the hook passed: resolved. It failed again: the rerun reports it itself, once.
        var elsewhere = Passed("elsewhere", ("file", "tests/b.e2e.ts"));
        Assert.Null(Carry(before, DocumentOf([InScope(Passed), elsewhere])));
        Assert.Null(Carry(before, DocumentOf([InScope(Passed), elsewhere], [AfterAll])));

        // A hook failure carried from further back stays carried with its scope.
        var second = DocumentOf([LeftOut("in-scope"), Passed("elsewhere", ("file", "tests/b.e2e.ts"))], [], Result(("carried", new PreviousRun(before.Report, before.Document).Carried(narrowed))));
        var later = Carry(second, DocumentOf([LeftOut("in-scope"), LeftOut("elsewhere")]));
        Assert.Single(later!.Errors);
        Assert.Equivalent(before.Report.Run.Errors[0], later.Errors[0], strict: true);
    }

    [Fact]
    public void Resolves_a_hook_whose_scope_ran_again_without_it_failing_though_another_filter_left_part_of_the_scope_out()
    {
        JsonObject Sibling(Func<string, (string Name, JsonNode? Value)[], JsonObject> status) => status("sibling", [("titlePath", new JsonArray("teardown", "sibling"))]);
        var before = DocumentOf([InScope(Passed), Sibling(Passed)], [AfterAll]);
        Assert.Null(Carry(before, DocumentOf([InScope(Passed), LeftOut("sibling")])));

        // Failing again it is the rerun's own error, and the sibling it left out stays owed.
        var again = Carry(before, DocumentOf([InScope(Passed), LeftOut("sibling")], [AfterAll]));
        Assert.Equal(["sibling"], CarriedIds(again));
        Assert.Empty(again!.Errors);

        // A scope test the rerun selected but never ran does not show the hook passing.
        var limited = Result(("id", "in-scope"), ("titlePath", new JsonArray("teardown", "in-scope")), ("status", "skipped"), ("skip", Result(("cause", "failure-limit"), ("reason", "stopped"))));
        var held = Carry(before, DocumentOf([limited, LeftOut("sibling")]));
        Assert.Single(held!.Errors);
        Assert.Same(before.Report.Run.Errors[0], held.Errors[0]);
    }

    [Fact]
    public void Carries_a_test_on_a_target_or_agent_the_rerun_did_not_select_while_the_test_and_the_target_are_still_there()
    {
        var before = DocumentOf([Failed("a"), Failed("a", ("targetId", "mobile"), ("id", "a-mobile"), ("testId", "a")), Failed("a", ("agent", "other"), ("id", "a-other"), ("testId", "a"))]);
        var onWeb = Carry(before, DocumentOf([Passed("a")]));
        Assert.Equal(new[] { ("mobile", "default"), ("web", "other") }, onWeb!.Results.Select(result => (result.TargetId, result.Agent)));

        // A target the config no longer has cannot run it again.
        var webOnly = Result(("targets", new JsonArray(Result(("id", "web")))));
        Assert.Null(Carry(before, DocumentOf([Passed("a"), Passed("a", ("agent", "other"), ("id", "a-other"), ("testId", "a"))], [], webOnly)));
    }

    [Fact]
    public void Carries_a_setup_whose_afterAll_failed_when_the_rerun_needed_none_of_its_sessions_so_the_report_has_no_row_for_it()
    {
        var setupScope = With(AfterAll, ("scopeId", "file"), ("scope", Result(("file", "tests/auth.e2e.ts"), ("targetId", "web"), ("titlePath", new JsonArray()))));
        var signIn = Passed("sign in", ("kind", "setup"), ("file", "tests/auth.e2e.ts"));
        var before = DocumentOf([signIn, Passed("signed in"), Failed("unrelated")], [setupScope]);
        var narrowed = DocumentOf([LeftOut("signed in"), Passed("unrelated")]);
        var carried = Carry(before, narrowed, ["sign in"]);
        Assert.Equal(["sign in"], CarriedIds(carried));
        Assert.Single(carried!.Errors);
        Assert.Same(before.Report.Run.Errors[0], carried.Errors[0]);

        // A setup the rerun no longer collects is gone with its debt.
        Assert.Null(Carry(before, narrowed));
    }

    [Fact]
    public void Carries_a_test_in_a_file_a_narrowed_rerun_found_but_could_not_collect_and_drops_it_once_the_file_is_gone()
    {
        var before = DocumentOf([Failed("a"), Failed("b", ("file", "tests/b.e2e.ts"))]);
        var narrowed = DocumentOf([Passed("a")]);
        Assert.Equal(["b"], CarriedIds(Carry(before, narrowed, [], ["tests/b.e2e.ts"])));
        Assert.Null(Carry(before, narrowed));
    }

    [Fact]
    public async Task Reads_the_tests_a_report_carries_back_as_ones_to_run_again()
    {
        var carried = Result(("results", new JsonArray(Failed("owed"))));
        var file = ReportFile(_roots, ReportText([Passed("a")], [], Result(("carried", carried))));
        Assert.Equal([IdOf("owed")], Last.LastFailedIds(await Last.ReadLastRunAsync(file)));
    }

    /// <summary>Partial: the port has no serial groups, so the group's video and its hosted link are not in the report.</summary>
    [Fact]
    public void Partial_Names_every_artifact_file_a_report_points_at_its_carried_ones_included()
    {
        var hosted = Result(("url", "https://hosted.example/v.mp4"));
        var linked = With(Failed("linked"), ("attempts", new JsonArray(Result(("artifacts", new JsonArray(hosted))))));
        var carried = Result(("results", new JsonArray(Failed("old"), linked)));
        var paths = Last.ReportArtifactPaths(DocumentOf([Failed("new"), Passed("fine")], [], Result(("carried", carried))).Report);
        Assert.Equal(["web/new/attempt-0/failure.png", "web/old/attempt-0/failure.png"], paths.Order(StringComparer.Ordinal));
    }
}
