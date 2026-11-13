// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentPolicy;

/// <summary>The <c>describe('tool output sent to the model')</c> of upstream's <c>agent-policy.test.ts</c>.</summary>
public sealed class ToolOutputSentToTheModelTests
{
    private const string Token = "synthetic-model-token-314159";

    private static readonly JsonObject EmptySchema = new() { ["type"] = "object", ["properties"] = new JsonObject() };

    /// <summary>
    /// Partial: a port tool answers text, never a structured value, a dictionary, a serialized object, or a <c>toModelOutput</c>, so the
    /// structured, key, dictionary, serialized, and custom-output tools of the upstream <c>it</c> have no port. Upstream also reads the
    /// secret from the config; here it is a param of the act.
    /// </summary>
    [Fact]
    public async Task Partial_Redacts_strings_structured_fields_and_keys_tool_errors_and_engine_errors()
    {
        var world = new DocumentWorld().Map("/", page => page.Button("Go", () => throw new InvalidOperationException($"engine rejected {Token}")));
        var turn = 0;
        var model = new ScriptedModel(request =>
        {
            Assert.DoesNotContain(Token, JsonSerializer.Serialize(request, JsonSerializerOptions.Web), StringComparison.Ordinal);
            if (turn++ > 0)
            {
                return ModelResponses.Done("passed", "checked");
            }

            return new ModelResponse
            {
                ToolCalls =
                [
                    Call("lookup", new { }),
                    Call("bounded", new { }),
                    Call("boom", new { }),
                    Call("tap", new { role = "button", name = "Go" }),
                ],
            };
        });
        var tools = new Dictionary<string, AgentTool>
        {
            ["lookup"] = new AgentTool("lookup", EmptySchema, false, (_, _) => Task.FromResult($"row {Token}")),
            ["bounded"] = new AgentTool("bounded", EmptySchema, false, (_, _) => Task.FromResult(new string('x', 16120) + Token + new string('x', 300))),
            ["boom"] = new AgentTool("boom", EmptySchema, false, (_, _) => throw new InvalidOperationException($"tool rejected {Token}")),
        };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("try the tools", new ActOptions { Params = new Dictionary<string, object?> { ["key"] = Secret.Create("key", Token) } });
            },
            world,
            model,
            agentTools: tools);

        Assert.Null(result.Error);
        Assert.Equal(2, model.CallCount);
        var outputs = model.Requests[1].Messages.Where(message => message.Role == "tool").Select(message => message.Content ?? "").ToList();
        var bounded = outputs[1];
        Assert.Matches("^x+", bounded);
        Assert.Contains("<secret:", bounded, StringComparison.Ordinal);
        Assert.Contains("[tool output truncated:", bounded, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(bounded) <= 16384);
        var results = string.Join('\n', outputs);
        Assert.Contains("row <secret:key>", results, StringComparison.Ordinal);
        Assert.Contains("tool rejected <secret:key>", results, StringComparison.Ordinal);
        Assert.Contains("engine rejected <secret:key>", results, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, results, StringComparison.Ordinal);
        Assert.DoesNotContain(Token[..8], results, StringComparison.Ordinal);
    }

    private static ModelToolCall Call(string name, object arguments) =>
        new() { Id = "call_" + name, Name = name, Arguments = JsonSerializer.SerializeToElement(arguments) };
}
