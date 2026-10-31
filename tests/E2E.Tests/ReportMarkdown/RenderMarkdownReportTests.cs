// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportMarkdown.MarkdownKit;

namespace E2E.Tests.ReportMarkdown;

/// <summary>Upstream's <c>describe('renderMarkdownReport')</c> (<c>tests/unit/report-markdown.test.ts</c>).</summary>
public sealed class RenderMarkdownReportTests
{
    private static string Render(Report1Document document, MarkdownReportOptions? options = null) => MarkdownReport.RenderMarkdownReport(document, options);

    [Fact]
    public void Leads_with_the_counts_and_the_spend_blocks_each_failure_with_its_step_and_the_agents_word_folds_the_flaky_tests_and_folds_every_test_by_file()
    {
        var body = Render(Page(ReportRunStatus.Failed, [Passing, Failing, Flaky, Skipped, Judged], usage: Spent), Links);

        Assert.Equal(
            string.Join(
                '\n',
                "### 🔴 e2e: 1 failed, 1 flaky, 2 passed, 1 skipped",
                "4 agent steps · 1 replayed from cache · 17 model calls · 128.4k tokens (62% cached) · $0.31",
                "",
                string.Join(
                    "\n\n",
                    "**🔴 members › an email invitation is accepted by the invited account only**  \n[tests/members.e2e.ts:41](https://github.com/o/r/blob/abc/tests/members.e2e.ts#L41)",
                    "**ASSERTION_FAILED** at step 4 of 6: `agent.act` \"Accept the invitation from the email\", after 38.0s and 12 model calls",
                    "> expected heading \"Welcome, Ada\" to be visible\n> The Accept button opened a page that still shows Sign in.",
                    "Evidence: [screenshot](https://github.com/o/r/actions/runs/9), [video](https://github.com/o/r/actions/runs/9)"),
                "",
                string.Join(
                    '\n',
                    "<details>",
                    "<summary>⚠️ 1 flaky test passed on a retry</summary>",
                    "",
                    string.Join(
                        "\n\n",
                        "**⚠️ todos survive a filter round-trip**  \n[tests/todos.e2e.ts:3](https://github.com/o/r/blob/abc/tests/todos.e2e.ts#L3)",
                        "**STEP_TIMEOUT** at step 2 of 2: `agent.waitFor` \"the filtered list\", after 30.0s",
                        "> slow",
                        "Evidence: [screenshot](https://github.com/o/r/actions/runs/9)"),
                    "</details>"),
                "",
                string.Join(
                    '\n',
                    "<details>",
                    "<summary>All 5 tests in 3 files</summary>",
                    "",
                    "|  | Test | Agent | Time |",
                    "| --- | --- | --- | --- |",
                    "| 🔴 | **tests/members.e2e.ts** · 1 failed | 2 steps · 16 calls | 1.2s |",
                    "| 🔴 | members › an email invitation is accepted by the invited account only | 2 steps · 16 calls | 1.2s |",
                    "| ⚠️ | **tests/todos.e2e.ts** · 1 flaky, 1 skipped | 1 step | 6.4s |",
                    "| ⚠️ | todos survive a filter round-trip (1 failed attempt first) | 1 step | 6.4s |",
                    "| ⏭️ | not ready yet (skipped: waiting on the API) |  |  |",
                    "| 🟢 | **tests/smoke.e2e.ts** · 2 passed | 1 step · 1 call | 3.0s |",
                    "| 🟢 | opens the app |  | 850ms |",
                    "| 🟢 | dashboard › opens directly | 1 step · 1 call | 2.1s |",
                    "</details>"),
                "",
                "<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web · [run artifacts](https://github.com/o/r/actions/runs/9)</sub>",
                ""),
            body);
    }

    [Fact]
    public void Is_a_passing_headline_the_folded_table_without_an_agent_column_and_the_footer_when_everything_passed_with_no_spend_line_for_a_deterministic_run()
    {
        Assert.Equal(
            string.Join(
                '\n',
                "### 🟢 e2e: 1 passed",
                "",
                "<details>\n<summary>All 1 test in 1 file</summary>\n\n|  | Test | Time |\n| --- | --- | --- |\n| 🟢 | **tests/smoke.e2e.ts** · 1 passed | 850ms |\n| 🟢 | opens the app | 850ms |\n</details>",
                "",
                "<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web</sub>",
                ""),
            Render(Page(results: [Passing])));
    }

