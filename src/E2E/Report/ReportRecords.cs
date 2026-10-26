// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using VcsInfo = E2E.Reports.VcsInfo;

namespace E2E;

/// <summary>
/// What the trace page reads of a run (upstream's <c>Report1Document</c> and its parts, <c>report/build.ts</c>). A session builds these
/// from the records it already keeps. A record holds what the page and the run-level blocks use, not yet the whole of upstream's
/// shape: no serial groups (the port has none), and the wire form of a result and its steps is not written.
/// </summary>
/// <param name="Run">The run the page tells.</param>
internal sealed record Report1Document(ReportRun Run);

/// <summary>One run: its id, the runner that made it, the targets it ran on, and the results.</summary>
/// <param name="Id">The run's id.</param>
/// <param name="RunnerVersion">The version of the runner (upstream's <c>runner.version</c>).</param>
/// <param name="Targets">The targets the run used; a page names a result's target only when there is more than one.</param>
/// <param name="Results">One per test.</param>
/// <param name="Explore">What the run explored, when it was an exploration (upstream's <c>run.explore</c>).</param>
internal sealed record ReportRun(string Id, string RunnerVersion, IReadOnlyList<ReportTarget> Targets, IReadOnlyList<ReportResult> Results, ReportExplore? Explore = null)
{
    /// <summary>How the run ended, once <see cref="BuildReport.DeriveRunStatus"/> has read its results. Null on a run that only a page reads.</summary>
    public ReportRunStatus? Status { get; init; }

    public int? ExitCode { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>The project the run belongs to: upstream's <c>project</c>.</summary>
    public ReportProject Project { get; init; } = ReportProject.Unknown;

    /// <summary>What the run ran on: upstream's <c>environment</c>.</summary>
    public ReportEnvironment? Environment { get; init; }

    /// <summary>The commit, branch, and cleanliness of the checkout the run tested; null outside git and CI.</summary>
    public VcsInfo? Vcs { get; init; }

    /// <summary>The errors of the run itself, such as a report that could not be written.</summary>
    public IReadOnlyList<ReportError> Errors { get; init; } = [];

    public ReportSummary? Summary { get; init; }

    public ReportLimits? Limits { get; init; }

    public ReportUsage? Usage { get; init; }

    /// <summary>The change the test filters judged, or why none ran; null when the run has no filters to apply (upstream's <c>run.changes</c>).</summary>
    public ReportChanges? Changes { get; init; }

    /// <summary>The test filters the run applied, in order; empty when none ran (upstream's <c>run.filters</c>).</summary>
    public IReadOnlyList<ReportFilter> Filters { get; init; } = [];
}

/// <summary>How a run ended (upstream's <c>BuildReportOptions['status'] | 'blocked'</c>).</summary>
public enum ReportRunStatus
{
    Passed,
    Failed,
    Error,
    Interrupted,

    /// <summary>Not passed, and every failing result carries a blockable code: nothing contradicts that the run could not reach a product verdict.</summary>
    Blocked,
}

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

/// <summary>The error of an attempt or a step as a page tells it. Upstream's <c>ReportError</c>.</summary>
/// <param name="Code">The error code, such as <c>ASSERTION_FAILED</c>.</param>
/// <param name="Message">The message, redacted.</param>
/// <param name="Phase">The hook the error came from (<c>cleanup</c>, say); null for the test body.</param>
/// <param name="Source">The line the failure unwound through, when the stack named one.</param>
/// <param name="Details">The structured facts of the error, when the thrower had them.</param>
internal sealed record ReportError(string Code, string Message, string? Phase = null, SourceLocation? Source = null, ErrorDetails? Details = null)
{
    /// <summary><c>test</c>, <c>configuration</c>, <c>infrastructure</c>, <c>internal</c>, or <c>interrupted</c>.</summary>
    public string Category { get; init; } = "test";

    /// <summary>Whether the caller may observe again and repeat the operation.</summary>
    public bool Retryable { get; init; }

