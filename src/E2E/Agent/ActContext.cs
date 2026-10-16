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

    [SetsRequiredMembers]
    public ActContext(AttemptScope scope, ObservationFeed feed, StepAccounting budget, ActionDispatcher dispatcher, StepSpec spec, ResolvedAgent agent, Observation? start, ReplayedPrefix? prefix)
    {
        _scope = scope;
        _feed = feed;
        _budget = budget;
        var declared = spec.Params?.Values.OfType<Secret>().ToList() ?? [];
        Step = new ExecutorStep
        {
            Kind = spec.Kind,
            Index = scope.NextStepIndex(),
            Instruction = scope.Redactor.Redact(spec.Instruction),
            Params = ProjectParams(spec.Params, scope.Redactor),
            Secrets = declared.Select(secret => new ExecutorSecret(secret.Name, secret.Purpose)).ToList(),
        };
        Attempt = new ExecutorAttempt
        {
            TestId = scope.TestTitle,
            AttemptId = scope.AttemptId,
            Index = scope.Attempt - 1,
            CancellationToken = scope.Ended.Token,
            Memory = scope.ExecutorMemory,
        };
        Target = new ExecutorTargetInfo { Name = scope.TargetName, Platform = scope.EnginePlatform, Verbs = VerbsOf(scope.EngineCapabilities) };
        ReplayedPrefix = prefix;
        Model = agent.Model;
        ProviderOptions = agent.ProviderOptions;
        Ledger = string.Join('\n', scope.Completed.Select(step => "- " + step));
        AgentContext = agent.Context is null ? null : scope.Redactor.Redact(agent.Context);
        Budgets = budget;
        Actions = dispatcher;
        Host = new ActHost(scope, feed, budget, spec.Params);
        Start = start is null ? null : new ExecutorObservation(SnapshotText.Render(start, scope.Redactor, scope.AppOrigin));
    }

    public override bool PixelsTainted => _scope.Exposure.WithholdsPixels;

    public override async Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
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
    }

    public override void AttachTranscript(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _transcript = text;
        }
    }

    public override Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var screenshots = _scope.Screenshots
            ?? throw new TestException("UNSUPPORTED_CAPABILITY", "this attempt keeps no results, so there is nowhere to keep the screenshot");
        return Task.FromResult(screenshots.KeepEvidence(UnsafeInName().Replace(label, "-"), pixels.Data));
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

    // The verbs the engine can honor: the grammar the built-in tools offer for these capabilities.
    private static HashSet<ExecutorVerb> VerbsOf(EngineCapabilities capabilities)
    {
        var verbs = new HashSet<ExecutorVerb>
        {
            ExecutorVerb.Tap,
            ExecutorVerb.DoubleTap,
            ExecutorVerb.Type,
            ExecutorVerb.TypeSecret,
            ExecutorVerb.Clear,
            ExecutorVerb.Press,
            ExecutorVerb.Select,
            ExecutorVerb.Check,
            ExecutorVerb.Navigate,
        };
        if (capabilities.HasFlag(EngineCapabilities.Scroll))
        {
            verbs.UnionWith([ExecutorVerb.ScrollTo, ExecutorVerb.ScrollUntil, ExecutorVerb.Scroll]);
        }

        if (capabilities.HasFlag(EngineCapabilities.History))
        {
            verbs.Add(ExecutorVerb.Back);
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
    // test spelled that holds a secret value the attempt knows is redacted, keys included.
    private static Dictionary<string, JsonElement>? ProjectParams(IReadOnlyDictionary<string, object?>? parameters, Redactor redactor)
    {
        if (parameters is null)
        {
            return null;
        }

        var projected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, node) in ActParams.Projected(parameters) ?? new JsonObject())
        {
            AddKey(projected, redactor.Redact(key), RedactJson(node, redactor));
        }

        return projected;
    }

    private static JsonElement RedactJson(JsonNode? node, Redactor redactor)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonSerializer.SerializeToElement(redactor.Redact(text));
            case JsonObject map:
                var cleaned = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var (key, entry) in map)
                {
                    AddKey(cleaned, redactor.Redact(key), RedactJson(entry, redactor));
                }

                return JsonSerializer.SerializeToElement(cleaned);
            case JsonArray list:
                return JsonSerializer.SerializeToElement(list.Select(entry => RedactJson(entry, redactor)).ToList());
            default:
                return JsonSerializer.SerializeToElement(node);
        }
    }

    private static void AddKey(Dictionary<string, JsonElement> map, string key, JsonElement value)
    {
        if (!map.TryAdd(key, value))
        {
            throw new TestException("INVALID_ARGUMENT", "agent.act params has two keys that read \"" + key + "\" once secret values are redacted; a key cannot be told apart by a secret");
        }
    }

    [GeneratedRegex("[^A-Za-z0-9_-]+")]
    private static partial Regex UnsafeInName();
}
