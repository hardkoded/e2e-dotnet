// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;

namespace E2E;

/// <summary>
/// Upstream's <c>StepExecutor</c> (<c>agent/executor.ts</c>): the part of an act step that thinks. The runtime owns
/// the step's cache, budget, deadline, and verdict; an executor reads the screen, acts through the context, and
/// returns a verdict. Swapping executors changes the thinking, never the safety, the budgets, or the verdict grammar.
/// <para>
/// An executor is trusted project code, in the same trust domain as the test project that builds it: it may hold its
/// own model and credentials. What the runtime enforces against it is accounting, not secrecy: the budgets, the
/// deadline, the recording, and the verdict grammar hold whoever thinks.
/// </para>
/// </summary>
public interface IStepExecutor
{
    string Name { get; }

    /// <summary>The version this executor reports. Upstream's <c>version</c>.</summary>
    string? Version => null;

    /// <summary>How the executor takes part in the step cache. Upstream's <c>cache</c>.</summary>
    ExecutorCache Cache => ExecutorCache.Inherit;

    /// <summary>
    /// The model this executor brought, if it has one. Config resolution reads it as the agent's model when the entry sets
    /// none, so one executor drives both <c>act</c> and the judgments. <c>INVALID_CONFIG</c> when both are set and differ.
    /// </summary>
    IAgentModel? Model => null;

    /// <summary>
    /// The model the judgments (<c>assert</c> through <see cref="AssertOptions"/>, <c>waitFor</c>, and <c>extract</c>) call
    /// for this executor's agent, when it differs from <see cref="Model"/>. <c>INVALID_CONFIG</c> when the entry sets another.
    /// </summary>
    IAgentModel? Judge => null;

    /// <summary>
    /// Runs one step. <paramref name="cancellationToken"/> ends when the test is cancelled, when the step deadline
    /// passes, or on any other hard stop. Stop promptly: the runtime settles the step at the hard stop either way, so
    /// a late verdict is never trusted over it.
    /// </summary>
    Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Trace-cache participation for an executor's steps. Upstream's <c>cache: 'inherit' | 'off'</c>.
/// </summary>
public enum ExecutorCache
{
    /// <summary>Follows the configured cache mode.</summary>
    Inherit,

    /// <summary>
    /// Never replays a cached trace for the executor's steps and records none, so every step reaches
    /// <see cref="IStepExecutor.RunStepAsync"/>. For an executor whose own history or memory is the source of truth.
    /// </summary>
    Off,
}

/// <summary>
/// What an executor receives for one step (upstream's <c>StepExecutorContext</c>). The runtime builds it. A test of an
/// executor can build its own and override the members it reads: each one that does work throws until it is overridden.
/// </summary>
public class StepExecutorContext
{
    public required ExecutorStep Step { get; init; }

    /// <summary>The attempt this step runs in: its identity, the signal that ends it, and the scratch memory.</summary>
    public required ExecutorAttempt Attempt { get; init; }

    /// <summary>The target this step runs on.</summary>
    public required ExecutorTargetInfo Target { get; init; }

    /// <summary>Present when a cached replay ran part of this step before it handed the step over.</summary>
    public ReplayedPrefix? ReplayedPrefix { get; init; }

    /// <summary>The model the agent's entry is configured with, or the one the executor brought. Null when there is none.</summary>
    public IAgentModel? Model { get; init; }

    /// <summary>The agent's provider options, keyed by provider. The built-in executor sends them with every model call unless it was given its own.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>The completed earlier steps of this attempt, serialized for prompt context, one per line, oldest first. Empty when none.</summary>
    public string Ledger { get; init; } = "";

    /// <summary>The agent's project context. Trusted, and already redacted.</summary>
    public string? AgentContext { get; init; }

    public required IExecutorBudgets Budgets { get; init; }

    public required IExecutorActions Actions { get; init; }

