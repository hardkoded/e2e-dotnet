// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.DecisionModels;

/// <summary>
/// .NET-only: the runtime hands an executor the caller's <see cref="CancellationToken"/> beside the attempt's, so a text-model call must
/// honor it. No upstream <c>it</c> covers it (upstream has one signal).
/// </summary>
public sealed class TextModelCancellationTests
{
    [Fact]
    public async Task Refuses_a_value_the_text_model_returns_after_the_caller_cancelled()
    {
        using var caller = new CancellationTokenSource();
        var text = new CancellingText(caller);
        var fixture = Context(new ContextOptions { Model = text });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            global::E2E.Decision.Text.AskTextAsync(
                fixture.Ctx,
                text,
                global::E2E.Decision.Text.TextAsk,
                new FieldInput
                {
                    Goal = "Fill Name",
                    Params = new JsonObject(),
                    Page = "Name",
                    RecentActions = [],
                },
                caller.Token
            )
        );
    }

    /// <summary>A text model that answers, but only after the caller cancelled.</summary>
    private sealed class CancellingText(CancellationTokenSource caller) : IAgentModel
    {
        public string Name => "scripted-text/cancelling";

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            caller.Cancel();
            return Task.FromResult(new ModelResponse { Content = """{"text":"Ada"}""" });
        }
    }
}
