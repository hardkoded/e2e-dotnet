// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using E2E.Reports;

namespace E2E.Tests.ArtifactStore;

/// <summary>
/// <c>describe('createAttemptArtifacts with an ArtifactStore')</c> of upstream <c>artifact-store.test.ts</c>: the store seam at its plug
/// point. Every registered artifact is handed to the store as it lands, with its bytes, digest, and identity; the store's reference is
/// recorded; a failing store never surfaces; and without a store the streaming measure path is unchanged.
/// </summary>
public sealed class CreateAttemptArtifactsWithAnArtifactStoreTests : ArtifactStoreHarness
{
    [Fact]
    public async Task Hands_each_artifact_to_the_store_as_produced_with_bytes_digest_and_identity_and_records_the_ref()
    {
        var store = Capturing();
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            CurrentStepId = () => "step-3",
            Store = store,
            Identity = new ArtifactIdentity("run-9", "test-1"),
        });
        var png = Encoding.UTF8.GetBytes("not really a png");
        File.WriteAllBytes(Path.Combine(artifacts.Dir, "shot.png"), png);
        var id = artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        await artifacts.SettleAsync();

        var put = Assert.Single(store.Puts);
        Assert.Equal(ArtifactKind.Screenshot, put.Kind);
        Assert.Equal("image/png", put.MediaType);
        Assert.True(put.Bytes.Span.SequenceEqual(png));
        Assert.Equal(png.Length, put.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), put.Sha256);
        Assert.Equal("web/test-1/attempt-0/shot.png", put.Path);
        Assert.Equal("run-9", put.RunId);
        Assert.Equal("test-1", put.TestId);
        Assert.Equal("att-1", put.AttemptId);
        Assert.Equal("step-3", put.StepId);

        var record = artifacts.Records[0];
        Assert.Equal(id, record.Id);
        Assert.Equal("r2://bucket/" + put.Sha256[..8], record.Ref);
        Assert.Equal("web/test-1/attempt-0/shot.png", record.Path);
        Assert.Equal(png.Length, record.Size);
        Assert.Equal(put.Sha256, record.Sha256);
    }

    [Fact]
    public async Task Carries_a_video_segment_start_time_to_the_record_and_the_store_and_marks_it_unredacted()
    {
        var store = Capturing();
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            Store = store,
            Identity = new ArtifactIdentity("run-9", "test-1"),
        });
        Directory.CreateDirectory(Path.Combine(artifacts.Dir, "video"));
        File.WriteAllBytes(Path.Combine(artifacts.Dir, "video", "video.webm"), Encoding.UTF8.GetBytes("not really webm"));
        var startedAt = DateTimeOffset.Parse("2026-09-09T10:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture);
        artifacts.Sink.Register(ArtifactKind.Video, "video/video.webm", new ArtifactRegistration { StartedAt = startedAt });
        await artifacts.SettleAsync();

        var video = Assert.Single(store.Puts);
        Assert.Equal(ArtifactKind.Video, video.Kind);
        Assert.Equal("video/webm", video.MediaType);
        Assert.Equal(startedAt, video.StartedAt);
        var record = artifacts.Records[0];
        Assert.Equal(ArtifactKind.Video, record.Kind);
        Assert.Equal("video/webm", record.MediaType);
        Assert.Equal(startedAt, record.StartedAt);
        Assert.Equal(ArtifactRedaction.Incomplete, record.Redaction);
        Assert.Equal("web/test-1/attempt-0/video/video.webm", record.Path);

        // Other kinds carry no start time and stay vouched for.
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        await artifacts.SettleAsync();
        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[1].Redaction);
        Assert.Null(artifacts.Records[1].StartedAt);
        Assert.Null(store.Puts[1].StartedAt);
    }

    [Fact]
    public async Task Hands_the_store_the_report_owning_attempt_id_while_artifact_ids_still_mint_from_the_member()
    {
        var store = Capturing();
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "group", "attempt-0"],
            AttemptId = "member-private",
            Store = store,
            Identity = new ArtifactIdentity("run", "group", "group-attempt"),
        });
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        await artifacts.SettleAsync();

        // The report files a serial member's artifacts under the group attempt: a store correlating by attempt must see that id, not a
        // private one absent from the report.
        Assert.Equal("group-attempt", store.Puts[0].AttemptId);
        Assert.Equal("member-private:artifact:0", artifacts.Records[0].Id);
    }

    [Fact]
    public async Task Keeps_the_local_record_and_omits_ref_when_the_store_fails_without_throwing()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "t", "attempt-0"],
            AttemptId = "a",
            Store = new DelegateStore(_ => throw new InvalidOperationException("bucket unreachable")),
        });
        File.WriteAllText(Path.Combine(artifacts.Dir, "video.webm"), "webm");
        artifacts.Sink.Register(ArtifactKind.Video, "video.webm");
        await artifacts.SettleAsync();

        var record = artifacts.Records[0];
        Assert.Null(record.Ref);
        Assert.Equal("web/t/attempt-0/video.webm", record.Path);
        Assert.NotNull(record.Sha256);
    }

    [Fact]
    public async Task Ignores_an_empty_ref_and_never_records_a_missing_file()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "t", "attempt-0"],
            AttemptId = "a",
            Store = new DelegateStore(_ => Task.FromResult("")),
        });
        File.WriteAllText(Path.Combine(artifacts.Dir, "a.png"), "x");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "a.png");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "never-written.png");
        await artifacts.SettleAsync();

        Assert.Null(artifacts.Records[0].Ref);
        Assert.NotNull(artifacts.Records[0].Path);
        Assert.Null(artifacts.Records[1].Path);
        Assert.Null(artifacts.Records[1].Ref);
    }

    [Fact]
    public async Task Without_a_store_measures_by_streaming_and_records_no_ref()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "t", "attempt-0"],
            AttemptId = "a",
        });
        var body = Encoding.UTF8.GetBytes("hello");
        File.WriteAllBytes(Path.Combine(artifacts.Dir, "log.txt"), body);
        artifacts.Sink.Register(ArtifactKind.Log, "log.txt");
        await artifacts.SettleAsync();

        var record = artifacts.Records[0];
        Assert.Null(record.Ref);
        Assert.Equal(5, record.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(body)), record.Sha256);
        Assert.Equal("text/plain", record.MediaType);
    }
}
