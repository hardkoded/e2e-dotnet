// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Engine;

/// <summary>Handed to a provider's <c>RecordAsync</c>, once per attempt that records video.</summary>
public sealed record ProviderRecordContext
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    /// <summary>The attempt the recording covers.</summary>
    public required string AttemptId { get; init; }

    /// <summary>The run's environment, the same the provider's acquire saw.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }
}

/// <summary>Handed to <see cref="ProviderRecording.StopAsync"/>, once per recording.</summary>
public sealed record ProviderRecordingStopContext
{
    /// <summary>Absolute directory, already created, a recording written as a file goes into.</summary>
    public required string Dir { get; init; }
}

/// <summary>
/// What a stopped recording became: a file the provider wrote into the directory it was given, by name
/// (<see cref="File"/>), or a recording its service keeps, by <c>http(s)</c> URL and media type
/// (<see cref="Url"/> and <see cref="MediaType"/>).
/// </summary>
public sealed record ProviderRecordingResult
{
    /// <summary>The plain name of a file written into <see cref="ProviderRecordingStopContext.Dir"/>.</summary>
    public string? File { get; init; }

    /// <summary>The <c>http(s)</c> URL of a recording the service keeps.</summary>
    public string? Url { get; init; }

    /// <summary>The media type <see cref="Url"/> serves: <c>video/mp4</c>, or <c>text/html</c> for a player page.</summary>
    public string? MediaType { get; init; }
}

/// <summary>A recording a provider's service makes of the browser or device it leased, as <c>RecordAsync</c> started it.</summary>
public abstract class ProviderRecording
{
    /// <summary>When the recording started. Its first frame is at or just after it.</summary>
    public abstract DateTimeOffset StartedAt { get; }

    public abstract Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken);
}

/// <summary>Where the attempt's recording lands and who made it, for <see cref="ProviderRecordings.StopProviderRecordingAsync"/>.</summary>
public sealed record ProviderRecordingTarget
{
    /// <summary>Absolute attempt artifact directory; a file recording goes into its <c>video</c> directory.</summary>
    public required string ArtifactsDir { get; init; }

    /// <summary>The provider as error messages name it: <c>browser provider "kernel"</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The lease the recording covers, named in error messages.</summary>
    public required string LeaseId { get; init; }
}

/// <summary>
/// One recorded video of an attempt: a file the engine wrote into the attempt artifact directory
/// (<see cref="VideoFile"/>), or a recording a hosted service keeps and links to (<see cref="VideoLink"/>).
/// </summary>
public abstract record VideoSegment
{
    /// <summary>When the segment started recording, as an ISO timestamp; its first frame is at or just after it.</summary>
    public required string StartedAt { get; init; }
}

/// <summary>A video file in the attempt artifact directory.</summary>
public sealed record VideoFile : VideoSegment
{
    /// <summary>Path relative to the attempt artifact directory.</summary>
    public required string Path { get; init; }
}

/// <summary>A recording a hosted service keeps, by URL: the report links to it and the runner never downloads it.</summary>
public sealed record VideoLink : VideoSegment
{
    /// <summary><c>http(s)</c> URL of the recording.</summary>
    public required string Url { get; init; }

    /// <summary>Media type of what the URL serves: <c>video/mp4</c>, or <c>text/html</c> for a player page.</summary>
    public required string MediaType { get; init; }
}

/// <summary>
/// The recording seam every engine's provider shares (upstream <c>recording.ts</c>): a hosted browser or device service
/// recording the attempt itself, in place of the engine's own capture. A provider's <c>RecordAsync</c> starts a
/// <see cref="ProviderRecording"/> for the lease the attempt rides; <see cref="StopProviderRecordingAsync"/> ends it and
/// checks what the provider handed back, so every engine turns a recording into a <see cref="VideoSegment"/> the same way.
/// </summary>
public static class ProviderRecordings
{
    /// <summary>The longest URL the report admits for a hosted video, as for a failure's <c>url</c>.</summary>
    private const int MaxVideoUrlChars = 2048;

    /// <summary>
    /// Ends a provider's recording and returns it as the attempt's segment: a file in the attempt's <c>video</c>
    /// directory, or a link. What the provider handed back is checked, since the engine trusts nothing it did not write:
    /// a file must be a plain name that exists in the directory, a link an <c>http(s)</c> URL with a media type. The URL
    /// is kept in its parsed form and the start time as an ISO timestamp, the shapes the report schema admits. Every
    /// failure names the provider and the lease.
    /// </summary>
    public static async Task<VideoSegment> StopProviderRecordingAsync(ProviderRecording recording, ProviderRecordingTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(target);
        var dir = Path.Combine(target.ArtifactsDir, "video");
        var what = $"{target.Provider} recording lease {target.LeaseId}";
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"{what} has no video directory to write into, {dir}: {ex.Message}", retryable: false, ex);
        }

        ProviderRecordingResult? result;
        try
        {
            result = await recording.StopAsync(new ProviderRecordingStopContext { Dir = dir }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"{what} could not finish: {ex.Message}", retryable: false, ex);
        }

        var startedAt = recording.StartedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        if (IsWrittenFile(result, dir))
        {
            return new VideoFile { Path = "video/" + result!.File, StartedAt = startedAt };
        }

        if (IsLink(result))
        {
            return new VideoLink { Url = HostedVideoUrl(result!.Url)!, MediaType = result.MediaType!.Trim(), StartedAt = startedAt };
        }

        throw new EngineException(EngineErrorCodes.EngineFailure, $"{what} finished without naming a file it wrote into {dir} or an http(s) URL with a media type", retryable: false);
    }

    /// <summary>
    /// The parsed form of an <c>http(s)</c> URL with a host, at most 2048 characters, the one shape the report admits
    /// for a video a hosted service keeps; null for anything else.
    /// </summary>
    internal static string? HostedVideoUrl(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return null;
        }

        var hosted = (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp) && parsed.Host.Length > 0;
        return hosted && parsed.AbsoluteUri.Length <= MaxVideoUrlChars ? parsed.AbsoluteUri : null;
    }

    /// <summary>A plain file name, no directory part, of a regular file in <paramref name="dir"/>.</summary>
    private static bool IsWrittenFile(ProviderRecordingResult? result, string dir)
    {
        var file = result?.File;
        if (string.IsNullOrEmpty(file) || file is "." or ".." || !string.Equals(file, Path.GetFileName(file), StringComparison.Ordinal))
        {
            return false;
        }

        // A name the filesystem refuses names no file the provider wrote; File.Exists answers false for one.
        return File.Exists(Path.Combine(dir, file));
    }

    /// <summary>An <c>http(s)</c> URL with a non-empty media type.</summary>
    private static bool IsLink(ProviderRecordingResult? result) =>
        HostedVideoUrl(result?.Url) is not null && !string.IsNullOrWhiteSpace(result?.MediaType);
}
