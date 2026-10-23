// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.ToolLoopPrefix;

/// <summary>
/// What upstream's <c>tool-loop-prefix.test.ts</c> keeps at the top of the file: a page, a vocabulary, and the real Anthropic
/// client over a recording handler whose canned replies carry signed thinking blocks. The assertions are on the wire format.
/// </summary>
internal static class PrefixKit
{
    public const string Overflow = """{ "type": "error", "error": { "type": "invalid_request_error", "message": "prompt is too long: 300000 tokens > 200000 maximum" } }""";

    /// <summary>A screen whose text is far longer than the overflow clip, as a dense page is.</summary>
    public static DocumentWorld BigScreen() => new DocumentWorld().Map("/big", page =>
    {
        for (var i = 0; i < 600; i++)
        {
            page.Paragraph("row " + i + " " + new string('x', 40));
        }
    });

    public static DocumentWorld SmallScreen() => new DocumentWorld().Map("/small", page => page.Button("Peek"));

    /// <summary>Every Messages API reply of a step: a signed thinking block, then one tool call.</summary>
    public static string Reply(int n, string tool, string input = "{}") =>
        $$"""{ "content": [ { "type": "thinking", "thinking": "", "signature": "sig-{{n}}" }, { "type": "tool_use", "id": "toolu_{{n}}", "name": "{{tool}}", "input": {{input}} } ], "stop_reason": "tool_use", "usage": { "input_tokens": 10, "output_tokens": 5 } }""";

    public static string Conclude(int n) => Reply(n, "done", """{ "status": "passed", "summary": "done" }""");

    public static AnthropicModel Claude(RecordingHandler handler) =>
        new(new AnthropicModelOptions { Model = "claude-opus-5-5", ApiKey = "k" }, new HttpClient(handler));

    public static RecordingHandler Handler(params string[] replies) => new(replies);

    public static Task<E2ESession> StartAsync(DocumentWorld world, IAgentModel model, IStepExecutor? executor = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            AgentExecutor = executor,
            Model = model,
            BaseUrl = "https://prefix.test",
            TestTitle = "prefix",
            StepTimeout = TimeSpan.FromSeconds(10),
            TestTimeout = TimeSpan.FromSeconds(20),
        });

    public static StepRecord AgentStep(E2ESession session) => session.Steps.Single(step => step.Api == "agent.act");

    public static bool Noted(E2ESession session, string text) =>
        AgentStep(session).Turns!.Any(turn => turn.Outcome.Contains(text, StringComparison.Ordinal));

    public static ToolLoopTool Look() => new(
        new ModelTool { Name = "look", Description = "Looks.", Parameters = JsonDocument.Parse("""{ "type": "object", "properties": {} }""").RootElement.Clone() },
        (_, _) => Task.FromResult(new ToolLoopResult("looked")));

    /// <summary>A custom executor whose only tool is <c>look</c>, with <paramref name="prepare"/> between turns.</summary>
    public static IStepExecutor Executor(string name, Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? prepare = null) =>
        ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = name,
            Tools = (_, _) => [Look()],
            BuildPrompt = _ => Task.FromResult("Execute this test step: look twice, then conclude."),
            PrepareMessages = prepare,
        });

    public static ModelImage Png() => new([137, 80, 78, 71], "image/png", 1, 1);

    /// <summary><c>messages.&lt;i&gt;.content.&lt;j&gt;=&lt;signature&gt;</c> for every thinking block the request replays.</summary>
    public static List<string> ReplayedThinking(JsonObject body)
    {
        var replayed = new List<string>();
        var messages = body["messages"]!.AsArray();
        for (var i = 0; i < messages.Count; i++)
        {
            var content = messages[i]!["content"]!.AsArray();
            for (var j = 0; j < content.Count; j++)
            {
                if (content[j]!["type"]!.GetValue<string>() == "thinking")
                {
                    replayed.Add($"messages.{i}.content.{j}={content[j]!["signature"]!.GetValue<string>()}");
                }
            }
        }

        return replayed;
    }

    public static string FirstUserText(JsonObject body) =>
        string.Concat(body["messages"]![0]!["content"]!.AsArray().Select(block => block!["text"]?.GetValue<string>() ?? ""));
}
