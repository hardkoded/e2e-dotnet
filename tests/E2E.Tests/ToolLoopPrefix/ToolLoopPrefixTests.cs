// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopPrefix;

/// <summary>
/// The request prefix an agent step sends turn after turn, as Anthropic reads it: it binds each thinking block to the system
/// prompt and every message before it, and rejects a replayed block whose prefix changed. The real <see cref="AnthropicModel"/>
/// runs over a recording handler that answers with signed thinking blocks, and the assertions read the wire format.
/// Not ported: "keeps the system prompt identical on every turn, the switch to auto included" (the port sends <c>auto</c> from
/// the first turn, so it has no switch), and the test of a message an executor edits in place, below the part (a
/// <see cref="ModelMessage"/> is immutable, so an executor replaces a message and cannot edit it). "stops replaying the
/// thinking from a message an elision rewrote" is partial and carries another name: the port elides screens only to shrink an
/// overflowed history, so the rewrite is that shrink.
/// </summary>
public sealed class ToolLoopPrefixTests
{
    private const string Overflow = """{ "type": "error", "error": { "type": "invalid_request_error", "message": "prompt is too long: 300000 tokens > 200000 maximum" } }""";

    private static string Answer(int n, string tool, object input) =>
        new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "thinking", ["thinking"] = "", ["signature"] = "sig-" + n },
                new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_" + n, ["name"] = tool, ["input"] = JsonSerializer.SerializeToNode(input) },
            },
            ["stop_reason"] = "tool_use",
        }.ToJsonString();

    private static string Look(int n) => Answer(n, "look", new { });

    private static string Done(int n) => Answer(n, "done", new { status = "passed", summary = "done" });

    /// <summary><c>messages.i.content.j=signature</c> for every thinking block the request replays.</summary>
    private static List<string> ReplayedThinking(RecordedRequest request) =>
        request.Json()["messages"]!.AsArray()
            .SelectMany((message, i) => message!["content"]!.AsArray()
                .Select((block, j) => (block, j))
                .Where(entry => entry.block!["type"]!.GetValue<string>() == "thinking")
                .Select(entry => "messages." + i + ".content." + entry.j + "=" + entry.block!["signature"]!.GetValue<string>()))
            .ToList();

    private static IStepExecutor Looking(Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? prepare = null) =>
        ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = "looking",
            System = _ => "Look, then conclude.",
            BuildPrompt = _ => Task.FromResult("Execute this test step: look twice, then conclude."),
            Tools = (_, _) =>
            [
                new ToolLoopTool(
                    new ModelTool { Name = "look", Description = "Looks.", Parameters = JsonDocument.Parse("""{ "type": "object", "properties": {} }""").RootElement.Clone() },
                    (_, _) => Task.FromResult(new ToolLoopResult("looked"))),
            ],
            PrepareMessages = prepare,
        });

    private static ModelMessage Copy(ModelMessage message, string? content = null) => new()
    {
        Role = message.Role,
        Content = content ?? message.Content,
        ToolCalls = message.ToolCalls,
        ToolCallId = message.ToolCallId,
        Name = message.Name,
        ReasoningItems = message.ReasoningItems,
        Images = message.Images,
    };

    private static async Task<(Exception? Error, RecordingHandler Handler)> RunAsync(RecordingHandler handler, IStepExecutor executor, DocumentWorld? world = null)
    {
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "claude-opus-5.5", ApiKey = "k" }, http);
        var attempt = await CoreTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look twice, then conclude");
            },
            world ?? new DocumentWorld().Map("/start", page => page.Button("Peek")),
            model,
            executor: executor);
        return (attempt.Error, handler);
    }

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
    public async Task Stops_replaying_every_earlier_thinking_when_an_executor_edits_the_opening_message_later()
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
    public async Task Keeps_every_reasoning_part_when_a_custom_executor_clones_the_history_each_turn()
    {
        var handler = new RecordingHandler(Look(1), Look(2), Done(3));
        var executor = Looking((messages, _) => messages.Select(message => Copy(message)).ToList());

        var (error, _) = await RunAsync(handler, executor);

        Assert.Null(error);
        Assert.Equal(3, handler.Requests.Count);
        // Equal content in new objects is no edit: nothing is dropped.
        Assert.Equal(["messages.1.content.0=sig-1", "messages.3.content.0=sig-2"], ReplayedThinking(handler.Requests[2]));
    }

    [Fact]
    public async Task Replays_every_reasoning_part_on_other_providers_after_a_rewrite_since_nothing_binds_them_to_the_prefix()
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
        Assert.Equal([0, 1, 2], requests.Select(request => request.Count(message => message.ReasoningItems is { Count: > 0 })));
    }
}
