// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using static E2E.Tests.ToolLoopPrefix.PrefixKit;

namespace E2E.Tests.ToolLoopPrefix;

/// <summary>
/// The request prefix a step sends, turn after turn, as Anthropic's API reads it: Claude Opus 5.5, Sonnet 5.5, and Fable 5.1
/// bind each thinking block to the system prompt and every message before it, and reject a replayed block whose prefix
/// changed. The real <see cref="AnthropicModel"/> runs over a recording handler: no key, no network, and the assertions are on
/// the wire format.
/// <para>
/// The port has no forced tool choice, so the "switch to auto" half of the first test has no counterpart, and it has no
/// per-turn screen compaction: only a shrink after a refusal elides a screen, so the second test drives the rewrite through one.
/// A <see cref="ModelMessage"/> is immutable but for the bytes of its images, so "edits a tool result in place, below the part"
/// replaces the message instead.
/// </para>
/// </summary>
public sealed class TheRequestPrefixOfAnAgentStepOnAnthropicTests
{
    /// <summary>Partial port of "keeps the system prompt identical on every turn, the switch to auto included": the port always asks with <c>auto</c>.</summary>
    [Fact]
    public async Task Keeps_the_system_prompt_identical_on_every_turn()
    {
        var handler = Handler(Reply(1, "look"), Reply(2, "look"), Reply(3, "look"), Conclude(4));
        using var model = Claude(handler);
        await using var session = await StartAsync(SmallScreen(), model, Executor("fixed"));

        await session.Agent.ActAsync("look around, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        var requests = handler.Requests.Select(request => request.Json()).ToList();
        Assert.Equal(4, requests.Count);
        Assert.Equal(["auto", "auto", "auto", "auto"], requests.Select(body => body["tool_choice"]!["type"]!.GetValue<string>()));
        Assert.Equal(Enumerable.Repeat(requests[0]["system"]!.ToJsonString(), 4), requests.Select(body => body["system"]!.ToJsonString()));
        Assert.Equal(Enumerable.Repeat("look,done", 4), requests.Select(body => string.Join(',', body["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>()))));
        // Nothing before any block changed, so every block is replayed.
        Assert.Equal(["messages.1.content.0=sig-1", "messages.3.content.0=sig-2", "messages.5.content.0=sig-3"], ReplayedThinking(requests[3]));
    }

    /// <summary>Partial port of "stops replaying the thinking from a message an elision rewrote, and keeps the thinking produced after it": the elision is a shrink after a refusal, as the port has no per-turn compaction.</summary>
    [Fact]
    public async Task Stops_replaying_the_thinking_from_a_message_a_shrink_rewrote_and_keeps_the_thinking_produced_after_it()
    {
        var handler = Handler(Reply(1, "observe"), Reply(2, "observe")).Then(HttpStatusCode.BadRequest, Overflow).Then(HttpStatusCode.OK, Reply(4, "observe")).Then(HttpStatusCode.OK, Conclude(5));
        using var model = Claude(handler);
        await using var session = await StartAsync(BigScreen(), model);

        await session.App.OpenAsync("/big");
        await session.Agent.ActAsync("look around twice, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        var requests = handler.Requests.Select(request => request.Json()).ToList();
        Assert.Equal(5, requests.Count);
        Assert.Contains("Screen (", FirstUserText(requests[2]), StringComparison.Ordinal);
        // The retried request elided the opening screen at messages.0: the blocks produced under the whole screen are not sent again.
        Assert.Contains("[earlier screen elided", FirstUserText(requests[3]), StringComparison.Ordinal);
        Assert.Empty(ReplayedThinking(requests[3]));
        // The next request leaves messages.0 as the retry sent it, so the block produced under the elided screen is valid and goes out; the first two are still gone.
        Assert.Equal(FirstUserText(requests[3]), FirstUserText(requests[4]));
        Assert.Equal(["messages.5.content.0=sig-4"], ReplayedThinking(requests[4]));
        Assert.True(Noted(session, "message 1 was rewritten, so the reasoning after it is not sent again"));
    }

    /// <summary>Partial port of "notices a message a custom executor edits in place, not only one it replaces": the bytes of an image are all a message lets an executor edit through the object it was handed.</summary>
    [Fact]
    public async Task Notices_the_bytes_of_an_image_a_custom_executor_edits_in_place_not_only_a_message_it_replaces()
    {
        var handler = Handler(Reply(1, "look"), Reply(2, "look"), Conclude(3));
        using var model = Claude(handler);
        var image = Png();
        var executor = Executor("editing", (messages, turn) =>
        {
            var next = messages.ToList();
            if (turn.StepNumber == 0)
            {
                next[0] = new ModelMessage { Role = "user", Content = next[0].Content, Images = [image] };
            }

            // Before the third turn, the image's bytes are edited through the array the loop handed over, and the same list comes back.
            if (turn.StepNumber == 2)
            {
                image.Data[0] = 0;
            }

            return next;
        });
        await using var session = await StartAsync(SmallScreen(), model, executor);

        await session.Agent.ActAsync("look twice, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        var requests = handler.Requests.Select(request => request.Json()).ToList();
        Assert.Equal(3, requests.Count);
        Assert.Equal(["messages.1.content.0=sig-1"], ReplayedThinking(requests[1]));
        Assert.NotEqual(requests[1]["messages"]![0]!.ToJsonString(), requests[2]["messages"]![0]!.ToJsonString());
        Assert.Empty(ReplayedThinking(requests[2]));
    }

    /// <summary>Partial port of "notices a tool result a custom executor edits in place, below the part": a tool result has no part to edit, so the executor replaces the message.</summary>
    [Fact]
    public async Task Notices_a_tool_result_a_custom_executor_replaces()
    {
        var handler = Handler(Reply(1, "look"), Reply(2, "look"), Conclude(3));
        using var model = Claude(handler);
        var executor = Executor("editing", (messages, turn) =>
        {
            var next = messages.ToList();
            // Before the third turn, the first tool result is replaced by one with another output.
            if (turn.StepNumber == 2)
            {
                var result = next[2];
                next[2] = new ModelMessage { Role = result.Role, ToolCallId = result.ToolCallId, Name = result.Name, Content = "looked [edited]" };
            }

            return next;
        });
        await using var session = await StartAsync(SmallScreen(), model, executor);

        await session.Agent.ActAsync("look twice, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        var requests = handler.Requests.Select(request => request.Json()).ToList();
        Assert.Equal(3, requests.Count);
        Assert.Contains("looked [edited]", requests[2]["messages"]![2]!.ToJsonString(), StringComparison.Ordinal);
        // The edit sits at messages.2, so the block before it stays and the one after it goes.
        Assert.Equal(["messages.1.content.0=sig-1"], ReplayedThinking(requests[2]));
    }

    /// <summary>Partial port of "keeps every reasoning part when a custom executor clones the history each turn": the port has no structured clone, so the executor copies every message and its image bytes by hand.</summary>
    [Fact]
    public async Task Keeps_every_reasoning_part_when_a_custom_executor_clones_the_history_each_turn()
    {
        var handler = Handler(Reply(1, "look"), Reply(2, "look"), Conclude(3));
        using var model = Claude(handler);
        var executor = Executor("cloning", (messages, turn) => messages
            .Select((message, index) => new ModelMessage
            {
                Role = message.Role,
                // The opening message carries bytes too, which a clone copies into new arrays.
                Content = message.Content,
                ToolCallId = message.ToolCallId,
                Name = message.Name,
                ToolCalls = message.ToolCalls?.Select(call => new ModelToolCall { Id = call.Id, Name = call.Name, Arguments = call.Arguments.Clone() }).ToList(),
                ReasoningItems = message.ReasoningItems?.Select(item => item.Clone()).ToList(),
                Images = turn.StepNumber == 0 && index == 0 ? [Png()] : message.Images?.Select(image => image with { Data = [.. image.Data] }).ToList(),
            })
            .ToList());
        await using var session = await StartAsync(SmallScreen(), model, executor);

        await session.Agent.ActAsync("look twice, then conclude");

        Assert.Equal(StepStatus.Passed, AgentStep(session).Status);
        var requests = handler.Requests.Select(request => request.Json()).ToList();
        Assert.Equal(3, requests.Count);
        // Equal content in new objects is no edit: nothing is dropped.
        Assert.Equal(["messages.1.content.0=sig-1", "messages.3.content.0=sig-2"], ReplayedThinking(requests[2]));
        Assert.False(Noted(session, "was rewritten"));
    }
}