    /// <summary>
    /// True once a secret was filled in this attempt: <see cref="ObserveAsync"/> withholds pixels for the rest of it. An
    /// executor reads it when it assembles its vocabulary, to leave screenshot tools out rather than offer tools that can
    /// only decline.
    /// </summary>
    public virtual bool PixelsTainted => false;

    /// <summary>What the built-in executor reads of the runtime beyond the public context. Null for a context a test built, which the built-in executor cannot drive.</summary>
    internal ActHost? Host { get; init; }

    /// <summary>The screen the step began on, settled once for the replay and the recording, which the built-in executor reads instead of a second look. No upstream equivalent.</summary>
    internal ExecutorObservation? Start { get; init; }

    /// <summary>
    /// Captures one fresh, redacted observation. The text is always present; the tree and the pixels are opt-in, so an
    /// executor that renders its own view of the screen asks for exactly what it uses.
    /// </summary>
    public virtual Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This context does not observe.");

    /// <summary>
    /// Attaches the executor's model transcript to the step. It is written to the results directory as a <c>log</c> file
    /// when the attempt keeps results, and dropped otherwise. Call it once, at the end.
    /// </summary>
    public virtual void AttachTranscript(string text)
    {
    }

    /// <summary>
    /// Keeps pixels the executor or one of its tools observed as a screenshot of the step in progress, and returns the
    /// path of the file, relative to the project root. The file is named by <paramref name="label"/> and lands in the
    /// attempt's results directory. For a tool with evidence worth keeping.
    /// </summary>
    public virtual Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This context does not keep screenshots.");
}

/// <summary>
/// One step handed to an executor. An <see cref="ExecutorStepKind.Act"/> plans and executes a flow. An
/// <see cref="ExecutorStepKind.Assert"/> judges a condition and must not change the app.
/// </summary>
public sealed class ExecutorStep
{
    public required ExecutorStepKind Kind { get; init; }

    /// <summary>The zero-based position of this step among the agent steps of the attempt.</summary>
    public int Index { get; init; }

    /// <summary>The instruction or assertion the test passed.</summary>
    public required string Instruction { get; init; }

    /// <summary>
    /// The call params as JSON. A <see cref="Secret"/> is projected to <c>{"kind":"secret","name":...,"purpose":...}</c>: the
    /// plaintext never reaches the executor, which fills fields through <see cref="IExecutorActions.TypeSecretAsync"/>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }

    /// <summary>The secrets declared in the params, which <see cref="IExecutorActions.TypeSecretAsync"/> may fill.</summary>
    public IReadOnlyList<ExecutorSecret> Secrets { get; init; } = [];
}

/// <summary>Upstream's <c>ExecutorStep.kind</c>.</summary>
public enum ExecutorStepKind
{
    Act,
    Assert,
}

/// <summary>A secret declared in a step's params: its stable name and what it is for. Never its value.</summary>
public sealed record ExecutorSecret(string Name, string? Purpose);

/// <summary>
/// The attempt a step belongs to: identity and lifecycle. It lets an executor keep state across the <c>ActAsync</c> calls of
/// one test, a running conversation or a plan or notes, without mixing it with the next test's or a retry's.
/// </summary>
public sealed class ExecutorAttempt
{
    public required string TestId { get; init; }

    public required string AttemptId { get; init; }

    /// <summary>The zero-based retry index of this attempt.</summary>
    public int Index { get; init; }