    [Fact]
    public void Partial_Leaves_a_result_the_selection_did_not_choose_out_of_the_counts_and_the_list_and_reads_one_without_the_flag_as_selected()
    {
        // Partial: the second half reads a document written before results carried the flag. The port's result is a record whose flag is
        // not optional (it defaults to selected), so only the first half has something to check.
        var unselected = Named("agent journey", ResultStatus.Skipped, file: "tests/agent/tests.e2e.ts", skip: new ReportSkip("filtered", "file not selected by a positional argument")) with { Selected = false };

        var body = Render(Page(results: [Passing, unselected]));

        Assert.Contains("### 🟢 e2e: 1 passed\n", body);
        Assert.Contains("All 1 test in 1 file", body);
        Assert.DoesNotContain("tests/agent/tests.e2e.ts", body);
        Assert.DoesNotContain("positional argument", body);
    }

    [Fact]
    public void Names_the_page_after_its_title_so_two_comments_on_one_pull_request_read_apart()
    {
        Assert.Contains("### 🟢 e2e regression: 1 passed\n", Render(Page(results: [Passing]), new MarkdownReportOptions { Title = "regression" }));
        Assert.Contains("### 🟢 e2e: 1 passed\n", Render(Page(results: [Passing]), new MarkdownReportOptions { Title = "  " }));
        Assert.Contains("### 🔴 e2e nightly explore: 1 issue\n", Render(Explored(), new MarkdownReportOptions { Title = "nightly" }));
    }

