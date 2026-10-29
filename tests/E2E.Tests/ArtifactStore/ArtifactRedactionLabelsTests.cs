// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Tests.ArtifactStore;

/// <summary><c>describe('artifact redaction labels')</c> of upstream <c>artifact-store.test.ts</c>.</summary>
public sealed class ArtifactRedactionLabelsTests : ArtifactStoreHarness
{
    [Fact]
    public async Task Labels_each_kind_by_what_the_runner_masked_a_screenshot_and_a_log_complete_a_video_incomplete()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
        });
        Directory.CreateDirectory(Path.Combine(artifacts.Dir, "video"));
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        File.WriteAllText(Path.Combine(artifacts.Dir, "video", "video.webm"), "webm bytes");
        File.WriteAllText(Path.Combine(artifacts.Dir, "screen.txt"), "text");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        artifacts.Sink.Register(ArtifactKind.Video, "video/video.webm");
        artifacts.Sink.Register(ArtifactKind.Log, "screen.txt");
        await artifacts.SettleAsync();

        Assert.Equal([ArtifactRedaction.Complete, ArtifactRedaction.Incomplete, ArtifactRedaction.Complete], artifacts.Records.Select(record => record.Redaction));

        // An unmasked video still carries its file.
        Assert.Equal("web/test-1/attempt-0/video/video.webm", artifacts.Records[1].Path);
        Assert.Equal(10, artifacts.Records[1].Size);
    }

    [Fact]
    public async Task Hands_the_store_the_redaction_the_record_carries()
    {
        var store = Capturing();
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            Store = store,
        });
        File.WriteAllText(Path.Combine(artifacts.Dir, "shot.png"), "x");
        File.WriteAllText(Path.Combine(artifacts.Dir, "video.webm"), "webm");
        artifacts.Sink.Register(ArtifactKind.Screenshot, "shot.png");
        artifacts.Sink.Register(ArtifactKind.Video, "video.webm");
        await artifacts.SettleAsync();

        // Puts land as each file's read finishes, in no fixed order.
        Assert.Equal(ArtifactRedaction.Complete, store.Puts.First(put => put.Kind == ArtifactKind.Screenshot).Redaction);
        Assert.Equal(ArtifactRedaction.Incomplete, store.Puts.First(put => put.Kind == ArtifactKind.Video).Redaction);
    }
}