    /// <summary>
    /// Ends when the attempt ends: passed, failed, or cancelled. An executor that holds state for the attempt releases it
    /// on this token. Upstream's <c>signal</c>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Scratch space for the attempt, kept by the runtime for the attempt's life and never persisted, reported, or shown to
    /// a model by the runtime: what goes in is the executor's, redaction included. Keyed, so several executors, or an
    /// executor and its tools, can share it.
    /// </summary>
    public IDictionary<string, object?> Memory { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

/// <summary>
/// The target a step runs on. Tools scope themselves by <see cref="Platform"/>, and <see cref="Verbs"/> is the part of
/// the action grammar the engine declared, so an executor offers a model exactly the vocabulary the surface can honor.
/// </summary>
public sealed class ExecutorTargetInfo
{
    public required string Name { get; init; }

    public required string Platform { get; init; }

    public IReadOnlySet<ExecutorVerb> Verbs { get; init; } = new HashSet<ExecutorVerb>();
}

/// <summary>One verb of <see cref="IExecutorActions"/>. The port has the verbs its engines can perform.</summary>
public enum ExecutorVerb
{
    Tap,
    DoubleTap,
    Type,
    TypeSecret,
    Clear,
    Press,
    Select,
    Check,
    ScrollTo,
    ScrollUntil,
    Scroll,
    Navigate,
    Back,
}

/// <summary>What an executor asks <see cref="StepExecutorContext.ObserveAsync"/> to include beyond the text.</summary>
public sealed class ExecutorObserveOptions
{
    /// <summary>Includes the redacted node tree as <see cref="ExecutorObservation.Tree"/>.</summary>
    public bool Tree { get; init; }

    /// <summary>
    /// Includes the masked pixels as <see cref="ExecutorObservation.Pixels"/>. They are granted only when the engine
    /// captures pixels, its masking is proven, and no secret was filled in this attempt. Otherwise
    /// <see cref="ExecutorObservation.PixelsWithheld"/> says why.
    /// </summary>
    public bool Pixels { get; init; }
}

/// <summary>
/// One node of the redacted semantic tree. Names, text, values, and attribute values are secret-redacted, and a secure
/// node carries no value at all. <see cref="Id"/> is the node's ref in the observation, which an action names it by.
/// </summary>
public sealed class ExecutorNode
{
    public required string Id { get; init; }

    public string? Role { get; init; }

    public string? Name { get; init; }

    public string? Text { get; init; }

    public string? Value { get; init; }

    public string? Selection { get; init; }

    public string? InputPurpose { get; init; }

    public NodeStates States { get; init; } = new();

    public IReadOnlyDictionary<string, string>? Attributes { get; init; }

    public BoundingBox? Rect { get; init; }

    public IReadOnlyList<ExecutorNode> Children { get; init; } = [];
}

/// <summary>Masked viewport pixels cleared for model input: the engine's capture, sized for the model, and its proven mask count.</summary>
/// <param name="Data">The encoded image.</param>
/// <param name="MediaType">The media type of <paramref name="Data"/>.</param>
/// <param name="Width">Width in image pixels.</param>
/// <param name="Height">Height in image pixels.</param>
/// <param name="Scale">Image pixels per viewport (CSS) pixel.</param>
/// <param name="MaskedRegionCount">How many regions the engine covered.</param>
public sealed record ExecutorPixels(byte[] Data, string MediaType, int Width, int Height, double Scale, int MaskedRegionCount);

/// <summary>A redacted, size-bounded observation an executor may show its model.</summary>
/// <param name="Text">The screen as the model reads it, already secret-redacted.</param>
public sealed record ExecutorObservation(string Text)
{
    /// <summary>Semantic capture failed: use the accompanying pixels, and never read the listing as evidence of absence.</summary>
    public bool TreeUnavailable { get; init; }

    /// <summary>True when the engine left nodes out to stay within its budget. Scrolling brings them in.</summary>
    public bool Truncated { get; init; }

    /// <summary>The location of the screen, redacted, when the engine reports one. Null on an engine without one.</summary>
    public string? Path { get; init; }

    /// <summary>The redacted node tree. Present when the call asked for it with <see cref="ExecutorObserveOptions.Tree"/>.</summary>
    public IReadOnlyList<ExecutorNode>? Tree { get; init; }

    /// <summary>The masked pixels. Present when the call asked for them with <see cref="ExecutorObserveOptions.Pixels"/> and they were granted.</summary>
    public ExecutorPixels? Pixels { get; init; }

    /// <summary>Why requested pixels were withheld.</summary>
    public VisionDegradation? PixelsWithheld { get; init; }
}

/// <summary>
/// A node named by its id from the newest observation (upstream's <c>{ id }</c>). The id is the node's ref in the text and in
/// <see cref="ExecutorNode.Id"/>. A ref the screen no longer lists fails the action instead of acting on the wrong node.
/// </summary>
public sealed class ExecutorTarget
{
    public ExecutorTarget(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    internal ExecutorTarget(SemanticNode node)
    {
        Id = node.Ref;
        Node = node;
    }

    public string Id { get; }

    /// <summary>
    /// The node the runtime resolved the id to. A target the tool loop built already holds its node. An action that finds the
    /// node again after the page changed it (a scroll that pages a list) leaves the node it found last there.
    /// </summary>
    internal SemanticNode? Node { get; set; }

    /// <summary>The recorded action a replay found this node for, so the action that follows reads its target as recorded.</summary>
    internal RecordedAction? Recorded { get; init; }
}

/// <summary>
/// Upstream's <c>ExecutorActions</c>, with the verbs the port's engines support. Every verb bottoms out in the runtime, which
/// enforces the deadline and the action budget and records the action for the step's cache entry. A ref is only valid against
/// the newest observation: a stale one fails the action rather than acting on the wrong node. Actions and observations run
/// in call order. An engine failure is thrown as an <see cref="E2EException"/>, and a hard stop of the step as an
/// <see cref="AgentException"/> with a runtime code. <see cref="ClearAsync"/> is a verb the port's grammar has and
/// upstream's does not (upstream clears with <c>type</c>).
/// </summary>
public interface IExecutorActions
{
    Task TapAsync(ExecutorTarget target, CancellationToken cancellationToken = default);

    /// <summary>Double-taps one node.</summary>
    Task DoubleTapAsync(ExecutorTarget target, CancellationToken cancellationToken = default);

    Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills one secret declared in the step's params into an input. The runtime authorizes the fill (a secret the step
    /// declared and the attempt knows, an enabled editable sink, and a password field for a password) and hands the
    /// plaintext straight to the engine: it never passes through the executor or any model. A refusal is
    /// <c>POLICY_DENIED</c>. Pixel evidence is tainted from here on.
    /// </summary>
    Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default);

    Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default);

    /// <summary>Presses <paramref name="key"/> on <paramref name="target"/>, or on the focused control when there is none.</summary>
    Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default);

    Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a checkbox, switch, or radio to <paramref name="check"/>. A control already in that state is left alone, which
    /// is what tells this verb from a tap that flips whatever state it finds.
    /// </summary>
    Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default);

