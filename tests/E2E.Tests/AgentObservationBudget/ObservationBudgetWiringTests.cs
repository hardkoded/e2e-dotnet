// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.Tests.AgentObservationBudget;

/// <summary>
/// .NET-only: no upstream <c>it</c> covers where the budget is applied (upstream tests <c>observationByteBudget</c> as a function). These
/// check the call sites: a judgment and an act cut the screen to the agent's limits, and a judgment over <c>MaxInputTokens</c> is refused
/// before it is sent.
/// </summary>
public sealed class ObservationBudgetWiringTests
{
    [Fact]
    public async Task A_judgment_cuts_a_screen_over_the_agents_MaxObservationBytes_and_marks_the_step_truncated()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));
        await using var session = await StartAsync(judge: judge, maxObservationBytes: 1_024);

        await session.Context.App.OpenAsync("/");
        await session.Context.Agent.AssertAsync("a button is on the page");
        session.Complete(null);

        var message = judge.Requests[0].Messages[0].Content!;
        Assert.Contains("Button number 0", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Button number 299", message, StringComparison.Ordinal);
        Assert.Contains(SnapshotText.TruncationMarker, message, StringComparison.Ordinal);
        Assert.True(session.StepRecords.Single(step => step.Api == "agent.assert").ObservationTruncated);
    }

    [Fact]
    public async Task A_judgment_over_the_agents_MaxInputTokens_fails_before_the_model_is_called()
    {
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));
        await using var session = await StartAsync(judge: judge, maxInputTokens: 1);

        await session.Context.App.OpenAsync("/");
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Context.Agent.AssertAsync("a button is on the page"));
        session.Complete(error);

        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.Contains("exceeds the agent's maxInputTokens 1", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, judge.CallCount);
    }

    [Fact]
    public async Task An_act_cuts_the_screen_the_model_reads_to_the_agents_MaxObservationBytes()
    {
        var actor = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(actor: actor, maxObservationBytes: 1_024);

        await session.Context.App.OpenAsync("/");
        await session.Context.Agent.ActAsync("look at the page");
        session.Complete(null);

        var text = string.Join('\n', actor.Requests[0].Messages.Select(message => message.Content));
        Assert.Contains(SnapshotText.TruncationMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Button number 299", text, StringComparison.Ordinal);
        Assert.True(session.StepRecords.Single(step => step.Api == "agent.act").ObservationTruncated);
    }

    private static async Task<E2ESession> StartAsync(ScriptedModel? actor = null, ScriptedModel? judge = null, int? maxObservationBytes = null, int? maxInputTokens = null) =>
        await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(World()),
            Model = actor,
            Judge = judge,
            MaxObservationBytes = maxObservationBytes ?? E2EDefaults.MaxObservationBytes,
            MaxInputTokens = maxInputTokens ?? E2EDefaults.MaxInputTokens,
            BaseUrl = "https://app.test",
            TestTitle = "observation budget",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });

    private static DocumentWorld World() => new DocumentWorld().Map("/", page =>
    {
        for (var index = 0; index < 300; index++)
        {
            page.Button("Button number " + index.ToString(CultureInfo.InvariantCulture));
        }
    });
}
