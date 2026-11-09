// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>Upstream's <c>DispatchSpec</c> (<c>agent/act.ts</c>): everything one dispatched step is, resolved before it opens.</summary>
internal sealed class StepSpec
{
    public required ExecutorStepKind Kind { get; init; }

    public required string Instruction { get; init; }

    public IReadOnlyDictionary<string, object?>? Params { get; init; }

    public TimeSpan? Timeout { get; init; }

    public int? MaxSteps { get; init; }

    public int? MaxModelCalls { get; init; }

    /// <summary>Act only: the test's hook for each completed action.</summary>
    public ActionHook? OnAction { get; init; }

    /// <summary>Act only: the test's hook before each action.</summary>
    public BeforeActionHook? OnBeforeAction { get; init; }

    /// <summary>Assert only: whether the runtime keeps a redacted screenshot after the verdict. <c>false</c> opts out.</summary>
    public bool? Screenshot { get; init; }
}

/// <summary>What the built-in executor reads of the runtime beyond the public context: the attempt, the looks, and the accounting of its step.</summary>
internal sealed record ActHost(AttemptScope Scope, ObservationFeed Feed, StepAccounting Accounting, IReadOnlyDictionary<string, object?>? Params);

/// <summary>
/// The context an executor receives from the runtime (upstream's <c>ActDispatch.context()</c>): the step as the executor may
/// read it, the budgets and actions of the step, and the looks. Every call checks the step's clock first.
/// </summary>
internal sealed partial class ActContext : StepExecutorContext
{
    private readonly AttemptScope _scope;
    private readonly ObservationFeed _feed;
    private readonly StepAccounting _budget;
    private string? _transcript;
    private IReadOnlyList<StepTurn>? _turns;

    // What the step record keeps of the turns an executor attaches.
    private const int MaxTurns = 12;
    private const int MaxCalls = 8;
    private const int MaxCallChars = 200;
    private const int MaxOutcomeChars = 2048;

    [SetsRequiredMembers]
    public ActContext(AttemptScope scope, ObservationFeed feed, StepAccounting budget, ActionDispatcher dispatcher, StepSpec spec, ResolvedAgent agent, int stepIndex, IReadOnlyList<Secret> declared, LedgerContext ledger, Observation? start, ReplayedPrefix? prefix)
    {
        _scope = scope;
        _feed = feed;
        _budget = budget;
        Step = new ExecutorStep
        {
            Kind = spec.Kind,
            Index = stepIndex,
            Instruction = scope.Redactor.Redact(spec.Instruction),
            Params = ProjectParams(spec.Params, scope.Redactor, spec.Kind == ExecutorStepKind.Act ? "agent.act" : "agent.assert"),
            Secrets = declared.Select(secret => new ExecutorSecret(secret.Name, secret.Purpose)).ToList(),
        };
        Attempt = new ExecutorAttempt
        {
            TestId = scope.TestTitle,
            AttemptId = scope.AttemptId,
            TitlePath = scope.TitlePath,
            Index = scope.Attempt - 1,
            CancellationToken = scope.Ended.Token,
            Memory = scope.ExecutorMemory,
        };
        Target = new ExecutorTargetInfo { Name = scope.TargetName, Platform = scope.EnginePlatform, Verbs = VerbsOf(scope.EngineCapabilities, scope.ActionKinds, scope.Session.PointerActionKinds, scope.Session.CanDismissKeyboard), TapModifiers = scope.EngineCapabilities.HasFlag(EngineCapabilities.TapModifiers) };
        ReplayedPrefix = prefix;
        Model = agent.Model;
        ProviderOptions = agent.ProviderOptions;
        Ledger = ledger.Text;
        LedgerBytes = ledger.Bytes;
        AgentContext = agent.Context is null ? null : scope.Redactor.Redact(agent.Context);
        Budgets = budget;
        Actions = dispatcher;
        Host = new ActHost(scope, feed, budget, spec.Params);
        Start = start is null ? null : feed.View(start, scope);
    }

    /// <summary>Bytes of <see cref="StepExecutorContext.Ledger"/>, which the step's metrics keep.</summary>
    public int LedgerBytes { get; }

