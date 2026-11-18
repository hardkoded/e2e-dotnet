// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportJunit.JunitKit;
using JunitReportOptions = E2E.Reports.JunitReportOptions;

namespace E2E.Tests.ReportJunit;

/// <summary>
/// Upstream's <c>describe('renderJunitReport')</c> (<c>tests/unit/report-junit.test.ts</c>). Not ported, since the port has no serial groups and no
/// <c>test.fail</c> expected-failure mark in its report: <c>times a failed serial member by its verdict when a %s retry was cut</c>,
/// <c>reads a serial member from its group attempt</c>, <c>links the failure screenshot of a failed serial member</c>,
/// <c>keeps the expected note between the flaky note and the screenshot</c>, and <c>links the failure screenshot of a test.fail pass after the expected note</c>.
/// </summary>
public sealed class RenderJunitReportTests
{
    private static ReportError Interrupt(string message) =>
        ReportBuilders.Error("INTERRUPTED", message) with
        {
            Category = "interrupted",
        };

    private static ReportResult Failed(ReportError error, string id = "result-1", string agent = "default") =>
        ReportBuilders.Result(
            id: id,
            status: ResultStatus.Failed,
            attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, error)]
        ) with
        {
            Agent = agent,
        };

    [Fact]
    public void Renders_an_empty_run_as_a_declaration_and_a_root_with_zero_counts_and_no_suites_ending_in_a_newline()
    {
        var xml = Render(Doc());

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<testsuites ", xml);
        Assert.EndsWith("</testsuites>\n", xml);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["name"] = "e2e",
                ["tests"] = "0",
                ["failures"] = "0",
                ["errors"] = "0",
                ["skipped"] = "0",
                ["time"] = "0.000",
            },
            RootAttributes(xml)
        );
        Assert.DoesNotContain("<testsuite ", xml);
    }

    [Fact]
    public void Leaves_a_result_the_selection_did_not_choose_out_of_the_suites_and_the_counts()
    {
        var unselected = ReportBuilders.Result(
            id: "result-2",
            file: "tests/agent/tests.e2e.ts",
            status: ResultStatus.Skipped,
            attempts: []
        ) with
        {
            Selected = false,
            Skip = new ReportSkip("filtered", "file not selected by a positional argument"),
        };

        var xml = Render(Doc([ReportBuilders.Result(), unselected]));

        Assert.DoesNotContain("tests/agent/tests.e2e.ts", xml);
        var root = RootAttributes(xml);
        Assert.Equal("1", root["tests"]);
        Assert.Equal("0", root["skipped"]);
    }

    [Fact]
    public void Renders_a_test_a_filter_left_out_as_a_skipped_case_naming_the_filter_and_its_reason_so_a_run_the_filters_left_empty_is_not_an_empty_file()
    {
        var leftOut = ReportBuilders.Result(
            id: "result-2",
            titlePath: ["pays"],
            file: "tests/billing.e2e.ts",
            status: ResultStatus.Skipped,
            attempts: []
        ) with
        {
            Selected = false,
            Skip = new ReportSkip("filtered", "Jev: 4% likely affected", "jev"),
        };

        var xml = Render(Doc([leftOut]));

        Assert.Contains(
            string.Join(
                '\n',
                "  <testsuite name=\"tests/billing.e2e.ts\" tests=\"1\" failures=\"0\" errors=\"0\" skipped=\"1\" time=\"0.000\">",
                "    <testcase name=\"pays [web]\" classname=\"tests/billing.e2e.ts\" time=\"0.000\">",
                "      <skipped message=\"left out by filter jev: Jev: 4% likely affected\"/>",
                "    </testcase>",
                "  </testsuite>"
            ),
            xml
        );
        var root = RootAttributes(xml);
        Assert.Equal("1", root["tests"]);
        Assert.Equal("1", root["skipped"]);
    }

    [Fact]
    public void Renders_a_passed_result_as_a_bare_testcase_named_by_title_path_and_target()
    {
        var xml = Render(Doc([ReportBuilders.Result()]));

        Assert.Contains(
            "<testcase name=\"auth &gt; signs in [web]\" classname=\"tests/auth.e2e.ts\" time=\"1.234\"/>",
            xml
        );
        Assert.Contains(
            "<testsuite name=\"tests/auth.e2e.ts\" tests=\"1\" failures=\"0\" errors=\"0\" skipped=\"0\" time=\"1.234\">",
            xml
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0", "0"], [root["tests"], root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Appends_the_agent_to_the_case_name_when_it_is_not_default_so_a_persona_sweep_keeps_one_case_per_persona()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result() with
                {
                    Agent = "buyer",
                },
                ReportBuilders.Result(id: "result-2") with
                {
                    Agent = "admin",
                },
            ])
        );

        Assert.Contains("<testcase name=\"auth &gt; signs in [web] [buyer]\"", xml);
        Assert.Contains("<testcase name=\"auth &gt; signs in [web] [admin]\"", xml);
        Assert.Equal("2", RootAttributes(xml)["tests"]);
    }

    [Fact]
    public void Uses_the_final_attempt_for_time_and_notes_flaky_passes_in_system_out()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(
                    status: ResultStatus.Flaky,
                    attempts:
                    [
                        ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error()) with
                        {
                            DurationMs = 5000,
                        },
                        ReportBuilders.Attempt() with
                        {
                            DurationMs = 800,
                        },
                    ]
                ),
            ])
        );

        Assert.Contains("time=\"0.800\">", xml);
        Assert.Contains("<system-out>flaky: 1 failed attempt before passing</system-out>", xml);
        Assert.DoesNotContain("<failure", xml);
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0"], [root["tests"], root["failures"], root["errors"]]);
    }

    [Fact]
    public void Times_a_failed_test_by_its_failed_attempt_when_the_run_interrupted_its_retry()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error()) with
                        {
                            DurationMs = 5000,
                        },
                        ReportBuilders.Attempt(AttemptStatus.Interrupted) with
                        {
                            DurationMs = 20,
                        },
                    ]
                ),
            ])
        );

        Assert.Contains("time=\"5.000\">", xml);
    }

    [Fact]
    public void Renders_a_test_category_error_as_failure_with_code_message_and_body()
    {
        var xml = Render(Doc([Failed(ReportBuilders.Error())]));

        Assert.Contains(
            "<failure message=\"expected &quot;Sign in&quot; to be visible\" type=\"ASSERTION_FAILED\">"
                + "ASSERTION_FAILED: expected &quot;Sign in&quot; to be visible</failure>",
            xml
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0"], [root["failures"], root["errors"]]);
    }

    [Fact]
    public void Renders_infrastructure_and_configuration_errors_as_error()
    {
        var infrastructure = ReportBuilders.Error("DRIVER_CRASHED", "gone", "launch") with
        {
            Category = "infrastructure",
        };
        var configuration = ReportBuilders.Error("MODEL_UNAVAILABLE", "no model") with { Category = "configuration" };

        var xml = Render(
            Doc([
                Failed(infrastructure),
                Failed(configuration, "result-2") with
                {
                    DeclarationIndex = 1,
                    TitlePath = ["agent", "plans"],
                },
            ])
        );

        Assert.Contains("<error message=\"gone\" type=\"DRIVER_CRASHED\">DRIVER_CRASHED (launch): gone</error>", xml);
        Assert.Contains(
            "<error message=\"no model\" type=\"MODEL_UNAVAILABLE\">MODEL_UNAVAILABLE: no model</error>",
            xml
        );
        Assert.DoesNotContain("<failure", xml);
        var root = RootAttributes(xml);
        Assert.Equal(["2", "0", "2"], [root["tests"], root["failures"], root["errors"]]);
    }

    [Fact]
    public void Falls_back_to_the_result_status_when_a_failed_result_carries_no_error()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(
                    status: ResultStatus.TimedOut,
                    attempts: [ReportBuilders.Attempt(AttemptStatus.TimedOut)]
                ),
                ReportBuilders.Result(
                    id: "result-2",
                    status: ResultStatus.Interrupted,
                    attempts: [ReportBuilders.Attempt(AttemptStatus.Interrupted)]
                ) with
                {
                    DeclarationIndex = 1,
                },
            ])
        );

        Assert.Contains("<failure message=\"timed-out\" type=\"timed-out\">timed-out</failure>", xml);
        Assert.Contains("<skipped message=\"interrupted: the run was stopped\"/>", xml);
    }

    [Fact]
    public void Tells_the_failure_an_interrupted_retry_was_cut_short_after()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error()),
                        ReportBuilders.Attempt(AttemptStatus.Interrupted, Interrupt("run interrupted in phase body")),
                    ]
                ),
            ])
        );

        Assert.Contains(
            "<failure message=\"expected &quot;Sign in&quot; to be visible\" type=\"ASSERTION_FAILED\">",
            xml
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0"], [root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Renders_an_interrupted_result_as_skipped_with_why_never_as_a_failure_or_an_error()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(
                    status: ResultStatus.Interrupted,
                    attempts:
                    [
                        ReportBuilders.Attempt(AttemptStatus.Interrupted, Interrupt("run interrupted in phase body")),
                    ]
                ),
            ])
        );

        Assert.Contains("<skipped message=\"interrupted: run interrupted in phase body\"/>", xml);
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0", "1"], [root["tests"], root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Renders_skipped_results_with_their_reason_and_no_attempt_time()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(status: ResultStatus.Skipped, attempts: []) with
                {
                    Skip = new ReportSkip("filtered", "tag filter excluded it"),
                },
            ])
        );

        Assert.Contains("time=\"0.000\">", xml);
        Assert.Contains("<skipped message=\"tag filter excluded it\"/>", xml);
        var root = RootAttributes(xml);
        Assert.Equal(["1", "1"], [root["tests"], root["skipped"]]);
    }

    [Fact]
    public void Groups_results_into_one_suite_per_file_in_report_order()
    {
        var xml = Render(
            Doc([
                ReportBuilders.Result(file: "tests/a.e2e.ts"),
                ReportBuilders.Result(id: "result-2", file: "tests/b.e2e.ts"),
                ReportBuilders.Result(id: "result-3", file: "tests/a.e2e.ts") with
                {
                    TargetId = "webkit",
                },
            ])
        );

        var suites = System
            .Text.RegularExpressions.Regex.Matches(xml, "<testsuite name=\"([^\"]*)\" tests=\"(\\d+)\"")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
            .ToList();
        Assert.Equal([("tests/a.e2e.ts", "2"), ("tests/b.e2e.ts", "1")], suites);
        Assert.Contains("[webkit]", xml);
    }

    [Fact]
    public void Escapes_markup_in_attributes_and_text_and_strips_control_characters()
    {
        var xml = Render(
            Doc([
                Failed(ReportBuilders.Error(message: "line one\nline <two>\u0007 & done")) with
                {
                    TitlePath = ["<b>bold</b> & \"quoted\""],
                },
            ])
        );

        Assert.Contains("name=\"&lt;b&gt;bold&lt;/b&gt; &amp; &quot;quoted&quot; [web]\"", xml);
        Assert.Contains("message=\"line one&#10;line &lt;two&gt;� &amp; done\"", xml);
        Assert.Contains(">ASSERTION_FAILED: line one\nline &lt;two&gt;� &amp; done</failure>", xml);
        Assert.DoesNotContain("\u0007", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_a_carriage_return_in_the_failure_message_and_body_as_a_character_reference()
    {
        var error = ReportError.Of(ErrorSerializer.Serialize(new TestException("ERROR", "line one\r\nline two")));
        var xml = Render(Doc([Failed(error)]));

        Assert.Contains("message=\"line one&#13;&#10;line two\"", xml);
        Assert.Contains(">ERROR: line one&#13;\nline two</failure>", xml);
        Assert.DoesNotContain("�", xml);
        Assert.DoesNotContain("\r", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Replaces_XML_forbidden_noncharacters_and_lone_surrogates_in_text_and_attributes()
    {
        const string lonePair = "\ud83d\ude00";
        // Built as the public records a reporter receives: the wire form refuses to name a test by a lone surrogate, so it is not a way in.
        var report = new E2E.Reports.Report
        {
            Run = new E2E.Reports.ReportRun
            {
                Results =
                [
                    new E2E.Reports.ReportResult
                    {
                        Id = "result-1",
                        File = "tests/auth.e2e.ts",
                        TitlePath = ["fffe\ufffe high\ud83d low\ude00 pair" + lonePair],
                        TargetId = "web",
                        Agent = "default",
                        Status = E2E.Reports.ResultStatus.Failed,
                        Attempts =
                        [
                            new E2E.Reports.ReportAttempt
                            {
                                Status = E2E.Reports.AttemptStatus.Failed,
                                Error = new E2E.Reports.ReportError
                                {
                                    Category = E2E.Reports.ErrorCategory.Test,
                                    Code = "ASSERTION_FAILED",
                                    Message = "ffff\uffff end\udc00 start\udbff",
                                },
                            },
                        ],
                    },
                    new E2E.Reports.ReportResult
                    {
                        Id = "result-2",
                        File = "tests/auth.e2e.ts",
                        TitlePath = ["auth"],
                        TargetId = "web",
                        Agent = "default",
                        Status = E2E.Reports.ResultStatus.Skipped,
                        Skip = new E2E.Reports.SkipInfo
                        {
                            Cause = E2E.Reports.SkipCause.Filtered,
                            Reason = "reason\ufffe\ud800",
                        },
                    },
                ],
                Errors =
                [
                    new E2E.Reports.ReportError
                    {
                        Category = E2E.Reports.ErrorCategory.Infrastructure,
                        Code = "APP_UNREACHABLE",
                        Message = "run\uffff\udfff",
                    },
                ],
            },
        };

        var xml = E2E.Reports.JunitReport.RenderJunitReport(report);
        AssertWellFormed(xml);

        Assert.DoesNotContain("\ufffe", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("\uffff", xml, StringComparison.Ordinal);
        Assert.Contains("name=\"fffe\ufffd high\ufffd low\ufffd pair" + lonePair + " [web]\"", xml);
        Assert.Contains("message=\"ffff\ufffd end\ufffd start\ufffd\"", xml);
        Assert.Contains(">ASSERTION_FAILED: ffff\ufffd end\ufffd start\ufffd</failure>", xml);
        Assert.Contains("<skipped message=\"reason\ufffd\ufffd\"/>", xml);
        Assert.Contains("message=\"run\ufffd\ufffd\" type=\"APP_UNREACHABLE\"", xml);
    }

    [Fact]
    public void Rejects_the_well_formedness_check_itself_on_a_forbidden_character()
    {
        const string head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";

        Assert.Contains(
            "forbidden",
            Assert.Throws<InvalidOperationException>(() => AssertWellFormed(head + "<a>￾</a>\n")).Message
        );
        Assert.Contains(
            "forbidden",
            Assert.Throws<InvalidOperationException>(() => AssertWellFormed(head + "<a>\ud800</a>\n")).Message
        );
        Assert.Contains(
            "unbalanced",
            Assert.Throws<InvalidOperationException>(() => AssertWellFormed(head + "<a><b></a>\n")).Message
        );
        Assert.Contains(
            "ampersand",
            Assert.Throws<InvalidOperationException>(() => AssertWellFormed(head + "<a x=\"1 & 2\"/>\n")).Message
        );
    }

    [Fact]
    public void Adds_a_run_suite_with_one_error_case_per_run_level_error()
    {
        var xml = Render(
            Doc(
                errors:
                [
                    ReportBuilders.Error("APP_UNREACHABLE", "http://127.0.0.1:3000 did not answer", "launch") with
                    {
                        Category = "infrastructure",
                    },
                ]
            )
        );

        Assert.Contains(
            "<testsuite name=\"run\" tests=\"1\" failures=\"0\" errors=\"1\" skipped=\"0\" time=\"0.000\">",
            xml
        );
        Assert.Contains("<testcase name=\"APP_UNREACHABLE (launch)\" classname=\"run\" time=\"0.000\">", xml);
        Assert.Contains(
            "<error message=\"http://127.0.0.1:3000 did not answer\" type=\"APP_UNREACHABLE\">"
                + "infrastructure error APP_UNREACHABLE (launch): http://127.0.0.1:3000 did not answer</error>",
            xml
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "1"], [root["tests"], root["errors"]]);
    }

    [Theory]
    [InlineData(".e2e/results", ".e2e/results/checkout/001-failure.png")]
    [InlineData(".", "checkout/001-failure.png")]
    public void Links_the_failure_screenshot_in_system_out_below_the_artifacts_dir(string artifactsDir, string expected)
    {
        var xml = Render(FailedWithShot(), new JunitReportOptions { ArtifactsDir = artifactsDir });

        Assert.Contains($"<system-out>[[ATTACHMENT|{expected}]]</system-out>", xml);
    }

    [Fact]
    public void Writes_no_system_out_when_no_artifactsDir_is_given() =>
        Assert.DoesNotContain("<system-out>", Render(FailedWithShot(), new JunitReportOptions()));

    [Fact]
    public void Writes_no_system_out_when_the_failure_has_no_screenshot() =>
        Assert.DoesNotContain(
            "<system-out>",
            Render(
                FailedWithShot(a => a with { Failure = new E2E.FailureEvidence() }),
                new JunitReportOptions { ArtifactsDir = "." }
            )
        );

    [Fact]
    public void Writes_no_system_out_when_the_screenshot_has_no_path() =>
        Assert.DoesNotContain(
            "<system-out>",
            Render(
                FailedWithShot(a =>
                    a with
                    {
                        Artifacts = [ShotArtifact("shot-1", null, "https://example.com/shot.png")],
                    }
                ),
                new JunitReportOptions { ArtifactsDir = "." }
            )
        );

    [Fact]
    public void Escapes_XML_characters_in_the_attachment_path()
    {
        var xml = Render(
            FailedWithShot(a => a with { Artifacts = [ShotArtifact("shot-1", "shots/foo&<bar>.png")] }),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );

        Assert.Contains("<system-out>[[ATTACHMENT|.e2e/shots/foo&amp;&lt;bar&gt;.png]]</system-out>", xml);
    }

    [Fact]
    public void Picks_the_failure_screenshot_by_id_not_the_first_artifact()
    {
        var xml = Render(
            FailedWithShot(a =>
                a with
                {
                    Artifacts = [ShotArtifact("other", "shots/first.png"), ShotArtifact("shot-1", "shots/target.png")],
                }
            ),
            new JunitReportOptions { ArtifactsDir = "." }
        );

        Assert.Contains("<system-out>[[ATTACHMENT|shots/target.png]]</system-out>", xml);
    }

    [Fact]
    public void Takes_the_screenshot_of_the_told_attempt_when_the_last_retry_was_interrupted()
    {
        var interrupted = ReportBuilders.Attempt(AttemptStatus.Interrupted, Interrupt("cut"));

        var xml = Render(FailedWithShot(extraAttempts: [interrupted]), new JunitReportOptions { ArtifactsDir = "." });

        Assert.Contains("<system-out>[[ATTACHMENT|checkout/001-failure.png]]</system-out>", xml);
    }

    [Fact]
    public void Links_the_failure_screenshot_of_a_flaky_test_after_the_flaky_note()
    {
        var xml = Render(
            FailedWithShot(status: ResultStatus.Flaky, extraAttempts: [ReportBuilders.Attempt()]),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );

        Assert.Contains(
            "<system-out>flaky: 1 failed attempt before passing\n[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_an_errored_test_after_its_error()
    {
        var crashed = ReportBuilders.Error("DRIVER_CRASHED", "gone") with { Category = "infrastructure" };

        var xml = Render(
            FailedWithShot(a => a with { Error = crashed }),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );

        Assert.Contains(
            "<error message=\"gone\" type=\"DRIVER_CRASHED\">DRIVER_CRASHED: gone</error>\n      <system-out>[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_a_timed_out_test()
    {
        var xml = Render(
            FailedWithShot(a => a with { Status = AttemptStatus.TimedOut }, ResultStatus.TimedOut),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );

        Assert.Contains("<system-out>[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>", xml);
    }
}
