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
/// The request prefix an agent step sends turn after turn, as Anthropic reads it: it binds each thinking block to the system
/// prompt and every message before it, and rejects a replayed block whose prefix changed. The real <see cref="AnthropicModel"/>
/// runs over a recording handler that answers with signed thinking blocks, and the assertions read the wire format.
/// Not ported: "keeps the system prompt identical on every turn, the switch to auto included" (the port sends <c>auto</c> from
/// the first turn, so it has no switch), and "notices a tool result a custom executor edits in place, below the part" (a
/// <see cref="ModelMessage"/> is immutable, so an executor replaces a message and cannot edit it). Partial: "stops replaying the
/// thinking from a message an elision rewrote" carries another name, since the port elides screens only to shrink an overflowed
/// history, so the rewrite is that shrink; "notices a message a custom executor edits in place" replaces the message, for the
/// same reason; "keeps every reasoning part when a custom executor clones the history each turn" has a text opening message
/// with no bytes, and does not read the turn notes. The test of a message an executor replaces on the first turn, and keeps the
/// thinking produced after it, is .NET-only: upstream covers that through its elision, which the port lacks.
/// </summary>
public sealed class ToolLoopPrefixOnAnthropicTests
{
    [Fact]
    public async Task Stops_replaying_the_thinking_from_a_message_a_history_shrink_rewrote_PARTIAL()
    {
        var big = new DocumentWorld().Map("/start", page =>
        {
            for (var i = 0; i < 600; i++)
            {
                page.Paragraph("row " + i + " " + new string('x', 40));
            }
        });
        var handler = new RecordingHandler(Answer(1, "tap", new { role = "button", name = "Peek" })).Then(HttpStatusCode.BadRequest, Overflow).Then(HttpStatusCode.OK, Done(2));
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "claude-opus-5.5", ApiKey = "k" }, http);

        var attempt = await CoreTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("read the big output");
            },
            big,
            model);

        Assert.Null(attempt.Error);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(["messages.1.content.0=sig-1"], ReplayedThinking(handler.Requests[1]));
        // The retried request carries the clipped opening screen, so the block produced under the whole screen is not sent again.
        Assert.Contains("more characters cut", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Empty(ReplayedThinking(handler.Requests[2]));
    }

    [Fact]
    public async Task Stops_replaying_the_thinking_from_a_message_an_executor_replaced_and_keeps_the_thinking_produced_after_it()
    {
        var handler = new RecordingHandler(Look(1), Look(2), Done(3));
        var executor = Looking((messages, turn) =>
        {
            // Before the second turn, the opening message is replaced by an edited one.
            var next = messages.ToList();
            if (turn.StepNumber == 1)
            {
                next[0] = Copy(next[0], "Execute this test step: look twice, then conclude. [edited]");
            }

            return next;
        });

        var (error, _) = await RunAsync(handler, executor);

        Assert.Null(error);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("[edited]", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Empty(ReplayedThinking(handler.Requests[1]));
        // The third request leaves messages.0 as the second sent it: the block produced under the edited message is valid and goes out.
        Assert.Equal(["messages.3.content.0=sig-2"], ReplayedThinking(handler.Requests[2]));
    }

    [Fact]
    public async Task Notices_a_message_a_custom_executor_edits_in_place_not_only_one_it_replaces_PARTIAL()
    {
        var handler = new RecordingHandler(Look(1), Look(2), Done(3));
        var executor = Looking((messages, turn) =>
        {
            var next = messages.ToList();
            if (turn.StepNumber == 2)
            {
                next[0] = Copy(next[0], "Execute this test step: look twice, then conclude. [edited]");
            }

            return next;
        });

        var (error, _) = await RunAsync(handler, executor);

        Assert.Null(error);
        Assert.Equal(["messages.1.content.0=sig-1"], ReplayedThinking(handler.Requests[1]));
        Assert.Contains("[edited]", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Empty(ReplayedThinking(handler.Requests[2]));
    }

    [Fact]
    public async Task Keeps_every_reasoning_part_when_a_custom_executor_clones_the_history_each_turn_PARTIAL()
    {
        var handler = new RecordingHandler(Look(1), Look(2), Done(3));
        var executor = Looking((messages, _) => messages.Select(message => Copy(message)).ToList());

        var (error, _) = await RunAsync(handler, executor);

        Assert.Null(error);
        Assert.Equal(3, handler.Requests.Count);
        // Equal content in new objects is no edit: nothing is dropped.
        Assert.Equal(["messages.1.content.0=sig-1", "messages.3.content.0=sig-2"], ReplayedThinking(handler.Requests[2]));
    }
}
