// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>.NET-only: upstream's page reads a report that was redacted when it was built. The port's page is built from records in memory, so the text is redacted once more on its way to the file.</summary>
public sealed class TracePageRedactionTests : TracePageKit
{
    [Fact]
    public async Task Redacts_the_page_text_before_it_is_written()
    {
        var root = Project();
        var results = Path.Combine(root, ".e2e", "results");
        var step = ReportBuilders.Step(0, StepKind.Agent, "agent.act", "sign in", cache: new CacheInfo { Mode = "missed", Reason = "no-entry", Detail = "typed hunter2-hunter2 into the box" });
        var result = ReportBuilders.Result(id: "aaaaaaaa11", status: ResultStatus.Failed, attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error(message: "the password hunter2-hunter2 was wrong"), steps: [step])]);

        await TracePages.WriteTracePagesAsync(
            ReportBuilders.Document(result),
            new HashSet<string> { result.Id },
            new WriteTracePagesOptions(root, results),
            text => text.Replace("hunter2-hunter2", "[secret:password]", StringComparison.Ordinal));

        var page = await File.ReadAllTextAsync(Directory.GetFiles(results, "trace.md", SearchOption.AllDirectories).Single());
        Assert.DoesNotContain("hunter2", page, StringComparison.Ordinal);
        Assert.Contains("[secret:password]", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Links_a_video_only_when_its_url_is_http_or_https()
    {
        ReportResult Failed(string id, string url) =>
            ReportBuilders.Result(id: id, status: ResultStatus.Failed, attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error()) with { Artifacts = [new("v:0", "video", Url: url)] }]);

        var unsafePage = Assert.Single(await PagesOfAsync(Failed("bbbbbbbb22", "javascript:alert(1)"))).Text;
        var safePage = Assert.Single(await PagesOfAsync(Failed("cccccccc33", "https://replay.example/a.mp4"))).Text;

        Assert.DoesNotContain("javascript:", unsafePage, StringComparison.Ordinal);
        Assert.Contains("[video](https://replay.example/a.mp4)", safePage, StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_a_token_github_would_link_as_code_and_escapes_what_could_open_markdown()
    {
        Assert.Equal("see `@mallory` and `https://evil.example/x`", MarkdownText.Cell("see @mallory and https://evil.example/x"));
        Assert.Equal("\\# heading \\*bold\\* \\| &lt;b&gt;", MarkdownText.Cell("# heading *bold* | <b>"));
        Assert.Equal("1\\. one", MarkdownText.Cell("1. one"));
        Assert.Equal("`` a`b ``", MarkdownText.Code("a`b"));
    }

    [Fact]
    public void Clips_by_code_point_and_tells_durations_in_the_unit_that_reads_best()
    {
        Assert.Equal("ab…", MarkdownText.Cell("abcdef", 3));
        Assert.Equal("😀…", MarkdownText.Cell("😀😀😀", 2));
        Assert.Equal(["12ms", "5.0s", "2m 0s"], new[] { MarkdownText.FormatDuration(12), MarkdownText.FormatDuration(5000), MarkdownText.FormatDuration(119_500) });
    }
}
