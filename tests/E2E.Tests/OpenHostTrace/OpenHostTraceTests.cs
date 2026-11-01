// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.OpenSession;
using E2E.Tests.Traces;

namespace E2E.Tests.OpenHostTrace;

/// <summary>
/// Upstream's <c>describe('openHost: trace')</c> (<c>tests/integration/open-host-trace.test.ts</c>), through <see cref="E2EHost"/>. Both `it` are partial.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class OpenHostTraceTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-host-trace-").FullName;
    private readonly List<E2EHost> _hosts = [];

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.CloseAsync().GetAwaiter().GetResult();
        }

        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Results => Path.Combine(_output, ".e2e", "results");

    [GeneratedRegex(@"\]\(([^)]+)\)")]
    private static partial Regex Link();

    private async Task<E2EHost> OpenHostAsync(IEngine engine, string config)
    {
        await File.WriteAllTextAsync(Path.Combine(_output, E2EConfig.FileName), config);
        var host = await E2EHost.OpenAsync(new OpenHostOptions
        {
            Cwd = _output,
            Env = new Dictionary<string, string?>(),
            EngineFactory = _ => engine,
        });
        _hosts.Add(host);
        return host;
    }

    /// <summary>
    /// <c>it.each(['failed', 'passed'])</c>. Partial: the video is the real web engine's (upstream's fake engine writes <c>fake.webm</c>
    /// and counts <c>artifacts.startVideo</c> and <c>artifacts.stopVideo</c> calls), and the session's directory is <c>attempt-1</c> of its
    /// test under the results directory, where upstream's is the one the host names.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Partial_Records_hosted_retain_on_failure_video_and_only_keeps_a_session_file(bool failed)
    {
        using var app = await FixtureApp.StartAsync();
        var title = "hosted video " + (failed ? "failed" : "passed");
        var opened = await OpenHostAsync(
            new WebEngine(),
            $$"""{ "trace": "on", "video": "retain-on-failure", "targets": [{ "name": "web", "platform": "web", "app": { "url": "{{app.Url}}" } }] }""");
        var session = await opened.OpenSessionAsync(new OpenSessionOptions { Name = title });
        await session.Fixtures.App.OpenAsync("/form");
        if (failed)
        {
            await Expect.Soft(session.Fixtures.Screen.GetByRole("textbox")).ToHaveCountAsync(5, TimeSpan.FromMilliseconds(200));
        }

        var failures = await session.CloseAsync();
        Assert.Equal(failed ? ["ASSERTION_FAILED"] : [], failures.OfType<E2EException>().Select(failure => failure.Code));
        var tracePath = session.TracePath;
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

        Assert.Empty(await opened.CloseAsync());
    }

    /// <summary>
    /// Partial: the port has no <c>gadget.dropFile()</c> fixture, so the gadget log file is not checked. The secret is the session's own
    /// (<c>Remember</c>), not a config secret an engine resolves. The two sessions share one title, so they share one results directory and one
    /// page, where upstream's hosted sessions each have their own directory under <c>results/host</c> (the page of the second session is checked,
    /// and the path inequality is not). The session directory is one segment below the results directory, not below <c>results/host</c>.
    /// </summary>
    [Fact]
    public async Task Partial_Writes_safe_per_session_trace_pages_with_readable_artifact_links_and_redacted_names_and_errors()
    {
        const string secret = "host-secret-7Xq-91";
        var opened = await OpenHostAsync(
            new HostFakeEngine(),
            """{ "trace": "on", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""");
        var name = $"../../escape\\folder/<script>\u001b[31m {secret}";
        for (var index = 0; index < 2; index++)
        {
            var session = await opened.OpenSessionAsync(new OpenSessionOptions { Name = name });
            session.Remember([Secret.Create("hostToken", secret)]);
            await session.Fixtures.App.ScreenshotAsync($"home {secret}");
            await Expect.Soft(session.Fixtures.Screen.GetByRole("button", "Submit")).ToHaveCountAsync(2, TimeSpan.FromMilliseconds(200));
            var failures = await session.CloseAsync();
            Assert.DoesNotContain(secret, string.Join(' ', failures.Select(failure => failure.Message)) + System.Text.Json.JsonSerializer.Serialize(session.Steps()), StringComparison.Ordinal);
            Assert.True(Path.IsPathRooted(session.TracePath));
            var relative = Path.GetRelativePath(Results, session.ArtifactsDir);
            Assert.DoesNotMatch(@"^\.\.(?:[\\/]|$)", relative);
            Assert.False(Path.IsPathRooted(relative));
            Assert.DoesNotContain(secret, relative, StringComparison.Ordinal);

            var trace = await File.ReadAllTextAsync(session.TracePath);
            Assert.Contains("app.screenshot", trace, StringComparison.Ordinal);
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
                Assert.NotNull(await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(session.TracePath)!, Uri.UnescapeDataString(link)))));
            }
        }

        Assert.Empty(await opened.CloseAsync());
        foreach (var file in Directory.EnumerateFiles(_output, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(secret, file, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)), StringComparison.Ordinal);
        }
    }

    /// <summary>.NET-only: a hosted session writes its page through <c>HostTrace.WriteHostTraceAsync</c>: a standalone page, no <c>report.json</c>, and a trace even when the trace mode keeps none.</summary>
    [Fact]
    public async Task Hosted_session_writes_a_standalone_page_even_when_the_trace_mode_is_off()
    {
        var opened = await OpenHostAsync(
            new HostFakeEngine(),
            """{ "trace": "off", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""");
        var session = await opened.OpenSessionAsync(new OpenSessionOptions { Name = "no trace" });
        Assert.Empty(await session.CloseAsync());
        Assert.Contains("no trace", await File.ReadAllTextAsync(session.TracePath), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Results, "report.json")));
        Assert.Equal(session.AttemptId, session.Report()!.Run.Results[0].Attempts[0].Id);
        Assert.Empty(await opened.CloseAsync());
    }

    /// <summary>.NET-only: <c>HostTrace.WriteHostTraceAsync</c> is upstream's <c>writeHostTrace</c></summary>
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
