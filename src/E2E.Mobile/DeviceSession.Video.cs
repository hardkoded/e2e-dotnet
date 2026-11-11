// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

public sealed partial class DeviceSession
{
    /// <summary>What the worker records its device through when its provider records: the provider, and the lease the slot rides.</summary>
    private readonly LeaseRecorder? _leaseRecorder;

    /// <summary>One recording of the device: agent-device's screen recording, or the device provider's own. Held by the worker, across attempts.</summary>
    internal abstract class Recording;

    /// <summary>agent-device's screen recording: where its file lands, and when the device confirmed it was on.</summary>
    internal sealed class ScreenRecording(string relative, string absolute, string startedAt) : Recording
    {
        public string Relative { get; } = relative;

        public string Absolute { get; } = absolute;

        public string StartedAt { get; set; } = startedAt;

        /// <summary>The stop in flight, shared: it outlives a caller's cancelled wait, and its result with it (upstream's <c>stopping</c>).</summary>
        public Task<JsonNode?>? Stopping { get; set; }
    }

    /// <summary>The device provider's recording of the leased device, as its <c>RecordAsync</c> is starting it or started it.</summary>
    internal sealed class LeaseRecording(LeaseRecorder recorder, string artifactsDir, Task<ProviderRecording?> started) : Recording
    {
        public LeaseRecorder Recorder { get; } = recorder;

        public string ArtifactsDir { get; } = artifactsDir;

        public Task<ProviderRecording?> Started { get; } = started;

        /// <summary>The stop in flight, shared: a retry while a stop that ran out of budget is still running waits for it instead of stopping twice.</summary>
        public Task<VideoSegment?>? Stopping { get; set; }
    }

    /// <summary>What a worker records its device through when its provider records: the provider, and the lease the slot rides.</summary>
    internal sealed record LeaseRecorder(IDeviceProvider Provider, DeviceLease Lease, string RunId, string TargetName, string AttemptId, IReadOnlyDictionary<string, string?> Env);

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
        if (_worker.Recording is not null)
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
        _worker.Recording = recording;

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
        if (_worker.Recording is LeaseRecording lease)
        {
            return await StopLeaseRecordingAsync(_worker, lease, cancellationToken).ConfigureAwait(false) is { } segment ? [segment] : [];
        }

        if (_worker.Recording is not ScreenRecording video)
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
    private Task<JsonNode?> StopScreenRecordingAsync(ScreenRecording video, CancellationToken cancellationToken) =>
        DeviceErrors.RunCommandAsync("stop video recording", _ => video.Stopping ??= Track(StopSharedAsync(video)), cancellationToken, _where);

    /// <summary>
    /// The one stop request of a screen recording. It runs on its own, not under a caller's token, so a caller that leaves does not
    /// cancel it. A stop that fails is dropped, so the next caller or the attempt's cleanup sends it again.
    /// </summary>
    private async Task<JsonNode?> StopSharedAsync(ScreenRecording video)
    {
        // Held back until the caller stored this task, so a stop that fails at once cannot leave a stale one.
        await Task.Yield();
        try
        {
            var result = await CommandAsync("stop video recording", DeviceMethods.RecordingRecord, Selection.ToArgs(("action", JsonValue.Create("stop"))), CancellationToken.None).ConfigureAwait(false);
            ClearRecording(_worker, video);
            if (result is JsonObject { } data && data["recordOnlySession"] is JsonValue only && only.TryGetValue<bool>(out var recordOnly) && recordOnly)
            {
                ScreenReplaced();
            }

            return result;
        }
        catch (Exception)
        {
            video.Stopping = null;
            throw;
        }
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

        // Read, so a start that fails after its caller gave up is not left unobserved.
        _ = started.ContinueWith(static settled => settled.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _worker.Recording = video;
        try
        {
            if (await started.WaitAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The start outlived the caller's budget: it stays marked, so a recording that comes up anyway is stopped when the session closes.
            throw DeviceSupport.Cancelled("start video recording cancelled");
        }
        catch (Exception)
        {
            // A start that failed leaves nothing recording.
            ClearRecording(_worker, video);
            throw;
        }

        // The provider records nothing here.
        ClearRecording(_worker, video);
        return false;
    }

    /// <summary>
    /// Ends the provider's recording as the attempt's one segment, a file in its <c>video</c> directory or a link. Null when the provider
    /// recorded nothing. Shared by every caller: a stop in flight is waited for, not sent twice, and a stop that fails is dropped, so the next
    /// caller sends it again (upstream's <c>stopLeaseRecording</c>, with its <c>stopping</c>).
    /// </summary>
    internal static async Task<VideoSegment?> StopLeaseRecordingAsync(DeviceWorker worker, LeaseRecording video, CancellationToken cancellationToken)
    {
        var stopping = video.Stopping;
        if (stopping is null)
        {
            stopping = StopSharedAsync();
            video.Stopping = stopping;

            // Cleared once settled, after the field holds the task, so a stop that finishes at once cannot leave a stale one.
            _ = stopping.ContinueWith(
                settled =>
                {
                    // Read, so a stop that fails after its caller gave up is not left unobserved.
                    _ = settled.Exception;
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
                new ConfirmedRecording(recording, () => ClearRecording(worker, video)),
                new ProviderRecordingTarget { ArtifactsDir = video.ArtifactsDir, Provider = $"device provider \"{video.Recorder.Provider.Name}\"", LeaseId = video.Recorder.Lease.Id },
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Clears the worker's marked recording once it is confirmed stopped, or reset. Only the recording still marked is cleared.</summary>
    internal static void ClearRecording(DeviceWorker worker, Recording video)
    {
        if (worker.Recording == video)
        {
            worker.Recording = null;
        }
    }

    /// <summary>Stops a provider's recording the worker never collected, best effort. Whatever stops it, the close goes on.</summary>
    internal static async Task StopLeaseQuietlyAsync(DeviceWorker worker, LeaseRecording video, CancellationToken cancellationToken)
    {
        try
        {
            await StopLeaseRecordingAsync(worker, video, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session is going away either way.
        }
    }

    /// <summary>
    /// Stops a recording the worker never collected, on the client that is closing. A stop already in flight is waited for, not sent
    /// twice, and sent again only if it failed. Whatever stops it, the close goes on.
    /// </summary>
    internal static async Task StopQuietlyAsync(IDeviceClient client, JsonObject stopArgs, ScreenRecording video, CancellationToken cancellationToken)
    {
        try
        {
            if (video.Stopping is { } pending)
            {
                try
                {
                    await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // The shared stop failed: it is sent again below.
                }
            }

            await client.CallAsync(DeviceMethods.RecordingRecord, stopArgs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session is going away either way.
        }
    }

    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
