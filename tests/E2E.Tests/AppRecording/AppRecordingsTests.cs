// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.AppRecording;

/// <summary>
/// <c>describe('app recordings')</c> of upstream <c>tests/unit/app-recording.test.ts</c>: <c>app.startRecording</c> /
/// <c>app.stopRecording</c> over the engine's one recorder, with spans moved into <c>recordings/</c>, the attempt's video and a span
/// never sharing it, and a failed start leaving nothing held.
/// </summary>
public sealed class AppRecordingsTests : IDisposable
{
    private const string StartedAt = "2026-10-27T10:00:00.000Z";

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-app-recording-").FullName;
    private readonly List<string> _calls = [];
    private readonly List<(ArtifactKind Kind, string Path, DateTimeOffset? StartedAt)> _registered = [];
    private readonly List<string> _linked = [];

    // What the next stopVideo writes into the attempt directory and returns; an https URL is a provider's link.
    private IReadOnlyList<string> _written = ["video/video.webm"];
    private bool _failStart;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SessionRecordings Recordings(bool video = true, string target = "web") =>
        E2E.Recordings.SessionRecordings(Session(), target, video);

    private IEngineSession Session()
    {
        var session = DispatchProxy.Create<IEngineSession, SessionProxy>();
        ((SessionProxy)(object)session).Owner = this;
        return session;
    }

    private FakeSink Sink() => new(this);

    [Fact]
    public async Task Moves_each_span_into_recordings_numbered_per_session_and_returns_absolute_paths()
    {
        var recordings = Recordings();
        await recordings.StartSpanAsync("dialog opens", default);
        Assert.True(recordings.SpanRunning);
        var first = await recordings.StopSpanAsync(Sink(), default);
        Assert.Equal([new RecordedVideoFile { Path = Path.Combine(_dir, "recordings", "001-dialog_opens.webm"), StartedAt = StartedAt }], first.Videos);
        Assert.False(File.Exists(Path.Combine(_dir, "video", "video.webm")));

        _written = ["video/video-part2.webm", "video/video-part3.webm"];
        await recordings.StartSpanAsync(null, default);
        var second = await recordings.StopSpanAsync(Sink(), default);
        Assert.Equal(
            [Path.Combine("recordings", "002-recording.webm"), Path.Combine("recordings", "002-recording-part2.webm")],
            second.Videos.Select(video => video is RecordedVideoFile file ? Path.GetRelativePath(_dir, file.Path) : ((RecordedVideoLink)video).Url));
        Assert.Equal(["artifact-2", "artifact-3"], second.Ids);
        Assert.Equal([ArtifactKind.Video, ArtifactKind.Video, ArtifactKind.Video], _registered.Select(artifact => artifact.Kind));
        Assert.Equal("2", File.ReadAllText(Path.Combine(_dir, "recordings", "001-dialog_opens.webm")));
        Assert.Equal(["start", "stop", "start", "stop"], _calls);
        Assert.False(recordings.SpanRunning);
    }

    [Fact]
    public async Task Returns_a_provider_link_as_a_url_and_registers_it_as_a_link()
    {
        _written = ["https://videos.example.com/lease/1.mp4"];
        var recordings = Recordings();
        await recordings.StartSpanAsync("hosted", default);
        var stopped = await recordings.StopSpanAsync(Sink(), default);
        Assert.Equal([new RecordedVideoLink { Url = "https://videos.example.com/lease/1.mp4", MediaType = "video/mp4", StartedAt = StartedAt }], stopped.Videos);
        Assert.Equal(["link-1"], stopped.Ids);
        Assert.Empty(_registered);
    }