    /// <summary>The report's error for a serialized one.</summary>
    public static ReportError Of(SerializedError error, string? phase = null) => new(error.Code, error.Message, phase, error.Source, error.Details) { Category = error.Category, Retryable = error.Retryable };
}

/// <summary>A file or link an attempt kept (upstream's <c>ReportArtifact</c>).</summary>
/// <param name="Id">Names the artifact among the attempt's.</param>
/// <param name="Kind"><c>video</c>, <c>screenshot</c>, <c>log</c>, or <c>screen</c>.</param>
/// <param name="Path">Relative to the results directory; null for a link a hosted service keeps.</param>
/// <param name="Url">Where a hosted service keeps it.</param>
/// <param name="StartedAt">When a recording began; steps read their offset into it.</param>
/// <param name="Size">The size of the file in bytes, when known; the run's <c>usage.artifactBytes</c> adds them up.</param>
internal sealed record ReportArtifact(string Id, string Kind, string? Path = null, string? Url = null, DateTimeOffset? StartedAt = null, long? Size = null);

/// <summary>What was seen when a failure landed (upstream's <c>FailureEvidence</c>).</summary>
internal sealed partial record FailureEvidence
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

/// <summary>How an attempt's teardown ended (upstream's attempt <c>cleanup</c>).</summary>
internal enum AttemptCleanup
{
    Complete,
    Failed,
    Forced,
}

/// <summary>Why an attempt skipped (upstream's <c>SkipInfo</c>).</summary>
/// <param name="Cause">One of the schema's causes, such as <c>explicit</c> for a test that called <c>Skip</c>.</param>
/// <param name="Reason">The reason, redacted.</param>
/// <param name="Filter">The name of the test filter that left the test out; only with cause <c>filtered</c>.</param>
internal sealed record ReportSkip(string Cause, string Reason, string? Filter = null);

/// <summary>One attempt of a test, as a page reads it (upstream's <c>ReportAttempt</c> and <c>AttemptView</c> in one).</summary>
internal sealed record ReportAttempt
{
    /// <summary>The attempt's id, a UUID version 7; also the head of its steps' ids.</summary>
    public string Id { get; init; } = "";

    /// <summary>When the attempt began; null for a record built by hand, which reads as its first step's start.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    public AttemptCleanup Cleanup { get; init; } = AttemptCleanup.Complete;

    /// <summary>Why the attempt skipped; set on a skipped attempt.</summary>
    public ReportSkip? Skip { get; init; }

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

    /// <summary>The platform of the result's target; blank for a record built by hand, which reads as its target's.</summary>
    public string Platform { get; init; } = "";

    public string Agent { get; init; } = "default";

    /// <summary>0 for the first run of a repeated test.</summary>
    public int Repeat { get; init; }

    /// <summary>The order the test is declared in within its file; results are reported by file, then this, then target.</summary>
    public int DeclarationIndex { get; init; }

    /// <summary>The tags the test declares; empty when none.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Whether the selection chose this test. A test left out is still reported, as skipped, but the summary's <c>selected</c> does not
    /// count it.
    /// </summary>
    public bool Selected { get; init; } = true;

    public ResultStatus Status { get; init; } = ResultStatus.Passed;

    public IReadOnlyList<ReportAttempt> Attempts { get; init; } = [];

    /// <summary>
    /// The directory of the result under the results directory, when it is not the one <see cref="ArtifactPaths.ResultSegment"/> names. The
    /// port's attempts write under the segment of the result's id and title, and the page goes beside them. Not in upstream.
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

/// <summary>The engine a target runs on, as the report names it (upstream's <c>engine</c> block of a target).</summary>
/// <param name="Name">The engine's name; <c>none</c> for a target without an engine.</param>
/// <param name="Version">The engine's version; <c>unversioned</c> for a target without an engine.</param>
/// <param name="SpiVersion">The version of the engine contract (upstream's <c>ENGINE_SPI_VERSION</c>).</param>
internal sealed record ReportEngine(string Name, string Version, int SpiVersion);

/// <summary>What a target's engine declaration says about it (upstream's <c>TargetProvenance</c>).</summary>
/// <param name="Engine">The engine.</param>
/// <param name="Capabilities">What the engine can do, sorted.</param>
/// <param name="ArtifactCapabilities">What the engine can capture: <c>screenshot</c>, <c>video</c>.</param>
/// <param name="StateCapability">Whether the engine can capture and restore state.</param>
internal sealed record TargetProvenance(ReportEngine Engine, IReadOnlyList<string> Capabilities, IReadOnlyList<string> ArtifactCapabilities, bool StateCapability);

/// <summary>One target of the run (upstream's <c>ReportTarget</c>).</summary>
/// <param name="Id">The target's name.</param>
/// <param name="Index">Its place in the config's targets.</param>
/// <param name="Platform">Its platform.</param>
/// <param name="Provenance">What its engine declares.</param>
/// <param name="Environment">What the app is: <c>test</c>, <c>staging</c>, or <c>production</c>.</param>
/// <param name="BaseOrigin">The origin of the target's app URL; null for a surface without one.</param>
internal sealed record ReportTarget(string Id, int Index, string Platform, TargetProvenance Provenance, string Environment = "test", string? BaseOrigin = null);

/// <summary>The project a run belongs to (upstream's <c>project</c>).</summary>
/// <param name="Id">The project id.</param>
/// <param name="ConfigDigest">The digest of the resolved config.</param>
internal sealed record ReportProject(string Id, string ConfigDigest)
{
    /// <summary>What upstream reports without a resolved config. The port has no project id or config digest.</summary>
    public static ReportProject Unknown { get; } = new("unknown", new string('0', 64));
}

/// <summary>What a run ran on (upstream's <c>environment</c> block of the run).</summary>
/// <param name="Ci">Whether the run was in CI.</param>
/// <param name="TrustNoticeShown">Upstream records the trust model here and never prints it, so it is false.</param>
/// <param name="Os">The platform and its release, as <c>darwin 24.1.0</c>.</param>
/// <param name="Arch">The processor architecture, in Node's spelling (<c>x64</c>, <c>arm64</c>).</param>
/// <param name="Runtime">The runtime, as <c>.NET 10.0.0</c>.</param>
internal sealed record ReportEnvironment(bool Ci, bool TrustNoticeShown, string Os, string Arch, string Runtime);

/// <summary>The run's counts by result (upstream's <c>ReportSummary</c>). The status counts are over the selected results and add up to <paramref name="Selected"/>.</summary>
internal sealed record ReportSummary(int Discovered, int Selected, int Executed, int Passed, int Failed, int Interrupted, int Flaky, int Skipped);

/// <summary>The limits the run ran under (upstream's <c>ReportLimits</c>).</summary>
internal sealed record ReportLimits(int MaxAgentContextBytes, int MaxLedgerBytes, int MaxObservationBytes, int MaxEventsPerStep, int MaxModelTokensPerCall)
{
    /// <summary>
    /// Upstream's <c>DEFAULT_LIMITS</c>: the limits of a run no agent config has shaped, which is also what a run that failed before its
    /// config resolved reports.
    /// </summary>
    public static ReportLimits Default { get; } = new(ResolvedAgent.MaxContextBytes, Ledger.MaxLedgerBytes, E2EDefaults.MaxObservationBytes, 1_000, E2EDefaults.MaxInputTokens);

