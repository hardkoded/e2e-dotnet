// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentModelRefusal;

/// <summary>
/// Upstream's <c>agent-model-refusal.test.ts</c>. The port's model is an <see cref="IAgentModel"/> that answers with tool calls, not an AI SDK
/// language model, so a refused turn is a response that finished with <see cref="ModelResponse.ContentFilter"/> and called no tool. Upstream's
/// <c>structured</c> flag, the SDK's two output modes, has no port: <c>assert</c> and <c>extract</c> are the two judgment calls here. The port's
/// model names no provider, so the message names the model alone.
/// </summary>
public sealed class ModelRefusalTests
{
    private static ModelResponse Refusal() => new() { FinishReason = ModelResponse.ContentFilter, RawFinishReason = "refusal" };

    private static async Task<E2ESession> StartAsync(IAgentModel model) =>
        await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(new DocumentWorld().Map("/", page => page.Paragraph("page content"))),
            Model = model,
            BaseUrl = "https://todos.test",
            TestTitle = "refusal",
            CacheEnabled = false,
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fails_a_refused_call_as_MODEL_REFUSED(bool structured)
    {
        var model = new ScriptedModel(_ => Refusal());
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAsync<AgentException>(() => structured
            ? session.Agent.ExtractAsync<Token>("extract target token")
            : session.Agent.AssertAsync("the page shows the target token"));
        session.Complete(error);

        Assert.Equal("MODEL_REFUSED", error.Code);
        Assert.Equal("the model scripted refused the request (refusal)", error.Message);
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task Keeps_a_usable_answer_that_finished_with_content_filter()
    {
        var model = new ScriptedModel(_ => new ModelResponse
        {
            ToolCalls = ModelResponses.Call("extract", new { data = new { found = true } }).ToolCalls,
            FinishReason = ModelResponse.ContentFilter,
            RawFinishReason = "refusal",
        });
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var token = await session.Agent.ExtractAsync<Token>("extract target token");
        session.Complete(null);

        Assert.True(token.Found);
    }

    [Fact]
    public async Task Fails_agent_act_as_MODEL_REFUSED()
    {
        var model = new ScriptedModel(_ => Refusal());
        await using var session = await StartAsync(model);
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("probe internal details"));
        session.Complete(error);

        Assert.Equal("MODEL_REFUSED", error.Code);
        Assert.Equal(1, model.CallCount);
        var step = session.StepRecords.Last(candidate => candidate.Api == "agent.act");
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.Equal("MODEL_REFUSED", step.Error?.Code);
    }

    private sealed record Token(bool Found);
}
