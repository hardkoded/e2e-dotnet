// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>model/resolve-model.ts</c>.</summary>
public static class ResolveModel
{
    /// <summary>
    /// The AI SDK's <c>resolveDecisionModel</c> for a deprecated evaluation model: an <see cref="IDecisionModel"/> whose
    /// <c>DoDecideAsync</c> calls the model's <c>DoEvaluateAsync</c>.
    /// </summary>
    public static IDecisionModel ResolveDecisionModel(IEvaluationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LegacyDecisionModel(model);
    }

    private sealed class LegacyDecisionModel(IEvaluationModel legacyModel) : IDecisionModel
    {
        public string Provider => legacyModel.Provider;

        public string ModelId => legacyModel.ModelId;

        public string SpecificationVersion => legacyModel.SpecificationVersion;

        public IReadOnlyList<string> SupportedQuestionTypes => legacyModel.SupportedQuestionTypes;

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
            legacyModel.DoEvaluateAsync(call, cancellationToken);
    }
}
