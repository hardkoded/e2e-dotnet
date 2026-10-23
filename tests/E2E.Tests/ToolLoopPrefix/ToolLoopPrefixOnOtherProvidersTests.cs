// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;
using static E2E.Tests.ToolLoopPrefix.ToolLoopPrefixHelpers;

namespace E2E.Tests.ToolLoopPrefix;

/// <summary>
/// Another provider than Anthropic binds no reasoning to the request prefix, so a rewritten message drops none. The test is
/// partial: the port elides screens only to shrink an overflowed history, so an executor rewrites the opening message instead.
/// </summary>
public sealed class ToolLoopPrefixOnOtherProvidersTests
{
    [Fact]
    public async Task Replays_every_reasoning_part_after_an_elision_nothing_binds_them_to_the_prefix_PARTIAL()
    {
        var requests = new List<IReadOnlyList<ModelMessage>>();
        var reasoning = JsonDocument.Parse("\"thought\"").RootElement.Clone();
        var model = new ScriptedModel(request =>
        {
            requests.Add([.. request.Messages]);
            var answer = requests.Count <= 2 ? ModelResponses.Call("look", new { }) : ModelResponses.Done("passed", "done");
            return new ModelResponse { ToolCalls = answer.ToolCalls, ReasoningItems = [reasoning] };
        });
        var executor = Looking((messages, turn) =>
        {
            var next = messages.ToList();
            if (turn.StepNumber == 2)
            {
                next[0] = Copy(next[0], "Execute this test step: look twice, then conclude. [edited]");
            }

            return next;
        });

        var attempt = await CoreTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look twice, then conclude");
            },
            new DocumentWorld().Map("/start", page => page.Button("Peek")),
            model,
            executor: executor);

        Assert.Null(attempt.Error);
        Assert.Equal(3, requests.Count);
        Assert.Contains("[edited]", requests[2][0].Content, StringComparison.Ordinal);
        Assert.Equal([0, 1, 2], requests.Select(request => request.Count(message => message.ReasoningItems is { Count: > 0 })));
    }
}