    public override async Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _budget.Queue.RunAsync(async () =>
        {
            _budget.Checkpoint();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_budget.Token, cancellationToken);
            try
            {
                return await _feed.ObserveAsync(options, linked.Token).ConfigureAwait(false);
            }
            catch (Exception) when (CheckStep())
            {
                throw;
            }
        }).ConfigureAwait(false);
    }

    public override void AttachTranscript(string text)
    {
        // Debug detail only: transcripts are model prose and can be large.
        if (_scope.Debug.Enabled && !string.IsNullOrEmpty(text))
        {
            _transcript = text;
        }
    }

    public override void AttachTurns(IReadOnlyList<StepTurn> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        // Model prose and tool output are not model input, but they are a record: the redactor that guards the tree guards the turns.
        // The record is bounded here, whoever attaches: redact first, then cut, so a cut never leaves half a secret. Upstream cuts first.
        _turns =
        [
            .. turns.TakeLast(MaxTurns).Select(turn => new StepTurn(
                turn.Index,
                [.. turn.Calls.Take(MaxCalls).Select(call => TurnLog.Truncate(_scope.Redactor.Redact(call), MaxCallChars))],
                TurnLog.Truncate(_scope.Redactor.Redact(turn.Outcome), MaxOutcomeChars))),
        ];
    }

    /// <summary>The turns the executor attached, redacted; null when it attached none.</summary>
    public IReadOnlyList<StepTurn>? Turns => _turns is { Count: > 0 } ? _turns : null;

    public override Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var screenshots = _scope.Screenshots;
        var artifacts = _scope.Artifacts;
        if (screenshots is null || artifacts is null)
        {
            throw new TestException("UNSUPPORTED_CAPABILITY", "this attempt keeps no results, so there is nowhere to keep the screenshot");
        }

        // Upstream writes the file, registers it as a `screenshot` artifact, and attaches it to the step in progress.
        var kept = Path.GetFullPath(Path.Combine(screenshots.ProjectRoot, screenshots.KeepEvidence(UnsafeInName().Replace(label, "-"), pixels.Data)));
        var id = artifacts.Sink.Register(Reports.ArtifactKind.Screenshot, Path.GetRelativePath(artifacts.Dir, kept).Replace('\\', '/'));
        _scope.Steps.AttachArtifact(id);
        return Task.FromResult(id);
    }

    /// <summary>Keeps the transcript the executor attached, redacted like any log. Best effort: a transcript never fails the step.</summary>
    public void WriteTranscript()
    {
        if (_transcript is null || _scope.Screenshots is not { } screenshots)
        {
            return;
        }

        try
        {
            screenshots.KeepTranscript(_scope.Redactor.Redact(_transcript));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The transcript is debug detail.
        }
    }

    // Upstream's KINDS_BY_VERB (engine/session.ts): the action kinds an engine must declare for each kind-derived grammar verb. `check` sets a
    // state rather than flipping one, so it needs both directions; `fill` unlocks the plain and the secret fill alike. The port's `clear` verb
    // follows its own kind: upstream has no clear verb.
    private static readonly (ExecutorVerb Verb, string[] Kinds)[] KindsByVerb =
    [
        (ExecutorVerb.Tap, ["tap"]),
        (ExecutorVerb.DoubleTap, ["doubleTap"]),
        (ExecutorVerb.Type, ["fill"]),
        (ExecutorVerb.TypeSecret, ["fill"]),
        (ExecutorVerb.Clear, ["clear"]),
        (ExecutorVerb.Press, ["press"]),
        (ExecutorVerb.Select, ["selectOption"]),
        (ExecutorVerb.Check, ["check", "uncheck"]),
    ];

    // The verbs the engine can honor: the grammar the built-in tools offer for these declarations (upstream's declaredVerbs).
    internal static HashSet<ExecutorVerb> VerbsOf(EngineCapabilities capabilities, IReadOnlyCollection<string>? actionKinds, IReadOnlyCollection<string> pointerKinds, bool canDismissKeyboard = false)
    {
        var verbs = new HashSet<ExecutorVerb> { ExecutorVerb.Navigate };
        var declared = actionKinds ?? [];
        foreach (var (verb, required) in KindsByVerb)
        {
            if (required.All(kind => declared.Contains(kind)))
            {
                verbs.Add(verb);
            }
        }

        foreach (var (kind, verb) in AgentTools.PointerVerbs)
        {
            if (actionKinds?.Contains(kind) == true)
            {
                verbs.Add(verb);
            }
        }

        if (capabilities.HasFlag(EngineCapabilities.Upload))
        {
            verbs.Add(ExecutorVerb.Upload);
        }

        if (capabilities.HasFlag(EngineCapabilities.Scroll))
        {
            verbs.UnionWith([ExecutorVerb.ScrollTo, ExecutorVerb.ScrollUntil, ExecutorVerb.Scroll]);
        }

        if (capabilities.HasFlag(EngineCapabilities.History))
        {
            verbs.Add(ExecutorVerb.Back);
        }

        // Upstream lists resize when `session.resize` is defined.
        if (capabilities.HasFlag(EngineCapabilities.Resize))
        {
            verbs.Add(ExecutorVerb.Resize);
        }

        // Keyboard input to the focused field follows the engine's keyboard (upstream's declaredVerbs).
        if (capabilities.HasFlag(EngineCapabilities.Keyboard))
        {
            verbs.UnionWith([ExecutorVerb.TypeText, ExecutorVerb.PressKey]);

            // A keyboard that can be sent away (a device's) adds dismissKeyboard, as upstream's `keyboard.dismiss` hook does.
            if (canDismissKeyboard)
            {
                verbs.Add(ExecutorVerb.DismissKeyboard);
            }
        }

        // The point verbs follow the pointer kinds the engine declared (upstream's declaredVerbs).
        if (pointerKinds.Contains("tap"))
        {
            verbs.Add(ExecutorVerb.TapAt);
        }

        if (pointerKinds.Contains("hover"))
        {
            verbs.Add(ExecutorVerb.HoverAt);
        }

        if (pointerKinds.Contains("dragTo"))
        {
            verbs.Add(ExecutorVerb.DragAt);
        }

        return verbs;
    }

    // Throws the step's hard stop when the clock or a cancel ended it. Otherwise the call's own failure goes through.
    private bool CheckStep()
    {
        _budget.Checkpoint();
        return false;
    }

    // The call params as JSON the executor may read: a Secret is its name and purpose, never its value, and every string the
    // test spelled that holds a secret value the attempt knows is redacted, keys included. `api` names the call in error messages.
    internal static Dictionary<string, JsonElement>? ProjectParams(IReadOnlyDictionary<string, object?>? parameters, Redactor redactor, string api)
    {
        if (parameters is null)
        {
            return null;
        }

        var (redacted, _) = ActParams.RedactParams(ActParams.Projected(parameters, api) ?? new JsonObject(), [], redactor.Redact, api);
        return redacted.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal);
    }

    [GeneratedRegex("[^A-Za-z0-9_-]+")]
    private static partial Regex UnsafeInName();
}
