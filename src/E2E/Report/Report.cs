// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Reports;

/// <summary>
/// The report-1 document, the shape <see cref="E2ESession.Report"/> returns and <c>report.json</c> holds (upstream's <c>Report</c>, an alias of
/// <c>Report1Document</c>, <c>report/build.ts</c>). A report is a record of what happened. Changing it changes nothing, and a caller that
/// wants the document as JSON reads <c>report.json</c>. The port has no serial groups and no <c>carried</c> block, so the document has neither.
/// </summary>
public sealed record Report
{
    /// <summary>Always <c>report-1</c>.</summary>
    public string SchemaVersion { get; init; } = "report-1";

    /// <summary>The run the document tells.</summary>
    public ReportRun Run { get; init; } = new();
}

/// <summary>The run of a report: its id, the targets it ran on, the results, and the counts (upstream's <c>Report1Document['run']</c>).</summary>
public sealed record ReportRun
{
    public string Id { get; init; } = "";

    /// <summary>Always <c>0.1</c>.</summary>
    public string SpecVersion { get; init; } = "0.1";

    public ReportRunner Runner { get; init; } = new();

    public ReportRunStatus Status { get; init; }

    /// <summary>0, 1, 2, 3, 4, or 130.</summary>
    public int ExitCode { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public ReportProject Project { get; init; } = new();

    public ReportEnvironment Environment { get; init; } = new();

    /// <summary>The commit, branch, and cleanliness of the checkout the run tested. Null outside git and CI.</summary>
    public VcsInfo? Vcs { get; init; }

    public IReadOnlyList<ReportTarget> Targets { get; init; } = [];

    public IReadOnlyList<ReportResult> Results { get; init; } = [];

    /// <summary>The errors of the run itself, such as a report that could not be written.</summary>
    public IReadOnlyList<ReportError> Errors { get; init; } = [];

    public ReportSummary Summary { get; init; } = new();

    public ReportLimits Limits { get; init; } = new();

    public ReportUsage Usage { get; init; } = new();

    /// <summary>The change the test filters judged, or why none ran. Null when the run has no filters to apply.</summary>
    public ReportChanges? Changes { get; init; }

    /// <summary>The test filters the run applied, in order. Null when none ran.</summary>
    public IReadOnlyList<ReportFilter>? Filters { get; init; }

    /// <summary>What an exploration asked, did, and found. Null on a run that did not explore.</summary>
    public ReportExplore? Explore { get; init; }
}

/// <summary>The runner that made a report (upstream's <c>run.runner</c>).</summary>
public sealed record ReportRunner
{
    public string Name { get; init; } = "e2e";

    public string Version { get; init; } = "";
}

/// <summary>The project a run belongs to (upstream's <c>run.project</c>).</summary>
public sealed record ReportProject
{
    public string Id { get; init; } = "";

    /// <summary>The digest of the resolved config: 64 hexadecimal characters.</summary>
    public string ConfigDigest { get; init; } = "";
}

/// <summary>What a run ran on (upstream's <c>run.environment</c>).</summary>
public sealed record ReportEnvironment
{
    public bool Ci { get; init; }

    public bool TrustNoticeShown { get; init; }

    /// <summary>The platform and its release, such as <c>darwin 24.1.0</c>.</summary>
    public string Os { get; init; } = "";

    public string Arch { get; init; } = "";

    /// <summary>The runtime, such as <c>.NET 10.0.0</c>.</summary>
    public string Runtime { get; init; } = "";
}

/// <summary>Where the code under test came from (upstream's <c>VcsInfo</c>, <c>internal/vcs.ts</c>).</summary>
/// <param name="Commit">The commit the working tree is checked out at.</param>
/// <param name="Branch">The branch name; null on a detached HEAD or when only the commit is known.</param>
/// <param name="Dirty">True when tracked files differ from the commit; null when git could not say.</param>
public sealed record VcsInfo(string Commit, string? Branch = null, bool? Dirty = null);

/// <summary>A line in a file of the project (upstream's <c>ReportSource</c>).</summary>
public sealed record ReportSource
{
    /// <summary>Relative to the project root, with <c>/</c> separators.</summary>
    public string File { get; init; } = "";

