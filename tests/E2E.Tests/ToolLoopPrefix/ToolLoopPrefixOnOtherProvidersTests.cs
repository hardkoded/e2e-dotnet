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
/// Another provider than Anthropic binds no reasoning to the request prefix, so a rewritten message drops none. The stale
/// opening screen is elided by the built-in agent, as upstream's step does.
/// </summary>
public sealed class ToolLoopPrefixOnOtherProvidersTests
{
    [Fact]
    public async Task Replays_every_reasoning_part_after_an_elision_nothing_binds_them_to_the_prefix()
    {
        var requests = new List<IReadOnlyList<ModelMessage>>();
        var reasoning = JsonDocument.Parse("\"thought\"").RootElement.Clone();
        var model = new ScriptedModel(request =>
        {
            requests.Add([.. request.Messages]);
            var answer = requests.Count <= 2 ? ModelResponses.Call("observe", new { }) : ModelResponses.Done("passed", "done");
            return new ModelResponse { ToolCalls = answer.ToolCalls, ReasoningItems = [reasoning] };
        });
        var big = new DocumentWorld().Map("/start", page =>
        {
            for (var i = 0; i < 600; i++)
            {
                page.Paragraph("row " + i + " " + new string('x', 90));
            }
        });

        var attempt = await CoreTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look around twice, then conclude");
            },
            big,
            model);

        Assert.Null(attempt.Error);
        Assert.Equal(3, requests.Count);
        Assert.Contains("[earlier screen elided", requests[1][0].Content, StringComparison.Ordinal);
        Assert.Equal([0, 1, 2], requests.Select(request => request.Count(message => message.ReasoningItems is { Count: > 0 })));
    }
}
