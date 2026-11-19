// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.Decide;

/// <summary>The AI SDK's <c>decide.test.ts</c> fixtures (<c>questions</c>, <c>answers</c>, <c>setup</c>) and its recording telemetry integration.</summary>
internal static class DecideTestHelpers
{
    public static IReadOnlyDictionary<string, DecisionQuestion> QuestionMap { get; } =
        new Dictionary<string, DecisionQuestion>
        {
            ["topic"] = DecisionQuestion.Choice(
                "Pick a topic.",
                [new DecisionChoice("billing"), new DecisionChoice("support")]
            ),
            ["refund"] = DecisionQuestion.Boolean("Refund?"),
        };

    public static IReadOnlyDictionary<string, DecisionAnswer> AnswerMap { get; } =
        new Dictionary<string, DecisionAnswer>
        {
            ["topic"] = new()
            {
                Type = "choice",
                Choice = "billing",
                Probabilities = new Dictionary<string, double> { ["billing"] = 0.9, ["support"] = 0.1 },
            },
            ["refund"] = new() { Type = "boolean", Probability = 0.8 },
        };

    /// <summary>A model that answers as <paramref name="result"/> (or <see cref="AnswerMap"/>) and records its calls, as <c>setup()</c>.</summary>
    public static (RecordingModel Model, List<DecisionModelCall> Calls) Setup(DecisionModelResult? result = null)
    {
        var model = new RecordingModel(result ?? new DecisionModelResult { Answers = AnswerMap });
        return (model, model.Calls);
    }

    public sealed class RecordingModel(DecisionModelResult result) : IDecisionModel
    {
        private readonly Queue<Exception> _failures = new();

        public List<DecisionModelCall> Calls { get; } = [];

        public string Provider => "mock-provider";

        public string ModelId => "mock-model-id";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["choice", "score", "boolean", "box"];

        /// <summary>Makes the next call fail with <paramref name="error"/> (<c>mockRejectedValueOnce</c>).</summary>
        public void FailOnce(Exception error) => _failures.Enqueue(error);

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            Calls.Add(call);
            return _failures.Count > 0
                ? Task.FromException<DecisionModelResult>(_failures.Dequeue())
                : Task.FromResult(result);
        }
    }

    /// <summary>A telemetry integration that records every event it receives, in order.</summary>
    public sealed class RecordingTelemetry : ITelemetry
    {
        public List<DecideStartEvent> DecideStarts { get; } = [];

        public List<DecisionModelCallStartEvent> ModelCallStarts { get; } = [];

        public List<DecisionModelCallEndEvent> ModelCallEnds { get; } = [];

        public List<DecideEndEvent> DecideEnds { get; } = [];

        public List<TelemetryErrorEvent> Errors { get; } = [];

        public Task OnDecideStartAsync(DecideStartEvent @event)
        {
            DecideStarts.Add(@event);
            return Task.CompletedTask;
        }

        public Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event)
        {
            ModelCallStarts.Add(@event);
            return Task.CompletedTask;
        }

        public Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event)
        {
            ModelCallEnds.Add(@event);
            return Task.CompletedTask;
        }

        public Task OnDecideEndAsync(DecideEndEvent @event)
        {
            DecideEnds.Add(@event);
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(TelemetryErrorEvent @event)
        {
            Errors.Add(@event);
            return Task.CompletedTask;
        }
    }
}
