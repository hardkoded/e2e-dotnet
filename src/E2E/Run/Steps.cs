// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// Why a vision call fell back to tree-only input. Pixel evidence degrades rather than fails, except under
/// <see cref="VisionMode.Only"/>, which fails with <c>POLICY_DENIED</c> naming the reason.
/// </summary>
public enum VisionDegradation
{
    /// <summary>A secret was filled in this attempt, so the app may show it outside a secure field and no pixels leave the session.</summary>
    PixelTainted,

    /// <summary>The engine masked fewer regions than the tree has secure nodes, so the image cannot be proven redacted.</summary>
    MaskingUnproven,

    /// <summary>The engine captures no pixels.</summary>
    UnsupportedCapability,
}

/// <summary>The closed set of step kinds (upstream's <c>STEP_KINDS</c>).</summary>
internal enum StepKind
{
    Agent,
    Locator,
    Assertion,
    Screen,
    App,
    Session,
    Resource,
}

/// <summary>How a step ended. A step still running is <see cref="Passed"/> until it fails.</summary>
internal enum StepStatus
{
    Passed,
    Failed,
    Blocked,
    TimedOut,
    Cancelled,
}

/// <summary>The hooks a step can run in outside the test body.</summary>
internal enum StepPhase
{
    BeforeEach,
    AfterEach,
}

/// <summary>What a child event of a step is.</summary>
internal enum StepEventKind
{
    Poll,
    Observation,
    Model,
    Policy,
    Engine,
    Schema,

    /// <summary>Where the app went on its own (a navigation the engine did not start, a tab, a frame), in <see cref="StepEvent.Detail"/>.</summary>
    Navigation,
}

/// <summary>How a child event of a step ended.</summary>
internal enum StepEventStatus
{
    Passed,
    Failed,
    Cancelled,
}

/// <summary>The work a running agent step can be waiting on; see <see cref="StepProgress"/>.</summary>
internal enum StepActivity
{
    Observe,
    Model,
    Action,
}

/// <summary>The names the report gives step values: lower case, with a hyphen where the status has two words.</summary>
internal static class StepNames
{
    public static string Of(StepKind kind) => kind.ToString().ToLowerInvariant();

    public static string Of(StepStatus status) => status == StepStatus.TimedOut ? "timed-out" : status.ToString().ToLowerInvariant();
}

/// <summary>
/// Child event of one public step: polls, model calls, policy decisions. <see cref="StartedAt"/> is when the event began, not when it
/// was recorded, so events sorted by it are in causal order while the step's list is in recording order.
/// </summary>
internal sealed record StepEvent
{
    public required StepEventKind Kind { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public long DurationMs { get; init; }

    public StepEventStatus Status { get; init; } = StepEventStatus.Passed;

    public string? Name { get; init; }

    public int? Count { get; init; }

    public int? Bytes { get; init; }

    public string? Code { get; init; }

    /// <summary>Redacted human prose for what the event did, bounded. Secret values never appear: the name stands in.</summary>
    public string? Detail { get; init; }
}

/// <summary>The accounting of one agent step. The port does not measure the observation and pixel bytes upstream also keeps.</summary>
internal sealed record StepMetrics
{
    public int ModelCalls { get; init; }

    public int ActionSteps { get; init; }

    public int ContextBytes { get; init; }

    public int LedgerBytes { get; init; }
}

/// <summary>Agent-specific step detail attached while the step is still running.</summary>
internal sealed record StepAgentDetails
{
    public StepMetrics? Metrics { get; init; }

    public CacheInfo? Cache { get; init; }

    /// <summary>Absent when the step failed before a judgment could be requested.</summary>
    public string? Explanation { get; init; }
}

/// <summary>The viewport a step established.</summary>
internal sealed record StepViewport(int Width, int Height, double Scale);

/// <summary>One step of an attempt: a public API call, what it did, and how it ended.</summary>
internal sealed class StepRecord
{
    public required string Id { get; init; }

    public required int Index { get; init; }

    public required StepKind Kind { get; init; }

    public required string Api { get; init; }

    public required string Label { get; init; }

    /// <summary>The test line the step was called from; null when no project line was on the stack.</summary>
    public SourceLocation? Source { get; init; }