    [Fact]
    public void Names_the_failed_step_whatever_its_status_and_never_retells_the_steps_before_it()
    {
        var many = Enumerable.Range(0, 9).Select(index => Step(index, "step " + index, StepKind.Agent, "agent.act")).ToList();
        string At(StepStatus status) =>
            Render(Page(
                ReportRunStatus.Failed,
                [
                    Named(
                        "long flow",
                        ResultStatus.Failed,
                        attempts:
                        [
                            Attempt(
                                AttemptStatus.Failed,
                                error: new ReportError("E", "m"),
                                steps:
                                [
                                    .. many,
                                    Step(9, "the order confirmation", StepKind.Agent, "agent.waitFor", status, 30_000, error: ReportBuilders.Error("E", "m")),
                                    Step(10, "after", status: StepStatus.Cancelled),
                                ]),
                        ]),
                ]));

        foreach (var status in new[] { StepStatus.TimedOut, StepStatus.Blocked, StepStatus.Cancelled })
        {
            Assert.Contains("**🔴 long flow**  \n`tests/example.e2e.ts:3`\n\n**E** at step 10 of 11: `agent.waitFor` \"the order confirmation\", after 30.0s\n\n> m\n", At(status));
            Assert.DoesNotContain("step 8", At(status));
        }

        // A step that is not the agent's reads as the one call it was.
        var single = Named("one", ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", "m"), steps: [Step(0, "only", status: StepStatus.Failed, error: ReportBuilders.Error("E", "m"))])]);
        Assert.Contains("**E** at step 1 of 1: `screen.tap only`, after 900ms", Render(Page(ReportRunStatus.Failed, [single])));
    }

    [Fact]
    public void Names_the_step_whose_error_ended_the_test_not_an_earlier_failure_the_test_caught_and_went_on_from()
    {
        static SourceLocation At(int line) => new("tests/example.e2e.ts", line, 5);
        var caught = Named(
            "caught waitFor, then a failing assertion",
            ResultStatus.Failed,
            attempts:
            [
                Attempt(
                    AttemptStatus.Failed,
                    error: new ReportError("ASSERTION_FAILED", "expect.toHaveCount failed\nexpected: count 5\nobserved: count 1"),
                    steps:
                    [
                        Step(0, "/todos", StepKind.App, "app.open", source: At(4)),
                        Step(1, "getByText(\"Nothing here\") → visible", StepKind.Locator, "locator.waitFor", StepStatus.Failed, 303, At(8), error: ReportBuilders.Error("LOCATOR_NOT_FOUND", "locator did not become visible")),
                        Step(2, "tap Add", source: At(12)),
                        Step(3, "getByTestId(\"todo\")", StepKind.Assertion, "expect.toHaveCount", StepStatus.Failed, 502, At(15), error: ReportBuilders.Error(message: "expect.toHaveCount failed")),
                    ]),
            ]);

        var body = Render(Page(ReportRunStatus.Failed, [caught]));

        // The line and the lead are the assertion's; the caught step is not the failure.
        Assert.Contains("**🔴 caught waitFor, then a failing assertion**  \n`tests/example.e2e.ts:15`\n\n**ASSERTION_FAILED** at step 4 of 4: `expect.toHaveCount getByTestId(\"todo\")`, after 502ms", body);
        Assert.DoesNotContain("locator.waitFor", body);
    }

    [Fact]
    public void Quotes_nothing_when_the_message_only_says_the_api_failed_lists_the_facts_and_shows_a_loopback_screen_as_its_path()
    {
        List<StepRecord> locators =
        [
            Step(0, "getByRole(\"button\", name: \"Save Changes\")", StepKind.Locator, "getByRole"),
            Step(1, "getByRole(\"button\", name: \"Save Changes\")", StepKind.Assertion, "expect.toBeHidden", StepStatus.Failed, 15_100, error: ReportBuilders.Error(message: "expect.toBeHidden failed")),
        ];
        var error = new ReportError("ASSERTION_FAILED", "expect.toBeHidden failed\nlocator: getByRole(\"button\", name: \"Save Changes\")\nexpected: hidden or absent\nobserved: default states (match count 1)")
        {
            Details = new ErrorDetails { Locator = "getByRole(\"button\", name: \"Save Changes\")", Expected = "hidden or absent", Observed = "default states", Matches = 1 },
        };
        var failedAttempt = Attempt(AttemptStatus.Failed, error: error, steps: locators) with
        {
            Failure = new E2E.FailureEvidence { Url = "http://127.0.0.1:3100/dashboard/p1/settings?tab=env", Candidates = ["#n41 button \"Save Changes\" [disabled]"] },
        };

        var body = Render(Page(ReportRunStatus.Failed, [Named("settings", ResultStatus.Failed, attempts: [failedAttempt])]));

        // The message's first line only says the api failed, which the lead says; nothing is quoted. Nobody can open a loopback URL, so its path is enough.
        Assert.Contains(
            string.Join(
                "\n\n",
                "**🔴 settings**  \n`tests/example.e2e.ts:3`",
                "**ASSERTION_FAILED** at step 2 of 2: `expect.toBeHidden getByRole(\"button\", name: \"Save Changes\")`, after 15.1s",
                "- Expected: hidden or absent\n- Observed: default states (1 match)\n- Screen: `/dashboard/p1/settings?tab=env`\n- Closest to the locator: `#n41 button \"Save Changes\" [disabled]`"),
            body);

        // The errors the app logged come last among the facts, oldest first, warnings and info left to the trace page.
        var logged = Attempt(AttemptStatus.Failed, error: error, steps: locators) with
        {
            AppLog =
            [
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Info, "ready", At.AddSeconds(-3), 0),
                new AppLogRecord(AppLogSource.Error, AppLogLevel.Error, "TypeError: x at http://127.0.0.1:3100/app.js:3:7", At.AddSeconds(-1), 1),
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Error, "Error occurred prerendering page \"/settings\"", At.AddSeconds(-2), 0),
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Warning, "deprecated", At, 1),
            ],
        };
        var withLog = Render(Page(ReportRunStatus.Failed, [Named("settings", ResultStatus.Failed, attempts: [logged])]));
        Assert.Contains("- Observed: default states (1 match)\n- App console error: `Error occurred prerendering page \"/settings\"`\n- App uncaught error: `TypeError: x at /app.js:3:7`\n\n", withLog);
        Assert.DoesNotContain("`ready`", withLog);
        Assert.DoesNotContain("deprecated", withLog);

        // A message that says more than the api is quoted, first line only: the facts carry the rest.
        var worded = Render(Page(ReportRunStatus.Failed, [Named("settings", ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, error: error with { Message = "the Save button stayed\nexpected: hidden" }, steps: locators)])]));
        Assert.Contains(", after 15.1s\n\n> the Save button stayed\n\n- Expected:", worded);
    }

    [Fact]
    public void Puts_run_level_errors_before_the_failures_and_says_no_tests_ran()
    {
        var body = Render(Page(ReportRunStatus.Error, errors: [new ReportError("APP_UNREACHABLE", "http://127.0.0.1:3000 did not answer", "launch") { Category = "infrastructure", Retryable = true }]));

        Assert.Contains("### 🔴 e2e: no tests ran\n\n> **APP_UNREACHABLE** (launch) `http://127.0.0.1:3000` did not answer\n", body);
        Assert.DoesNotContain("<details>", body);
    }

    [Fact]
    public void Says_no_tests_selected_when_nothing_ran_and_nothing_failed()
    {
        Assert.Equal("### 🟢 e2e: no tests selected\n\n<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web</sub>\n", Render(Page()));
    }

    [Fact]
    public void Escapes_what_a_test_wrote_keeps_it_on_one_line_and_keeps_a_link_destination_closed()
    {
        var hostile = Named(
            "a | b <img src=x onerror=alert(1)> *c*",
            ResultStatus.Failed,
            file: "tests/we|rd.e2e.ts",
            attempts:
            [
                Attempt(
                    AttemptStatus.Failed,
                    error: new ReportError("ASSERTION_FAILED", "line one\nline two \u001b[31mred\u001b[0m \u0007`tick`"),
                    steps:
                    [
                        Step(0, "tap `x` | y", api: "screen.`tap`", status: StepStatus.Failed, explanation: "saw <b>bold</b>\nand more", error: ReportBuilders.Error()),
                        Step(1, "z"),
                    ]),
            ]);

        var body = Render(Page(ReportRunStatus.Failed, [hostile]), new MarkdownReportOptions { ArtifactsUrl = "https://x.test/run (1)", SourceUrl = (_, _) => "https://x.test/blob)<script>" });

        Assert.Contains("a \\| b &lt;img src=x onerror=alert(1)&gt; \\*c\\*", body);
        // The canonical sanitizer drops color sequences and marks a control character, as the terminal does.
        Assert.Contains("line one line two red �\\`tick\\`", body);
        // A backtick in a label lengthens the code span's delimiter instead of vanishing.
        Assert.Contains("at step 1 of 2: `` screen.`tap` tap `x` \\| y ``, after 900ms\n\n> line one line two red �\\`tick\\`\n> saw &lt;b&gt;bold&lt;/b&gt; and more", body);
        Assert.Contains("| 🔴 | **tests/we\\|rd.e2e.ts** · 1 failed | 1.2s |", body);
        Assert.Contains("[tests/we\\|rd.e2e.ts:3](https://x.test/blob%29%3Cscript%3E)", body);
        Assert.Contains("[run artifacts](https://x.test/run%20%281%29)", body);
        Assert.DoesNotContain("\u001b", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0007", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_a_token_GitHub_would_link_as_code_and_keeps_a_quoted_line_from_opening_a_block()
    {
        // GitHub's mention and issue filters run on the rendered text, so an entity or a backslash before `@` or `#` changes nothing; a code span is skipped.
        var hostile = Named(
            "ping @octocat @org/team, see #123 GH-7, open https://evil.example/p (www.evil.example) <https://e.example/`x`|y>",
            ResultStatus.Failed,
            attempts:
            [
                Attempt(
                    AttemptStatus.Failed,
                    error: new ReportError("ASSERTION_FAILED", "# Heading\n- item"),
                    steps: [Step(0, "pay", StepKind.Agent, "agent.act", StepStatus.Failed, explanation: "1. numbered\nmail a@b.example, color:#123456, x#1", error: ReportBuilders.Error())]),
            ]);

        var body = Render(Page(ReportRunStatus.Failed, [hostile]));

        Assert.Contains("ping `@octocat` `@org/team,` see `#123` `GH-7,` open `https://evil.example/p` `(www.evil.example)` `` <https://e.example/`x`\\|y> ``", body);
        Assert.Contains("\n\n> \\# Heading - item\n> 1\\. numbered mail `a@b.example,` `color:#123456,` `x#1`\n", body);
        Assert.DoesNotMatch("[^`]@octocat", body);
        Assert.DoesNotMatch("[^`]https://evil", body);
    }

    [Fact]
    public void Escapes_every_other_block_opener_at_the_start_of_a_quoted_line_a_plus_or_underscore_item_and_a_parenthesised_list_marker()
    {
        var opener = Named(
            "openers",
            ResultStatus.Failed,
            attempts:
            [
                Attempt(
                    AttemptStatus.Failed,
                    error: new ReportError("ASSERTION_FAILED", "+ item\n_ under"),
                    steps: [Step(0, "pay", StepKind.Agent, "agent.act", StepStatus.Failed, explanation: "1) list", error: ReportBuilders.Error())]),
            ]);

        var body = Render(Page(ReportRunStatus.Failed, [opener]));

        // The message collapses to one line, so only its first character opens a block; the explanation is its own quoted line.
        Assert.Contains("\n\n> \\+ item _ under\n> 1\\) list\n", body);
    }

    [Fact]
    public void Clips_long_text_by_code_point_never_through_an_emoji()
    {
        // 121 and 241 code points: the cut lands on the emoji, which must survive whole.
        var @long = Named(new string('x', 118) + "💥yz", ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", new string('m', 238) + "💥zz"))]);

        var body = Render(Page(ReportRunStatus.Failed, [@long]));

        Assert.False(HasLoneSurrogate(body));
        Assert.Contains(new string('x', 118) + "💥…", body);
        // A quoted message gets the room prose gets: 600 code points.
        Assert.Contains("> " + new string('m', 238) + "💥zz", body);
        var prose = Named("p", ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", new string('m', 598) + "💥zz"))]);
        Assert.Contains("> " + new string('m', 598) + "💥…", Render(Page(ReportRunStatus.Failed, [prose])));
    }

    [Fact]
    public void Rounds_a_duration_to_whole_seconds_before_splitting_off_the_minutes_and_formats_tokens_and_cost_as_the_terminal_does()
    {
        string At(string finishedAt) => Render(Page(results: [Passing], finishedAt: DateTimeOffset.Parse(finishedAt, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Contains("· 2m 0s ·", At("2026-01-01T00:01:59.500Z"));
        Assert.Contains("· 1m 29s ·", At("2026-01-01T00:01:29.400Z"));
        Assert.Contains("· 59.9s ·", At("2026-01-01T00:00:59.940Z"));

        string Usage(long modelTokens, double estimatedCostUsd) => Render(Page(results: [Judged], usage: new ReportUsage { ModelTokens = modelTokens, EstimatedCostUsd = estimatedCostUsd }));
        Assert.Contains("1 agent step · 1 model call · 950 tokens · $0.0040\n", Usage(950, 0.004));
        Assert.Contains("· 9.4k tokens · $1.50\n", Usage(9_400, 1.5));
        Assert.Contains("· 2.4M tokens · $12.00\n", Usage(2_400_000, 12));
    }

    [Fact]
    public void Names_the_target_only_when_the_results_ran_on_several_under_a_block_title_on_the_file_rows_and_in_the_footer_capped_there()
    {
        List<ReportTarget> twoTargets = [Target(), Target("mobile", 1)];
        var body = Render(Page(ReportRunStatus.Failed, [Failing, Passing with { TargetId = "mobile" }], twoTargets));
        Assert.Contains("**🔴 members › an email invitation is accepted by the invited account only**  \n`tests/members.e2e.ts:41` · web\n", body);
        // The file row names the target; the test rows under it need not.
        Assert.Contains("| 🟢 | **tests/smoke.e2e.ts (mobile)** · 1 passed |  | 850ms |\n| 🟢 | opens the app |  | 850ms |", body);

        // Two configured, one selected (`--target web`): the page is about that one.
        var one = Render(Page(ReportRunStatus.Failed, [Failing, Passing], twoTargets));
        Assert.DoesNotContain("(web)", one);
        Assert.Contains("`tests/members.e2e.ts:41`\n\n", one);
        Assert.Contains("<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web</sub>", one);

        // One file on two targets is two groups and one file.
        var twice = Render(Page(results: [Passing, Passing with { TargetId = "mobile" }], targets: [Target(), Target("mobile", 1)]));
        Assert.Contains("<summary>All 2 tests in 1 file</summary>", twice);
        Assert.Contains("2 targets (web, mobile)", body);
        var many = Render(Page(results: [Passing], targets: [.. Enumerable.Range(0, 12).Select(index => Target("t" + index, index))]));
        Assert.Contains("12 targets (t0, t1, t2, t3, t4, t5, t6, t7, and 4 more)</sub>", many);
    }

    [Fact]
    public void Lists_files_worst_first_in_the_fold_each_row_with_the_counts_of_its_tests()
    {
        var skippedOnly = Named("later", ResultStatus.Skipped, file: "tests/a.e2e.ts", skip: new ReportSkip("explicit", "later"));

        var body = Render(Page(ReportRunStatus.Failed, [Passing, skippedOnly, Flaky, Failing]));

        var rows = body.Split('\n').Where(line => line.Contains("| **tests/", StringComparison.Ordinal));
        Assert.Equal(
            [
                "| 🔴 | **tests/members.e2e.ts** · 1 failed | 2 steps · 16 calls | 1.2s |",
                "| ⚠️ | **tests/todos.e2e.ts** · 1 flaky | 1 step | 6.4s |",
                "| ⏭️ | **tests/a.e2e.ts** · 1 skipped |  | 0ms |",
                "| 🟢 | **tests/smoke.e2e.ts** · 1 passed |  | 850ms |",
            ],
            rows);
    }

    [Fact]
    public void Caps_the_folded_list_saying_how_many_more_there_are()
    {
        var results = Enumerable.Range(0, 450).Select(index => Named("t" + index, ResultStatus.Passed, file: "tests/f" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".e2e.ts", attempts: [Attempt()])).ToList();

        var body = Render(Page(results: results));

        Assert.Contains("<summary>All 450 tests in 450 files</summary>", body);
        Assert.Contains("|  | and 50 more |  |\n</details>", body);
        Assert.DoesNotContain("**tests/f449.e2e.ts**", body);
    }

    [Fact]
    public void Folds_every_flaky_test_under_one_summary_and_caps_them()
    {
        var flakes = Enumerable.Range(0, 35).Select(index => Named("flake " + index, ResultStatus.Flaky, attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", "m")), Attempt()])).ToList();

        var body = Render(Page(results: flakes));

        Assert.Contains("<summary>⚠️ 35 flaky tests passed on a retry</summary>", body);
        Assert.Contains("**⚠️ flake 29**", body);
        Assert.DoesNotContain("**⚠️ flake 30**", body);
        Assert.Contains("and 5 more\n</details>", body);
    }

    [Fact]
    public void Partial_Tolerates_a_document_written_before_results_carried_repeat_or_one_giving_a_flaky_test_a_single_attempt()
    {
        // Partial: a document without `repeat` cannot be built, the record's repeat is a number that defaults to 0; the check reads that default.
        var legacyBody = Render(Page(ReportRunStatus.Failed, [Named("checkout", ResultStatus.Failed)]));
        Assert.Contains("checkout", legacyBody);
        Assert.DoesNotContain("repeat #", legacyBody);

        var oneAttempt = Named("odd", ResultStatus.Flaky, attempts: [Attempt()]);

        var foreignBody = Render(Page(results: [oneAttempt]));

        Assert.Contains("| ⚠️ | odd (0 failed attempts first) | 1.2s |", foreignBody);
        Assert.DoesNotContain("-1", foreignBody);
    }

    [Fact]
    public void Caps_the_failure_blocks_and_drops_what_follows_before_the_body_outgrows_a_comment()
    {
        var failures = Enumerable.Range(0, 80).Select(index => Named("failure " + index, ResultStatus.Failed, attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", new string('x', 2_000)))]));
        var passes = Enumerable.Range(0, 600).Select(index => Named([string.Concat(Enumerable.Repeat("suite " + index + " ", 15)), new string('b', 120), new string('c', 120)], ResultStatus.Passed, file: "tests/file-" + (index % 40) + ".e2e.ts", attempts: [Attempt()]));

        var body = Render(Page(ReportRunStatus.Failed, [.. failures, .. passes]));

        // Thirty blocks, then how many were left out; the folded list does not fit, and the note says so.
        Assert.Contains("and 50 more failed", body);
        Assert.DoesNotContain("<details>", body);
        Assert.Contains("_Truncated to fit a pull request comment; the full report is in `report.json`._", body);
        Assert.True(body.Length < 65_536);
    }

    [Fact]
    public void Never_returns_a_body_GitHub_would_reject_whatever_the_report_holds()
    {
        // Sixty failures whose titles alone are longer than the whole budget allows.
        var wide = Enumerable.Range(0, 60).Select(index => Named(
            [.. Enumerable.Range(0, 30).Select(part => string.Concat(Enumerable.Repeat("segment " + index + "-" + part + " ", 8)))],
            ResultStatus.Failed,
            attempts: [Attempt(AttemptStatus.Failed, error: new ReportError("E", new string('m', 240)))])).ToList();
        var errors = Enumerable.Range(0, 40).Select(index => new ReportError("RUN_ERROR_" + index, new string('e', 240)) { Category = "infrastructure" }).ToList();

        var body = Render(Page(ReportRunStatus.Error, wide, errors: errors));

        Assert.True(body.Length <= 60_000);
        Assert.StartsWith("### 🔴 e2e: 60 failed\n", body);
        Assert.Contains("> and 20 more", body);
        Assert.Contains("_Truncated to fit a pull request comment; the full report is in `report.json`._", body);
        Assert.EndsWith("</sub>", body.TrimEnd());

        // A run with hundreds of targets does not escape the cap either.
        var targets = Enumerable.Range(0, 300).Select(index => Target("target-" + index + "-" + new string('t', 60), index)).ToList();
        Assert.True(Render(Page(results: [Passing], targets: targets)).Length <= 60_000);
    }
}
