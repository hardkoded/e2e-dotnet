// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using E2E.Tests.FailureEvidence;
using E2E.Tests.TypedReport;

namespace E2E.Tests.OpenHostReport;

/// <summary>
/// Upstream's <c>describe('openHost: report')</c> (<c>tests/integration/open-host-report.test.ts</c>), driven through sessions of
/// <see cref="E2ESession.StartAsync"/>: the port has no host to open sessions from, so "host reuse" is a second session over the same results
/// directory. The first `it` is ported as a partial test, renamed, without the <c>gadget.dropFile()</c> fixture
/// call, which the port's fake engine has no fixture for, and without the soft failure counted among <c>close()</c>'s failures: a session
/// returns none, <c>CloseSoftFailures</c> returns the aggregate the test fails with. The report holds the screenshot the failure kept.
/// "returns undefined when the trace write fails" is ported as a partial test, renamed, with <c>null</c>, and its <c>close()</c> failure is not: the port writes the trace page
/// as a courtesy and tells stderr when it cannot, so the session releases without a failure. <c>Tells_the_same_result_as_report_json_for_the_same_session</c> is .NET-only: upstream's hosted session writes no <c>report.json</c>.
/// </summary>
public sealed class OpenHostReportTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-host-report-").FullName;

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Results => Path.Combine(_output, "results");

    [Fact]
    public async Task Returns_independent_closed_reports_that_remain_stable_across_a_second_session()
    {
        var engine = new EvidenceEngine("Submit");
        var session = await StartAsync(engine, "snapshot");
        try
        {
            Assert.Null(session.Report());
            await session.App.OpenAsync("/");
            await Expect.Soft(session.Screen.GetByRole("button", "Submit")).ToHaveCountAsync(2, TimeSpan.FromMilliseconds(200));
            var soft = session.CloseSoftFailures();
            Assert.NotNull(soft);
            session.Complete(soft);
            await session.DisposeAsync();

            var report = session.Report()!;
            Assert.Equal(ReportRunStatus.Failed, report.Run.Status);
            var attempt = report.Run.Results[0].Attempts[0];
            Assert.Equal(session.AttemptId, attempt.Id);
            Assert.NotEmpty(attempt.Artifacts);
            foreach (var artifact in attempt.Artifacts)
            {
                Assert.Equal(artifact.Size, new FileInfo(Path.Combine(Results, artifact.Path!)).Length);
                Assert.Matches("^[a-f0-9]{64}$", artifact.Sha256!);
            }

            var original = ReportJsonWriter.Write(report);
            Assert.Same(report, session.Report());

            var next = await StartAsync(engine, "next attempt");
            try
            {
                Assert.Null(next.Report());
                next.Complete();
                await next.DisposeAsync();
                Assert.Equal(next.AttemptId, next.Report()!.Run.Results[0].Attempts[0].Id);
                Assert.Equal(original, ReportJsonWriter.Write(session.Report()));
            }
            finally
            {
                await next.DisposeAsync();
            }

            await session.DisposeAsync();
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tells_the_same_result_as_report_json_for_the_same_session()
    {
        var session = await StartAsync(new EvidenceEngine("Submit"), "same as the file");
        try
        {
            await session.App.OpenAsync("/");
            session.Complete(new TestException("TEST_FAILED", "nope"));
        }
        finally
        {
            await session.DisposeAsync();
        }

        var document = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(_output, "report.json")))!;
        var written = ReportSerializer.Read(document);

        // The typed report tells the whole document, bar the serial groups the port has none of.
        document["run"]!.AsObject().Remove("serialGroups");
        Assert.True(JsonNode.DeepEquals(document, JsonNode.Parse(ReportJsonWriter.Write(written))), "The typed report must tell the document of report.json.");
        var report = session.Report()!;
        var fromFile = written.Run.Results[0];
        Assert.NotEqual("unknown", fromFile.Source.File);
        Assert.Equal("TEST_FAILED", fromFile.Attempts[0].Error!.Code);
        Assert.Equal(ReportJsonWriter.Write(fromFile), ReportJsonWriter.Write(report.Run.Results[0]));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Status), ReportJsonWriter.Write(report.Run.Status));
        Assert.Equal(written.Run.ExitCode, report.Run.ExitCode);
        Assert.Equal(ReportJsonWriter.Write(written.Run.Project), ReportJsonWriter.Write(report.Run.Project));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Environment), ReportJsonWriter.Write(report.Run.Environment));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Vcs), ReportJsonWriter.Write(report.Run.Vcs));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Targets), ReportJsonWriter.Write(report.Run.Targets));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Summary), ReportJsonWriter.Write(report.Run.Summary));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Limits), ReportJsonWriter.Write(report.Run.Limits));
        Assert.Equal(ReportJsonWriter.Write(written.Run.Usage), ReportJsonWriter.Write(report.Run.Usage));
    }

    [Fact]
    public async Task Returns_null_when_the_trace_write_fails_and_the_session_still_releases()
    {
        var session = await StartAsync(new EvidenceEngine("Submit"), "failed write");
        try
        {
            Directory.CreateDirectory(Path.Combine(Results, AttemptReport.ResultSegmentOf(null, "failed write"), "trace.md"));
            session.Complete(new TestException("TEST_FAILED", "nope"));
            await session.DisposeAsync();
            Assert.Null(session.Report());
            await session.DisposeAsync();
            Assert.Null(session.Report());
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    private Task<E2ESession> StartAsync(EvidenceEngine engine, string title) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = title,
            BaseUrl = "http://127.0.0.1:4599",
            ProjectRoot = ProjectRoot(),
            ResultsDirectory = Results,
            CacheEnabled = false,
            Trace = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target),
        });

    private static string ProjectRoot([CallerFilePath] string testFile = "") => Path.GetDirectoryName(Path.GetDirectoryName(testFile))!;
}
