// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportFailureText.FailureTextKit;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('renderTracePage')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class RenderTracePageTests
{
    private static string Page(IReadOnlyList<ReportAttempt> attempts, Func<string, string?>? readArtifact = null)
    {
        var failing = ReportBuilders.Result(id: "r1", titlePath: ["todos", "archives a todo"], file: "tests/todos.e2e.ts", status: ResultStatus.Failed, attempts: attempts);
        var document = new Report1Document(new ReportRun("run-1", "0.0.0", [new ReportTarget("web", 0, "web", BuildReport.DescribeTarget(null))], [failing]));
        return FailureText.RenderTracePage(document, failing, Outcome.Of(failing), new TracePageOptions { ArtifactsDir = ".e2e/artifacts", ReadArtifact = readArtifact });
    }

    [Fact]
    public void Lists_the_screenshots_a_step_attached_under_that_step_and_no_other_artifact()
    {
        ReportArtifact[] artifacts =
        [
            new("a:0", "screenshot", "t/screenshots/home-expected.png"),
            new("a:1", "screenshot", "t/screenshots/home-actual.png"),
            new("a:2", "screenshot", "t/screenshots/home-diff.png"),
            new("a:3", "log", "t/console.txt"),
        ];

        var body = Page([Failed(steps: [Step(0), Step(1, api: "expect.toHaveScreenshot", status: StepStatus.Failed, artifacts: ["a:0", "a:1", "a:2", "a:3"])], artifacts: artifacts)]);

        var lines = body.Split('\n');
        var at = Array.FindIndex(lines, line => line.Contains("`expect.toHaveScreenshot`", StringComparison.Ordinal));
        Assert.Equal(
            [
                "   - screenshot `.e2e/artifacts/t/screenshots/home-expected.png`",
                "   - screenshot `.e2e/artifacts/t/screenshots/home-actual.png`",
                "   - screenshot `.e2e/artifacts/t/screenshots/home-diff.png`",
            ],
            lines[(at + 1)..(at + 4)]);
        Assert.DoesNotContain("console.txt", lines[at + 4], StringComparison.Ordinal);
    }

    [Fact]
    public void Quotes_a_step_explanation_that_starts_like_a_heading_as_prose_with_its_linkable_tokens_as_code_through_the_shared_cell()
    {
        var body = Page([Failed(steps: [Step(0, StepKind.Agent, "agent.act", StepStatus.Failed, label: "pay", explanation: "# Blocked\nsee @octocat at https://evil.example/p")])]);

        Assert.Contains("   > \\# Blocked see `@octocat` at `https://evil.example/p`\n", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\n# Blocked", body);
        Assert.DoesNotMatch(@"[^`]@octocat", body);
    }

    [Fact]
    public void Tells_the_whole_story_the_error_in_full_its_facts_the_line_every_step_the_turns_the_screen_inline_and_the_evidence()
    {
        var screen = new ReportArtifact("a:0", "log", "t/screen-at-failure.txt");
        var shot = new ReportArtifact("a:1", "screenshot", "t/001-failure.png");
        var source = new SourceLocation("tests/todos.e2e.ts", 8, 3);
        var body = Page(
            [
                Failed(
                    ReportBuilders.Error("ASSERTION_FAILED", "agent.act failed: no Archive button", source: source),
                    [
                        Step(0, StepKind.App, "app.open", label: "/todos", durationMs: 40, source: new("tests/todos.e2e.ts", 4, 9)),
                        Step(
                            1,
                            StepKind.Agent,
                            "agent.act",
                            StepStatus.Failed,
                            ReportBuilders.Error("ASSERTION_FAILED", "no Archive button", source: source),
                            source,
                            durationMs: 16_100,
                            metrics: new StepMetrics { ModelCalls = 6, ActionSteps = 4, ContextBytes = 1, LedgerBytes = 1 },
                            label: "Archive the todo",
                            explanation: "no Archive button",
                            turns:
                            [
                                new StepTurn(1, ["tap({\"target\":\"n19\"})"], "Tapped #n19.\n\nScreen changes: 4 added."),
                                new StepTurn(2, ["complete_step({\"status\":\"failed\"})"], "Step concluded."),
                            ]),
                    ],
                    [screen, shot],
                    new E2E.FailureEvidence { Url = "http://app.test/todos", Viewport = new StepViewport(1280, 720, 1), Nodes = 2, Screen = "a:0", Screenshot = "a:1" }),
            ],
            reportPath => reportPath == "t/screen-at-failure.txt" ? "#n1 document \"Todos\"\n #n19 button \"Add\"\n" : null);

        Assert.Equal(
            string.Join(
                '\n',
                "# ✗ todos › archives a todo",
                "",
                "`tests/todos.e2e.ts` · failed · 1.2s",
                "",
                "**ASSERTION_FAILED**",
                "",
                "```text",
                "agent.act failed: no Archive button",
                "```",
                "",
                "Look at: `tests/todos.e2e.ts:8`  ",
                "",
                "## Steps",
                "",
                "1. ✓ `app.open` `/todos` (40ms) — `tests/todos.e2e.ts:4`",
                "2. ✗ `agent.act` \"Archive the todo\" (16.1s, 6 model calls) — **ASSERTION_FAILED** — `tests/todos.e2e.ts:8`",
                "   > no Archive button",
                "",
                "## Agent turns of step 2",
                "",
                "The last 2 turns the model took, oldest first: what it called and what came back.",
                "",
                "**Turn 1** `tap({\"target\":\"n19\"})`  ",
                "> Tapped #n19.",
                "> ",
                "> Screen changes: 4 added.",
                "",
                "**Turn 2** `complete_step({\"status\":\"failed\"})`  ",
                "> Step concluded.",
                "",
                "## Screen at failure",
                "",
                "`http://app.test/todos` · viewport 1280x720 · 2 nodes  ",
                "",
                "The screen as the agent reads it, one node per line: `#id role \"name\" text=\"…\" [states]`.",
                "",
                "```text",
                "#n1 document \"Todos\"",
                " #n19 button \"Add\"",
                "```",
                "",
                "## Evidence",
                "",
                "- screenshot `.e2e/artifacts/t/001-failure.png`",
                "- screen `.e2e/artifacts/t/screen-at-failure.txt`",
                "",
                "<sub>e2e 0.0.0 · run `run-1` · the whole run is in `report.json`</sub>",
                ""),
            body);
    }

    [Fact]
    public void Points_at_the_screen_text_when_it_cannot_read_it_lists_the_candidates_and_cuts_a_screen_past_the_cap()
    {
        var screen = new ReportArtifact("a:0", "log", "t/screen-at-failure.txt");

        var unread = Page([Failed(artifacts: [screen], failure: new E2E.FailureEvidence { Screen = "a:0", Candidates = ["#n2 button \"Add\""] })]);
        Assert.Contains("Closest to what the locator asked for:  \n- `#n2 button \"Add\"`\nscreen `.e2e/artifacts/t/screen-at-failure.txt`  ", unread, StringComparison.Ordinal);

        var huge = Page([Failed(artifacts: [screen], failure: new E2E.FailureEvidence { Screen = "a:0" })], _ => new string('x', 30_000));
        Assert.Contains("…[cut at 24000 characters; the file has the rest]", huge, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_when_no_error_was_recorded_names_a_hook_phase_and_keeps_a_fence_in_a_message_from_closing_the_block()
    {
        Assert.Contains("_failed: no error was recorded._", Page([ReportBuilders.Attempt(AttemptStatus.Failed)]), StringComparison.Ordinal);

        var hook = Page([Failed(ReportBuilders.Error(phase: "afterEach", message: "cleanup ```broke```"))]);
        Assert.Contains("**ASSERTION_FAILED** in afterEach", hook, StringComparison.Ordinal);
        Assert.Contains("cleanup '''broke'''", hook, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_skip_a_failed_attempt_took_before_its_soft_failures_were_settled()
    {
        Assert.Contains("Also skipped: flag \\| off  ", Page([Failed(skip: new ReportSkip("explicit", "flag | off"))]), StringComparison.Ordinal);
        Assert.DoesNotContain("Also skipped", Page([Failed()]), StringComparison.Ordinal);
    }
}
