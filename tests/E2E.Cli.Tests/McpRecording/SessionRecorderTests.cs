// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Engine;

namespace E2E.Cli.Tests.McpRecording;

/// <summary><c>describe('SessionRecorder')</c> of upstream's <c>mcp-recording.test.ts</c>.</summary>
public sealed class SessionRecorderTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-mcp-recording-").FullName;
    private readonly List<string> _calls = [];
    private readonly HashSet<string> _failing = [];
    private readonly string _attemptDir;
    private readonly string _outDir;

    /// <summary>What the next stop writes, relative to the attempt directory, and returns.</summary>
    private string[] _written = ["video/video.mp4"];

    /// <summary>What every start waits on before it returns.</summary>
    private Task _startGate = Task.CompletedTask;

    private bool _tainted;

    public SessionRecorderTests()
    {
        _attemptDir = Path.Combine(_dir, "attempt");
        _outDir = Path.Combine(_dir, "recordings", "session");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Moves_each_recording_out_of_the_attempt_directory_so_a_second_one_at_the_same_engine_path_keeps_the_first()
    {
        using var recordings = Recorder();
        Assert.Equal(
            "Recording 1 \"login\" started. Act as usual; stop_recording saves the video, and close_session saves one still running.",
            await recordings.StartAsync("login"));
        Assert.True(recordings.IsRecording);
        var first = await recordings.StopAsync();
        await recordings.StartAsync(null);
        var second = await recordings.StopAsync();

        Assert.Equal([Path.Combine(_outDir, "1-login.mp4")], first!.Files);
        Assert.Equal([Path.Combine(_outDir, "2.mp4")], second!.Files);
        Assert.Equal("2:0", await File.ReadAllTextAsync(first.Files[0]));
        Assert.Equal("4:0", await File.ReadAllTextAsync(second.Files[0]));
        Assert.False(File.Exists(Path.Combine(_attemptDir, "video", "video.mp4")));
        Assert.True(first.Duration >= TimeSpan.FromMilliseconds(1900));
        Assert.False(recordings.IsRecording);
    }

    [Fact]
    public async Task Reports_a_recording_already_running_instead_of_restarting_it_and_a_stop_with_nothing_running_as_null()
    {
        using var recordings = Recorder();
        Assert.Null(await recordings.StopAsync());
        await recordings.StartAsync("demo");
        Assert.Matches(@"^Recording 1 ""demo"" is already running since \S+; stop_recording ends it\.$", await recordings.StartAsync("other"));
        Assert.Equal(["start"], _calls);
    }

    [Fact]
    public async Task Names_the_later_segments_of_one_recording_as_parts_in_play_order()
    {
        _written = ["video/video.webm", "video/video-part2.webm"];
        using var recordings = Recorder();
        await recordings.StartAsync("checkout");
        var recording = await recordings.StopAsync();
        Assert.Equal([Path.Combine(_outDir, "1-checkout.webm"), Path.Combine(_outDir, "1-checkout-part2.webm")], recording!.Files);
        var text = SessionRecorder.Describe(recording);
        Assert.Matches(@"^Recording 1 ""checkout"" stopped after \d+\.\d s\.\n2 files, one per page the surface showed; they play in this order:\n", text);
        Assert.EndsWith("\n- " + Path.Combine(_outDir, "1-checkout.webm") + "\n- " + Path.Combine(_outDir, "1-checkout-part2.webm"), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Says_so_when_the_engine_wrote_nothing()
    {
        _written = [];
        using var recordings = Recorder();
        await recordings.StartAsync(null);
        var recording = await recordings.StopAsync();
        Assert.Empty(recording!.Files);
        Assert.Matches(@"^Recording 1 stopped after \d+\.\d s\.\nThe engine wrote no video: nothing was shown while it ran\.$", SessionRecorder.Describe(recording));
    }

    [Fact]
    public async Task Stops_the_engine_after_a_failed_start_so_the_next_start_begins_from_nothing()
    {
        using var recordings = Recorder();
        _failing.Add("start");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => recordings.StartAsync("demo"));
        Assert.Equal("device refused to record", failure.Message);
        Assert.False(recordings.IsRecording);
        Assert.Equal(["start", "stop"], _calls);
        _failing.Clear();
        Assert.Contains("Recording 1 \"demo\" started.", await recordings.StartAsync("demo"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_recording_whose_stop_failed_running_so_a_second_stop_saves_it()
    {
        using var recordings = Recorder();
        await recordings.StartAsync("demo");
        _failing.Add("stop");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => recordings.StopAsync());
        Assert.Equal("device lost the recording", failure.Message);
        Assert.True(recordings.IsRecording);
        _failing.Clear();
        var recording = await recordings.StopAsync();
        Assert.Equal([Path.Combine(_outDir, "1-demo.mp4")], recording!.Files);
    }

    [Fact]
    public async Task Says_so_when_a_secret_was_filled_in_the_session_before_the_recording_stopped()
    {
        using var recordings = Recorder();
        await recordings.StartAsync(null);
        _tainted = true;
        var recording = await recordings.StopAsync();
        Assert.True(recording!.Tainted);
        Assert.EndsWith("\nA secret was filled in this session and videos are not masked: check it is not on screen before sharing.", SessionRecorder.Describe(recording), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_recording_whose_files_could_not_all_be_moved_and_moves_the_rest_on_the_next_stop_without_stopping_the_engine_again()
    {
        _written = ["video/video.webm", "video/video-part2.webm"];
        using var recordings = Recorder();
        await recordings.StartAsync("demo");

        // A directory in the way of the second file: the first moves, the second cannot.
        Directory.CreateDirectory(Path.Combine(_outDir, "1-demo-part2.webm", "blocker"));
        await Assert.ThrowsAnyAsync<Exception>(() => recordings.StopAsync());
        Assert.True(recordings.IsRecording);
        Directory.Delete(Path.Combine(_outDir, "1-demo-part2.webm"), recursive: true);
        var recording = await recordings.StopAsync();
        Assert.Equal([Path.Combine(_outDir, "1-demo.webm"), Path.Combine(_outDir, "1-demo-part2.webm")], recording!.Files);
        Assert.Equal("2:1", await File.ReadAllTextAsync(recording.Files[1]));
        Assert.Equal(["start", "stop"], _calls);
    }

    [Fact]
    public async Task Runs_a_stop_after_a_start_still_in_flight_so_a_close_never_misses_the_recording_it_started()
    {
        var release = new TaskCompletionSource();
        _startGate = release.Task;
        using var recordings = Recorder();
        var starting = recordings.StartAsync("late");

        // The engine start is in flight, and the recorder does not count it as running yet.
        await UntilAsync(() => _calls.Count == 1);
        Assert.False(recordings.IsRecording);
        var stopping = recordings.StopAsync();
        release.SetResult();
        await starting;
        Assert.Equal([Path.Combine(_outDir, "1-late.mp4")], (await stopping)!.Files);
        Assert.Equal(["start", "stop"], _calls);
        Assert.False(recordings.IsRecording);
    }

    private SessionRecorder Recorder() =>
        new(
            async token =>
            {
                _calls.Add("start");
                await _startGate.WaitAsync(token);
                if (_failing.Contains("start"))
                {
                    throw new InvalidOperationException("device refused to record");
                }
            },
            _ =>
            {
                _calls.Add("stop");
                if (_failing.Contains("stop"))
                {
                    throw new InvalidOperationException("device lost the recording");
                }

                IReadOnlyList<VideoSegment> segments = [.. _written.Select((relative, index) =>
                {
                    var file = Path.Combine(_attemptDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    File.WriteAllText(file, $"{_calls.Count}:{index}");
                    return (VideoSegment)new VideoFile { Path = relative, StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2).ToString("O") };
                })];
                return Task.FromResult(segments);
            },
            _attemptDir,
            _outDir,
            TimeSpan.FromSeconds(1),
            () => _tainted);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting");
            await Task.Delay(10);
        }
    }
}
