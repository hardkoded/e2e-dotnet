// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.CacheModelProvenance;

/// <summary>Upstream's <c>{ ...model, modelId }</c> of <c>counterModel</c>: a model that answers as <paramref name="inner"/> does under another name.</summary>
internal sealed class NamedModel(IAgentModel inner, string name) : IAgentModel
{
    public string Name => name;

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) => inner.CompleteAsync(request, cancellationToken);
}
