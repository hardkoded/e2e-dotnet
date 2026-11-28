// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Reports;

namespace E2E.Tests.ArtifactStore;

/// <summary><c>describe('artifact redaction labels')</c> of upstream <c>artifact-store.test.ts</c>.</summary>
public sealed class ArtifactRedactionLabelsTests : ArtifactStoreHarness
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Marks_files_that_never_appeared_incomplete_and_keeps_reports_schema_valid_store(bool withStore)
    {
        var store = Capturing();
        var ledger = Redactor.For(new (string Name, string Value)[] { ("api-key", "missing-download-secret-9921") });
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(
            new AttemptArtifactsOptions
            {
                ArtifactsRoot = Root(),
                Segments = ["web", "test-1", "attempt-0"],
                AttemptId = "att-1",
                Ledger = () => ledger,
                Store = withStore ? store : null,
            }
        );
        File.WriteAllText(Path.Combine(artifacts.Dir, "present.png"), "x");
        File.WriteAllText(Path.Combine(artifacts.Dir, "present.txt"), "ok");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "missing.png");
        artifacts.Sink.Register(ArtifactKind.Log, "missing.txt");
        artifacts.Sink.Register(ArtifactKind.Download, "missing.csv");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "present.png");
        artifacts.Sink.Register(ArtifactKind.Log, "present.txt");

        // Before settlement no path, size, or digest exists yet, so records stay incomplete.
        foreach (var record in artifacts.Records)
        {
            Assert.Null(record.Path);
            Assert.Equal(ArtifactRedaction.Incomplete, record.Redaction);
        }

        await artifacts.SettleAsync();

        foreach (var missing in artifacts.Records.Take(3))
        {
            Assert.Null(missing.Path);
            Assert.Null(missing.Size);
            Assert.Null(missing.Sha256);
            Assert.Equal(ArtifactRedaction.Incomplete, missing.Redaction);
        }

        var presentShot = artifacts.Records[3];
        var presentLog = artifacts.Records[4];
        Assert.Equal("web/test-1/attempt-0/present.png", presentShot.Path);
        Assert.Equal(ArtifactRedaction.Complete, presentShot.Redaction);
        Assert.Equal(1, presentShot.Size);
        Assert.Equal("web/test-1/attempt-0/present.txt", presentLog.Path);
        Assert.Equal(ArtifactRedaction.Complete, presentLog.Redaction);
        Assert.Equal(2, presentLog.Size);
        Assert.Equal(withStore ? 2 : 0, store.Puts.Count);

        var report = BuildReport.Build(
            new BuildReportOptions
            {
                RunId = Guid.CreateVersion7().ToString(),
                StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Status = ReportRunStatus.Passed,
                ExitCode = 0,
                Results =
                [
                    new ReportResult
                    {
                        Id = "result-missing-artifacts",
                        File = "tests/case.e2e.ts",
                        TitlePath = ["missing artifacts"],
                        Status = ResultStatus.Passed,
                        Attempts =
                        [
                            new ReportAttempt
                            {
                                Status = E2E.Internal.AttemptStatus.Passed,
                                DurationMs = 10,
                                Artifacts =
                                [
                                    .. artifacts.Records.Select(record => new ReportArtifact(
                                        record.Id,
                                        record.Kind.ToString().ToLowerInvariant(),
                                        record.Path,
                                        record.Url,
                                        record.StartedAt,
                                        Redaction: record.Redaction,
                                        Ref: record.Ref,
                                        Producer: record.Producer,
                                        Actor: record.Actor
                                    )),
                                ],
                            },
                        ],
                    },
                ],
            }
        );
        ReportSchema.AssertValidReport(ReportWire.Document(report, "results"));
    }

    [Fact]
    public async Task Labels_each_kind_by_what_the_runner_masked_a_screenshot_and_a_log_complete_a_video_incomplete()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(
            new AttemptArtifactsOptions
            {
                ArtifactsRoot = Root(),
                Segments = ["web", "test-1", "attempt-0"],
                AttemptId = "att-1",
            }
        );
        Directory.CreateDirectory(Path.Combine(artifacts.Dir, "video"));
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        File.WriteAllText(Path.Combine(artifacts.Dir, "video", "video.webm"), "webm bytes");
        File.WriteAllText(Path.Combine(artifacts.Dir, "screen.txt"), "text");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        artifacts.Sink.Register(ArtifactKind.Video, "video/video.webm");
        artifacts.Sink.Register(ArtifactKind.Log, "screen.txt");
        await artifacts.SettleAsync();

        Assert.Equal(
            [ArtifactRedaction.Complete, ArtifactRedaction.Incomplete, ArtifactRedaction.Complete],
            artifacts.Records.Select(record => record.Redaction)
        );

        // An unmasked video still carries its file.
        Assert.Equal("web/test-1/attempt-0/video/video.webm", artifacts.Records[1].Path);
        Assert.Equal(10, artifacts.Records[1].Size);
    }

    [Fact]
    public async Task Hands_the_store_the_redaction_the_record_carries()
    {
        var store = Capturing();
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(
            new AttemptArtifactsOptions
            {
                ArtifactsRoot = Root(),
                Segments = ["web", "test-1", "attempt-0"],
                AttemptId = "att-1",
                Store = store,
            }
        );
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        File.WriteAllText(Path.Combine(artifacts.Dir, "video.webm"), "webm");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        artifacts.Sink.Register(ArtifactKind.Video, "video.webm");
        await artifacts.SettleAsync();

        // Puts land as each file's read finishes, in no fixed order.
        Assert.Equal(
            ArtifactRedaction.Complete,
            store.Puts.First(put => put.Kind == ArtifactKind.Screenshot).Redaction
        );
        Assert.Equal(ArtifactRedaction.Incomplete, store.Puts.First(put => put.Kind == ArtifactKind.Video).Redaction);
    }
}
