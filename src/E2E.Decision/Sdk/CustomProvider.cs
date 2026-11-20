// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The decision members of the arguments of the AI SDK's <c>customProvider</c> (<c>ai/src/registry/custom-provider.ts</c>).</summary>
public sealed class CustomProviderOptions
{
    /// <summary>The decision models, by id.</summary>
    public IReadOnlyDictionary<string, IDecisionModel>? DecisionModels { get; init; }

    /// <summary>Deprecated: use <see cref="DecisionModels"/>. When both are set, <see cref="DecisionModels"/> takes precedence.</summary>
    public IReadOnlyDictionary<string, IEvaluationModel>? EvaluationModels { get; init; }

    /// <summary>The provider that answers an id the maps do not have.</summary>
    public IDecisionProvider? FallbackProvider { get; init; }
}

/// <summary>
/// The decision models of the AI SDK's <c>customProvider</c> (<c>custom-provider.ts</c>): a provider that serves its own models and
/// falls back to another provider.
/// </summary>
public sealed class CustomProvider : IDecisionProvider
{
    private readonly IReadOnlyDictionary<string, IDecisionModel>? _decisionModels;
    private readonly IReadOnlyDictionary<string, IEvaluationModel>? _evaluationModels;
    private readonly IDecisionProvider? _fallbackProvider;

    /// <summary>Upstream's <c>customProvider</c>.</summary>
    public CustomProvider(CustomProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _decisionModels = options.DecisionModels;
        _evaluationModels = options.EvaluationModels;
        _fallbackProvider = options.FallbackProvider;
    }

    /// <summary>Creates a decision model (upstream's <c>decisionModel</c>).</summary>
    public IDecisionModel DecisionModel(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        if (_decisionModels is not null)
        {
            if (_decisionModels.TryGetValue(modelId, out var model))
            {
                return model;
            }
        }
        else if (_evaluationModels is not null && _evaluationModels.TryGetValue(modelId, out var legacyModel))
        {
            return ResolveModel.ResolveDecisionModel(legacyModel);
        }

        if (_fallbackProvider?.ResolveDecisionModel(modelId) is { } fallbackModel)
        {
            return fallbackModel;
        }

        throw new NoSuchModelException(modelId, "decisionModel");
    }

    /// <summary>Deprecated: use <see cref="DecisionModel"/>.</summary>
    public EvaluationDecisionModel EvaluationModel(string modelId) =>
        ResolveModel.AsEvaluationModel(DecisionModel(modelId));

    IEvaluationModel? IDecisionProvider.EvaluationModel(string modelId) => EvaluationModel(modelId);
}
