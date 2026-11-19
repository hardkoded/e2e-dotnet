// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The decision members of the options of the AI SDK's <c>createProviderRegistry</c> (<c>ai/src/registry/provider-registry.ts</c>).</summary>
public sealed class ProviderRegistryOptions
{
    /// <summary>The separator between the provider id and the model id of a combined id. Defaults to <c>:</c>.</summary>
    public string Separator { get; init; } = ":";
}

/// <summary>The decision members of the AI SDK's <c>ProviderRegistryProvider</c> (<c>provider-registry.ts</c>): models by <c>providerId:modelId</c>.</summary>
public interface IProviderRegistryProvider
{
    /// <summary>Creates a decision model from a combined id, such as <c>typesafe-ai:jev-latest</c>.</summary>
    IDecisionModel DecisionModel(string id);

    /// <summary>Deprecated: use <see cref="DecisionModel"/>.</summary>
    EvaluationDecisionModel EvaluationModel(string id);
}

/// <summary>The AI SDK's <c>createProviderRegistry</c> and <c>DefaultProviderRegistry</c> (<c>provider-registry.ts</c>), for decision models.</summary>
public static class ProviderRegistry
{
    /// <summary>Upstream's <c>createProviderRegistry</c>.</summary>
    public static IProviderRegistryProvider CreateProviderRegistry(
        IReadOnlyDictionary<string, IDecisionProvider> providers,
        ProviderRegistryOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(providers);
        var registry = new DefaultProviderRegistry(options?.Separator ?? ":");
        foreach (var (id, provider) in providers)
        {
            registry.RegisterProvider(id, provider);
        }

        return registry;
    }
}

/// <summary>Upstream's <c>DefaultProviderRegistry</c>.</summary>
internal sealed class DefaultProviderRegistry(string separator) : IProviderRegistryProvider
{
    private readonly Dictionary<string, IDecisionProvider> _providers = [];

    public void RegisterProvider(string id, IDecisionProvider provider) => _providers[id] = provider;

    public IDecisionModel DecisionModel(string id)
    {
        var (providerId, modelId) = SplitId(id, "decisionModel");
        var provider = GetProvider(providerId, "decisionModel");
        return provider.ResolveDecisionModel(modelId) ?? throw new NoSuchModelException(id, "decisionModel");
    }

    public EvaluationDecisionModel EvaluationModel(string id) => ResolveModel.AsEvaluationModel(DecisionModel(id));

    private IDecisionProvider GetProvider(string id, string modelType) =>
        _providers.TryGetValue(id, out var provider)
            ? provider
            : throw new NoSuchProviderException(id, modelType, id, [.. _providers.Keys]);

    private (string ProviderId, string ModelId) SplitId(string id, string modelType)
    {
        var index = id.IndexOf(separator, StringComparison.Ordinal);
        if (index == -1)
        {
            throw new NoSuchModelException(
                id,
                modelType,
                "Invalid "
                    + modelType
                    + " id for registry: "
                    + id
                    + " (must be in the format \"providerId"
                    + separator
                    + "modelId\")"
            );
        }

        return (id[..index], id[(index + separator.Length)..]);
    }
}
