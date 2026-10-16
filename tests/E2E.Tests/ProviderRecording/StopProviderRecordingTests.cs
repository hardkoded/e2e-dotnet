// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.BrowserProvider;

namespace E2E.Tests.ProviderRecording;

/// <summary>
/// <c>describe('stopProviderRecording')</c> of upstream <c>provider-recording.test.ts</c>. Not ported: "knows a recording by a
/// parseable start time and a stop", because <c>isProviderRecording</c> is not: a <see cref="ProviderRecording"/> has both by type.
/// </summary>
public sealed class StopProviderRecordingTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly List<string> _made = [];

    public void Dispose()
    {
        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Turns_a_file_the_provider_wrote_into_the_attempt_video_directory_into_a_file_segment()
    {
        var where = Target();
        var segment = await ProviderRecordings.StopProviderRecordingAsync(
            Recording(async dir =>
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "replay.mp4"), "mp4", None);
                return new ProviderRecordingResult { File = "replay.mp4" };
            }),
            where,
            None);
        Assert.Equal(new VideoFile { Path = "video/replay.mp4", StartedAt = "2026-09-28T10:00:00.000Z" }, segment);
        Assert.True(File.Exists(Path.Combine(where.ArtifactsDir, "video", "replay.mp4")));
    }

    [Fact]
    public async Task Turns_an_http_s_URL_with_a_media_type_into_a_link_segment_in_the_parsed_forms_the_report_admits()
    {
        var segment = await ProviderRecordings.StopProviderRecordingAsync(
            Recording(_ => Task.FromResult(new ProviderRecordingResult { Url = "https://cloud.example/r/7", MediaType = "text/html" })),
            Target(),
            None);
        Assert.Equal(new VideoLink { Url = "https://cloud.example/r/7", MediaType = "text/html", StartedAt = "2026-09-28T10:00:00.000Z" }, segment);
        var loose = new ToyRecording("2026-09-28T10:00:00Z", _ => Task.FromResult(new ProviderRecordingResult { Url = " HTTPS://Cloud.example/r/\u001b]8;;x", MediaType = " video/mp4 " }));
        var normalized = await ProviderRecordings.StopProviderRecordingAsync(loose, Target(), None);
        Assert.Equal(new VideoLink { Url = "https://cloud.example/r/%1B]8;;x", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }, normalized);
    }

    [Fact]
    public async Task Refuses_a_file_outside_the_directory_or_never_written_a_non_http_URL_and_a_link_without_a_media_type_PARTIAL_a_result_is_an_object_or_none()
    {
        ProviderRecordingResult?[] results =
        [
            new() { File = "../escape.mp4" },
            new() { File = "missing.mp4" },
            new() { File = "nul\0.mp4" },
            new() { Url = "file:///tmp/r.mp4", MediaType = "video/mp4" },
            new() { Url = "https://", MediaType = "video/mp4" },
            new() { Url = "https://cloud.example/r" },
            new() { Url = $"https://cloud.example/{new string('r', 2048)}", MediaType = "video/mp4" },
            new(),
            null,
        ];
        foreach (var result in results)
        {
            var error = await Assert.ThrowsAsync<EngineException>(() => ProviderRecordings.StopProviderRecordingAsync(Recording(_ => Task.FromResult(result!)), Target(), None));
            Assert.Equal("ENGINE_FAILURE", error.Code);
            Assert.Contains("browser provider \"cloud\" recording lease lease-7 finished without naming a file", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Refuses_a_directory_named_as_the_file_the_provider_wrote()
    {
        var directory = Recording(dir =>
        {
            Directory.CreateDirectory(Path.Combine(dir, "clip.mp4"));
            return Task.FromResult(new ProviderRecordingResult { File = "clip.mp4" });
        });
        var error = await Assert.ThrowsAsync<EngineException>(() => ProviderRecordings.StopProviderRecordingAsync(directory, Target(), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
    }

    [Fact]
    public async Task Names_the_provider_and_the_lease_when_the_video_directory_cannot_be_made()
    {
        var where = Target();
        await File.WriteAllTextAsync(Path.Combine(where.ArtifactsDir, "video"), "a file where the directory goes", None);
        var error = await Assert.ThrowsAsync<EngineException>(() => ProviderRecordings.StopProviderRecordingAsync(Recording(_ => Task.FromResult(new ProviderRecordingResult { File = "replay.mp4" })), where, None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Contains("browser provider \"cloud\" recording lease lease-7 has no video directory to write into", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_provider_and_the_lease_when_the_stop_itself_fails()
    {
        var error = await Assert.ThrowsAsync<EngineException>(() => ProviderRecordings.StopProviderRecordingAsync(Recording(_ => throw new InvalidOperationException("502 from the recorder")), Target(), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Equal("browser provider \"cloud\" recording lease lease-7 could not finish: 502 from the recorder", error.Message);
    }

    private static ToyRecording Recording(Func<string, Task<ProviderRecordingResult>> stop) => new("2026-09-28T10:00:00.000Z", stop);

    private ProviderRecordingTarget Target()
    {
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-provider-recording-").FullName;
        _made.Add(artifactsDir);
        return new ProviderRecordingTarget { ArtifactsDir = artifactsDir, Provider = "browser provider \"cloud\"", LeaseId = "lease-7" };
    }
}
