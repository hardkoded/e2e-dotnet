// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.ReportJunit.JunitKit;

namespace E2E.Tests.ReportJunit;

// Inside the namespace, so the public records of a reporter win over the internal ones of the same names in E2E.
#pragma warning disable IDE0065 // Inside the namespace on purpose.
using E2E.Reports;
#pragma warning restore IDE0065

/// <summary>
/// Upstream's <c>describe('renderJunitReport')</c> (<c>tests/unit/report-junit.test.ts</c>). Not ported: the three serial-group <c>it</c> (the port has
/// no serial groups) and the one about the session a failed step ran on (the port's step has no <c>actor</c>).
/// </summary>
public sealed partial class RenderJunitReportTests
{
    [GeneratedRegex("<testsuite name=\"([^\"]*)\" tests=\"(\\d+)\"")]
    private static partial Regex Suite();

    [Fact]
    public void Renders_an_empty_run_as_a_declaration_and_a_root_with_zero_counts_and_no_suites_ending_in_a_newline()
    {
        var xml = Render(Document());
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<testsuites ", xml, StringComparison.Ordinal);
        Assert.EndsWith("</testsuites>\n", xml, StringComparison.Ordinal);
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
        Assert.DoesNotContain("<testsuite ", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_a_result_the_selection_did_not_choose_out_of_the_suites_and_the_counts()
    {
        var unselected = Result(
            id: "result-2",
            testId: "test-2",
            file: "tests/agent/tests.e2e.ts",
            selected: false,
            status: ResultStatus.Skipped,
            skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "file not selected by a positional argument" },
            attempts: []
        );
        var xml = Render(Document([Result(), unselected]));
        Assert.DoesNotContain("tests/agent/tests.e2e.ts", xml, StringComparison.Ordinal);
        var root = RootAttributes(xml);
        Assert.Equal("1", root["tests"]);
        Assert.Equal("0", root["skipped"]);
    }

    [Fact]
    public void Renders_a_test_a_filter_left_out_as_a_skipped_case_naming_the_filter_and_its_reason_so_a_run_the_filters_left_empty_is_not_an_empty_file()
    {
        var leftOut = Result(
            id: "result-2",
            testId: "test-2",
            titlePath: ["pays"],
            file: "tests/billing.e2e.ts",
            selected: false,
            status: ResultStatus.Skipped,
            skip: new SkipInfo
            {
                Cause = SkipCause.Filtered,
                Reason = "Jev: 4% likely affected",
                Filter = "jev",
            },
            attempts: []
        );
        var xml = Render(Document([leftOut]));
        Assert.Contains(
            string.Join(
                '\n',
                "  <testsuite name=\"tests/billing.e2e.ts\" tests=\"1\" failures=\"0\" errors=\"0\" skipped=\"1\" time=\"0.000\">",
                "    <testcase name=\"pays [web]\" classname=\"tests/billing.e2e.ts\" time=\"0.000\">",
                "      <skipped message=\"left out by filter jev: Jev: 4% likely affected\"/>",
                "    </testcase>",
                "  </testsuite>"
            ),
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal("1", root["tests"]);
        Assert.Equal("1", root["skipped"]);
    }

    [Fact]
    public void Renders_a_passed_result_as_a_bare_testcase_named_by_title_path_and_target()
    {
        var xml = Render(Document([Result()]));
        Assert.Contains(
            "<testcase name=\"auth &gt; signs in [web]\" classname=\"tests/auth.e2e.ts\" time=\"1.234\"/>",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<testsuite name=\"tests/auth.e2e.ts\" tests=\"1\" failures=\"0\" errors=\"0\" skipped=\"0\" time=\"1.234\">",
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0", "0"], [root["tests"], root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Appends_the_agent_to_the_case_name_when_it_is_not_default_so_a_persona_sweep_keeps_one_case_per_persona()
    {
        var xml = Render(Document([Result(agent: "buyer"), Result(id: "result-2", agent: "admin")]));
        Assert.Contains("<testcase name=\"auth &gt; signs in [web] [buyer]\"", xml, StringComparison.Ordinal);
        Assert.Contains("<testcase name=\"auth &gt; signs in [web] [admin]\"", xml, StringComparison.Ordinal);
        Assert.Equal("2", RootAttributes(xml)["tests"]);
    }

    [Fact]
    public void Uses_the_final_attempt_for_time_and_notes_flaky_passes_in_system_out()
    {
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Flaky,
                    attempts:
                    [
                        Attempt(index: 0, status: AttemptStatus.Failed, durationMs: 5000, error: Error()),
                        Attempt(id: "attempt-2", index: 1, durationMs: 800),
                    ]
                ),
            ])
        );
        Assert.Contains("time=\"0.800\">", xml, StringComparison.Ordinal);
        Assert.Contains(
            "<system-out>flaky: 1 failed attempt before passing</system-out>",
            xml,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("<failure", xml, StringComparison.Ordinal);
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0"], [root["tests"], root["failures"], root["errors"]]);
    }

    [Fact]
    public void Times_a_failed_test_by_its_failed_attempt_when_the_run_interrupted_its_retry()
    {
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        Attempt(index: 0, status: AttemptStatus.Failed, durationMs: 5000, error: Error()),
                        Attempt(id: "attempt-2", index: 1, status: AttemptStatus.Interrupted, durationMs: 20),
                    ]
                ),
            ])
        );
        Assert.Contains("time=\"5.000\">", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_a_test_category_error_as_failure_with_code_message_and_body()
    {
        var xml = Render(
            Document([
                Result(status: ResultStatus.Failed, attempts: [Attempt(status: AttemptStatus.Failed, error: Error())]),
            ])
        );
        Assert.Contains(
            "<failure message=\"expected &quot;Sign in&quot; to be visible\" type=\"ASSERTION_FAILED\">"
                + "ASSERTION_FAILED: expected &quot;Sign in&quot; to be visible</failure>",
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0"], [root["failures"], root["errors"]]);
    }

    [Fact]
    public void Renders_infrastructure_and_configuration_errors_as_error()
    {
        var infrastructure = Error(
            category: ErrorCategory.Infrastructure,
            code: "DRIVER_CRASHED",
            message: "gone",
            phase: ErrorPhase.Launch
        );
        var configuration = Error(
            category: ErrorCategory.Configuration,
            code: "MODEL_UNAVAILABLE",
            message: "no model"
        );
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Failed,
                    attempts: [Attempt(status: AttemptStatus.Failed, error: infrastructure)]
                ),
                Result(
                    id: "result-2",
                    testId: "test-2",
                    declarationIndex: 1,
                    titlePath: ["agent", "plans"],
                    status: ResultStatus.Failed,
                    attempts: [Attempt(status: AttemptStatus.Failed, error: configuration)]
                ),
            ])
        );
        Assert.Contains(
            "<error message=\"gone\" type=\"DRIVER_CRASHED\">DRIVER_CRASHED (launch): gone</error>",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<error message=\"no model\" type=\"MODEL_UNAVAILABLE\">MODEL_UNAVAILABLE: no model</error>",
            xml,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("<failure", xml, StringComparison.Ordinal);
        var root = RootAttributes(xml);
        Assert.Equal(["2", "0", "2"], [root["tests"], root["failures"], root["errors"]]);
    }

    [Fact]
    public void Falls_back_to_the_result_status_when_a_failed_result_carries_no_error()
    {
        var xml = Render(
            Document([
                Result(status: ResultStatus.TimedOut, attempts: [Attempt(status: AttemptStatus.TimedOut)]),
                Result(
                    id: "result-2",
                    testId: "test-2",
                    declarationIndex: 1,
                    status: ResultStatus.Interrupted,
                    attempts: [Attempt(status: AttemptStatus.Interrupted)]
                ),
            ])
        );
        Assert.Contains(
            "<failure message=\"timed-out\" type=\"timed-out\">timed-out</failure>",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains("<skipped message=\"interrupted: the run was stopped\"/>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Tells_the_failure_an_interrupted_retry_was_cut_short_after()
    {
        var interrupt = Error(
            category: ErrorCategory.Interrupted,
            code: "INTERRUPTED",
            message: "run interrupted in phase body"
        );
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        Attempt(index: 0, status: AttemptStatus.Failed, error: Error()),
                        Attempt(id: "attempt-2", index: 1, status: AttemptStatus.Interrupted, error: interrupt),
                    ]
                ),
            ])
        );
        Assert.Contains(
            "<failure message=\"expected &quot;Sign in&quot; to be visible\" type=\"ASSERTION_FAILED\">",
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0"], [root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Renders_an_interrupted_result_as_skipped_with_why_never_as_a_failure_or_an_error()
    {
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Interrupted,
                    attempts:
                    [
                        Attempt(
                            status: AttemptStatus.Interrupted,
                            error: Error(
                                category: ErrorCategory.Interrupted,
                                code: "INTERRUPTED",
                                message: "run interrupted in phase body"
                            )
                        ),
                    ]
                ),
            ])
        );
        Assert.Contains(
            "<skipped message=\"interrupted: run interrupted in phase body\"/>",
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "0", "0", "1"], [root["tests"], root["failures"], root["errors"], root["skipped"]]);
    }

    [Fact]
    public void Renders_skipped_results_with_their_reason_and_no_attempt_time()
    {
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Skipped,
                    attempts: [],
                    skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "tag filter excluded it" }
                ),
            ])
        );
        Assert.Contains("time=\"0.000\">", xml, StringComparison.Ordinal);
        Assert.Contains("<skipped message=\"tag filter excluded it\"/>", xml, StringComparison.Ordinal);
        var root = RootAttributes(xml);
        Assert.Equal(["1", "1"], [root["tests"], root["skipped"]]);
    }

    [Fact]
    public void Groups_results_into_one_suite_per_file_in_report_order()
    {
        var xml = Render(
            Document([
                Result(file: "tests/a.e2e.ts"),
                Result(id: "result-2", testId: "test-2", file: "tests/b.e2e.ts"),
                Result(id: "result-3", testId: "test-3", file: "tests/a.e2e.ts", targetId: "webkit"),
            ])
        );
        var suites = Suite().Matches(xml).Select(match => (match.Groups[1].Value, match.Groups[2].Value)).ToList();
        Assert.Equal([("tests/a.e2e.ts", "2"), ("tests/b.e2e.ts", "1")], suites);
        Assert.Contains("[webkit]", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_markup_in_attributes_and_text_and_strips_control_characters()
    {
        var xml = Render(
            Document([
                Result(
                    titlePath: ["<b>bold</b> & \"quoted\""],
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        Attempt(
                            status: AttemptStatus.Failed,
                            error: Error(message: "line one\nline \u0001<two> & done")
                        ),
                    ]
                ),
            ])
        );
        Assert.Contains(
            "name=\"&lt;b&gt;bold&lt;/b&gt; &amp; &quot;quoted&quot; [web]\"",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains("message=\"line one&#10;line �&lt;two&gt; &amp; done\"", xml, StringComparison.Ordinal);
        Assert.Contains(
            ">ASSERTION_FAILED: line one\nline �&lt;two&gt; &amp; done</failure>",
            xml,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("\u0001", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_a_carriage_return_in_the_failure_message_and_body_as_a_character_reference()
    {
        var xml = Render(
            Document([
                Result(
                    status: ResultStatus.Failed,
                    attempts:
                    [
                        Attempt(
                            status: AttemptStatus.Failed,
                            error: Error(code: "ERROR", message: "line one\r\nline two")
                        ),
                    ]
                ),
            ])
        );
        AssertWellFormed(xml);
        Assert.Contains("message=\"line one&#13;&#10;line two\"", xml, StringComparison.Ordinal);
        Assert.Contains(">ERROR: line one&#13;\nline two</failure>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("�", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Replaces_XML_forbidden_noncharacters_and_lone_surrogates_in_text_and_attributes()
    {
        const string lonePair = "😀";
        var xml = Render(
            Document(
                [
                    Result(
                        titlePath: [$"fffe\uFFFE high\uD83D low\uDE00 pair{lonePair}"],
                        status: ResultStatus.Failed,
                        attempts:
                        [
                            Attempt(
                                status: AttemptStatus.Failed,
                                error: Error(message: "ffff\uFFFF end\uDC00 start\uDBFF")
                            ),
                        ]
                    ),
                    Result(
                        id: "result-2",
                        testId: "test-2",
                        declarationIndex: 1,
                        status: ResultStatus.Skipped,
                        attempts: [],
                        skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "reason\uFFFE\uD800" }
                    ),
                ],
                [Error(category: ErrorCategory.Infrastructure, code: "APP_UNREACHABLE", message: "run\uFFFF\uDFFF")]
            )
        );
        Assert.DoesNotContain("\uFFFE", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("\uFFFF", xml, StringComparison.Ordinal);
        Assert.Contains($"name=\"fffe� high� low� pair{lonePair} [web]\"", xml, StringComparison.Ordinal);
        Assert.Contains("message=\"ffff� end� start�\"", xml, StringComparison.Ordinal);
        Assert.Contains(">ASSERTION_FAILED: ffff� end� start�</failure>", xml, StringComparison.Ordinal);
        Assert.Contains("<skipped message=\"reason��\"/>", xml, StringComparison.Ordinal);
        Assert.Contains("message=\"run��\" type=\"APP_UNREACHABLE\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_the_well_formedness_check_itself_on_a_forbidden_character()
    {
        Assert.Matches(
            "forbidden",
            Assert
                .Throws<InvalidOperationException>(() =>
                    AssertWellFormed("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a>\uFFFE</a>\n")
                )
                .Message
        );
        Assert.Matches(
            "forbidden",
            Assert
                .Throws<InvalidOperationException>(() =>
                    AssertWellFormed("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a>\uD800</a>\n")
                )
                .Message
        );
        Assert.Matches(
            "unbalanced",
            Assert
                .Throws<InvalidOperationException>(() =>
                    AssertWellFormed("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a><b></a>\n")
                )
                .Message
        );
        Assert.Matches(
            "ampersand",
            Assert
                .Throws<InvalidOperationException>(() =>
                    AssertWellFormed("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a x=\"1 & 2\"/>\n")
                )
                .Message
        );
    }

    [Fact]
    public void Adds_a_run_suite_with_one_error_case_per_run_level_error()
    {
        var xml = Render(
            Document(
                errors:
                [
                    Error(
                        category: ErrorCategory.Infrastructure,
                        code: "APP_UNREACHABLE",
                        message: "http://127.0.0.1:3000 did not answer",
                        phase: ErrorPhase.Launch
                    ),
                ]
            )
        );
        Assert.Contains(
            "<testsuite name=\"run\" tests=\"1\" failures=\"0\" errors=\"1\" skipped=\"0\" time=\"0.000\">",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<testcase name=\"APP_UNREACHABLE (launch)\" classname=\"run\" time=\"0.000\">",
            xml,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<error message=\"http://127.0.0.1:3000 did not answer\" type=\"APP_UNREACHABLE\">"
                + "infrastructure error APP_UNREACHABLE (launch): http://127.0.0.1:3000 did not answer</error>",
            xml,
            StringComparison.Ordinal
        );
        var root = RootAttributes(xml);
        Assert.Equal(["1", "1"], [root["tests"], root["errors"]]);
    }

    [Theory]
    [InlineData(".e2e/results", ".e2e/results/checkout/001-failure.png")]
    [InlineData(".", "checkout/001-failure.png")]
    public void Links_the_failure_screenshot_in_system_out_below_artifactsDir(string artifactsDir, string expected)
    {
        var xml = Render(FailedWithShot(), new JunitReportOptions { ArtifactsDir = artifactsDir });
        Assert.Contains($"<system-out>[[ATTACHMENT|{expected}]]</system-out>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_no_system_out_when_no_artifactsDir_is_given()
    {
        Assert.DoesNotContain(
            "<system-out>",
            Render(FailedWithShot(), new JunitReportOptions()),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Writes_no_system_out_when_the_failure_has_no_screenshot()
    {
        var document = FailedWithShot(failure: new FailureEvidence());
        Assert.DoesNotContain(
            "<system-out>",
            Render(document, new JunitReportOptions { ArtifactsDir = "." }),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Writes_no_system_out_when_the_screenshot_has_no_path()
    {
        var document = FailedWithShot(artifacts: [ShotArtifact("shot-1", null, "https://example.com/shot.png")]);
        Assert.DoesNotContain(
            "<system-out>",
            Render(document, new JunitReportOptions { ArtifactsDir = "." }),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Escapes_XML_characters_in_the_attachment_path()
    {
        var xml = Render(
            FailedWithShot(artifacts: [ShotArtifact("shot-1", "shots/foo&<bar>.png")]),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );
        Assert.Contains(
            "<system-out>[[ATTACHMENT|.e2e/shots/foo&amp;&lt;bar&gt;.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Picks_the_failure_screenshot_by_id_not_the_first_artifact()
    {
        var artifacts = new[] { ShotArtifact("other", "shots/first.png"), ShotArtifact("shot-1", "shots/target.png") };
        var xml = Render(FailedWithShot(artifacts: artifacts), new JunitReportOptions { ArtifactsDir = "." });
        Assert.Contains("<system-out>[[ATTACHMENT|shots/target.png]]</system-out>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Takes_the_screenshot_of_the_told_attempt_when_the_last_retry_was_interrupted()
    {
        var interrupted = Attempt(
            id: "attempt-2",
            index: 1,
            status: AttemptStatus.Interrupted,
            error: Error(category: ErrorCategory.Interrupted, code: "INTERRUPTED", message: "cut")
        );
        var xml = Render(FailedWithShot(extraAttempts: [interrupted]), new JunitReportOptions { ArtifactsDir = "." });
        Assert.Contains(
            "<system-out>[[ATTACHMENT|checkout/001-failure.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_a_flaky_test_after_the_flaky_note()
    {
        var passed = Attempt(id: "attempt-2", index: 1, status: AttemptStatus.Passed);
        var xml = Render(
            FailedWithShot(status: ResultStatus.Flaky, extraAttempts: [passed]),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );
        Assert.Contains(
            "<system-out>flaky: 1 failed attempt before passing\n[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Keeps_the_expected_note_between_the_flaky_note_and_the_screenshot()
    {
        var error = Error(code: "ASSERTION_FAILED", message: "cart is empty");
        var covered = Attempt(
            id: "attempt-2",
            index: 1,
            status: AttemptStatus.Passed,
            expectedFailure: new ExpectedFailureRecord { Reason = "bug 12", Error = error }
        );
        var xml = Render(
            FailedWithShot(status: ResultStatus.Flaky, extraAttempts: [covered]),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );
        Assert.Contains(
            "<system-out>flaky: 1 failed attempt before passing\nfailed as expected: bug 12\nASSERTION_FAILED: cart is empty\n[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_an_errored_test_after_its_error()
    {
        var crashed = Error(category: ErrorCategory.Infrastructure, code: "DRIVER_CRASHED", message: "gone");
        var xml = Render(FailedWithShot(error: crashed), new JunitReportOptions { ArtifactsDir = ".e2e" });
        Assert.Contains(
            "<error message=\"gone\" type=\"DRIVER_CRASHED\">DRIVER_CRASHED: gone</error>\n      <system-out>[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_a_timed_out_test()
    {
        var xml = Render(
            FailedWithShot(attemptStatus: AttemptStatus.TimedOut, status: ResultStatus.TimedOut),
            new JunitReportOptions { ArtifactsDir = ".e2e" }
        );
        Assert.Contains(
            "<system-out>[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            xml,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Links_the_failure_screenshot_of_a_test_fail_pass_after_the_expected_note()
    {
        var error = Error(code: "ASSERTION_FAILED", message: "cart is empty");
        var document = Document([
            Result(
                titlePath: ["checkout"],
                status: ResultStatus.Passed,
                attempts:
                [
                    Attempt(
                        status: AttemptStatus.Passed,
                        expectedFailure: new ExpectedFailureRecord { Reason = "bug 12", Error = error },
                        failure: new FailureEvidence { Screenshot = "shot-1" },
                        artifacts: [ShotArtifact("shot-1", "checkout/001-failure.png")]
                    ),
                ]
            ),
        ]);
        Assert.Contains(
            "<system-out>failed as expected: bug 12\nASSERTION_FAILED: cart is empty\n[[ATTACHMENT|.e2e/checkout/001-failure.png]]</system-out>",
            Render(document, new JunitReportOptions { ArtifactsDir = ".e2e" }),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<system-out>failed as expected: bug 12\nASSERTION_FAILED: cart is empty</system-out>",
            Render(document),
            StringComparison.Ordinal
        );
    }
}
