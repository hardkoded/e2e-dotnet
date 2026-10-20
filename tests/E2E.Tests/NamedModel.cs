// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests;

/// <summary>A scripted model that names itself <paramref name="name"/>, for a test that tells recordings by the model behind them.</summary>
internal sealed class NamedModel(string name, ScriptedModel inner) : IAgentModel
{
    public string Name => name;

    public int CallCount => inner.CallCount;

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) => inner.CompleteAsync(request, cancellationToken);
}
