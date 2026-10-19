// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>Where one attempt's trace page goes, and which attempts keep one.</summary>
/// <param name="Keep">Which verdicts keep the page, from the attempt's <c>trace</c> mode.</param>
/// <param name="AttemptDir">The attempt's artifact directory, <c>&lt;results&gt;/&lt;test&gt;/attempt-&lt;n&gt;</c>.</param>
/// <param name="CacheDir">The replay cache directory when a <see cref="FileStepCache"/> holds the cache.</param>
internal sealed record TraceOutput(Keep Keep, string AttemptDir, string? CacheDir)
{
    /// <summary>The test's directory under the results directory, where the page goes beside the attempts.</summary>
    public string TestDir => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AttemptDir))!;

    /// <summary>The results directory.</summary>
    public string ResultsRoot => Path.GetDirectoryName(TestDir)!;
}

/// <summary>
/// The in-memory report of one attempt, which the trace page reads. The port has no <c>report.json</c> and no run report: the session
/// builds this from the records it keeps (the step timeline, the environment facts, the videos) and hands it to
/// <see cref="TracePages.WriteTracePagesAsync"/>. Upstream builds the same page from the report of the whole run.
/// </summary>
internal static class AttemptReport
{
    private static readonly string RunId = Guid.NewGuid().ToString("N")[..12];

    /// <summary>The report of the attempt that ended <paramref name="status"/> with <paramref name="error"/>.</summary>
    public static (Report1Document Report, string ResultId) Build(
        AttemptScope scope,
        TraceOutput output,
        AttemptStatus status,
        Exception? error,
        long durationMs,
        IReadOnlyList<VideoSegment> videos)
    {
        var steps = scope.Steps.All();
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope.TestTitle)))[..16].ToLowerInvariant();
        var located = steps.Select(step => step.Source).OfType<SourceLocation>().FirstOrDefault();
        var directory = Path.GetFileName(output.TestDir);
        var attemptSegment = Path.GetFileName(output.AttemptDir);
        var failed = error is not null && status != AttemptStatus.Passed;
        var attempt = new ReportAttempt
        {
            Status = status,
            DurationMs = durationMs,
            Error = error is null ? null : ReportError.Of(ErrorSerializer.Serialize(error, scope.ProjectRoot, scope.Redactor.Redact)),
            Steps = steps,
            Failure = failed ? FailureAt(scope.Steps, steps) : null,
            Artifacts = [.. videos.Select((video, index) => Artifact(video, index, directory, attemptSegment))],
            Environment = scope.Environment.Read(scope.Redactor.Redact),
        };
        var result = new ReportResult
        {
            Id = id,
            File = located?.File ?? "unknown",
            Source = located ?? new SourceLocation("unknown", 0, 0),
            TitlePath = [scope.TestTitle],
            TargetId = scope.TargetName,
            Status = status switch
            {
                AttemptStatus.Passed => ResultStatus.Passed,
                AttemptStatus.TimedOut => ResultStatus.TimedOut,
                AttemptStatus.Interrupted => ResultStatus.Interrupted,
                AttemptStatus.Skipped => ResultStatus.Skipped,
                _ => ResultStatus.Failed,
            },
            Attempts = [attempt],
            Directory = directory,
        };
        return (new Report1Document(new ReportRun(RunId, ModelHttp.PackageVersion(), [scope.TargetName], [result])), id);
    }

    // What the app showed when the failure landed: the last screen a step saw, since the port captures no evidence at failure.
    private static FailureEvidence? FailureAt(StepRecorder recorder, IReadOnlyList<StepRecord> steps)
    {
        if (recorder.LastScreen is not { } screen)
        {
            return null;
        }

        return new FailureEvidence
        {
            Url = screen.Location,
            Nodes = screen.Lines.Count,
            Viewport = steps.LastOrDefault(step => step.Viewport is not null)?.Viewport,
        };
    }

    // A video file is registered under the results directory, as upstream registers an attempt's artifacts; a link stays a link.
    private static ReportArtifact Artifact(VideoSegment video, int index, string directory, string attemptSegment)
    {
        var id = "video-" + index.ToString(CultureInfo.InvariantCulture);
        var startedAt = DateTimeOffset.TryParse(video.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : (DateTimeOffset?)null;
        return video switch
        {
            VideoFile file => new ReportArtifact(id, "video", directory + "/" + attemptSegment + "/" + file.Path.Replace('\\', '/'), StartedAt: startedAt),
            VideoLink link => new ReportArtifact(id, "video", Url: link.Url, StartedAt: startedAt),
            _ => new ReportArtifact(id, "video", StartedAt: startedAt),
        };
    }
}
