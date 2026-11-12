// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopPrefix;

/// <summary>The recorded Anthropic answers and the executor that the tests of the request prefix share.</summary>
internal static class ToolLoopPrefixHelpers
{
    internal const string Overflow = """{ "type": "error", "error": { "type": "invalid_request_error", "message": "prompt is too long: 300000 tokens > 200000 maximum" } }""";

    internal static string Answer(int n, string tool, object input) =>
        new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "thinking", ["thinking"] = "", ["signature"] = "sig-" + n },
                new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_" + n, ["name"] = tool, ["input"] = JsonSerializer.SerializeToNode(input) },
            },
            ["stop_reason"] = "tool_use",
        }.ToJsonString();

    internal static string Look(int n) => Answer(n, "look", new { });

    internal static string Done(int n) => Answer(n, "done", new { status = "passed", summary = "done" });

    /// <summary><c>messages.i.content.j=signature</c> for every thinking block the request replays.</summary>
    internal static List<string> ReplayedThinking(RecordedRequest request) =>
        request.Json()["messages"]!.AsArray()
            .SelectMany((message, i) => message!["content"]!.AsArray()
                .Select((block, j) => (block, j))
                .Where(entry => entry.block!["type"]!.GetValue<string>() == "thinking")
                .Select(entry => "messages." + i + ".content." + entry.j + "=" + entry.block!["signature"]!.GetValue<string>()))
            .ToList();

    internal static IStepExecutor Looking(Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? prepare = null) =>
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

    internal static ModelMessage Copy(ModelMessage message, string? content = null) => new()
    {
        Role = message.Role,
        Content = content ?? message.Content,
        ToolCalls = message.ToolCalls,
        ToolCallId = message.ToolCallId,
        Name = message.Name,
        ReasoningItems = message.ReasoningItems,
        Images = message.Images,
    };

    /// <summary>
    /// Runs one agent step on <see cref="AnthropicModel"/> over the recorded answers. Returns the step's error and its turns,
    /// whose outcomes carry the loop's notes, as upstream's tests read them off <c>steps.all()[0].turns</c>.
    /// </summary>
    internal static async Task<(Exception? Error, IReadOnlyList<StepTurn> Turns)> RunAsync(
        RecordingHandler handler,
        IStepExecutor? executor = null,
        DocumentWorld? world = null,
        string instruction = "look twice, then conclude")
    {
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "claude-opus-5.5", ApiKey = "k" }, http);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world ?? new DocumentWorld().Map("/start", page => page.Button("Peek"))),
            Model = model,
            AgentExecutor = executor,
            BaseUrl = "https://billing.test",
            TestTitle = "prefix",
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync("/start");
            await session.Context.Agent.ActAsync(instruction);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return (error, session.StepRecords.Single(step => step.Api == "agent.act").Turns ?? []);
    }
}
