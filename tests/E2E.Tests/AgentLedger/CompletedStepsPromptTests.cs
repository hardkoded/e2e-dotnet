// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentLedger;

/// <summary>
/// .NET-only: the ledger reaches the model through the prompt of the next act. No upstream <c>it</c> covers that wiring, because
/// upstream's tests read <c>serializeLedger</c> alone.
/// </summary>
public sealed class CompletedStepsPromptTests
{
    [Fact]
    public async Task Lists_every_completed_step_of_the_attempt_in_the_order_it_ran()
    {
        var summaries = new[] { "opened the billing page", "chose the Pro plan" };
        var calls = 0;
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", calls < summaries.Length ? summaries[calls++] : "paid"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "ledger order",
        });
        await session.App.OpenAsync("/settings/billing");

        await session.Agent.ActAsync("open billing");
        await session.Agent.ActAsync("choose the plan");
        await session.Agent.ActAsync("pay");

        var opening = model.Requests[2].Messages[0].Content;
        Assert.Contains(
            "Completed steps:\n1. app.open passed :: /settings/billing\n2. agent.act passed :: open billing\n   observed: opened the billing page\n3. agent.act passed :: choose the plan\n   observed: chose the Pro plan\n",
            opening,
            StringComparison.Ordinal);
        session.Complete();
    }

    [Fact]
    public async Task Tells_the_next_step_that_a_failed_step_failed()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("failed", "the plan list never loaded"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "ledger failure",
        });
        await session.App.OpenAsync("/settings/billing");
        await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("open billing"));

        await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("choose the plan"));

        Assert.Contains(
            "2. agent.act failed :: open billing\n   observed: the plan list never loaded\n",
            model.Requests[1].Messages[0].Content,
            StringComparison.Ordinal);
        session.Complete();
    }
}