    /// <summary>One-based.</summary>
    public int Line { get; init; }

    /// <summary>One-based.</summary>
    public int Column { get; init; }
}

/// <summary>The engine a target runs on (upstream's <c>ReportTarget['engine']</c>).</summary>
public sealed record ReportEngine
{
    /// <summary><c>none</c> for a target without an engine.</summary>
    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public int SpiVersion { get; init; }
}

/// <summary>What an engine can capture.</summary>
public enum ArtifactCapability
{
    Screenshot,
    Video,
}

/// <summary>What the app a target tests is.</summary>
public enum TargetEnvironment
{
    Test,
    Staging,
    Production,
}

/// <summary>One target of the run (upstream's <c>ReportTarget</c>).</summary>
public sealed record ReportTarget
{
    public string Id { get; init; } = "";

    /// <summary>Its place in the config's targets.</summary>
    public int Index { get; init; }

    public string Platform { get; init; } = "";

    /// <summary>Origin of the target's declared app URL; null for a surface without one.</summary>
    public string? BaseOrigin { get; init; }

    public TargetEnvironment Environment { get; init; }

    public ReportEngine Engine { get; init; } = new();

    /// <summary>What the engine can do, sorted.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    public IReadOnlyList<ArtifactCapability> ArtifactCapabilities { get; init; } = [];

    /// <summary>Whether the engine can capture and restore state.</summary>
    public bool StateCapability { get; init; }
}

/// <summary>The limits the run ran under (upstream's <c>ReportLimits</c>).</summary>
public sealed record ReportLimits
{
    public int MaxAgentContextBytes { get; init; }

    public int MaxLedgerBytes { get; init; }

    public int MaxObservationBytes { get; init; }

    public int MaxEventsPerStep { get; init; }

    public int MaxModelTokensPerCall { get; init; }
}

/// <summary>What the run used, against <see cref="ReportLimits"/> (upstream's <c>ReportUsage</c>).</summary>
public sealed record ReportUsage
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
/// The run's counts by result (upstream's <c>ReportSummary</c>). <see cref="Passed"/>, <see cref="Failed"/> (timed out included),
/// <see cref="Interrupted"/>, <see cref="Flaky"/>, and <see cref="Skipped"/> count selected results only and add up to <see cref="Selected"/>.
/// </summary>
public sealed record ReportSummary
{
    public int Discovered { get; init; }

    public int Selected { get; init; }

    public int Executed { get; init; }

    public int Passed { get; init; }

    public int Failed { get; init; }

    /// <summary>Stopped by the run before reaching a verdict; never counted as failed.</summary>
    public int Interrupted { get; init; }

    public int Flaky { get; init; }

    public int Skipped { get; init; }
}

/// <summary>Whether a result is a test or a setup.</summary>
public enum ResultKind
{
    Test,
    Setup,
}

/// <summary>How a test ended, over all its attempts.</summary>
public enum ResultStatus
{
    Passed,
    Flaky,
    Failed,
    TimedOut,
    Interrupted,
    Skipped,
}

/// <summary>One test over all its attempts (upstream's <c>ReportResult</c>).</summary>
public sealed record ReportResult
{
    public string Id { get; init; } = "";

    public string TestId { get; init; } = "";

    public ResultKind Kind { get; init; }

    public int DeclarationIndex { get; init; }

    public IReadOnlyList<string> TitlePath { get; init; } = [];

    /// <summary>The test file, relative to the project root.</summary>
    public string File { get; init; } = "";

    /// <summary>Where the test is declared.</summary>
    public ReportSource Source { get; init; } = new();

    public string TargetId { get; init; } = "";

    public string Platform { get; init; } = "";

    /// <summary>The configured agent the test ran as.</summary>
    public string Agent { get; init; } = "";

    /// <summary>Which run of the test this is under repeated runs: 0 for the first.</summary>
    public int Repeat { get; init; }

    /// <summary>The tags the test declares; empty when none.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether the selection chose this test. A test left out is still reported, as skipped, but the summary's <c>Selected</c> does not count it.</summary>
    public bool Selected { get; init; } = true;

    public ResultStatus Status { get; init; }

    public SkipInfo? Skip { get; init; }

