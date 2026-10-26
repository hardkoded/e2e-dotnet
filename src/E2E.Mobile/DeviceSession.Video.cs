// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

public sealed partial class DeviceSession
{
    private Recording? _recording;

    /// <summary>What the worker records its device through when its provider records: the provider, and the lease the slot rides.</summary>
    private readonly LeaseRecorder? _leaseRecorder;

    /// <summary>One recording of the device: agent-device's screen recording, or the device provider's own.</summary>
    private abstract class Recording;

    /// <summary>agent-device's screen recording: where its file lands, and when the device confirmed it was on.</summary>
    private sealed class ScreenRecording(string relative, string absolute, string startedAt) : Recording
    {
        public string Relative { get; } = relative;

        public string Absolute { get; } = absolute;

        public string StartedAt { get; set; } = startedAt;
    }

    /// <summary>The device provider's recording of the leased device, as its <c>RecordAsync</c> is starting it or started it.</summary>
    private sealed class LeaseRecording(LeaseRecorder recorder, string artifactsDir, Task<ProviderRecording?> started) : Recording
    {
        public LeaseRecorder Recorder { get; } = recorder;

        public string ArtifactsDir { get; } = artifactsDir;

        public Task<ProviderRecording?> Started { get; } = started;

        /// <summary>The stop in flight, shared: a retry while a stop that ran out of budget is still running waits for it instead of stopping twice.</summary>
        public Task<VideoSegment?>? Stopping { get; set; }
    }

    /// <summary>What a worker records its device through when its provider records: the provider, and the lease the slot rides.</summary>
    private sealed record LeaseRecorder(IDeviceProvider Provider, DeviceLease Lease, string RunId, string TargetName, string AttemptId, IReadOnlyDictionary<string, string?> Env);

    /// <summary>A provider's recording that tells the session once its stop is confirmed, before what the provider wrote is checked.</summary>
    private sealed class ConfirmedRecording(ProviderRecording inner, Action stopped) : ProviderRecording
    {
        public override DateTimeOffset StartedAt => inner.StartedAt;

        public override async Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken)
        {
            var result = await inner.StopAsync(context, cancellationToken).ConfigureAwait(false);
            stopped();
            return result;
        }
    }

    /// <summary>
    /// Asks the device to record its screen into the attempt directory. Taps stay visible in the recording
    /// (agent-device's touch indicator), which is the closest a phone comes to a cursor, unless
    /// <see cref="MobileOptions.VideoTouches"/> is <c>false</c>. A device provider that records its devices records the
    /// attempt instead, when the slot rides one of its leases.
    /// </summary>
    public async Task StartVideoAsync(CancellationToken cancellationToken)
    {
        var artifactsDir = _artifactsDir ?? throw DeviceSupport.InvalidState("startVideo outside an attempt: start the session with an artifact directory");
        if (_recording is not null)
        {
            throw DeviceSupport.InvalidState("a video is already recording");
        }

        if (_leaseRecorder is { } recorder && await StartLeaseRecordingAsync(artifactsDir, recorder, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        const string relative = "video/video.mp4";
        var absolute = Path.Combine(artifactsDir, "video", "video.mp4");
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
        if (_recording is LeaseRecording lease)
        {
            return await StopLeaseRecordingAsync(lease, cancellationToken).ConfigureAwait(false) is { } segment ? [segment] : [];
        }

        if (_recording is not ScreenRecording video)
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
        ClearRecording(video);
        if (result is JsonObject { } data && data["recordOnlySession"] is JsonValue only && only.TryGetValue<bool>(out var recordOnly) && recordOnly)
        {
            ScreenReplaced();
        }

        return result;
    }

    /// <summary>
    /// Starts the device provider's recording of the leased device. Marked the moment <c>RecordAsync</c> is called, before it settles, as
    /// the screen recording is before its start command: a start that outlives its budget may still come up, and closing the session then
    /// stops it. False when the provider records nothing, which leaves the recording to agent-device.
    /// </summary>
    private async Task<bool> StartLeaseRecordingAsync(string artifactsDir, LeaseRecorder recorder, CancellationToken cancellationToken)
    {
        var context = new ProviderRecordContext { RunId = recorder.RunId, TargetName = recorder.TargetName, AttemptId = recorder.AttemptId, Env = recorder.Env };
        var started = DeviceProviders.RecordLeaseAsync(recorder.Provider, recorder.Lease, context, cancellationToken);
        var video = new LeaseRecording(recorder, artifactsDir, started);
        _recording = video;
        try
        {
            if (await started.WaitAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                return true;
            }
        }
        catch (OperationCanceledException) when (!started.IsCompleted)
        {
            // The start outlived the caller's budget: it stays marked, so a recording that comes up anyway is stopped when the session closes.
            throw DeviceSupport.Cancelled("start video recording cancelled");
        }
        catch (Exception)
        {
            // A start that failed leaves nothing recording.
            ClearRecording(video);
            throw;
        }

        // The provider records nothing here.
        ClearRecording(video);
        return false;
    }

    /// <summary>Ends the provider's recording as the attempt's one segment, a file in its <c>video</c> directory or a link. Null when the provider recorded nothing.</summary>
    private async Task<VideoSegment?> StopLeaseRecordingAsync(LeaseRecording video, CancellationToken cancellationToken)
    {
        var stopping = video.Stopping;
        if (stopping is null)
        {
            stopping = StopSharedAsync();
            video.Stopping = stopping;

            // Cleared once settled, after the field holds the task, so a stop that finishes at once cannot leave a stale one.
            _ = stopping.ContinueWith(
                _ =>
                {
                    if (ReferenceEquals(video.Stopping, stopping))
                    {
                        video.Stopping = null;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        try
        {
            return await stopping.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw DeviceSupport.Cancelled("stop video recording cancelled");
        }

        async Task<VideoSegment?> StopSharedAsync()
        {
            var recording = await video.Started.ConfigureAwait(false);
            if (recording is null)
            {
                return null;
            }

            return await ProviderRecordings.StopProviderRecordingAsync(
                new ConfirmedRecording(recording, () => ClearRecording(video)),
                new ProviderRecordingTarget { ArtifactsDir = video.ArtifactsDir, Provider = $"device provider \"{video.Recorder.Provider.Name}\"", LeaseId = video.Recorder.Lease.Id },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void ClearRecording(Recording video)
    {
        if (_recording == video)
        {
            _recording = null;
        }
    }

    /// <summary>Stops a provider's recording the session never collected, best effort. Whatever stops it, the close goes on.</summary>
    private async Task StopLeaseQuietlyAsync(LeaseRecording video, CancellationToken cancellationToken)
    {
        try
        {
            await StopLeaseRecordingAsync(video, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session is going away either way.
        }
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
