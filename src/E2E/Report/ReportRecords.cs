// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// What the trace page reads of a run (upstream's <c>Report1Document</c> and its parts, <c>report/build.ts</c>). The port has no
/// <c>report.json</c>: a session builds these from the records it already keeps, and a page is the only thing that reads them. Each
/// record holds what the page uses, not the whole of upstream's shape: no targets, serial groups, limits, or usage.
/// </summary>
/// <param name="Run">The run the page tells.</param>
internal sealed record Report1Document(ReportRun Run);

/// <summary>One run: its id, the runner that made it, the targets it ran on, and the results.</summary>
/// <param name="Id">The run's id.</param>
/// <param name="RunnerVersion">The version of the runner (upstream's <c>runner.version</c>).</param>
/// <param name="Targets">The names of the targets the run used; a page names a result's target only when there is more than one.</param>
/// <param name="Results">One per test.</param>
internal sealed record ReportRun(string Id, string RunnerVersion, IReadOnlyList<string> Targets, IReadOnlyList<ReportResult> Results);

/// <summary>How a test ended, over all its attempts.</summary>
internal enum ResultStatus
{
    Passed,
    Flaky,
    Failed,
    TimedOut,
    Interrupted,
    Skipped,
}

/// <summary>The structured facts of an error: what an assertion expected and observed, what a locator asked for, how long it waited.</summary>
internal sealed record ReportErrorDetails
{
    public string? Expected { get; init; }

    public string? Observed { get; init; }

    /// <summary>How many nodes the locator matched.</summary>
    public int? Matches { get; init; }

    public string? Role { get; init; }

    public string? Name { get; init; }

    public string? TestId { get; init; }

    public long? WaitedMs { get; init; }
}

/// <summary>The error of an attempt or a step as a page tells it. Upstream's <c>ReportError</c>.</summary>
/// <param name="Code">The error code, such as <c>ASSERTION_FAILED</c>.</param>
/// <param name="Message">The message, redacted.</param>
/// <param name="Phase">The hook the error came from (<c>cleanup</c>, say); null for the test body.</param>
/// <param name="Source">The line the failure unwound through, when the stack named one.</param>
/// <param name="Details">The structured facts of the error, when the thrower had them. The port's errors carry none yet.</param>
internal sealed record ReportError(string Code, string Message, string? Phase = null, SourceLocation? Source = null, ReportErrorDetails? Details = null)
{
    /// <summary>The report's error for a serialized one.</summary>
    public static ReportError Of(SerializedError error, string? phase = null) => new(error.Code, error.Message, phase, error.Source);
}

/// <summary>A file or link an attempt kept (upstream's <c>ReportArtifact</c>).</summary>
/// <param name="Id">Names the artifact among the attempt's.</param>
/// <param name="Kind"><c>video</c>, <c>screenshot</c>, <c>log</c>, or <c>screen</c>.</param>
/// <param name="Path">Relative to the results directory; null for a link a hosted service keeps.</param>
/// <param name="Url">Where a hosted service keeps it.</param>
/// <param name="StartedAt">When a recording began; steps read their offset into it.</param>
internal sealed record ReportArtifact(string Id, string Kind, string? Path = null, string? Url = null, DateTimeOffset? StartedAt = null);

/// <summary>What was seen when a failure landed (upstream's <c>FailureEvidence</c>).</summary>
internal sealed record FailureEvidence
{
    /// <summary>The page or screen the app was on.</summary>
    public string? Url { get; init; }

    public StepViewport? Viewport { get; init; }

    /// <summary>Nodes the screen listed.</summary>
    public int? Nodes { get; init; }

    /// <summary>The nodes closest to what a failed locator asked for.</summary>
    public IReadOnlyList<string>? Candidates { get; init; }

    /// <summary>The id of the artifact that holds a screenshot of the screen.</summary>
    public string? Screenshot { get; init; }

