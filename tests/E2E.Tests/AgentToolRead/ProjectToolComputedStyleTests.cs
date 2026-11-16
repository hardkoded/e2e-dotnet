// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.AgentToolRead;

/// <summary>
/// Upstream's <c>describe('project tool computed style')</c> (<c>packages/e2e/tests/integration/agent-tool-read.test.ts</c>, tester-army/e2e#1633):
/// project-tool reads cross the engine and the secret boundary. Upstream builds the runner and runs a project; the port runs one session
/// on the web engine over the fixture app with a scripted model, as its other web tests do.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ProjectToolComputedStyleTests
{
    [Fact]
    public async Task Measures_styles_without_pixels_and_keeps_secret_values_out_of_model_input_and_reports()
    {
        const string sentinel = "0.314159";
        using var site = await FixtureApp.StartAsync();
        ExecutorReadResult? measured = null;
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0
            ? ModelResponses.Call("inspect", new { })
            : ModelResponses.Done("passed", "measured the error style"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["inspect"] = new AgentTool("measures the error button", new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }, false, async (_, context, token) =>
            {
                var screen = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true }, token);
                var line = screen.Text.Split('\n').FirstOrDefault(entry => entry.Contains("button \"Error\"", StringComparison.Ordinal))
                    ?? throw new InvalidOperationException("error button is missing");
                var id = line.TrimStart().Split(' ')[0].TrimStart('#');
                measured = await context.ReadAsync(new ExecutorTarget(id), new ExecutorReadOptions { Style = ["color", "font-weight", "opacity"] }, token);
                return JsonSerializer.Serialize(new { style = measured.Style });
            }),
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = model,
            AgentTools = tools,
            BaseUrl = site.Url,
            TestTitle = "measures an error style",
            CacheEnabled = false,
            ActionTimeout = TimeSpan.FromSeconds(10),
            TestTimeout = TimeSpan.FromSeconds(60),
            StepTimeout = TimeSpan.FromSeconds(30),
        });
        session.Register("tone", sentinel);

        await session.App.OpenAsync("/");
        await session.Browser.EvaluateAsync<object?>("() => { document.body.innerHTML = '<button style=\"color: red; font-weight: 700; opacity: " + sentinel + "\">Error</button>'; }");
        await session.Agent.ActAsync("measure the error style");
        await Expect.That(session.Screen.GetByRole("button", "Error")).ToBeVisibleAsync();

        session.Complete();
        Assert.Equal(new Dictionary<string, string> { ["color"] = "rgb(255, 0, 0)", ["font-weight"] = "700", ["opacity"] = "<secret:tone>" }, measured!.Style);
        Assert.Contains(model.Requests[^1].Messages, message => message.Role == "tool" && message.Content!.Contains("rgb(255, 0, 0)", StringComparison.Ordinal));
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(model.Requests.Select(request => request.Messages)), StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(session.Report()), StringComparison.Ordinal);
        var step = Assert.Single(session.Steps(), entry => entry.Api == "agent.act");
        Assert.Equal(0, step.Metrics!.ActionSteps);
    }
}
