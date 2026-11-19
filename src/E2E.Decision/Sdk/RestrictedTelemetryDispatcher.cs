// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's telemetry dispatcher for the decide call: <c>createTelemetryDispatcher</c> (<c>telemetry/create-telemetry-dispatcher.ts</c>) and
/// <c>createRestrictedTelemetryDispatcher</c> (<c>decide/restricted-telemetry-dispatcher.ts</c>). It fans the decide events out to the
/// integrations, adds the call's telemetry settings to each, and applies <c>filterTelemetryEvent</c> and <c>filterIncludedContext</c>.
/// </summary>
internal sealed class TelemetryDispatcher
{
    /// <summary>The AI SDK version the events report (<c>VERSION</c>).</summary>
    internal const string AiVersion = "7.0.177";

    private static readonly IReadOnlyDictionary<string, object?> NoContext = new Dictionary<string, object?>();

    private static readonly IReadOnlyDictionary<string, DecisionQuestion> EmptyQuestions =
        new Dictionary<string, DecisionQuestion>();

    private static readonly IReadOnlyDictionary<string, System.Text.Json.JsonElement> EmptyOptions =
        new Dictionary<string, System.Text.Json.JsonElement>();

    private static readonly IReadOnlyDictionary<string, DecisionAnswer> EmptyAnswers =
        new Dictionary<string, DecisionAnswer>();

    private readonly TelemetryOptions? _telemetry;

    private readonly IReadOnlyList<ITelemetry> _integrations;

    /// <summary>
    /// Creates the dispatcher. When telemetry is explicitly disabled, it has no integrations. The global integration registry of the AI SDK
    /// (<c>telemetry-registry.ts</c>) is not ported, so a call with no <see cref="TelemetryOptions.Integrations"/> has none.
    /// </summary>
    internal TelemetryDispatcher(TelemetryOptions? telemetry)
    {
        _telemetry = telemetry;
        _integrations = telemetry?.IsEnabled == false ? [] : telemetry?.Integrations ?? [];
    }

    /// <summary>The AI SDK's <c>experimental_onDecideStart</c> of the restricted dispatcher: its <c>onStart</c>.</summary>
    internal Task OnStartAsync(DecideStartEvent @event)
    {
        var augmented = FilterEvent(
            Augment(@event) with
            {
                RuntimeContext = FilterIncludedContext(@event.RuntimeContext),
            }
        );
        return Fan(integration => integration.OnDecideStartAsync(augmented));
    }

    /// <summary>The restricted dispatcher's <c>onEnd</c>.</summary>
    internal Task OnEndAsync(DecideEndEvent @event)
    {
        var augmented = FilterEvent(
            Augment(@event) with
            {
                RuntimeContext = FilterIncludedContext(@event.RuntimeContext),
            }
        );
        return Fan(integration => integration.OnDecideEndAsync(augmented));
    }

    /// <summary>The dispatcher's <c>experimental_onDecisionModelCallStart</c>.</summary>
    internal Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event)
    {
        var augmented = FilterEvent(Augment(@event));
        return Fan(integration => integration.OnDecisionModelCallStartAsync(augmented));
    }

    /// <summary>The dispatcher's <c>experimental_onDecisionModelCallEnd</c>.</summary>
    internal Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event)
    {
        var augmented = FilterEvent(Augment(@event));
        return Fan(integration => integration.OnDecisionModelCallEndAsync(augmented));
    }

    /// <summary>The dispatcher's <c>onError</c>.</summary>
    internal Task OnErrorAsync(string callId, Exception error)
    {
        var augmented = FilterEvent(Augment(new TelemetryErrorEvent { CallId = callId, Error = error }));
        return Fan(integration => integration.OnErrorAsync(augmented));
    }

    /// <summary>The AI SDK's <c>notify</c> (<c>util/notify.ts</c>): runs every callback with the event in parallel. A callback's error does not break the call.</summary>
    internal static async Task NotifyAsync<TEvent>(
        TEvent @event,
        IEnumerable<Func<TEvent, Task>?> callbacks,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
                callbacks
                    .Where(callback => callback is not null)
                    .Select(async callback =>
                    {
                        try
                        {
                            await callback!(@event).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            // As the AI SDK: an error in a callback does not break the call.
                        }
                    })
            )
            .ConfigureAwait(false);
    }

    private async Task Fan(Func<ITelemetry, Task> call)
    {
        // The AI SDK's mergeCallbacks runs the integrations in parallel and a failing one does not stop the others.
        await Task.WhenAll(_integrations.Select(integration => call(integration))).ConfigureAwait(false);
    }

    /// <summary>The AI SDK's <c>augmentEvent</c> without its filtering: the call's telemetry settings and the SDK version.</summary>
    private DecideStartEvent Augment(DecideStartEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecideEndEvent Augment(DecideEndEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecisionModelCallStartEvent Augment(DecisionModelCallStartEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private DecisionModelCallEndEvent Augment(DecisionModelCallEndEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    private TelemetryErrorEvent Augment(TelemetryErrorEvent @event) =>
        @event with
        {
            RecordInputs = _telemetry?.RecordInputs,
            RecordOutputs = _telemetry?.RecordOutputs,
            FunctionId = _telemetry?.FunctionId,
            AiVersion = AiVersion,
        };

    /// <summary>The AI SDK's <c>filterIncludedContext</c>: only the top-level properties marked for inclusion.</summary>
    private IReadOnlyDictionary<string, object?> FilterIncludedContext(IReadOnlyDictionary<string, object?>? context)
    {
        if (context is null)
        {
            return NoContext;
        }

        var include = _telemetry?.IncludeRuntimeContext;
        return context
            .Where(pair => include is not null && include.TryGetValue(pair.Key, out var included) && included)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private bool HideInputs => _telemetry?.RecordInputs == false;

    private bool HideOutputs => _telemetry?.RecordOutputs == false;

    // The AI SDK's filterTelemetryEvent for the decide events, on a copy: hidden inputs empty the state, the questions, and the provider
    // options; hidden outputs empty the answers; and either hides the provider metadata and the error.
    private DecideStartEvent FilterEvent(DecideStartEvent @event) =>
        HideInputs || HideOutputs
            ? @event with
            {
                State = [],
                Questions = EmptyQuestions,
                ProviderOptions = EmptyOptions,
            }
            : @event;

    private DecideEndEvent FilterEvent(DecideEndEvent @event) =>
        HideInputs || HideOutputs
            ? @event with
            {
                State = HideInputs ? [] : @event.State,
                Questions = HideInputs ? EmptyQuestions : @event.Questions,
                ProviderOptions = HideInputs ? EmptyOptions : @event.ProviderOptions,
                Answers = HideOutputs ? EmptyAnswers : @event.Answers,
                ProviderMetadata = null,
            }
            : @event;

    private DecisionModelCallStartEvent FilterEvent(DecisionModelCallStartEvent @event) =>
        HideInputs ? @event with { State = [], Questions = EmptyQuestions } : @event;

    private DecisionModelCallEndEvent FilterEvent(DecisionModelCallEndEvent @event) =>
        HideInputs || HideOutputs
            ? @event with
            {
                State = HideInputs ? [] : @event.State,
                Questions = HideInputs ? EmptyQuestions : @event.Questions,
                Answers = HideOutputs ? EmptyAnswers : @event.Answers,
                ProviderMetadata = null,
            }
            : @event;

    private TelemetryErrorEvent FilterEvent(TelemetryErrorEvent @event) =>
        HideInputs || HideOutputs
            ? @event with
            {
                Error = new InvalidOperationException("AI SDK operation failed"),
            }
            : @event;
}
