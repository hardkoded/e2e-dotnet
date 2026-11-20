// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// A provider that creates decision models (the AI SDK's <c>DecisionProvider</c>, <c>ai/src/decide/decision-provider.ts</c>). Both members
/// are optional, as upstream's: a provider overrides the ones it has. A member that is not overridden returns null.
/// </summary>
public interface IDecisionProvider
{
    /// <summary>Creates a decision model (<c>decisionModel</c>).</summary>
    IDecisionModel? DecisionModel(string modelId) => null;

    /// <summary>Deprecated: use <see cref="DecisionModel"/>. Creates an evaluation model (<c>evaluationModel</c>).</summary>
    IEvaluationModel? EvaluationModel(string modelId) => null;
}
