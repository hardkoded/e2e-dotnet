// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Tests.ToolLoopPrefix.PrefixKit;

namespace E2E.Tests.ToolLoopPrefix;

public sealed class TheRequestPrefixOfAnAgentStepOnOtherProvidersTests
{
    /// <summary>Partial port of "replays every reasoning part after an elision: nothing binds them to the prefix": the elision is a shrink after a refusal, as the port has no per-turn compaction, so the retried request replays what the refused one held.</summary>
    [Fact]
    public async Task Replays_every_reasoning_part_after_a_shrink_nothing_binds_them_to_the_prefix()
    {
        var reasoningParts = new List<int>();
        var calls = 0;
        var model = new ScriptedModel(request =>
        {
            reasoningParts.Add(request.Messages.Sum(message => message.ReasoningItems?.Count ?? 0));
            return ++calls switch
            {
                1 or 2 => With(ModelResponses.Call("observe", new { }), calls),
                3 => throw new AgentException("CONTEXT_OVERFLOW", "The model request exceeded the context window: prompt is too long", blocked: true),
                _ => ModelResponses.Done("passed", "done"),
            };
        });
        await using var session = await StartAsync(BigScreen(), model);

        await session.App.OpenAsync("/big");
        await session.Agent.ActAsync("look around twice, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        Assert.Contains("[earlier screen elided", model.Requests[3].Messages[0].Content, StringComparison.Ordinal);
        Assert.Equal([0, 1, 2, 2], reasoningParts);
        Assert.False(Noted(session, "was rewritten"));
    }

    private static ModelResponse With(ModelResponse response, int turn) => new()
    {
        ToolCalls = response.ToolCalls,
        ReasoningItems = [JsonSerializer.SerializeToElement("thought " + turn)],
    };
}