    /// <summary>Brings one listed node into the viewport.</summary>
    Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages <paramref name="list"/>, or the viewport when there is none, screen by screen until a visible node reading
    /// <paramref name="text"/> is inside the viewport. One action however many screens it takes, up to eight hundred. It
    /// gives up as <c>LOCATOR_NOT_FOUND</c> when the screen stops moving or the cap is reached.
    /// </summary>
    Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default);

    /// <summary>Scrolls <paramref name="list"/>, or the viewport when there is none, <paramref name="times"/> times.</summary>
    Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default);

    /// <summary>Navigates to an http(s) URL or an app-relative path.</summary>
    Task NavigateAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Navigates back once.</summary>
    Task BackAsync(CancellationToken cancellationToken = default);
}

/// <summary>Step budgets, read and reported by the executor and enforced by the runtime.</summary>
public interface IExecutorBudgets
{
    int MaxActions { get; }

    int MaxModelCalls { get; }

    /// <summary>The action slots the step has used, replayed and live actions alike.</summary>
    int ActionsUsed();

    /// <summary>The milliseconds left of the step's deadline.</summary>
    int RemainingMs();

    /// <summary>
    /// Records one model call the executor made. Its usage feeds the step's model call and token counts. Throws
    /// <c>STEP_BUDGET_EXHAUSTED</c> once the call count passes <see cref="MaxModelCalls"/>: the budget is enforced, not advisory.
    /// </summary>
    void RecordModelCall(ExecutorModelCall? usage = null);

