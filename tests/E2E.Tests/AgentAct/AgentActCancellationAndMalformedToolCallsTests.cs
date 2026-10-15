// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentAct;

// .NET-only: upstream has no per-call cancellation token, and takes malformed tool input from the AI SDK.
public sealed class AgentActCancellationAndMalformedToolCallsTests
{
    [Fact]
    public async Task Surfaces_a_caller_cancellation_as_cancellation_not_as_a_step_timeout()
    {
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromMilliseconds(300));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("do something eventually", cancellationToken: cancel.Token);
            },
            CounterPage.World(),
            new HangingModel());

        Assert.IsAssignableFrom<OperationCanceledException>(result.Error);
    }

    [Fact]
    public async Task Marks_the_newest_message_for_caching_on_every_act_turn()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("passed", "clicked")
                : ModelResponses.Tap("button", "Increment"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("click once");
            },
            CounterPage.World(),
            model);

        Assert.Null(result.Error);
        Assert.True(model.Requests.Count >= 2);
        Assert.All(model.Requests, request => Assert.True(request.MarkLatest));
    }

    [Theory]
    [InlineData("{\"url\":\"/trunc")]
    [InlineData("[]")]
    public async Task Hands_a_tool_call_with_unusable_arguments_back_to_the_model_as_a_failed_tool(string text)
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("passed", "recovered")
                : new ModelResponse
                {
                    ToolCalls = [new ModelToolCall { Id = "call_1", Name = "navigate", Arguments = ModelHttp.Arguments(text) }],
                });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("go somewhere");
            },
            CounterPage.World(),
            model);

        Assert.Null(result.Error);
        Assert.Contains("navigate takes a JSON object of arguments.", CounterPage.ToolResults(model)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_arguments_that_are_not_valid_json_as_a_string_of_that_text()
    {
        var arguments = ModelHttp.Arguments("{\"url\":");

        Assert.Equal(JsonValueKind.String, arguments.ValueKind);
        Assert.Equal("{\"url\":", arguments.GetString());
    }

    [Fact]
    public void Sends_the_text_of_unparsed_arguments_back_as_it_came()
    {
        Assert.Equal("{\"url\":", ModelHttp.ArgumentsText(ModelHttp.Arguments("{\"url\":")));
        Assert.Equal("{\"url\":\"/\"}", ModelHttp.ArgumentsText(ModelHttp.Arguments("{\"url\":\"/\"}")));
    }

    [Fact]
    public void Links_a_caller_token_with_the_session_token_only_when_both_can_cancel()
    {
        using var caller = new CancellationTokenSource();
        using var session = new CancellationTokenSource();

        Assert.Equal(session.Token, E2E.Internal.Time.Link(default, session.Token));
        Assert.Equal(session.Token, E2E.Internal.Time.Link(session.Token, session.Token));
        Assert.Equal(caller.Token, E2E.Internal.Time.Link(caller.Token, default));
        var linked = E2E.Internal.Time.Link(caller.Token, session.Token);
        session.Cancel();
        Assert.True(linked.IsCancellationRequested);
    }

    [Fact]
    public async Task Repairs_an_extract_call_whose_arguments_are_not_an_object()
    {
        var model = new ScriptedModel(_ => new ModelResponse
        {
            ToolCalls = [new ModelToolCall { Id = "call_1", Name = "extract", Arguments = ModelHttp.Arguments("[]") }],
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ExtractAsync<Reading>("read the count");
            },
            CounterPage.World(),
            model);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.Equal(2, model.CallCount);
    }

    private sealed record Reading(string Count);

    private sealed class HangingModel : IAgentModel
    {
        public string Name => "hanging";

        public async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ModelResponse();
        }
    }
}
