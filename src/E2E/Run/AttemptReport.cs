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
/// <param name="Keep">Which verdicts keep the page, from the attempt's <c>trace</c> mode; null when the mode keeps none for this attempt.</param>
/// <param name="AttemptDir">The attempt's artifact directory, <c>&lt;results&gt;/&lt;test&gt;/attempt-&lt;n&gt;</c>.</param>
/// <param name="CacheDir">The replay cache directory when a <see cref="FileStepCache"/> holds the cache.</param>
internal sealed record TraceOutput(Keep? Keep, string AttemptDir, string? CacheDir)
{
    /// <summary>The test's directory under the results directory, where the page goes beside the attempts.</summary>
    public string TestDir => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AttemptDir))!;

    /// <summary>The results directory.</summary>
    public string ResultsRoot => Path.GetDirectoryName(TestDir)!;
}

/// <summary>
/// The in-memory report of one attempt, which the trace page reads. The port has no <c>report.json</c>: the session builds this from
/// the records it keeps (the step timeline, the environment facts, the videos), adds it to the test's other attempts in
/// <see cref="RunReport"/>, and hands the result to <see cref="TracePages.WriteTracePagesAsync"/>. Upstream builds the same page from
/// the report of the whole run.
/// </summary>
internal static class AttemptReport
{
    private static readonly string RunId = Guid.CreateVersion7().ToString();

    // Upstream forbids NUL in a title, so no title-only hash input starts with it and none can equal a hashed test id.
    private const char TestIdMark = '\0';

    /// <summary>
    /// The id of the result of the test titled <paramref name="title"/> in <paramref name="file"/>, whose head ends the result's directory
    /// name. It hashes the test id, <see cref="TestId"/>, behind a NUL mark that no title-only hash input has (the digest is the port's own, as upstream hashes JSON), so two tests with one title in two files keep apart. A session started with no
    /// file hashes the title alone.
    /// </summary>
    public static string ResultId(string? file, string title) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(file) ? title : TestIdMark + TestId(file, title))));

    /// <summary>
    /// The test id <c>file::title</c>, with the title percent-encoded so a <c>::</c> in a title cannot pass for the separator. Upstream's
    /// <c>testId</c> (<c>internal/ids.ts</c>) with a title path of one title.
    /// </summary>
    public static string TestId(string file, string title) => file + "::" + EncodeTitle(title);

    /// <summary>Percent-encodes one title: RFC 3986 unreserved characters stay literal, every other UTF-8 byte of its NFC form becomes uppercase <c>%HH</c>. Upstream's <c>encodeTitle</c>.</summary>
    public static string EncodeTitle(string title)
    {
        var encoded = new StringBuilder();
        foreach (var value in Encoding.UTF8.GetBytes(title.Normalize(NormalizationForm.FormC)))
        {
            if (value is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~')
            {
                encoded.Append((char)value);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>
    /// The name of the directory of the test titled <paramref name="title"/> in <paramref name="file"/> under the results directory
    /// (<see cref="ArtifactPaths.ResultSegment"/>). A .NET test has no source file of its own, so the file is the test class: its simple
    /// name reads in the segment and its full name goes into the id.
    /// </summary>
    public static string ResultSegmentOf(string? file, string title) => ArtifactPaths.ResultSegment(ResultId(file, title), file is null ? "" : SimpleName(file), [title]);

    /// <summary>The name of a type without its namespace and its declaring types.</summary>
    private static string SimpleName(string typeName)
    {
        // A closed generic's full name carries its type arguments, with versions and dots of their own.
        var name = typeName[..(typeName.IndexOfAny(['`', '[']) is var cut and >= 0 ? cut : typeName.Length)];
        return name[(name.LastIndexOfAny(['.', '+']) + 1)..];
    }

    /// <summary>The report of the attempt that ended <paramref name="status"/> with <paramref name="error"/>.</summary>
    public static (Report1Document Report, string ResultId) Build(
        AttemptScope scope,
        TraceOutput output,
        AttemptStatus status,
        Exception? error,
        long durationMs,
        IReadOnlyList<VideoSegment> videos,
        ReportExplore? explore = null,
        FailureEvidence? evidence = null)
    {
        var steps = scope.Steps.All();
        var id = ResultId(scope.TestFile, scope.TestTitle);
        var located = steps.Select(step => step.Source).OfType<SourceLocation>().FirstOrDefault();
        var directory = Path.GetFileName(output.TestDir);
        var attemptSegment = Path.GetFileName(output.AttemptDir);
        var failed = error is not null && status != AttemptStatus.Passed;
        ReportArtifact[] kept =
        [
            .. videos.Select((video, index) => Artifact(video, index, directory, attemptSegment)),
            .. scope.Attached.Select(attached => new ReportArtifact(attached.Id, attached.Kind, directory + "/" + attemptSegment + "/" + attached.Path, Redaction: attached.Redaction)),
            .. failed && evidence?.Screen is { } screen ? new[] { new ReportArtifact(screen, "log", directory + "/" + attemptSegment + "/" + FailureEvidence.ScreenFile) } : [],
            .. failed && evidence?.Screenshot is { } shot ? new[] { new ReportArtifact(shot, "screenshot", directory + "/" + attemptSegment + "/" + FailureEvidence.ScreenshotFile) } : [],
        ];
        var serialized = error is null ? null : ErrorSerializer.Serialize(error, scope.ProjectRoot, scope.Redactor.Redact);
        var attempt = new ReportAttempt
        {
            Id = scope.AttemptId,
            StartedAt = scope.StartedAt,
            Skip = status == AttemptStatus.Skipped ? new ReportSkip("explicit", serialized?.Message ?? "skipped") : null,
            Status = status,
            DurationMs = durationMs,
            Error = serialized is null ? null : ReportError.Of(serialized),
            Steps = steps,
            Failure = failed ? evidence : null,
            Artifacts = [.. kept.Select(artifact => artifact with { Size = SizeOf(output.ResultsRoot, artifact.Path) })],
            Environment = scope.Environment.Read(scope.Redactor.Redact),
            AppLog = scope.Steps.AppLog(),
        };
        var result = new ReportResult
        {
            Id = id,
            File = located?.File ?? "unknown",
            Source = located ?? new SourceLocation("unknown", 0, 0),
            TitlePath = [scope.TestTitle],
            TargetId = scope.TargetName,
            Platform = scope.EnginePlatform,
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
        return (new Report1Document(new ReportRun(RunId, ModelHttp.PackageVersion(), [new ReportTarget(scope.TargetName, 0, scope.EnginePlatform, BuildReport.DescribeTarget(scope.EnginePlatform, scope.EngineVersion, scope.EngineCapabilities), "test", BuildReport.BaseOrigin(scope.BaseUrl))], [result], explore)), id);
    }

    // The size of a file the attempt kept, which the run's usage adds up; null for a link and for a file that cannot be read.
    private static long? SizeOf(string resultsRoot, string? path)
    {
        try
        {
            return path is null ? null : new FileInfo(Path.Combine(resultsRoot, path)) is { Exists: true } file ? file.Length : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
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