    public StepStatus Status { get; set; } = StepStatus.Passed;

    public required DateTimeOffset StartedAt { get; init; }

    public long DurationMs { get; set; }

    public string? Explanation { get; set; }

    public StepViewport? Viewport { get; set; }

    public StepMetrics? Metrics { get; set; }

    public CacheInfo? Cache { get; set; }

    public List<StepEvent> Events { get; } = [];

    /// <summary>The configured agent an agent step ran with, by name.</summary>
    public string? Agent { get; init; }

    /// <summary>The hook the step ran in; null for a step of the test body.</summary>
    public StepPhase? Phase { get; init; }

    public SerializedError? Error { get; set; }

    public List<string> Artifacts { get; } = [];
}

/// <summary>Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test.</summary>
internal sealed record StepProgressIdentity(string AttemptId, int AttemptIndex, string StepId, int StepIndex);

/// <summary>What a progress notification says.</summary>
internal enum StepProgressPhase
{
    Start,
    End,
    Event,

    /// <summary>What the running step waits on right now. Emitted as each begins; the matching event reports its end.</summary>
    Activity,

    /// <summary>The trace cache took the step (<see cref="StepProgress.Active"/>), or handed it to the model after a replay that could not finish it.</summary>
    Replay,
}

/// <summary>
/// Live progress notification for one step, streamed to reporters as the step runs. Derived from the same records the report
/// persists; the report stays the canonical record. Which members are set depends on <see cref="Phase"/>.
/// </summary>
internal sealed record StepProgress
{
    public required StepProgressPhase Phase { get; init; }

    public StepProgressIdentity? Identity { get; init; }

    public required string Api { get; init; }

    /// <summary>Start and end.</summary>
    public StepKind? Kind { get; init; }

    /// <summary>Start and end.</summary>
    public string? Label { get; init; }

    /// <summary>End.</summary>
    public StepStatus? Status { get; init; }

    /// <summary>End.</summary>
    public long DurationMs { get; init; }

    /// <summary>End.</summary>
    public int ModelCalls { get; init; }

    /// <summary>End: the same redacted error the finished step records.</summary>
    public SerializedError? Error { get; init; }

    /// <summary>End.</summary>
    public string? Explanation { get; init; }

    /// <summary>Event.</summary>
    public StepEvent? Event { get; init; }

    /// <summary>Activity.</summary>
    public StepActivity? Activity { get; init; }

    /// <summary>Replay.</summary>
    public bool Active { get; init; }
}

/// <summary>Per-step options for <see cref="StepRecorder.RunAsync{T}"/>.</summary>
internal sealed record StepRunOptions
{
    /// <summary>
    /// Whether the step checks state rather than producing it: a deterministic assertion, a locator wait, or an agent judgment. Only
    /// such a step passing can confirm a staged action trace. An act passing is the executor's opinion of its own work, and an app or
    /// locator action passing proves only that the action could be performed. Declared where the step is minted, so the rule
    /// cannot drift from the api names.
    /// </summary>
    public bool Verifies { get; init; }

    /// <summary>The configured agent an agent step runs with, recorded on the step.</summary>
    public string? Agent { get; init; }
}

/// <summary>Options for a <see cref="StepRecorder"/>.</summary>
internal sealed record StepRecorderOptions
{
    /// <summary>The report-owning attempt; serial members share their group's attempt identity.</summary>
    public (string Id, int Index)? Attempt { get; init; }

    /// <summary>Caps events retained per step. The runner fixes it at 1000.</summary>
    public int MaxEventsPerStep { get; init; } = 1_000;

    /// <summary>Live progress sink; null in contexts with no reporter to feed.</summary>
    public Action<StepProgress>? OnProgress { get; init; }

    /// <summary>The project root; with it, every step and step error names the test line it came from.</summary>
    public string? ProjectRoot { get; init; }

    /// <summary>Replaces secret values in a step's label, and in its error's message, before the record keeps them.</summary>
    public Func<string, string>? Redact { get; init; }

    /// <summary>Cuts every running step when it fires: the step fails with <c>CANCELLED</c> and its body is abandoned.</summary>
    public CancellationToken Cancellation { get; init; }
}
