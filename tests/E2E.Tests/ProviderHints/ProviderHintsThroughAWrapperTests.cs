// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;
using static E2E.Tests.ProviderHints.HintedBodies;

namespace E2E.Tests.ProviderHints;

/// <summary>
/// .NET-only: a subscription model (<see cref="DelegatingModel"/>) wraps the client it settles on, and upstream has no wrapper to
/// look through. The hint reads the settled client, so Claude reached over a wrapper still binds reasoning to the prefix.
/// </summary>
public sealed class ProviderHintsThroughAWrapperTests
{
    [Fact]
    public async Task Binds_reasoning_to_the_prefix_for_a_wrapped_AnthropicModel_and_not_for_a_wrapped_other_model_NET_ONLY()
    {
        var handler = new RecordingHandler(AnthropicAnswer);
        using var http = new HttpClient(handler);
        using var claude = new AnthropicModel(new AnthropicModelOptions { Model = "claude-opus-5.5", ApiKey = "k" }, http);
        using var wrappedClaude = new Wrapper(claude);
        using var wrappedOther = new Wrapper(new ScriptedModel(_ => ModelResponses.Done("passed", "done")));

        // Before the first call the wrapper has not settled, so there is no client to look at.
        Assert.False(E2E.ProviderHints.BindsReasoningToPrefix(wrappedClaude));

        await wrappedClaude.CompleteAsync(Request(), CancellationToken.None);
        await wrappedOther.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(E2E.ProviderHints.BindsReasoningToPrefix(wrappedClaude));
        Assert.False(E2E.ProviderHints.BindsReasoningToPrefix(wrappedOther));
    }

    private sealed class Wrapper(IAgentModel inner) : DelegatingModel("wrapped", new HttpClient())
    {
        protected override IAgentModel Fallback => inner;

        protected override Task<IAgentModel?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult<IAgentModel?>(inner);
    }
}
