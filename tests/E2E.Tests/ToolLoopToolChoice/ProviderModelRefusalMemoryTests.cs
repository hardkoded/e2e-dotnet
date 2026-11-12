// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// .NET-only (no upstream <c>it</c>): upstream remembers a refusal per model object, which a worker builds once. The port builds a
/// provider model per session, so the loop keys a provider model (one that is disposable, like the built-in clients) by its type
/// and model id. A second session on a new model object of the same id starts in <c>auto</c>.
/// </summary>
public sealed class ProviderModelRefusalMemoryTests
{
    [Fact]
    public async Task A_provider_model_that_refused_a_forced_choice_starts_the_next_session_in_auto()
    {
        var choices = new List<string>();
        for (var session = 0; session < 2; session++)
        {
            using var model = new RefusingProviderModel(choices);
            var attempt = await RunAsync(
                async ctx =>
                {
                    await ctx.App.OpenAsync("/start");
                    await ctx.Agent.ActAsync("first");
                },
                new DocumentWorld().Map("/start", page => page.Button("Peek")),
                model);
            Assert.Null(attempt.Error);
        }

        Assert.Equal(["required", "auto", "auto"], choices);
    }

    private sealed class RefusingProviderModel(List<string> choices) : IAgentModel, IDisposable
    {
        public string Name => "provider-model-refusal-memory";

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            choices.Add(request.ToolChoice!.Type);
            return request.ToolChoice.Forced
                ? throw FakeLoopModel.Rejection
                : Task.FromResult(ModelResponses.Done("passed", "done"));
        }

        public void Dispose()
        {
        }
    }
}
