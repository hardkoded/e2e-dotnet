// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using E2E.Tests.FailureEvidence;

namespace E2E.Tests.OpenHostReport;

/// <summary>
/// Upstream's <c>describe('openHost: report')</c> (<c>tests/integration/open-host-report.test.ts</c>), driven through sessions of
/// <see cref="E2ESession.StartAsync"/>: the port has no host to open sessions from, so "host reuse" is a second session over the same results
/// directory. The first `it` is ported as a partial test, renamed, without the <c>gadget.dropFile()</c> fixture
/// call, which the port's fake engine has no fixture for, and without the soft failure counted among <c>close()</c>'s failures: a session
/// returns none, <c>CloseSoftFailures</c> returns the aggregate the test fails with. The report holds the screenshot the failure kept.
/// "returns undefined when the trace write fails" is ported as a partial test, renamed, with <c>null</c>, and its <c>close()</c> failure is not: the port writes the trace page
/// as a courtesy and tells stderr when it cannot, so the session releases without a failure.
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
            ReportSchema.AssertValidReport(report);
            Assert.Equal("failed", (string)report["run"]!["status"]!);
            var attempt = report["run"]!["results"]![0]!["attempts"]![0]!;
            Assert.Equal(session.AttemptId, (string)attempt["id"]!);
            var artifacts = attempt["artifacts"]!.AsArray();
            Assert.NotEmpty(artifacts);
            foreach (var artifact in artifacts)
            {
                Assert.Equal((long)artifact!["size"]!, new FileInfo(Path.Combine(Results, (string)artifact["path"]!)).Length);
                Assert.Matches("^[a-f0-9]{64}$", (string)artifact["sha256"]!);
            }

            var original = report.ToJsonString();
            report["run"]!["usage"]!["modelTokens"] = 999;
            report["run"]!["targets"]![0]!["engine"]!["name"] = "caller mutation";
            artifacts[0]!["size"] = 999;
            Assert.Equal(original, session.Report()!.ToJsonString());

            var next = await StartAsync(engine, "next attempt");
            try
            {
                Assert.Null(next.Report());
                next.Complete();
                await next.DisposeAsync();
                Assert.Equal(next.AttemptId, (string)next.Report()!["run"]!["results"]![0]!["attempts"]![0]!["id"]!);
                Assert.Equal(original, session.Report()!.ToJsonString());
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