    /// <summary>
    /// Runs one project tool under the step's accounting. A mutating tool reserves an action slot before its body runs, and may
    /// throw <c>STEP_BUDGET_EXHAUSTED</c>. It ends replay of the step, since the grammar cannot reproduce it.
    /// </summary>
    Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body);
}

/// <summary>Usage detail of one model call an executor made. Every field is optional.</summary>
public sealed class ExecutorModelCall
{
    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    /// <summary>Input tokens the provider served from its prompt cache, when it reports the split.</summary>
    public int? CacheReadTokens { get; init; }

    /// <summary>Input tokens written to the provider's prompt cache, when it reports the split.</summary>
    public int? CacheWriteTokens { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public int? DurationMs { get; init; }

    public string? Provider { get; init; }

    public string? ModelId { get; init; }

    /// <summary>The billed cost of the call in US dollars, when the provider reports one.</summary>
    public double? EstimatedCostUsd { get; init; }

    /// <summary>The model's own reasoning for the turn, when it produced one.</summary>
    public string? Reasoning { get; init; }
}

/// <summary>
/// Upstream's <c>ReplayedPrefix</c>: the mid-step hand-off from a diverged cache replay. The replayed actions already ran
/// against the live app under the same budgets and recording as the executor's own. The executor continues the step from
/// the current state and does not redo them.
/// </summary>
/// <param name="ReplayedActions">Prose summaries of the actions the replay performed, in order.</param>
/// <param name="TotalActions">How many actions the recording holds.</param>
/// <param name="StopReason">Why the replay stopped.</param>
/// <param name="UncertainAction">
/// Present exactly when <paramref name="StopReason"/> is <see cref="ReplayHandOffReason.ActionUncertain"/>: the summary of a
/// replayed action whose input may have reached the app although it failed. The executor verifies the state before it
/// tries anything like it again.
/// </param>
public sealed record ReplayedPrefix(IReadOnlyList<string> ReplayedActions, int TotalActions, ReplayHandOffReason StopReason, string? UncertainAction = null);

/// <summary>
/// Upstream's <c>ReplayHandOffReason</c>, why a cached replay stopped before it finished its trace. <see cref="ActionBudget"/>
/// and <see cref="InvalidEntry"/> are the port's: its replay draws on the step's action budget, and reads a recording back
/// from a store that can hold any shape.
/// </summary>
public enum ReplayHandOffReason
{
    Gap,
    TargetNotFound,
    TargetAmbiguous,
    ActionFailed,
    ActionUncertain,
    EndMismatch,
    ActionBudget,
    InvalidEntry,
}

/// <summary>The names a hand-off reason carries in a cache report and in the prompt the executor reads.</summary>
internal static class ReplayHandOffReasons
{
    private static readonly (ReplayHandOffReason Reason, string Name)[] Names =
    [
        (ReplayHandOffReason.Gap, "gap"),
        (ReplayHandOffReason.TargetNotFound, "target-not-found"),
        (ReplayHandOffReason.TargetAmbiguous, "target-ambiguous"),
        (ReplayHandOffReason.ActionFailed, "action-failed"),
        (ReplayHandOffReason.ActionUncertain, "action-uncertain"),
        (ReplayHandOffReason.EndMismatch, "end-mismatch"),
        (ReplayHandOffReason.ActionBudget, "action-budget"),
        (ReplayHandOffReason.InvalidEntry, "invalid-entry"),
    ];

    public static string Name(ReplayHandOffReason reason) => Names.First(item => item.Reason == reason).Name;