    public IReadOnlyList<ReportAttempt> Attempts { get; init; } = [];
}

/// <summary>How an attempt ended.</summary>
public enum AttemptStatus
{
    Passed,
    Failed,
    TimedOut,
    Interrupted,
    Skipped,
}

/// <summary>How an attempt's teardown ended.</summary>
public enum AttemptCleanup
{
    Complete,
    Failed,
    Forced,
}

/// <summary>Why a test or an attempt skipped (upstream's <c>SkipInfo</c>).</summary>
public sealed record SkipInfo
{
    public SkipCause Cause { get; init; }

    public string Reason { get; init; } = "";

    public string? RelatedId { get; init; }

    /// <summary>The name of the test filter that left the test out; only with cause <see cref="SkipCause.Filtered"/>.</summary>
    public string? Filter { get; init; }
}

/// <summary>The cause of a skip.</summary>
public enum SkipCause
{
    /// <summary>The test called <c>Skip</c>.</summary>
    Explicit,
    Filtered,
    PlatformUnavailable,
    CapabilityUnavailable,
    SetupFailed,
    SerialPredecessorFailed,
    HookFailed,
    InfrastructureUnavailable,
    FailureLimit,
    TimeLimit,
}

/// <summary>One attempt of a test (upstream's <c>ReportAttempt</c>).</summary>
public sealed record ReportAttempt
{
    /// <summary>A UUID version 7; also the head of its steps' ids.</summary>
    public string Id { get; init; } = "";

    /// <summary>Zero-based.</summary>
    public int Index { get; init; }

    public AttemptStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public long DurationMs { get; init; }

    public IReadOnlyList<ReportStep> Steps { get; init; } = [];

    /// <summary>What the app logged during the attempt, each line with the index of its step. Null when it logged nothing.</summary>
    public IReadOnlyList<AppLogRecord>? AppLog { get; init; }

    public IReadOnlyList<ArtifactRecord> Artifacts { get; init; } = [];

    /// <summary>What stopped the attempt.</summary>
    public ReportError? Error { get; init; }

    /// <summary>What the runner saw when the failure landed. Null on a pass or when nothing could be captured.</summary>
    public FailureEvidence? Failure { get; init; }

    /// <summary>Why the body skipped itself.</summary>
    public SkipInfo? Skip { get; init; }

    /// <summary>What else failed after the error: a teardown hook, the engine's cleanup.</summary>
    public IReadOnlyList<ReportError> SecondaryErrors { get; init; } = [];

    public AttemptCleanup Cleanup { get; init; }

    /// <summary>What the engine said the attempt ran on, such as <c>browser</c> or <c>device</c>. Null when it said nothing.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
}

/// <summary>What the runner saw when a failure landed (upstream's <c>FailureEvidence</c>).</summary>
public sealed record FailureEvidence
{
    /// <summary>The page or screen the app was on.</summary>
    public string? Url { get; init; }

    public ViewportSize? Viewport { get; init; }

    /// <summary>Nodes the screen listed.</summary>
    public int? Nodes { get; init; }

    /// <summary>The id of the artifact that holds the screen as the agent reads it, one node per line.</summary>
    public string? Screen { get; init; }

    /// <summary>The id of the artifact that holds a screenshot of the screen.</summary>
    public string? Screenshot { get; init; }

    /// <summary>The nodes closest to what a failed locator asked for.</summary>
    public IReadOnlyList<string>? Candidates { get; init; }
}

/// <summary>The size of a viewport in pixels (upstream's inline <c>FailureEvidence['viewport']</c>).</summary>
public sealed record ViewportSize
{
    public int Width { get; init; }

    public int Height { get; init; }
}

/// <summary>What kind of file an artifact is.</summary>
public enum ArtifactKind
{
    Screenshot,
    Video,
    Download,
    Log,
    Other,
}

/// <summary>Whether every secret was proven out of an artifact.</summary>
public enum ArtifactRedaction
{
    Complete,
    Incomplete,
}

/// <summary>Whether a step or the attempt made an artifact.</summary>
public enum ArtifactProducerKind
{
    Step,
    Attempt,
}

/// <summary>Who made an artifact (upstream's <c>ArtifactRecord['producer']</c>).</summary>
public sealed record ArtifactProducer
{
    public ArtifactProducerKind Kind { get; init; }