    /// <summary>
    /// The limits of two runs merged: the per-agent ones at the largest either allowed, so the report does not read lower than what a step
    /// could send. The port builds one report per test and merges them into the run's.
    /// </summary>
    public ReportLimits Max(ReportLimits other) => this with
    {
        MaxObservationBytes = Math.Max(MaxObservationBytes, other.MaxObservationBytes),
        MaxModelTokensPerCall = Math.Max(MaxModelTokensPerCall, other.MaxModelTokensPerCall),
    };
}

/// <summary>What the run used, against <see cref="ReportLimits"/> (upstream's <c>ReportUsage</c>).</summary>
internal sealed record ReportUsage
{
    public int DiscoveredResults { get; init; }

    public int MaxAgentContextBytes { get; init; }

    public int MaxLedgerBytes { get; init; }

    public int MaxObservationBytes { get; init; }

    public long ArtifactBytes { get; init; }

    public int Downloads { get; init; }

    public long Events { get; init; }

    public long ModelTokens { get; init; }

    /// <summary>Input tokens served from provider prompt caches; null until a step reports the split.</summary>
    public long? ModelCachedTokens { get; init; }

    public int MaxModelCallsInStep { get; init; }

    public int MaxActionStepsInStep { get; init; }

    public double? EstimatedCostUsd { get; init; }
}

/// <summary>
/// The change a run's test filters judged, by counts only, or why no filter ran (upstream's <c>ReportChanges</c>, <c>report/build.ts</c>):
/// <c>Base</c> and <c>Files</c> once git measured the change, <c>Skipped</c> alone when no filter ran before git was asked.
/// </summary>
/// <param name="Base">The merge base the change is measured from.</param>
/// <param name="Files">How many files changed since <paramref name="Base"/>; 0 when there is no change.</param>
/// <param name="Skipped">Why no filter ran, such as <c>no files changed since abc1234</c>, <c>--last-failed</c>, or <c>no tests to judge</c>.</param>
internal sealed record ReportChanges(string? Base = null, int? Files = null, string? Skipped = null);

/// <summary>Why a filter left nothing out without answering (upstream's <c>FilterOutcome</c>): it threw or answered with something invalid, or ran past 5 minutes.</summary>
internal enum FilterOutcome
{
    Failed,
    TimedOut,
}

/// <summary>One test filter the run applied, in order (upstream's <c>ReportFilter</c>).</summary>
/// <param name="Name">The filter's name.</param>
/// <param name="Candidates">How many tests it was given.</param>
/// <param name="Excluded">How many tests the run left out on its word.</param>
/// <param name="Note">The filter's note, redacted and cut to one line of 1024 characters; for a filter that failed or timed out, the warning saying why.</param>
/// <param name="Outcome">Set when the filter left nothing out because the call did not answer; null when it answered.</param>
internal sealed record ReportFilter(string Name, int Candidates, int Excluded, string? Note = null, FilterOutcome? Outcome = null);
