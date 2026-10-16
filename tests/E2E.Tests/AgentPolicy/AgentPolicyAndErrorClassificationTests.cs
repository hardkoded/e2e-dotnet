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

    /// <summary>Partial: the port has no step artifacts or policy events, so it reads the file the judgment would have kept and the exception.</summary>
    [Fact]
    public async Task Suppresses_screenshot_evidence_once_the_session_is_pixel_tainted()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var world = new DocumentWorld().Map("/login", page => page.Textbox("Password", "", secure: true));
        var calls = 0;
        var actor = new ScriptedModel(_ => calls++ == 0
            ? ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "password" })
            : ModelResponses.Done("passed", "filled the password"));
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the password field is empty"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ScreenshotEngine(world, _ => ScreenshotEngine.Png()),
            Model = actor,
            Judge = judge,
            BaseUrl = "https://app.test",
            ProjectRoot = root,
            TestTitle = "fills a secret into a password field",
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        await session.App.OpenAsync("/login");
        await session.Agent.ActAsync("sign in", new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("password", "hunter2-secret", "password") } });
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the password field has a value"));
        session.Complete(error);

        Assert.Null(error.Screenshot);
        Assert.False(Directory.Exists(Path.Combine(root, ".e2e")));
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

    private sealed class CounterReading
    {
        public required string Counter { get; init; }
    }
}
