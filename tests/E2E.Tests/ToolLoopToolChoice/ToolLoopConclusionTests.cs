// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>Tool loop conclusion: the first verdict of a turn ends the step.</summary>
public sealed class ToolLoopConclusionTests
{
    [Fact]
    public async Task Ends_the_step_on_the_first_verdict_of_a_turn_that_sends_two()
    {
        var failed = ModelResponses.Done("failed", "the total is wrong", "ASSERTION_FAILED").ToolCalls[0];
        var passed = ModelResponses.Done("passed", "All done.").ToolCalls[0];
        var model = new ScriptedModel(_ => new ModelResponse { ToolCalls = [failed, passed] });

        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(new DocumentWorld().Map("/start", page => page.Button("Peek"))),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "billing > case",
        });
        await session.App.OpenAsync("/start");

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("check the total"));
        session.Complete(error);

        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal(1, model.CallCount);
        Assert.Equal(StepStatus.Failed, session.StepRecords.Last(step => step.Api == "agent.act").Status);
    }
}
