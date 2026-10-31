// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ReportMarkdown.MarkdownKit;

namespace E2E.Tests.ReportMarkdown;

/// <summary>Upstream's <c>describe('renderMarkdownReport evidence paths')</c> (<c>tests/unit/report-markdown.test.ts</c>).</summary>
public sealed class RenderMarkdownReportEvidencePathsTests
{
    private static string Render(Report1Document document, MarkdownReportOptions? options = null) => MarkdownReport.RenderMarkdownReport(document, options);

    [Fact]
    public void Lists_one_artifact_path_per_kind_under_artifactsDir_when_there_is_no_run_page_in_kind_order_and_names_kinds_when_no_path_was_kept()
    {
        var evidence = Attempt(AttemptStatus.Failed, error: new ReportError("E", "m"), artifacts: ["download", "screenshot", "video", "log"]);

        var body = Render(Page(ReportRunStatus.Failed, [Named("t", ResultStatus.Failed, attempts: [evidence])]), new MarkdownReportOptions { ArtifactsDir = ".e2e/artifacts" });

        // One artifact per kind of the attempt the block tells, named by kind; a log is not evidence unless the failure captured it.
        Assert.Contains(
            "\n\nEvidence: screenshot `.e2e/artifacts/t/attempt-0/screenshot-1.bin`, video `.e2e/artifacts/t/attempt-0/video-2.bin`, download `.e2e/artifacts/t/attempt-0/download-0.bin`\n",
            body);
        var withheld = Attempt(AttemptStatus.Failed, error: new ReportError("E", "m"), artifacts: ["screenshot"]) with { Artifacts = [new ReportArtifact("attempt-1:artifact:0", "screenshot")] };
        var named2 = Render(Page(ReportRunStatus.Failed, [Named("t", ResultStatus.Failed, attempts: [withheld])]), new MarkdownReportOptions { ArtifactsDir = ".e2e/artifacts" });
        Assert.Contains("\n\nEvidence: screenshot\n", named2);
    }

    // .NET-only: upstream links any hosted URL; the port links only http and https (a URL from outside text).
    [Fact]
    public void NetOnly_Names_a_hosted_artifact_by_its_kind_when_its_URL_is_not_http_or_https()
    {
        var baseAttempt = Attempt(AttemptStatus.Failed, error: new ReportError("E", "m"), artifacts: ["screenshot"]);
        var evidence = baseAttempt with { Artifacts = [.. baseAttempt.Artifacts, new ReportArtifact("attempt-1:artifact:1", "video", Url: "javascript:alert(1)")] };

        var body = Render(Page(ReportRunStatus.Failed, [Named("t", ResultStatus.Failed, attempts: [evidence])]), new MarkdownReportOptions { ArtifactsUrl = "https://ci.test/run/1" });

        Assert.Contains("\n\nEvidence: [screenshot](https://ci.test/run/1), video\n", body);
        Assert.DoesNotContain("javascript:", body);
    }

    [Fact]
    public void Links_a_video_a_hosted_service_keeps_to_its_own_URL()
    {
        var baseAttempt = Attempt(AttemptStatus.Failed, error: new ReportError("E", "m"), artifacts: ["screenshot"]);
        var evidence = baseAttempt with { Artifacts = [.. baseAttempt.Artifacts, new ReportArtifact("attempt-1:artifact:1", "video", Url: "https://recordings.example/r1.mp4")] };
        var document = Page(ReportRunStatus.Failed, [Named("t", ResultStatus.Failed, attempts: [evidence])]);

        Assert.Contains(
            "\n\nEvidence: screenshot `.e2e/artifacts/t/attempt-0/screenshot-0.bin`, [video](https://recordings.example/r1.mp4)\n",
            Render(document, new MarkdownReportOptions { ArtifactsDir = ".e2e/artifacts" }));
        Assert.Contains(
            "\n\nEvidence: [screenshot](https://ci.test/run/1), [video](https://recordings.example/r1.mp4)\n",
            Render(document, new MarkdownReportOptions { ArtifactsUrl = "https://ci.test/run/1" }));
    }
}