    [Fact]
    public async Task Refuses_a_span_while_the_attempts_video_holds_the_recorder_and_leaves_the_attempts_video_alone()
    {
        var recordings = Recordings();
        await recordings.StartAttemptVideoAsync(default);
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => recordings.StartSpanAsync("x", default));
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);
        Assert.Contains("video: 'off'", refused.Message, StringComparison.Ordinal);
        var nothing = await Assert.ThrowsAsync<TestException>(() => recordings.StopSpanAsync(Sink(), default));
        Assert.Equal("INVALID_ARGUMENT", nothing.Code);
        Assert.Equal(["start"], _calls);
        Assert.Equal([new VideoFile { Path = "video/video.webm", StartedAt = StartedAt }], await recordings.StopAttemptVideoAsync(default));
        await recordings.StartSpanAsync("after", default);
        Assert.Equal(["start", "stop", "start"], _calls);
    }

    [Fact]
    public async Task Refuses_a_second_span_and_a_stop_with_nothing_running()
    {
        var recordings = Recordings();
        var stop = await Assert.ThrowsAsync<TestException>(() => recordings.StopSpanAsync(Sink(), default));
        Assert.Equal("INVALID_ARGUMENT", stop.Code);
        Assert.Contains("app.startRecording()", stop.Message, StringComparison.Ordinal);
        await recordings.StartSpanAsync("one", default);
        var second = await Assert.ThrowsAsync<TestException>(() => recordings.StartSpanAsync("two", default));
        Assert.Equal("INVALID_ARGUMENT", second.Code);
        Assert.Contains("already running", second.Message, StringComparison.Ordinal);
        Assert.Equal(["start"], _calls);
    }

    [Fact]
    public async Task Refuses_on_an_engine_without_video()
    {
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() => Recordings(video: false, target: "phone").StartSpanAsync("x", default));
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);
        Assert.Contains("target \"phone\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_a_start_that_failed_so_the_next_start_begins_from_nothing()
    {
        var recordings = Recordings();
        _failStart = true;
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => recordings.StartSpanAsync("x", default));
        Assert.Equal("device refused to record", failed.Message);
        Assert.False(recordings.SpanRunning);
        Assert.Equal(["start", "stop"], _calls);
        _failStart = false;
        await recordings.StartSpanAsync("y", default);
        var stopped = await recordings.StopSpanAsync(Sink(), default);
        Assert.Equal("001-y.webm", Path.GetFileName(Assert.IsType<RecordedVideoFile>(stopped.Videos[0]).Path));
    }

    [Fact]
    public async Task Fails_a_stop_whose_engine_wrote_no_video()
    {
        _written = [];
        var recordings = Recordings();
        await recordings.StartSpanAsync(null, default);
        var failed = await Assert.ThrowsAsync<EngineException>(() => recordings.StopSpanAsync(Sink(), default));
        Assert.Equal("ENGINE_FAILURE", failed.Code);
        Assert.False(recordings.SpanRunning);
    }

    private sealed class FakeSink(AppRecordingsTests owner) : IArtifactSink
    {
        public string Dir => owner._dir;

        public string Register(ArtifactKind kind, string relativePath, ArtifactRegistration? registration = null)
        {
            owner._registered.Add((kind, relativePath, registration?.StartedAt));
            return "artifact-" + owner._registered.Count;
        }

        public string Link(string url, ArtifactLink link)
        {
            owner._linked.Add(url);
            return "link-" + owner._linked.Count;
        }
    }

    public class SessionProxy : DispatchProxy
    {
        internal AppRecordingsTests Owner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IEngineSession.StartVideoAsync):
                    Owner._calls.Add("start");
                    return Owner._failStart ? Task.FromException(new InvalidOperationException("device refused to record")) : Task.CompletedTask;
                case nameof(IEngineSession.StopVideoAsync):
                    Owner._calls.Add("stop");
                    IReadOnlyList<VideoSegment> segments =
                    [
                        .. Owner._written.Select(relative =>
                        {
                            if (relative.StartsWith("https://", StringComparison.Ordinal))
                            {
                                return (VideoSegment)new VideoLink { Url = relative, MediaType = "video/mp4", StartedAt = StartedAt };
                            }

                            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Owner._dir, relative))!);
                            File.WriteAllText(Path.Combine(Owner._dir, relative), Owner._calls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            return new VideoFile { Path = relative, StartedAt = StartedAt };
                        }),
                    ];
                    return Task.FromResult(segments);
                default:
                    return null;
            }
        }
    }
}
