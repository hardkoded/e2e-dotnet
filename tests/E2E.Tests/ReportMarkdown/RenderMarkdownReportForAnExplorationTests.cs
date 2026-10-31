// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ReportMarkdown.MarkdownKit;

namespace E2E.Tests.ReportMarkdown;

/// <summary>Upstream's <c>describe('renderMarkdownReport for an exploration')</c> (<c>tests/unit/report-markdown.test.ts</c>).</summary>
public sealed class RenderMarkdownReportForAnExplorationTests
{
    private static string Render(Report1Document document, MarkdownReportOptions? options = null) => MarkdownReport.RenderMarkdownReport(document, options);

    [Fact]
    public void Renders_the_goal_the_steps_every_finding_with_its_evidence_and_the_assessment_in_place_of_the_test_list()
    {
        var body = Render(
            Explored(
                findings:
                [
                    Finding(0, "Newsletter label misspells Receive", ReportExploreFindingKind.Warning, 1, step: 2, reproduction: [], path: "/checkout"),
                    Finding(1, "Cart total ignores quantity", artifactId: "a:artifact:0"),
                ],
                steps:
                [
                    new ReportExploreStep(1, "Cart", "x", ReportExploreStepStatus.Passed, At, TimeSpan.FromMilliseconds(1)),
                    new ReportExploreStep(2, "Checkout", "y", ReportExploreStepStatus.Failed, At, TimeSpan.FromMilliseconds(1), "the pay button did nothing"),
                ],
                ended: ReportExploreEnded.StepLimit),
            new MarkdownReportOptions { ArtifactsDir = ".e2e/artifacts" });

        Assert.Equal(
            string.Join(
                '\n',
                "### 🔴 e2e explore: 1 issue, 1 warning",
                "",
                "**Goal:** Explore checkout  ",
                "**Steps:** 2 of 8 steps · 1 passed · 1 failed · the step limit was reached",
                "",
                "**Findings**",
                "",
                string.Join(
                    "  \n",
                    "1. **high issue** Cart total ignores quantity · `/cart` · step 1",
                    "   Expected: The total reflects the cart",
                    "   Actual: Total: $0.00",
                    "   Steps: 1. Add two items 2. Open the cart",
                    "   Evidence: screenshot `.e2e/artifacts/web/explore-checkout-d287e8aead677d36/default/attempt-0/finding-0.png`"),
                string.Join("  \n", "2. **trivial warning** Newsletter label misspells Receive · `/checkout` · step 2", "   Expected: The total reflects the cart", "   Actual: Total: $0.00"),
                "",
                "**Assessment**",
                "",
                "The cart is the weak spot.",
                "",
                "<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web</sub>",
                ""),
            body);
        // The exploration's own failure is the verdict the findings express, so it gets no block and no table.
        Assert.DoesNotContain("**🔴 explore", body);
        Assert.DoesNotContain("<details>", body);
    }

    [Fact]
    public void Keeps_a_backtick_inside_a_linkable_token_by_lengthening_the_code_span_delimiter_as_GFM_reads_it()
    {
        var body = Render(Explored(summary: "open https://example.test/`code` then https://x.example/``q`` and https://y.example/z` or plain https://ok.example"));

        Assert.Contains("open `` https://example.test/`code` `` then ``` https://x.example/``q`` ``` and `` https://y.example/z` `` or plain `https://ok.example`\n", body);
    }

    [Theory]
    [InlineData("# Verdict\n- the cart is the weak spot", "\\# Verdict - the cart is the weak spot")]
    [InlineData("1. the cart is the weak spot", "1\\. the cart is the weak spot")]
    [InlineData("---", "\\---")]
    [InlineData("1.5 stars, see @octocat", "1.5 stars, see `@octocat`")]
    [InlineData("+ item", "\\+ item")]
    [InlineData("= title", "\\= title")]
    [InlineData("_ under", "\\_ under")]
    [InlineData("1) list", "1\\) list")]
    [InlineData("a+b = c_d (1) e", "a+b = c_d (1) e")]
    public void Keeps_an_assessment_that_starts_like_a_heading_a_list_a_rule_or_another_block_opener_as_prose(string summary, string prose)
    {
        Assert.Contains("**Assessment**\n\n" + prose + "\n", Render(Explored(summary: summary)));
    }

    [Fact]
    public void Links_the_evidence_to_the_run_page_when_there_is_one_and_names_the_kind_when_the_reader_has_neither()
    {
        Assert.Contains("   Evidence: [screenshot](https://ci.test/run/1)", Render(Explored(), new MarkdownReportOptions { ArtifactsUrl = "https://ci.test/run/1" }));
        Assert.Contains("   Evidence: screenshot\n", Render(Explored()));
    }

    [Fact]
    public void Says_what_an_exploration_without_findings_amounts_to_and_keeps_a_failure_that_is_not_the_verdict()
    {
        Assert.Contains("### 🟢 e2e explore: no findings\n", Render(Explored(findings: [], status: ReportRunStatus.Passed)));
        Assert.Contains("### 🔴 e2e explore: explored nothing\n", Render(Explored(findings: [], steps: [], ended: ReportExploreEnded.Stuck, status: ReportRunStatus.Blocked)));

        var provider = Explored(findings: [], status: ReportRunStatus.Error);
        var result = provider.Run.Results[0];
        var attempt = result.Attempts[0] with
        {
            Status = AttemptStatus.Failed,
            Error = new ReportError("MODEL_PROVIDER_FAILED", "gateway 502") { Category = "infrastructure", Retryable = true },
        };
        var body = Render(new Report1Document(provider.Run with { Results = [result with { Status = ResultStatus.Failed, Attempts = [attempt] }] }));

        Assert.Contains("### 🔴 e2e explore: did not finish\n", body);
        Assert.Contains("**🔴 Explore checkout**  \n`explore:3`\n\n**MODEL_PROVIDER_FAILED**\n\n> gateway 502\n\nEvidence: screenshot", body);
    }

    [Fact]
    public void Caps_the_findings_and_escapes_what_the_agent_wrote()
    {
        var many = Enumerable.Range(0, 40).Select(index => Finding(index, "finding " + index + " <b>*x*</b>", severity: (index % 5) + 1)).ToList();

        var body = Render(Explored(findings: many));

        Assert.Contains("31. and 10 more", body);
        Assert.Contains("**critical issue** finding 4 &lt;b&gt;\\*x\\*&lt;/b&gt;", body);
    }
}
