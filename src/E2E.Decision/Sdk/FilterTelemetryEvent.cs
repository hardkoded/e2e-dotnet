// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>filterTelemetryEvent</c> (<c>telemetry/filter-telemetry-event.ts</c>) for the decide events, on a copy: hidden inputs empty
/// the state, the questions, and the provider options; hidden outputs empty the answers; and either hides the provider metadata and the error.
/// </summary>
internal static class FilterTelemetryEvent
{
    private static readonly IReadOnlyDictionary<string, DecisionQuestion> EmptyQuestions =
        new Dictionary<string, DecisionQuestion>();

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyOptions =
        new Dictionary<string, JsonElement>();

    private static readonly IReadOnlyDictionary<string, DecisionAnswer> EmptyAnswers =
        new Dictionary<string, DecisionAnswer>();

    internal static DecideStartEvent Filter(DecideStartEvent @event, TelemetryOptions? telemetry) =>
        Hides(telemetry, out var hideInputs, out _) && hideInputs
            ? @event with
            {
                State = [],
                Questions = EmptyQuestions,
                ProviderOptions = EmptyOptions,
            }
            : @event;

    internal static DecideEndEvent Filter(DecideEndEvent @event, TelemetryOptions? telemetry)
    {
        if (!Hides(telemetry, out var hideInputs, out var hideOutputs))
        {
            return @event;
        }

        return @event with
        {
            State = hideInputs ? [] : @event.State,
            Questions = hideInputs ? EmptyQuestions : @event.Questions,
            ProviderOptions = hideInputs ? EmptyOptions : @event.ProviderOptions,
            Answers = hideOutputs ? EmptyAnswers : @event.Answers,
            ProviderMetadata = null,
        };
    }

    internal static DecisionModelCallStartEvent Filter(
        DecisionModelCallStartEvent @event,
        TelemetryOptions? telemetry
    ) =>
        Hides(telemetry, out var hideInputs, out _) && hideInputs
            ? @event with
            {
                State = [],
                Questions = EmptyQuestions,
            }
            : @event;

    internal static DecisionModelCallEndEvent Filter(DecisionModelCallEndEvent @event, TelemetryOptions? telemetry)
    {
        if (!Hides(telemetry, out var hideInputs, out var hideOutputs))
        {
            return @event;
        }

        return @event with
        {
            State = hideInputs ? [] : @event.State,
            Questions = hideInputs ? EmptyQuestions : @event.Questions,
            Answers = hideOutputs ? EmptyAnswers : @event.Answers,
            ProviderMetadata = null,
        };
    }

    internal static TelemetryErrorEvent Filter(TelemetryErrorEvent @event, TelemetryOptions? telemetry) =>
        Hides(telemetry, out _, out _)
            ? @event with
            {
                Error = new InvalidOperationException("AI SDK operation failed"),
            }
            : @event;

    private static bool Hides(TelemetryOptions? telemetry, out bool hideInputs, out bool hideOutputs)
    {
        hideInputs = telemetry?.RecordInputs == false;
        hideOutputs = telemetry?.RecordOutputs == false;
        return hideInputs || hideOutputs;
    }
}
