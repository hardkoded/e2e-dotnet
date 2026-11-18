// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using E2E.Reports;

namespace E2E.HtmlReporter.Tests.Reporter;

public sealed class WriteReportTests : IDisposable
{
    private string? _root;

    public void Dispose()
    {
        if (_root is not null)
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string NewRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "e2e-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        return _root;
    }

    /// <summary>Upstream's <c>finishedRun</c>: the run is read from <c>.e2e/report.json</c>, where the report is written.</summary>
    private static FinishedRun FinishedRun(
        string projectRoot,
        System.Text.Json.Nodes.JsonObject? report = null,
        IReadOnlyDictionary<string, string>? traces = null,
        Func<string, string>? redact = null
    )
    {
        var reportPath = Path.Combine(projectRoot, ".e2e", "report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var document = report ?? ReporterFixtures.ReadReport();
        File.WriteAllText(reportPath, document.ToJsonString());
        return new FinishedRun(
            ReportSerializer.Read(document),
            ReportRunStatus.Passed,
            0,
            projectRoot,
            reportPath,
            Path.Combine(projectRoot, ".e2e", "results")
        )
        {
            Traces = traces ?? new Dictionary<string, string>(),
            Redact = redact ?? (text => text),
        };
    }

    private static async Task<(ReporterSummaryRow Row, JsonObject Payload)> WriteAsync(
        FinishedRun run,
        HtmlReporterOptions options,
        string pagePath
    )
    {
        var row = await HtmlReporter.WriteReportAsync(run, options, ReporterFixtures.Template);
        return (row, ReporterFixtures.ParsePayload(await File.ReadAllTextAsync(pagePath)));
    }

    [Fact]
    public async Task Writes_the_page_beside_the_results_directory_by_default()
    {
        var root = NewRoot();
        var trace = Path.Combine(".e2e", "results", "checkout-1a2b", "trace.md");
        var run = FinishedRun(root, traces: new Dictionary<string, string> { ["result-1"] = trace });
        var (row, payload) = await WriteAsync(
            run,
            new HtmlReporterOptions(),
            Path.Combine(root, ".e2e", "report.html")
        );

        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(
                    """
                    {
                      "media": { "mode": "link", "base": "results" },
                      "resultsDir": ".e2e/results",
                      "traces": { "result-1": ".e2e/results/checkout-1a2b/trace.md" },
                      "sources": {}
                    }
                    """
                ),
                new JsonObject
                {
                    ["media"] = payload["media"]!.DeepClone(),
                    ["resultsDir"] = payload["resultsDir"]!.DeepClone(),
                    ["traces"] = payload["traces"]!.DeepClone(),
                    ["sources"] = payload["sources"]!.DeepClone(),
                }
            )
        );
        Assert.True(JsonNode.DeepEquals(ReporterFixtures.ReadReport(), payload["report"]));
        Assert.Equal("HTML report", row.Label);
        Assert.EndsWith("report.html", row.Text);
    }

    [Fact]
    public async Task Keeps_the_test_file_lines_around_each_source_location_redacted_and_no_file_outside_the_project()
    {
        var root = NewRoot();
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(Path.Combine(project, "tests"));
        await File.WriteAllTextAsync(
            Path.Combine(project, "tests", "example.e2e.ts"),
            string.Join(
                "\n",
                Enumerable
                    .Range(0, 20)
                    .Select(index => $"line {index + 1}{(index == 9 ? " fill('hunter2-staging')" : "")}")
            )
        );
        await File.WriteAllTextAsync(Path.Combine(root, "secret.ts"), "secret");
        var outside = ReporterFixtures.ReadReport();
        outside["run"]!["results"]![2]!["source"]!["file"] = "../secret.ts";
        var run = FinishedRun(project, outside, redact: text => text.Replace("hunter2-staging", "[secret]"));
        var (_, payload) = await WriteAsync(
            run,
            new HtmlReporterOptions(),
            Path.Combine(project, ".e2e", "report.html")
        );
        var sources = payload["sources"]!.AsObject();

        // The fixture points at lines 1, 3, 9, and 16; each frame keeps two lines either side.
        Assert.Equal(["tests/example.e2e.ts"], sources.Select(file => file.Key));
        var lines = sources["tests/example.e2e.ts"]!.AsObject();
        Assert.Equal(
            [1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 14, 15, 16, 17, 18],
            lines.Select(line => int.Parse(line.Key)).Order()
        );
        Assert.Equal("line 9", (string?)lines["9"]);
        Assert.Equal("line 10 fill('[secret]')", (string?)lines["10"]);
    }

    [Fact]
    public async Task Links_artifacts_from_wherever_outputFile_puts_the_page()
    {
        var root = NewRoot();
        var (_, payload) = await WriteAsync(
            FinishedRun(root),
            new HtmlReporterOptions { OutputFile = "reports/e2e/index.html" },
            Path.Combine(root, "reports", "e2e", "index.html")
        );

        Assert.Equal("link", (string?)payload["media"]!["mode"]);
        Assert.Equal("../../.e2e/results", (string?)payload["media"]!["base"]);
    }

    /// <summary>The fixture report with these artifacts on its first attempt.</summary>
    private static JsonObject WithArtifacts(JsonArray artifacts)
    {
        var report = ReporterFixtures.ReadReport();
        report["run"]!["serialGroups"] = new JsonArray();
        report["run"]!["results"]![0]!["attempts"]![0]!["artifacts"] = artifacts;
        return report;
    }

    private static JsonObject Artifact(
        string id,
        string mediaType,
        string path,
        string kind = "download",
        string redaction = "complete"
    ) =>
        new()
        {
            ["id"] = id,
            ["kind"] = kind,
            ["mediaType"] = mediaType,
            ["path"] = path,
            ["redaction"] = redaction,
            ["producer"] = new JsonObject { ["kind"] = "attempt" },
        };

    [Fact]
    public async Task Embeds_the_files_the_report_names_under_the_results_directory_except_downloads_not_scanned_for_secrets()
    {
        var root = NewRoot();
        var results = Path.Combine(root, ".e2e", "results");
        Directory.CreateDirectory(Path.Combine(results, "a"));
        await File.WriteAllTextAsync(Path.Combine(results, "a", "shot.png"), "png bytes");
        await File.WriteAllTextAsync(Path.Combine(results, "a", "page.html"), "<script>alert(1)</script>");
        await File.WriteAllTextAsync(Path.Combine(results, "a", "export.pdf"), "unscanned bytes");
        await File.WriteAllTextAsync(Path.Combine(root, "secret.txt"), "outside");
        var report = WithArtifacts([
            Artifact("shot", "image/png", "a/shot.png", "screenshot"),
            Artifact("page", "text/html", "a/page.html"),
            Artifact("gone", "video/webm", "a/video.webm"),
            Artifact("escape", "text/plain", "../../secret.txt"),
            Artifact("unscanned", "application/pdf", "a/export.pdf", redaction: "incomplete"),
        ]);

        var (row, payload) = await WriteAsync(
            FinishedRun(root, report),
            new HtmlReporterOptions { Media = HtmlReporterMedia.Embed },
            Path.Combine(root, ".e2e", "report.html")
        );

        Assert.True(
            JsonNode.DeepEquals(
                new JsonObject
                {
                    ["mode"] = "embed",
                    ["files"] = new JsonObject
                    {
                        ["a/shot.png"] =
                            $"data:image/png;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes("png bytes"))}",
                        ["a/page.html"] =
                            $"data:application/octet-stream;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes("<script>alert(1)</script>"))}",
                    },
                },
                payload["media"]
            )
        );
        Assert.Matches(@"report\.html \(one file, \d+ KB\)\z", row.Text);
    }

    [Fact]
    public async Task Leaves_media_out_for_text_only()
    {
        var root = NewRoot();
        var (row, payload) = await WriteAsync(
            FinishedRun(root),
            new HtmlReporterOptions { Media = HtmlReporterMedia.None },
            Path.Combine(root, ".e2e", "report.html")
        );

        Assert.Equal("none", (string?)payload["media"]!["mode"]);
        Assert.Matches(@"\(text only\)\z", row.Text);
    }
}