    /// <summary>The id of the artifact that holds the screen as the agent reads it, one node per line.</summary>
    public string? Screen { get; init; }
}

/// <summary>One attempt of a test, as a page reads it (upstream's <c>ReportAttempt</c> and <c>AttemptView</c> in one).</summary>
internal sealed record ReportAttempt
{
    public AttemptStatus Status { get; init; } = AttemptStatus.Passed;

    public long DurationMs { get; init; }

    /// <summary>What stopped the attempt.</summary>
    public ReportError? Error { get; init; }

    public IReadOnlyList<StepRecord> Steps { get; init; } = [];

    /// <summary>What the app logged during the attempt, each line with its step.</summary>
    public IReadOnlyList<AppLogRecord> AppLog { get; init; } = [];

    /// <summary>What the runner saw when the failure landed, when it captured anything.</summary>
    public FailureEvidence? Failure { get; init; }

    public IReadOnlyList<ReportArtifact> Artifacts { get; init; } = [];

    /// <summary>What else failed after the error: a teardown hook, the engine's cleanup.</summary>
    public IReadOnlyList<ReportError> SecondaryErrors { get; init; } = [];

    /// <summary>What the engine said the attempt ran on.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
}

/// <summary>One test over all its attempts (upstream's <c>ReportResult</c>).</summary>
internal sealed record ReportResult
{
    public required string Id { get; init; }

    /// <summary>The test file, as the project names it.</summary>
    public required string File { get; init; }

    public required IReadOnlyList<string> TitlePath { get; init; }

    /// <summary>Where the test is declared.</summary>
    public SourceLocation Source { get; init; } = new("unknown", 0, 0);

    public string TargetId { get; init; } = "web";

    public string Agent { get; init; } = "default";

    /// <summary>0 for the first run of a repeated test.</summary>
    public int Repeat { get; init; }

    public ResultStatus Status { get; init; } = ResultStatus.Passed;

    public IReadOnlyList<ReportAttempt> Attempts { get; init; } = [];

    /// <summary>
    /// The directory of the result under the results directory, when it is not the one <see cref="ArtifactPaths.ResultSegment"/> names. The
    /// port's attempts already write under a slug of the test's title, and the page goes beside them. Not in upstream.
    /// </summary>
    public string? Directory { get; init; }
}

/// <summary>
/// What one result left behind (upstream's <c>Outcome</c>, <c>report/outcome.ts</c>). The port has no serial groups, so a result always
/// answers from its own attempts.
/// </summary>
/// <param name="DurationMs">The final attempt's duration.</param>
/// <param name="Final">The final attempt: the verdict of a failed test, the passing retry of a flaky one.</param>
/// <param name="LastFailed">The last attempt before the final one that did not pass, when there was one: what a flaky test hit.</param>
/// <param name="Attempts">Every attempt in order, so a page can say whether they failed alike.</param>
/// <param name="FailedAttempts">Attempts that did not pass before the final one; what a flaky pass cost.</param>
internal sealed record Outcome(long DurationMs, ReportAttempt Final, ReportAttempt? LastFailed, IReadOnlyList<ReportAttempt> Attempts, int FailedAttempts)
{
    /// <summary>What a result that never ran an attempt reads as.</summary>
    private static readonly ReportAttempt NoAttempt = new() { Status = AttemptStatus.Skipped };

    /// <summary>Upstream's <c>outcome()</c>.</summary>
    public static Outcome Of(ReportResult result)
    {
        var earlier = result.Attempts.Take(Math.Max(0, result.Attempts.Count - 1)).ToList();
        var final = result.Attempts.LastOrDefault() ?? NoAttempt;
        return new Outcome(
            final.DurationMs,
            final,
            earlier.LastOrDefault(attempt => attempt.Status != AttemptStatus.Passed),
            result.Attempts,
            earlier.Count(attempt => attempt.Status != AttemptStatus.Passed));
    }
}
