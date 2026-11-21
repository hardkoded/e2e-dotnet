// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentGrammarTools;

/// <summary>
/// The <c>describe('observe matches the act rules on checking a commit')</c> of upstream's <c>agent-grammar-tools.test.ts</c>
/// (tester-army/e2e#1993). Upstream builds the tools from a fake executor context; the port reads the description the model is offered.
/// </summary>
public sealed class ObserveMatchesTheActRulesOnCheckingACommitTests
{
    [Fact]
    public async Task Allows_observe_to_check_any_press_that_commits_something_not_only_a_save_or_submit()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));

        await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/settings/billing");
                await ctx.Agent.ActAsync("look");
            },
            model: model
        );

        var description = model.Requests[0].Tools.First(tool => tool.Name == "observe").Description;
        Assert.Contains(
            "or to check whether a press that commits something (a save, a submit, a delete) went through",
            description,
            StringComparison.Ordinal
        );
        Assert.Contains("never to re-read another action's result", description, StringComparison.Ordinal);
    }
}
