// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentActPixelTools;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentGrammarTools;

/// <summary>
/// The <c>it</c>s outside any <c>describe</c> of upstream's <c>agent-grammar-tools.test.ts</c> that tester-army/e2e#1568 added. The port's verdict
/// tool is <c>done</c>, with a <c>code</c> where upstream's <c>complete_step</c> has an <c>errorCode</c>, and its project tools are
/// <see cref="AgentTool"/>s, which <see cref="AgentTools.CreateGrammarTools"/> offers with the grammar, not a separate <c>projectTools</c>.
/// </summary>
public sealed class AgentGrammarToolsTests
{
    private static JsonElement Call(object arguments) => JsonSerializer.SerializeToElement(arguments);

    private static async Task<(Exception? Error, PixelsEngine Engine)> AssertAsync(DocumentWorld world, AssertOptions options, IAgentModel model, IReadOnlyDictionary<string, AgentTool>? tools = null, string path = "/")
    {
        var engine = new PixelsEngine(new DocumentEngine(world));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            Model = model,
            AgentTools = tools,
            BaseUrl = "https://billing.test",
            TestTitle = "grammar",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
        });
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync(path);
            await session.Context.Agent.AssertAsync("the screen shows the content", options);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return (error, engine);
    }

    [Fact]
    public async Task Does_not_repeat_a_static_assertion_opening_to_request_pixels_it_cannot_use()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "read static content"));
        var world = new DocumentWorld().Map("/", page => page.Status("Static content"));
        var (error, engine) = await AssertAsync(world, new AssertOptions { Explore = true, Screenshot = false }, model);

        Assert.Null(error);
        // Every look asked for the tree alone: no pixels, no pixel fallback, and (with the evidence screenshot off) none taken.
        Assert.NotEmpty(engine.Looks);
        Assert.All(engine.Looks, look => Assert.True(look is not { PixelFallback: true } and not { Pixels: true }));
        Assert.Equal(0, engine.Shots);
    }

    [Fact]
    public async Task Offers_only_viewport_exploration_tools_to_assertion_steps()
    {
        IReadOnlyList<string> offered = [];
        var executor = new TestExecutor("offers", (context, _) =>
        {
            offered = [.. AgentTools.CreateGrammarTools(context).Select(tool => tool.Definition.Name).Order(StringComparer.Ordinal)];
            return Task.FromResult(Passed("listed"));
        })
        {
            Vision = true,
        };
        var engine = new PixelsEngine(new DocumentEngine(CounterPage.World()));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = engine, AgentExecutor = executor, BaseUrl = "https://billing.test", TestTitle = "grammar" });
        await session.Context.App.OpenAsync("/");
        await session.Context.Agent.AssertAsync("the counter shows zero", new AssertOptions { Explore = true, Vision = VisionMode.On });

        Assert.Equal(["observe", "screenshot", "scroll", "scroll_to"], offered);
    }

    [Fact]
    public async Task Offers_read_only_project_tools_alongside_assertion_exploration()
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        var tools = new Dictionary<string, AgentTool>
        {
            ["read_status"] = new AgentTool("reads", schema, mutates: false, (_, _) => Task.FromResult("read")),
            ["write_status"] = new AgentTool("writes", schema, mutates: true, (_, _) => Task.FromResult("wrote")),
        };
        var asserting = new ScriptedModel(_ => ModelResponses.Done("passed", "read the status"));
        var (assertError, _) = await AssertAsync(CounterPage.World(), new AssertOptions { Explore = true }, asserting, tools);
        var acting = new ScriptedModel(_ => ModelResponses.Done("passed", "did the step"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions { Engine = new DocumentEngine(CounterPage.World()), Model = acting, AgentTools = tools, BaseUrl = "https://billing.test", TestTitle = "grammar" });
        await session.Context.App.OpenAsync("/");
        await session.Context.Agent.ActAsync("update the status");

        Assert.Null(assertError);
        var assertTools = asserting.Requests[0].Tools.Select(tool => tool.Name).Where(name => name.EndsWith("_status", StringComparison.Ordinal));
        Assert.Equal(["read_status"], assertTools);
        var actTools = acting.Requests[0].Tools.Select(tool => tool.Name).Where(name => name.EndsWith("_status", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
        Assert.Equal(["read_status", "write_status"], actTools);
    }

    [Fact]
    public void Drops_an_inconclusive_code_from_an_act_verdict_and_keeps_it_on_an_assertion()
    {
        var arguments = Call(new { status = "failed", summary = "no evidence", code = "ASSERTION_INCONCLUSIVE" });
        var call = new ModelToolCall { Id = "verdict", Name = VerdictTool.Name, Arguments = arguments };

        var act = AgentTools.CreateVerdictTool(ExecutorStepKind.Act);
        Assert.Contains("ASSERTION_INCONCLUSIVE", act.Tool.Parameters.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(
            "Step concluded as failed; the code ASSERTION_INCONCLUSIVE you sent applies only to assertions and was ignored.",
            act.Execute(call).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Failed, "no evidence"), act.Verdict);

        var assertion = AgentTools.CreateVerdictTool(ExecutorStepKind.Assert);
        Assert.Equal("Step concluded.", assertion.Execute(call).Content);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Failed, "no evidence", "ASSERTION_INCONCLUSIVE"), assertion.Verdict);
    }
}
