// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.FailureEvidence;
using E2E.Tests.Traces;

namespace E2E.Tests.OpenHostTrace;

/// <summary>
/// Upstream's <c>describe('openHost: trace')</c> (<c>tests/integration/open-host-trace.test.ts</c>), driven through sessions of
/// <see cref="E2ESession.StartAsync"/>: the port has no host to open sessions from (see COMPATIBILITY.md). Both `it` are partial.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class OpenHostTraceTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-host-trace-").FullName;

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Results => Path.Combine(_output, "results");

    [GeneratedRegex(@"\]\(([^)]+)\)")]
    private static partial Regex Link();

    /// <summary>
    /// <c>it.each(['failed', 'passed'])</c>. Partial: the video is the real web engine's (upstream's fake engine writes <c>fake.webm</c>
    /// and counts <c>artifacts.startVideo</c> and <c>artifacts.stopVideo</c> calls), the session is not opened by a host, and the failure is
    /// the test's own error where upstream's is a soft assertion that <c>close()</c> returns. The page is the session's (a run page footer),
    /// not the standalone page of <see cref="HostTrace"/>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Records_hosted_retain_on_failure_video_and_only_keeps_a_session_file_PARTIAL_real_web_engine_and_no_host(bool failed)
    {
        using var app = await FixtureApp.StartAsync();
        var title = "hosted video " + (failed ? "failed" : "passed");
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(),
            BaseUrl = app.Url,
            TestTitle = title,
            ResultsDirectory = Results,
            ProjectRoot = _output,
            CacheEnabled = false,
            Video = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Test),
            Trace = new ResolvedRecording(RecordingMode.On, RecordingSource.Test),
        });
        try
        {
            await session.App.OpenAsync();
            session.Complete(failed ? new TestException("ASSERTION_FAILED", "video soft failure") : null);
        }
        finally
        {
            await session.DisposeAsync();
        }

        var tracePath = Path.Combine(Results, AttemptReport.ResultSegmentOf(null, title), "trace.md");
        var trace = await File.ReadAllTextAsync(tracePath);
        if (failed)
        {
            var kept = Assert.IsType<VideoFile>(Assert.Single(session.Videos));
            var video = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tracePath)!, "attempt-1", kept.Path));
            Assert.True(new FileInfo(video).Length > 0);
            var links = Link().Matches(trace).Select(match => Uri.UnescapeDataString(match.Groups[1].Value));
            Assert.Contains(links, link => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tracePath)!, link)) == video);
            Assert.Contains("ASSERTION_FAILED", trace, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(session.Videos);
            Assert.DoesNotContain(".webm", trace, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(Results, "*.webm", SearchOption.AllDirectories));
        }
    }

    /// <summary>
    /// Partial, renamed: the port has no <c>app.screenshot(...)</c> or <c>gadget.dropFile()</c> fixture, so the page tells a step and the
    /// failure screen instead of those two calls and the gadget log file is not checked. The secret is the session's own, not a config secret
    /// an engine resolves. The two sessions share one title, so they share one results directory and one page, where upstream's hosted
    /// sessions each have their own directory (the page of the second attempt is checked, and the path inequality is not). The
    /// attempt directory is two segments below the results directory, where upstream's is one under <c>results/host</c>.
    /// </summary>
    [Fact]
    public async Task Writes_safe_per_session_trace_pages_with_readable_artifact_links_and_redacted_names_and_errors_PARTIAL_no_host_screenshot_or_gadget()
    {
        const string secret = "host-secret-7Xq-91";
        var name = $"../../escape\\folder/<script>\u001b[31m {secret}";
        var engine = new EvidenceEngine("Submit");
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = engine,
                TestTitle = name,
                Attempt = attempt,
                BaseUrl = "http://127.0.0.1:4599",
                ProjectRoot = _output,
                ResultsDirectory = Results,
                CacheEnabled = false,
                Trace = new ResolvedRecording(RecordingMode.On, RecordingSource.Test),
            });
            try
            {
                session.Remember([Secret.Create("hostToken", secret)]);
                await session.App.OpenAsync("/");
                session.Complete(new TestException("ASSERTION_FAILED", $"private {secret}"));
            }
            finally
            {
                await session.DisposeAsync();
            }

            var testDir = Path.Combine(Results, AttemptReport.ResultSegmentOf(null, name));
            var relative = Path.GetRelativePath(Results, testDir);
            Assert.DoesNotMatch(@"^\.\.(?:[\\/]|$)", relative);
            Assert.False(Path.IsPathRooted(relative));
            Assert.Single(relative.Split(Path.DirectorySeparatorChar));
            Assert.DoesNotContain(secret, relative, StringComparison.Ordinal);

            var tracePath = Path.Combine(testDir, "trace.md");
            var trace = await File.ReadAllTextAsync(tracePath);
            Assert.Contains("app.open", trace, StringComparison.Ordinal);
            Assert.Contains("ASSERTION_FAILED", trace, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, trace, StringComparison.Ordinal);
            Assert.DoesNotContain('\u001b', trace);
            Assert.DoesNotContain("<script>", trace, StringComparison.Ordinal);
            var local = Link().Matches(trace).Select(match => match.Groups[1].Value)
                .Where(link => !Regex.IsMatch(link, "^[a-z][a-z\\d+.-]*:", RegexOptions.IgnoreCase) && !link.StartsWith('#'))
                .ToList();
            Assert.NotEmpty(local);
            foreach (var link in local)
            {
                Assert.NotNull(await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(testDir, Uri.UnescapeDataString(link)))));
            }
        }

        foreach (var file in Directory.EnumerateFiles(_output, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(secret, file, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)), StringComparison.Ordinal);
        }
    }

    /// <summary>.NET-only: <c>HostTrace.WriteHostTraceAsync</c> is upstream's <c>writeHostTrace</c>, which nothing in the port calls until a host exists.</summary>
    [Fact]
    public async Task Writes_a_standalone_page_and_returns_the_report_of_the_attempt()
    {
        const string secret = "host-secret-7Xq-91";
        var result = ReportBuilders.Result(
            id: "aaaaaaaa11",
            status: ResultStatus.Failed,
            attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error(message: $"private {secret}"))]);
        var tracePath = Path.Combine(_output, "hosted", "trace.md");
        var report = await HostTrace.WriteHostTraceAsync(new HostTraceOptions
        {
            Attempt = ReportBuilders.Document(result),
            ProjectRoot = _output,
            ResultsDir = Results,
            Segments = ["aaaaaaaa11"],
            TracePath = tracePath,
            Redact = text => text.Replace(secret, "[secret:hostToken]", StringComparison.Ordinal),
        });

        var page = await File.ReadAllTextAsync(tracePath);
        Assert.Contains("ASSERTION_FAILED", page, StringComparison.Ordinal);
        Assert.Contains("[secret:hostToken]", page, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, page, StringComparison.Ordinal);
        Assert.DoesNotContain("report.json", page, StringComparison.Ordinal);
        Assert.Equal(ReportRunStatus.Failed, report.Run.Status);
        Assert.Equal(E2E.Reports.ResultStatus.Failed, Assert.Single(report.Run.Results).Status);
    }
}