    /// <summary>The step's id, when a step made it.</summary>
    public string? StepId { get; init; }
}

/// <summary>A file or link an attempt kept (upstream's <c>ArtifactRecord</c>, <c>run/records.ts</c>).</summary>
public sealed record ArtifactRecord
{
    /// <summary>Names the artifact among the attempt's.</summary>
    public string Id { get; init; } = "";

    public ArtifactKind Kind { get; init; }

    public string MediaType { get; init; } = "";

    /// <summary>Relative to the results directory; null for a link a hosted service keeps.</summary>
    public string? Path { get; init; }

    /// <summary>Where a hosted service keeps it.</summary>
    public string? Url { get; init; }

    public long? Size { get; init; }

    /// <summary>The digest of the file: 64 hexadecimal characters.</summary>
    public string? Sha256 { get; init; }

    public string? Ref { get; init; }

    /// <summary>When a recording began.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    public ArtifactRedaction Redaction { get; init; }

    public ArtifactProducer Producer { get; init; } = new();
}

/// <summary>One line the app logged (upstream's <c>AppLogRecord</c>).</summary>
public sealed record AppLogRecord
{
    public AppLogSource Source { get; init; }

    public AppLogLevel Level { get; init; }

    public string Text { get; init; } = "";

    public DateTimeOffset At { get; init; }

    /// <summary>The index of the step the line belongs to; null before the first step.</summary>
    public int? Step { get; init; }
}

/// <summary>Where an app log line came from.</summary>
public enum AppLogSource
{
    Console,
    Error,
    Network,
    System,
}

/// <summary>How serious an app log line is.</summary>
public enum AppLogLevel
{
    Error,
    Warning,
    Info,
}

/// <summary>What kind of call a step is.</summary>
public enum StepKind
{
    Agent,
    Locator,
    Assertion,
    Screen,
    App,
    Session,
    Resource,
}

/// <summary>How a step ended.</summary>
public enum StepStatus
{
    Passed,
    Failed,
    Blocked,
    TimedOut,
    Cancelled,
}

/// <summary>The hook a step ran in; a step of the test body has none.</summary>
public enum StepPhase
{
    BeforeEach,
    AfterEach,
}

/// <summary>Why a step's pixels were not trusted.</summary>
public enum VisionDegradation
{
    PixelTainted,
    MaskingUnproven,
    UnsupportedCapability,
}

/// <summary>One call of the test (upstream's <c>ReportStep</c>).</summary>
public sealed record ReportStep
{
    public string Id { get; init; } = "";

    public int Index { get; init; }

    public StepKind Kind { get; init; }

    /// <summary>The call, such as <c>locator.click</c>.</summary>
    public string Api { get; init; } = "";

    public string Label { get; init; } = "";

    public ReportSource Source { get; init; } = new();

    public StepStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public long DurationMs { get; init; }

    public string? ObservationRevision { get; init; }

    /// <summary>True when at least one semantic observation in the step was truncated (serialized <c>observationTruncated</c>).</summary>
    public bool? ObservationTruncated { get; init; }

    public string? Explanation { get; init; }

    public bool? VisionInput { get; init; }

    public VisionDegradation? VisionDegraded { get; init; }

    public bool? VisionOnly { get; init; }

    public StepViewport? Viewport { get; init; }

    public StepMetrics? Metrics { get; init; }

    public StepCacheRecord? Cache { get; init; }

    public IReadOnlyList<StepEvent> Events { get; init; } = [];

    /// <summary>The last model turns of an agent step, oldest first.</summary>
    public IReadOnlyList<StepTurn>? Turns { get; init; }

    public StepModelInfo? Model { get; init; }

    /// <summary>The configured agent an agent step ran with, by name.</summary>
    public string? Agent { get; init; }

    public StepPhase? Phase { get; init; }

    /// <summary>How the last screen the step saw differs from the step before's. Null when it saw none.</summary>
    public StepScreen? Screen { get; init; }

    public ReportError? Error { get; init; }