    public static ReplayHandOffReason Parse(string? name) => Names.First(item => string.Equals(item.Name, name, StringComparison.Ordinal)).Reason;
}

/// <summary>
/// Upstream's <c>StepVerdict</c>: how an executor ended the step. <see cref="StepVerdictStatus.Failed"/> means the app did not
/// behave as the step required. <see cref="StepVerdictStatus.Blocked"/> means the environment, the credentials, or the
/// executor's own budget prevented a verdict about the app, and it always carries a blockable code.
/// </summary>
/// <param name="Status">How the step ended.</param>
/// <param name="Summary">What happened, in a sentence or two. Never empty.</param>
/// <param name="ErrorCode">The agent error code, such as <c>ASSERTION_FAILED</c>. A passed verdict carries none.</param>
public readonly record struct StepVerdict(StepVerdictStatus Status, string Summary, string? ErrorCode = null);

/// <summary>Upstream's <c>StepVerdict.status</c>.</summary>
public enum StepVerdictStatus
{
    Passed,
    Failed,
    Blocked,
}

/// <summary>Upstream's <c>RUNTIME_CODES</c>, <c>BLOCKABLE_CODES</c> and <c>isRuntimeHardStop</c> (<c>agent/executor.ts</c>).</summary>
public static class StepExecutors
{
    /// <summary>
    /// The codes only the runtime may assign. An executor can carry one it was given, in a hard-stop error the context raised,
    /// but cannot invent one: the runtime rejects a runtime code it did not record.
    /// </summary>
    public static IReadOnlySet<string> RuntimeCodes { get; } = new HashSet<string>(["STEP_BUDGET_EXHAUSTED", "STEP_TIMEOUT", "CANCELLED"], StringComparer.Ordinal);

    /// <summary>
    /// The codes a <see cref="StepVerdictStatus.Blocked"/> verdict may carry: credentials, the environment, seed data, and test
    /// setup have outside owners, and <c>automation</c> means the executor ran out of room. Every other code describes the app
    /// and belongs to a failed verdict.
    /// </summary>
    public static IReadOnlySet<string> BlockableCodes { get; } = new HashSet<string>(
        [
            "AUTH_CREDENTIAL_UNAVAILABLE",
            "AUTH_CREDENTIAL_INVALID",
            "SECRET_UNAVAILABLE",
            "ENVIRONMENT_UNAVAILABLE",
            "APP_UNREACHABLE",
            "APP_ALREADY_RUNNING",
            "SEED_DATA_MISSING",
            "TEST_SETUP_FAILED",
            "APP_NOT_OPEN",
            "POLICY_DENIED",
            "MODEL_UNAVAILABLE",
            "AUTOMATION_UNSUPPORTED",
            "STEP_BUDGET_EXHAUSTED",
            "STEP_TIMEOUT",
            "CONTEXT_OVERFLOW",
        ],
        StringComparer.Ordinal);

    /// <summary>Every code an agent step may end with. A verdict carries one of these or none.</summary>
    internal static IReadOnlySet<string> AgentCodes { get; } = new HashSet<string>(
        [
            .. BlockableCodes,
            "MODEL_PROVIDER_FAILED",
            "CANCELLED",
            "AUTHENTICATION_FAILED",
            "MODEL_OUTPUT_INVALID",
            "LOCATOR_NOT_FOUND",
            "LOCATOR_AMBIGUOUS",
            "ACTION_FAILED",
            "STEP_NO_CONCLUSION",
            "ASSERTION_FAILED",
            "ASSERTION_INCONCLUSIVE",
            "REPLAY_STALE",
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// Whether an error is one of the runtime's own hard stops. Such an error is the step's truth wherever it lands, inside a
    /// tool call or a replayed action, and is surfaced untouched rather than absorbed as a divergence or an action failure.
    /// </summary>
    public static bool IsRuntimeHardStop(E2EException error) =>
        error is AgentException { Code: "STEP_BUDGET_EXHAUSTED" or "STEP_TIMEOUT" or "CANCELLED" } or EngineException { Code: EngineErrorCodes.Cancelled };
}
