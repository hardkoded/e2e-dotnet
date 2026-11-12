// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ReportConfigDigest;

/// <summary>
/// .NET-only: upstream has no <c>it</c> that pins <c>project.configDigest</c> in <c>buildReport</c> (its schema fixtures hold the digest). These
/// check the wiring of <c>config?.configDigest ?? '0'.repeat(64)</c> in <c>report/build.ts</c>.
/// </summary>
public sealed class ReportConfigDigestTests
{
    private static readonly string Digest = new('a', 64);

    private static ResolvedConfig Config(string digest) => new() { ProjectRoot = "/repo/app", Targets = [], ConfigDigest = digest };

    private static Report1Document Build(ResolvedConfig? config) =>
        BuildReport.Build(new BuildReportOptions
        {
            RunId = "run-1",
            StartedAt = DateTimeOffset.UnixEpoch,
            Status = ReportRunStatus.Passed,
            ExitCode = 0,
            Config = config,
        });

    [Fact]
    public void Reports_the_digest_of_the_resolved_config()
    {
        var document = Build(Config(Digest));

        Assert.Equal(Digest, document.Run.Project.ConfigDigest);
        Assert.Equal(Digest, ReportWire.Document(document, "results")["run"]!["project"]!["configDigest"]!.GetValue<string>());
    }

    [Fact]
    public void Reports_the_zero_digest_when_the_run_failed_before_the_config_resolved()
    {
        Assert.Equal(new string('0', 64), Build(null).Run.Project.ConfigDigest);
    }

    [Fact]
    public void Reports_the_zero_digest_when_the_resolver_computed_none()
    {
        Assert.Equal(new string('0', 64), Build(Config("")).Run.Project.ConfigDigest);
    }

    [Fact]
    public void Reports_a_digest_that_passes_the_report_1_schema()
    {
        ReportSchema.AssertValidReport(ReportWire.Document(Build(Config(Digest)), "results"));
    }
}