    /// <summary>The ids of the artifacts the step made.</summary>
    public IReadOnlyList<string> Artifacts { get; init; } = [];
}

/// <summary>The viewport a step ran in.</summary>
public sealed record StepViewport
{
    public int Width { get; init; }

    public int Height { get; init; }

    public double Scale { get; init; }
}

/// <summary>What an agent step used (upstream's <c>StepMetrics</c>).</summary>
public sealed record StepMetrics
{
    public int ModelCalls { get; init; }

    public int ActionSteps { get; init; }

    public int ObservationBytes { get; init; }

    public int ContextBytes { get; init; }

    public int LedgerBytes { get; init; }

    public int? PixelBytes { get; init; }
}

/// <summary>How the screen a step saw differs from the one before (upstream's <c>StepScreen</c>).</summary>
public sealed record StepScreen
{
    public string? Location { get; init; }

    public int Nodes { get; init; }

    public int? Since { get; init; }

    public IReadOnlyList<string> Changes { get; init; } = [];

    public int? More { get; init; }
}

/// <summary>How an agent step used the cache.</summary>
public enum CacheMode
{
    SelfFinalized,
    AgentConcluded,
    Missed,
}

/// <summary>Why a cached step did not replay.</summary>
public enum CacheMissReason
{
    Retry,
    NoEntry,
    InvalidEntry,
    Truncated,
    WrongContext,
    Gap,
    TargetNotFound,
    TargetAmbiguous,
    ViewportChanged,
    ActionFailed,
    ActionUncertain,
    EndMismatch,
}

/// <summary>What a replayed action found its target by.</summary>
public enum CacheDerived
{
    Pixels,
    WholeNode,
    MintedToken,
    Date,
}

/// <summary>Why a recording was not kept.</summary>
public enum CacheNotRecorded
{
    ParamCollision,
}

/// <summary>What happened to the cache entry after the step.</summary>
public enum CacheWrite
{
    Saved,
    Kept,
    Unconfirmed,
    Evicted,
    NoChange,
    NotWritten,
}

/// <summary>The model that recorded a cache entry.</summary>
public sealed record CacheRecordedBy
{
    public string Provider { get; init; } = "";

    public string Model { get; init; } = "";
}

/// <summary>How an agent step used the cache (upstream's <c>StepCacheRecord</c>).</summary>
public sealed record StepCacheRecord
{
    public CacheMode Mode { get; init; }

    public CacheRecordedBy? RecordedBy { get; init; }

    public CacheMissReason? Reason { get; init; }

    public CacheDerived? Derived { get; init; }

    public CacheNotRecorded? NotRecorded { get; init; }

    public int ReplayedActions { get; init; }

    public int TotalActions { get; init; }

    /// <summary>The digest of the entry: 64 hexadecimal characters.</summary>
    public string? Entry { get; init; }

    public string? Detail { get; init; }

    public CacheWrite? Write { get; init; }
}

/// <summary>How a model call counted its tokens.</summary>
public enum TokenAccounting
{
    Provider,
    AdapterUpperBound,
}

/// <summary>The model an agent step called and what it cost (upstream's <c>StepModelInfo</c>).</summary>
public sealed record StepModelInfo
{
    public string Provider { get; init; } = "";

    public string Model { get; init; } = "";

    /// <summary><c>local</c>, <c>provider-default</c>, or a URI.</summary>
    public string Endpoint { get; init; } = "";

    public string AdapterVersion { get; init; } = "";

    public string PolicyVersion { get; init; } = "";

    public int Calls { get; init; }

    public TokenAccounting TokenAccounting { get; init; }

    public long PeakTokensPerCall { get; init; }

    public long InputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long? CacheReadTokens { get; init; }

    public long? CacheWriteTokens { get; init; }

    public double? EstimatedCostUsd { get; init; }
}

/// <summary>One turn of an agent step (upstream's <c>StepTurn</c>).</summary>
public sealed record StepTurn
{
    public int Index { get; init; }

    public IReadOnlyList<string> Calls { get; init; } = [];

