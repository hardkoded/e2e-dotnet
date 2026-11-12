// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.SchemaFixtures;

/// <summary>Upstream's <c>describe.each(schemas)('%s schema')</c> for <c>report-v1</c> (<c>tests/unit/schema-fixtures.test.ts</c>).</summary>
public sealed class ReportV1SchemaTests
{
    private static JsonNode Valid() => ReportSchema.ReadJson("fixtures", "report-v1.valid.json");

    private static JsonObject Run(JsonNode report) => report["run"]!.AsObject();

    private static JsonObject Result(JsonNode report, int index = 0) => Run(report)["results"]![index]!.AsObject();

    private static JsonObject Attempt(JsonNode report, int result = 0, int index = 0) => Result(report, result)["attempts"]![index]!.AsObject();

    private static JsonArray Repeat(string text, int count) => [.. Enumerable.Repeat(text, count).Select(each => System.Text.Json.Nodes.JsonValue.Create(each))];

    private static void Check(JsonNode report, params (Action Break, Action Fix)[] cases)
    {
        foreach (var (breakIt, fixIt) in cases)
        {
            breakIt();
            Assert.False(ReportSchema.IsValid(report));
            fixIt();
            Assert.True(ReportSchema.IsValid(report));
        }
    }

    [Fact]
    public void Accepts_the_valid_fixture()
    {
        ReportSchema.AssertValidReport(ReportSchema.ReadJson("fixtures", "report-v1.valid.json"));
    }

    [Fact]
    public void Rejects_the_invalid_fixture() => Assert.False(ReportSchema.IsValid(ReportSchema.ReadJson("fixtures", "report-v1.invalid.json")));

