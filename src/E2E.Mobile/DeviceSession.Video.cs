// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

public sealed partial class DeviceSession
{
    private ScreenRecording? _recording;

    /// <summary>agent-device's screen recording: where its file lands, and when the device confirmed it was on.</summary>
    private sealed class ScreenRecording(string relative, string absolute, string startedAt)
    {
        public string Relative { get; } = relative;

        public string Absolute { get; } = absolute;

        public string StartedAt { get; set; } = startedAt;
    }

    /// <summary>
    /// Asks the device to record its screen into the attempt directory. Taps stay visible in the recording
    /// (agent-device's touch indicator), which is the closest a phone comes to a cursor, unless
    /// <see cref="MobileOptions.VideoTouches"/> is <c>false</c>.
    /// </summary>
    public async Task StartVideoAsync(CancellationToken cancellationToken)
    {
        var artifactsDir = _artifactsDir ?? throw DeviceSupport.InvalidState("startVideo outside an attempt: start the session with an artifact directory");
        if (_recording is not null)
        {
            throw DeviceSupport.InvalidState("a video is already recording");
        }

        var relative = Path.Combine("video", "video.mp4");
        var absolute = Path.Combine(artifactsDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // Marked before the device is asked: a start that outlives its budget still records, and closing the session must be able to stop it.
        var recording = new ScreenRecording(relative, absolute, Now());
        _recording = recording;

        // The whole screen: an app scope refuses to start while no app is open, before app.open() and after closeApp().
        await CommandAsync(
            "start video recording",
            DeviceMethods.RecordingRecord,
            Selection.ToArgs(
                ("action", JsonValue.Create("start")),
                ("path", JsonValue.Create(absolute)),
                ("quality", JsonValue.Create("medium")),
                ("recordingScope", JsonValue.Create("device")),
                ("hideTouches", Options.VideoTouches ? null : JsonValue.Create(true))),
            cancellationToken).ConfigureAwait(false);

        // The device confirmed: it is recording from about now.
        recording.StartedAt = Now();
    }

    /// <summary>Stops the recording and returns its one segment, or none when the device wrote nothing.</summary>
    public async Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken)
    {
        if (_recording is not { } video)
        {
            return [];
        }

        var result = await StopScreenRecordingAsync(video, cancellationToken).ConfigureAwait(false);

        // The device may finalize the file under a path of its own choosing. The artifact must live where the attempt directory expects it.
        var written = Text(result?["outPath"]) ?? video.Absolute;
        if (written != video.Absolute && File.Exists(written))
        {
            File.Move(written, video.Absolute, overwrite: true);
        }

        return File.Exists(video.Absolute) ? [new VideoFile { Path = video.Relative, StartedAt = video.StartedAt }] : [];
    }

    /// <summary>
    /// Stops the device's screen recording. A device-scope recording started with no session made one of its own,
    /// and stopping it ends that session, whatever the test opened in it since, so the session is then as
    /// <see cref="CloseAppAsync"/> leaves it.
    /// </summary>
    private async Task<JsonNode?> StopScreenRecordingAsync(ScreenRecording video, CancellationToken cancellationToken)
    {
        var result = await CommandAsync("stop video recording", DeviceMethods.RecordingRecord, Selection.ToArgs(("action", JsonValue.Create("stop"))), cancellationToken).ConfigureAwait(false);
        if (_recording == video)
        {
            _recording = null;
        }

        if (result is JsonObject { } data && data["recordOnlySession"] is JsonValue only && only.TryGetValue<bool>(out var recordOnly) && recordOnly)
        {
            ScreenReplaced();
        }

        return result;
    }

    /// <summary>Stops a recording the session never collected, on the client that is closing. Whatever stops it, the close goes on.</summary>
    private async Task StopQuietlyAsync(IDeviceClient client, CancellationToken cancellationToken)
    {
        try
        {
            await client.CallAsync(DeviceMethods.RecordingRecord, Selection.ToArgs(("action", JsonValue.Create("stop"))), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session is going away either way.
        }
    }

    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