    public string Outcome { get; init; } = "";
}

/// <summary>What kind of thing a step event records.</summary>
public enum StepEventKind
{
    Poll,
    Observation,
    Model,
    ToolProposal,
    Policy,
    Engine,
    Schema,
    Navigation,
}

/// <summary>How a step event ended.</summary>
public enum StepEventStatus
{
    Passed,
    Failed,
    Cancelled,
}

/// <summary>Whether a policy allowed a proposal.</summary>
public enum PolicyDecision
{
    Allowed,
    Denied,
}

/// <summary>One thing a step did (upstream's <c>StepEvent</c>).</summary>
public sealed record StepEvent
{
    public StepEventKind Kind { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public long DurationMs { get; init; }

    public StepEventStatus Status { get; init; }

    public string? Name { get; init; }

    public long? Count { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? Bytes { get; init; }

    public PolicyDecision? Decision { get; init; }

    public string? Code { get; init; }

    public string? Detail { get; init; }

    public string? Reasoning { get; init; }
}

/// <summary>Whose fault an error is.</summary>
public enum ErrorCategory
{
    Test,
    Configuration,
    Infrastructure,
    Internal,
    Interrupted,
}

/// <summary>The part of a run an error came from.</summary>
public enum ErrorPhase
{
    Config,
    Collection,
    Filter,
    Launch,
    BeforeAll,
    BeforeEach,
    Body,
    AfterEach,
    AfterAll,
    Cleanup,
    Report,
}

/// <summary>The tests a hook failure leaves unverified (upstream's <c>HookScope</c>).</summary>
public sealed record HookScope
{
    public string File { get; init; } = "";

    public string TargetId { get; init; } = "";

    public IReadOnlyList<string> TitlePath { get; init; } = [];
}

/// <summary>An error as a report tells it: the serialized error without its stack (upstream's <c>ReportError</c>).</summary>
public sealed record ReportError
{
    public ErrorCategory Category { get; init; }

    /// <summary>The error code, such as <c>ASSERTION_FAILED</c>.</summary>
    public string Code { get; init; } = "";

    public string Message { get; init; } = "";

    /// <summary>Whether the caller may observe again and repeat the operation.</summary>
    public bool Retryable { get; init; }

    public ErrorPhase? Phase { get; init; }

    public string? ScopeId { get; init; }

    public HookScope? Scope { get; init; }

    /// <summary>The structured facts beside the message: an assertion's expected and observed, a locator's role and name.</summary>
    public ErrorDetails? Details { get; init; }

    /// <summary>The line in the test file the failure unwound through.</summary>
    public ReportSource? Source { get; init; }
}

/// <summary>
/// The change a run's test filters judged, by counts only, or why no filter ran (upstream's <c>ReportChanges</c>): <c>Base</c> and
/// <c>Files</c> once git measured the change, <c>Skipped</c> alone when no filter ran before git was asked.
/// </summary>
public sealed record ReportChanges
{
    /// <summary>The merge base the change is measured from.</summary>
    public string? Base { get; init; }

    /// <summary>How many files changed since <see cref="Base"/>; 0 when there is no change.</summary>
    public int? Files { get; init; }

    /// <summary>Why no filter ran, such as <c>no files changed since abc1234</c>, <c>--last-failed</c>, or <c>no tests to judge</c>.</summary>
    public string? Skipped { get; init; }
}

/// <summary>Why a filter left nothing out without answering (upstream's <c>FilterOutcome</c>).</summary>
public enum FilterOutcome
{
    /// <summary>It threw or answered with something invalid.</summary>
    Failed,

    /// <summary>It ran past 5 minutes and the run stopped waiting.</summary>
    TimedOut,
}

/// <summary>One test filter the run applied, in order (upstream's <c>ReportFilter</c>).</summary>
public sealed record ReportFilter
{
    /// <summary>The filter's name.</summary>
    public string Name { get; init; } = "";

    /// <summary>How many tests it was given.</summary>
    public int Candidates { get; init; }

    /// <summary>How many tests the run left out on its word.</summary>
    public int Excluded { get; init; }

    /// <summary>The filter's note, redacted; for a filter that failed or timed out, the warning saying why.</summary>
    public string? Note { get; init; }

    /// <summary>Set when the filter left nothing out because the call did not answer; null when it answered.</summary>
    public FilterOutcome? Outcome { get; init; }
}