    [Fact]
    public void Accepts_optional_runner_cut_retry_decisions_and_rejects_other_types()
    {
        var report = Valid();
        var attempt = Run(report)["serialGroups"]![0]!["attempts"]![1]!.AsObject();

        foreach (var cutRetry in new[] { true, false })
        {
            attempt["cutRetry"] = cutRetry;
            Assert.True(ReportSchema.IsValid(report));
        }

        foreach (JsonNode? cutRetry in new JsonNode?[] { System.Text.Json.Nodes.JsonValue.Create("true"), System.Text.Json.Nodes.JsonValue.Create(1), null })
        {
            attempt["cutRetry"] = cutRetry;
            Assert.False(ReportSchema.IsValid(report));
        }

        attempt.Remove("cutRetry");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Bounds_what_a_step_tells_and_what_the_app_logged_a_log_lines_source_and_level_a_hook_phase_and_the_cache_detail_entry_and_write()
    {
        var report = Valid();
        var attempt = Attempt(report);
        var step = attempt["steps"]![0]!.AsObject();
        var cache = step["cache"]!.AsObject();
        var line = attempt["appLog"]![0]!.AsObject();
        var navigation = step["events"]!.AsArray().Select(each => each!.AsObject()).First(each => (string?)each["kind"] == "navigation");

        Check(
            report,
            (() => line["level"] = "fatal", () => line["level"] = "error"),
            (() => line["source"] = "navigation", () => line["source"] = "network"),
            (() => line["text"] = new string('x', 301), () => line["text"] = new string('x', 300)),
            (() => line["step"] = -1, () => line["step"] = 0),
            (() => navigation["level"] = "info", () => navigation.Remove("level")),
            (() => step["phase"] = "body", () => step.Remove("phase")),
            (() => cache["detail"] = new string('x', 601), () => cache["detail"] = new string('x', 600)),
            (() => cache["entry"] = "not-a-digest", () => cache["entry"] = new string('a', 64)),
            (() => cache["write"] = "written", () => cache["write"] = "kept"));
    }

    [Fact]
    public void Bounds_a_steps_screen_and_an_attempts_environment()
    {
        var report = Valid();
        var attempt = Attempt(report);
        var screen = attempt["steps"]![1]!["screen"]!.AsObject();

        Check(
            report,
            (() => screen["changes"] = Repeat("added x", 13), () => screen["changes"] = Repeat("added x", 12)),
            (() => screen["more"] = 0, () => screen["more"] = 1),
            (() => screen.Remove("nodes"), () => screen["nodes"] = 0),
            (() => attempt["environment"] = new JsonObject { ["browser"] = new string('x', 201) }, () => attempt["environment"] = new JsonObject { ["browser"] = new string('x', 200) }),
            (() => attempt["environment"] = new JsonObject { ["cores"] = 8 }, () => attempt["environment"] = new JsonObject { ["cores"] = "8" }));
    }

    [Fact]
    public void Constrains_result_tags_to_distinct_names_tag_can_spell_back()
    {
        var report = Valid();
        var result = Result(report);

        foreach (string[] tags in new string[][] { [""], ["a", "a"], [" a"], ["a,b"] })
        {
            result["tags"] = new JsonArray([.. tags.Select(tag => System.Text.Json.Nodes.JsonValue.Create(tag))]);
            Assert.False(ReportSchema.IsValid(report));
        }

        foreach (string[] tags in new string[][] { [], ["a"], ["Login Form", "billing:refunds"] })
        {
            result["tags"] = new JsonArray([.. tags.Select(tag => System.Text.Json.Nodes.JsonValue.Create(tag))]);
            Assert.True(ReportSchema.IsValid(report));
        }
    }

    [Fact]
    public void Lets_a_video_be_a_link_a_hosted_service_keeps_http_s_only()
    {
        var report = Valid();
        var link = Attempt(report)["artifacts"]!.AsArray().Select(each => each!.AsObject()).First(each => each["url"] is not null);

        Assert.Equal("video", (string?)link["kind"]);
        Assert.Equal("incomplete", (string?)link["redaction"]);
        Assert.True(ReportSchema.IsValid(report));
        foreach (var url in new[] { "file:///tmp/replay.mp4", "replay.mp4", "" })
        {
            link["url"] = url;
            Assert.False(ReportSchema.IsValid(report));
        }
    }

    [Fact]
    public void Lets_a_suite_hook_failure_name_its_scope_a_project_relative_file_a_target_and_describe_titles()
    {
        var report = Valid();
        JsonObject Hook(JsonObject scope) => new()
        {
            ["category"] = "test",
            ["code"] = "HOOK_FAILED",
            ["message"] = "afterAll failed: teardown broke",
            ["retryable"] = false,
            ["phase"] = "afterAll",
            ["scopeId"] = "checkout",
            ["scope"] = scope,
        };
        JsonObject Scope() => new() { ["file"] = "tests/example.e2e.ts", ["targetId"] = "web", ["titlePath"] = new JsonArray("checkout") };

        Run(report)["errors"] = new JsonArray(Hook(Scope()));
        Assert.True(ReportSchema.IsValid(report));
        Action<JsonObject>[] bad =
        [
            scope => scope["file"] = "/abs/example.e2e.ts",
            scope => scope["file"] = "../example.e2e.ts",
            scope => scope["targetId"] = "",
            scope => scope["titlePath"] = new JsonArray(""),
            scope => scope.Remove("titlePath"),
        ];
        foreach (var change in bad)
        {
            var scope = Scope();
            change(scope);
            Run(report)["errors"] = new JsonArray(Hook(scope));
            Assert.False(ReportSchema.IsValid(report));
        }
    }

    [Fact]
    public void Names_the_filter_only_on_a_row_a_filter_left_out_records_each_filter_by_name_alone_and_the_change_they_judged_by_counts_alone_marks_a_filter_that_timed_out_or_failed_and_names_the_filter_phase()
    {
        var report = Valid();
        var run = Run(report);
        var skip = run["results"]!.AsArray().Select(each => each!["skip"]).First(each => each?["filter"] is not null)!.AsObject();
        Assert.Equal("filtered", (string?)skip["cause"]);
        Assert.Equal("areas", (string?)skip["filter"]);
        var filter = run["filters"]![0]!.AsObject();
        var timedOut = run["filters"]!.AsArray().Select(each => each!.AsObject()).First(each => (string?)each["outcome"] == "timed-out");
        var changes = run["changes"]!.AsObject();
        var error = run["errors"]![0]!.AsObject();
        Assert.True(ReportSchema.IsValid(report));

        Check(
            report,
            (() => skip["cause"] = "explicit", () => skip["cause"] = "filtered"),
            (() => skip["filter"] = "", () => skip["filter"] = "areas"),
            (() => skip["filter"] = new string('x', 257), () => skip["filter"] = new string('x', 256)),
            (() => filter["decisions"] = new JsonArray(), () => filter.Remove("decisions")),
            (() => filter.Remove("name"), () => filter["name"] = "areas"),
            (() => filter.Remove("candidates"), () => filter["candidates"] = 2),
            (() => filter.Remove("excluded"), () => filter["excluded"] = 1),
            (() => filter["durationMs"] = 12, () => filter.Remove("durationMs")),
            (() => filter["name"] = "", () => filter["name"] = "areas"),
            (() => filter["excluded"] = -1, () => filter["excluded"] = 0),
            (() => filter["candidates"] = 1.5, () => filter["candidates"] = 2),
            (() => filter["note"] = new string('x', 1025), () => filter["note"] = new string('x', 1024)),
            (() => timedOut["outcome"] = "answered", () => timedOut["outcome"] = "timed-out"),
            (() => timedOut["timedOut"] = true, () => timedOut.Remove("timedOut")),
            (() => changes["base"] = "main", () => changes["base"] = new string('a', 40)),
            (() => changes["base"] = new string('A', 40), () => changes["base"] = new string('a', 64)),
            (() => changes.Remove("files"), () => changes["files"] = 0),
            (() => changes.Remove("base"), () => changes["base"] = new string('a', 40)),
            (() => changes["files"] = -1, () => changes["files"] = 3),
            (() => changes["pinned"] = 1, () => changes.Remove("pinned")),
            (() => changes["skipped"] = "", () => changes["skipped"] = "no tests to judge"),
            (() => changes["skipped"] = new string('x', 1025), () => changes["skipped"] = new string('x', 1024)),
            (() => changes["paths"] = new JsonArray("src/app.ts"), () => changes.Remove("paths")),
            (() => error["phase"] = "filtering", () => error["phase"] = "filter"));

        // A run that called no filter before git was asked says why, and nothing about a change.
        run["changes"] = new JsonObject { ["skipped"] = "--last-failed" };
        Assert.True(ReportSchema.IsValid(report));
        foreach (var counted in new[] { ("files", (JsonNode)0), ("base", new string('a', 40)) })
        {
            run["changes"] = new JsonObject { ["skipped"] = "--last-failed", [counted.Item1] = counted.Item2.DeepClone() };
            Assert.False(ReportSchema.IsValid(report));
        }

        run["changes"] = new JsonObject();
        Assert.False(ReportSchema.IsValid(report));
        run.Remove("filters");
        run.Remove("changes");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Counts_interrupted_results_in_the_summary_apart_from_failed_ones()
    {
        var report = Valid();

        Assert.True(ReportSchema.IsValid(report));
        Run(report)["summary"]!.AsObject().Remove("interrupted");
        Assert.False(ReportSchema.IsValid(report));
        Assert.Contains(ReportSchema.Errors(report, "required"), message => message.Contains("interrupted", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_a_skip_reason_on_a_skipped_or_failed_attempt_never_on_a_passed_one()
    {
        var report = Valid();
        var attempt = Attempt(report, result: 1);
        Assert.Equal("skipped", (string?)attempt["status"]);
        Assert.Equal("explicit", (string?)attempt["skip"]!["cause"]);

        var skip = attempt["skip"]!;
        attempt.Remove("skip");
        Assert.False(ReportSchema.IsValid(report));
        attempt["skip"] = skip;
        attempt["status"] = "failed";
        attempt["error"] = new JsonObject { ["category"] = "test", ["code"] = "ASSERTION_FAILED", ["message"] = "1 soft assertion failed", ["retryable"] = true, ["phase"] = "body" };
        Assert.True(ReportSchema.IsValid(report));
        attempt.Remove("error");
        attempt["status"] = "passed";
        Assert.False(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Lets_a_last_failed_rerun_carry_results_serial_groups_and_hook_failures_at_least_one_result_of_them()
    {
        var report = Valid();
        var carried = Run(report)["carried"]!.AsObject();
        Assert.NotEmpty(carried["results"]!.AsArray());

        Action<JsonObject>[] bad =
        [
            each => each["results"] = new JsonArray(),
            each => each.Remove("serialGroups"),
            each => each.Remove("errors"),
            each => each["results"] = new JsonArray(new JsonObject()),
            each => each["extra"] = true,
        ];
        foreach (var change in bad)
        {
            var copy = carried.DeepClone().AsObject();
            change(copy);
            Run(report)["carried"] = copy;
            Assert.False(ReportSchema.IsValid(report));
        }

        Run(report).Remove("carried");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Requires_judgment_evidence_after_a_model_call_while_allowing_capture_failures_before_one()
    {
        var report = Valid();
        var judgment = Attempt(report)["steps"]!.AsArray().Select(each => each!.AsObject()).First(each => (string?)each["api"] == "agent.assert");
        Assert.Equal(0, (int)judgment["metrics"]!["modelCalls"]!);
        Assert.True(ReportSchema.IsValid(report));

        judgment["status"] = "passed";
        Assert.False(ReportSchema.IsValid(report));
        judgment["status"] = "failed";
        judgment["metrics"]!["modelCalls"] = 1;
        Assert.False(ReportSchema.IsValid(report));
        var errors = ReportSchema.Errors(report, "required");
        Assert.Contains(errors, message => message.Contains("observationRevision", StringComparison.Ordinal));
        Assert.Contains(errors, message => message.Contains("explanation", StringComparison.Ordinal));
        judgment["observationRevision"] = "b1";
        judgment["explanation"] = "visible in the screenshot";
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Accepts_only_a_boolean_observation_truncation_marker()
    {
        var report = Valid();
        var act = Attempt(report)["steps"]!.AsArray().Select(each => each!.AsObject()).First(each => (string?)each["api"] == "agent.act");
        Assert.True((bool)act["observationTruncated"]!);
        Assert.True(ReportSchema.IsValid(report));
        act["observationTruncated"] = "yes";
        Assert.False(ReportSchema.IsValid(report));
        act.Remove("observationTruncated");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Lets_only_a_gap_name_the_rule_that_made_a_typed_value_run_time_data()
    {
        var report = Valid();
        var cache = Attempt(report)["steps"]!.AsArray().Select(each => each!.AsObject()).First(each => (string?)each["api"] == "agent.act")["cache"]!.AsObject();
        Assert.Equal("gap", (string?)cache["reason"]);
        Assert.Equal("minted-token", (string?)cache["derived"]);
        Assert.True(ReportSchema.IsValid(report));

        cache["reason"] = "target-not-found";
        Assert.False(ReportSchema.IsValid(report));
        cache["reason"] = "gap";
        cache["derived"] = "guessed";
        Assert.False(ReportSchema.IsValid(report));
        cache.Remove("derived");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Validates_recording_identities_while_keeping_legacy_reports_valid()
    {
        var report = Valid();
        var cache = Attempt(report)["steps"]![0]!["cache"]!.AsObject();

        foreach (var field in new[] { "provider", "model" })
        {
            var recordedBy = new JsonObject { ["provider"] = "test", ["model"] = "A" };
            recordedBy[field] = new string('x', 300);
            cache["recordedBy"] = recordedBy.DeepClone();
            Assert.True(ReportSchema.IsValid(report));
            recordedBy[field] = new string('x', 301);
            cache["recordedBy"] = recordedBy.DeepClone();
            Assert.False(ReportSchema.IsValid(report));
        }

        JsonNode?[] bad =
        [
            null,
            new JsonObject { ["provider"] = "", ["model"] = "A" },
            new JsonObject { ["provider"] = "test", ["model"] = "  " },
            new JsonObject { ["provider"] = "test" },
            new JsonObject { ["provider"] = "test", ["model"] = 1 },
            new JsonObject { ["provider"] = "test", ["model"] = "A", ["calls"] = 1 },
        ];
        foreach (var recordedBy in bad)
        {
            cache["recordedBy"] = recordedBy;
            Assert.False(ReportSchema.IsValid(report));
        }

        cache.Remove("recordedBy");
        Assert.True(ReportSchema.IsValid(report));
    }

    [Fact]
    public void Lets_a_missed_or_handed_off_step_say_why_it_was_not_recorded_with_a_closed_reason()
    {
        var report = Valid();
        var steps = Run(report)["results"]!.AsArray().SelectMany(result => result!["attempts"]!.AsArray()).SelectMany(attempt => attempt!["steps"]!.AsArray()).Select(step => step!.AsObject());
        var cache = steps.First(step => step["cache"]?["notRecorded"] is not null)["cache"]!.AsObject();
        Assert.Equal("missed", (string?)cache["mode"]);
        Assert.Equal("param-collision", (string?)cache["notRecorded"]);
        Assert.True(ReportSchema.IsValid(report));

        cache["notRecorded"] = "guessed";
        Assert.False(ReportSchema.IsValid(report));
        cache["notRecorded"] = "param-collision";
        cache["mode"] = "self-finalized";
        cache.Remove("reason");
        cache.Remove("derived");
        Assert.False(ReportSchema.IsValid(report));
    }
}
