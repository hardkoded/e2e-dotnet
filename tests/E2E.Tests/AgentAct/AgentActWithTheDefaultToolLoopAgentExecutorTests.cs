// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentActVerbs;
using E2E.Tests.CacheModelProvenance;
using E2E.Tests.ReportJson;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentAct;

[Collection(BrowserCollection.Name)]
public sealed class AgentActWithTheDefaultToolLoopAgentExecutorTests
{
    private const string Instruction = "increment the counter once and verify it shows 1";

    [Fact]
    public async Task Drives_the_step_through_the_tool_loop_to_a_passed_verdict()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            CounterPage.World(),
            IncrementThenConclude()
        );

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Offers_the_default_toolset_and_concludes_through_complete_step()
    {
        using var site = await TinySite.StartAsync(context => TinySite.RespondAsync(context, CounterSitePage));
        var model = IncrementThenConclude();
        await using var session = await GesturesSession.StartAsync(
            site,
            model,
            new FileStepCache(TempCache()),
            "default toolset"
        );
        await GesturesSession.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync(Instruction);
        });

        Assert.True(model.CallCount >= 2);
        Assert.Equal(
            [
                "back",
                "check",
                "close_tab",
                "complete_step",
                "double_tap",
                "drag",
                "drag_at",
                "hover",
                "hover_at",
                "long_press",
                "navigate",
                "observe",
                "press",
                "press_at",
                "resize_viewport",
                "right_click",
                "screenshot",
                "scroll",
                "scroll_to",
                "select",
                "select_at",
                "switch_tab",
                "tap",
                "tap_at",
                "type",
                "type_at",
                // Offered on a step without secrets too, so every step sends the same tools.
                "type_secret",
                "upload",
            ],
            model.Requests[0].Tools.Select(tool => tool.Name).Order(StringComparer.Ordinal)
        );
        var opening = model.Requests[0].Messages[0].Content!;
        Assert.Contains("increment the counter once", opening, StringComparison.Ordinal);
        Assert.Matches(@"Current screen \(revision b\d+, path /, \d+ nodes\):", opening);
        // The result reports the change since the opening screen, not the tree again.
        var result = CounterPage.ToolResults(model)[0];
        Assert.Contains("Screen changes since revision", result, StringComparison.Ordinal);
        Assert.Matches(
            @"changed #\S+ status ""Counter"" text=""1"" \(was: #\S+ status ""Counter"" text=""0""\)",
            result
        );
        Assert.DoesNotContain("button \"Increment\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accounts_executor_model_calls_in_the_step_metrics_and_provenance()
    {
        using var report = new ReportDirectory();
        ActResult? act = null;
        ActResult? replayed = null;
        var directory = TempCache();
        var model = new NamedModel(
            new ScriptedModel(request => new ModelResponse
            {
                ToolCalls =
                    request.Messages.Any(message =>
                        message.Content?.Contains("Statement:", StringComparison.Ordinal) == true
                    ) || request.Messages.Any(message => message.Role == "tool")
                        ? ModelResponses.Done("passed", "upgraded").ToolCalls
                        : ModelResponses.Tap("button", "Upgrade to Pro").ToolCalls,
                Usage = new ModelCallUsage { InputTokens = 11, OutputTokens = 3 },
            }),
            "fake-loop/scripted-loop"
        );
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/settings/billing");
                act = await ctx.Agent.ActAsync("upgrade the workspace to the Pro plan");
                await ctx.Agent.AssertAsync("the invoice preview shows a prorated amount");
            },
            model: model,
            cacheDirectory: directory,
            report: report
        );
        var second = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/settings/billing");
                replayed = await ctx.Agent.ActAsync("upgrade the workspace to the Pro plan");
                await ctx.Agent.AssertAsync("the invoice preview shows a prorated amount");
            },
            model: model,
            cacheDirectory: directory
        );

        Assert.Null(result.Error);
        Assert.Null(second.Error);
        Assert.Equal(0, replayed!.ModelCalls);
        Assert.Equal(1, replayed.Actions);
        Assert.True(act!.ModelCalls >= 2);
        Assert.Equal(1, act.Actions);
        // The session also counts the one judgment of the assertion.
        Assert.Equal(act.ModelCalls + 1, result.ModelCalls);
        Assert.Equal(11 * result.ModelCalls, result.InputTokens);
        Assert.Equal(3 * result.ModelCalls, result.OutputTokens);
        var step = report.Step("agent.act");
        var calls = (int)step["metrics"]!["modelCalls"]!;
        Assert.True(calls >= 2);
        Assert.Equal(1, (int)step["metrics"]!["actionSteps"]!);
        Assert.Equal(
            ("fake-loop", "scripted-loop", "provider", calls),
            (
                (string)step["model"]!["provider"]!,
                (string)step["model"]!["model"]!,
                (string)step["model"]!["tokenAccounting"]!,
                (int)step["model"]!["calls"]!
            )
        );
        Assert.True((int)step["model"]!["inputTokens"]! > 0);
        Assert.Equal(calls, step["events"]!.AsArray().Count(each => (string)each!["kind"]! == "model"));
    }

    private const string CounterSitePage = """
        <!doctype html>
        <html>
        <head><title>Counter</title></head>
        <body>
          <button onclick="const c = document.getElementById('count'); c.textContent = String(Number(c.textContent) + 1)">Increment</button>
          <p id="count" role="status" aria-label="Counter">0</p>
        </body>
        </html>
        """;

    private static ScriptedModel IncrementThenConclude() => new(IncrementThenConcludeTurn);

    private static ModelResponse IncrementThenConcludeTurn(ModelRequest request)
    {
        return request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "tapped Increment and the counter shows 1")
            : ModelResponses.Tap("button", "Increment");
    }
}
