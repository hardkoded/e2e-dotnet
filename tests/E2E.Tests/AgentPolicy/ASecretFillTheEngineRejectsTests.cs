// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentPolicy;

/// <summary>
/// The <c>describe('a secret fill the engine rejects')</c> of upstream's <c>agent-policy.test.ts</c>. Upstream runs a project and reads the
/// failure from its report; the port runs a session and reads it from the steps it recorded, the report's counterpart. Partial: the
/// <see cref="AgentException"/> the act throws carries the executor's summary as it was written, where upstream's report redacts it, so
/// that message is not checked.
/// </summary>
public sealed class ASecretFillTheEngineRejectsTests
{
    private const string Token = "tok-7Qz-rejected";

    [Fact]
    public async Task Partial_Keeps_the_value_out_of_the_failure_and_the_report_when_the_engine_fails_after_typing_it()
    {
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Roots.Add(new DocumentElement
            {
                Role = "textbox",
                Name = "Token",
                OnFill = value => throw new InvalidOperationException($"the field detached after {value} was typed"),
            });
        });
        var executor = new TestExecutor("rejected-fill", async (context, _) =>
        {
            var observation = await context.ObserveAsync();
            var failure = await Record.ExceptionAsync(() => context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Token\""), "token"));
            return Failed(failure?.ToString() ?? "no failure");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            AgentExecutor = executor,
            BaseUrl = "https://app.test",
            TestTitle = "fills a token the engine rejects",
        });
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync(
            "fill the token",
            new ActOptions { Params = new Dictionary<string, object?> { ["token"] = Secret.Create("token", Token) } }));
        session.Complete(error);

        var act = session.Steps.Single(step => step.Api == "agent.act");
        Assert.Equal(StepStatus.Failed, act.Status);
        Assert.Contains("the field detached after <secret:token> was typed", act.Error!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, JsonSerializer.Serialize(session.Steps), StringComparison.Ordinal);
    }
}
