// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentPolicy;

public sealed class AgentPolicyAndErrorClassificationTests
{
    // Upstream also checks the error category, which only its report carries.
    [Fact]
    public async Task Reports_an_unconfigured_credential_with_AUTH_CREDENTIAL_UNAVAILABLE()
    {
        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/");
            await ctx.Screen.GetByLabel("Email").FillAsync(Credentials.User("ghost").Password);
        }, CounterPage.World());

        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", Assert.IsType<TestException>(result.Error).Code);
    }

    [Fact]
    public async Task Spends_one_repair_call_to_satisfy_the_caller_schema()
    {
        // The first answer is a bare value. The repair round wraps it.
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return ModelResponses.Call("extract", new { data = text.Contains("<previous-attempt-rejected>", StringComparison.Ordinal) ? (object)new { counter = "0" } : "0" });
        });
        CounterReading? reading = null;
        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/");
            reading = await ctx.Agent.ExtractAsync<CounterReading>("the counter value");
        }, CounterPage.World(), model);

        Assert.Null(result.Error);
        Assert.Equal("0", reading?.Counter);
        Assert.Equal(2, model.CallCount);
        Assert.Equal(2, result.ModelCalls);
    }

    /// <summary>Partial: the port has no step record, so the model that judged is read from the model itself, which answered once.</summary>
    [Fact]
    public async Task Partial_Judges_with_the_model_a_custom_executor_brought_with_no_entry_model()
    {
        var brought = new ScriptedModel(_ => ModelResponses.Call("extract", new { data = new { counter = "0" } }));
        var executor = new TestExecutor("house-brain", (_, _) => Task.FromResult(Passed("unused"))) { Model = brought };
        CounterReading? reading = null;

        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/");
            reading = await ctx.Agent.ExtractAsync<CounterReading>("the counter value");
        }, CounterPage.World(), executor: executor);

        Assert.Null(result.Error);
        Assert.Equal("0", reading?.Counter);
        Assert.Equal(1, brought.CallCount);
    }

    // Partial: upstream runs this on a real model and shares one run with its extract test. Here a scripted model answers, so this checks that the request builder never carries the value.
    [Fact]
    public async Task Partial_Never_sends_a_secret_value_to_the_model()
    {
        // Upstream's testbed /echo page: the field puts what it is given on the page.
        string? filled = null;
        var world = new DocumentWorld().Map("/echo", page =>
        {
            var echo = page.Status("Token echo");
            echo.Text = "nothing yet";
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Token", Value = "", Secure = true, InputPurpose = "password", OnFill = value => { filled = value; echo.Text = value; } });
        });
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "the field has a value"));
        var result = await RunAsync(async ctx =>
        {
            await ctx.App.OpenAsync("/echo");
            await ctx.Screen.GetByLabel("Token").FillAsync(Secret.Create("member.password", "hunter2-secret", "password"));
            await ctx.Agent.AssertAsync("the token echo shows a value");
        }, world, model);

        Assert.Null(result.Error);
        Assert.Equal("hunter2-secret", filled);
        var sent = model.Requests.Select(request => System.Text.Json.JsonSerializer.Serialize(request, System.Text.Json.JsonSerializerOptions.Web)).ToList();
        var judged = sent.Where(text => text.Contains("the token echo shows a value", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(judged);
        Assert.All(judged, text =>
        {
            Assert.Contains("Token echo", text, StringComparison.Ordinal);
            Assert.Contains("secret:member.password", text, StringComparison.Ordinal);
        });
        Assert.All(sent, text => Assert.DoesNotContain("hunter2-secret", text, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CounterReading
    {
        public required string Counter { get; init; }
    }
}
