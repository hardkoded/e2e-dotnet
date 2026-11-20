// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>packages/ai/src/model/resolve-model.ts</c> (<c>resolveDecisionModel</c>, <c>asEvaluationModel</c>).</summary>
public static class ResolveModel
{
    /// <summary>
    /// The AI SDK's <c>globalThis.AI_SDK_DEFAULT_PROVIDER</c>: the provider that resolves a model id. Null means the Gateway.
    /// Process-wide, as upstream's global.
    /// </summary>
    public static IDecisionProvider? AiSdkDefaultProvider { get; set; }

    /// <summary>
    /// The AI SDK's <c>resolveDecisionModel</c> for a deprecated evaluation model: an <see cref="IDecisionModel"/> whose
    /// <c>DoDecideAsync</c> calls the model's <c>DoEvaluateAsync</c>.
    /// </summary>
    public static IDecisionModel ResolveDecisionModel(IEvaluationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LegacyDecisionModel(model);
    }

    /// <summary>
    /// The AI SDK's <c>resolveDecisionModel</c> for a model id: the default provider's <c>decisionModel</c>, else its deprecated
    /// <c>evaluationModel</c>.
    /// </summary>
    public static IDecisionModel ResolveDecisionModel(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        var provider = AiSdkDefaultProvider ?? new GatewayProvider();
        return provider.ResolveDecisionModel(modelId) ?? throw new NoSuchModelException(modelId, "decisionModel");
    }

    /// <summary>
    /// The decision-model factory chain of upstream's <c>provider.decisionModel ?? provider.evaluationModel</c>, resolved to a decision
    /// model. Null when the provider has neither or its factory returns null.
    /// </summary>
    internal static IDecisionModel? ResolveDecisionModel(this IDecisionProvider provider, string modelId)
    {
        if (provider.DecisionModel(modelId) is { } decisionModel)
        {
            return decisionModel;
        }

        return provider.EvaluationModel(modelId) is { } evaluationModel ? ResolveDecisionModel(evaluationModel) : null;
    }

    /// <summary>The AI SDK's <c>asEvaluationModel</c>: internal adapter for deprecated provider factories.</summary>
    public static EvaluationDecisionModel AsEvaluationModel(IDecisionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model as EvaluationDecisionModel ?? new EvaluationDecisionModel(model);
    }

    // .NET-only: upstream returns an object literal from resolveDecisionModel; C# needs a named type for it.
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

/// <summary>
/// A decision model that also has the deprecated <c>doEvaluate</c> (upstream's <c>DecisionModelV4 &amp; { doEvaluate }</c>, what
/// <c>asEvaluationModel</c> returns). .NET-only: C# needs a named type for the intersection.
/// </summary>
public sealed class EvaluationDecisionModel(IDecisionModel model) : IDecisionModel, IEvaluationModel
{
    public string Provider => model.Provider;

    public string ModelId => model.ModelId;

    public string SpecificationVersion => model.SpecificationVersion;

    public IReadOnlyList<string> SupportedQuestionTypes => model.SupportedQuestionTypes;

    public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
        model.DoDecideAsync(call, cancellationToken);

    /// <summary>Deprecated: use <see cref="DoDecideAsync"/>.</summary>
    public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
        model.DoDecideAsync(call, cancellationToken);
}
