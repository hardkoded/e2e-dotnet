// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.AgentAiTrace;

// Upstream's second describe, "--ai-trace on child-process workers", has no port: the port has no workers and no runner. Each
// test process writes its own file instead (ai-trace-<process id>.json), so there is nothing to collect across workers.
// The model is the port's ScriptedModel, so the model id is "scripted" and the provider is "Scripted", where upstream's
// fake model is "scripted-loop" from "fake-loop". The port has no run outcome, so the path is the one the trace file has next to the results directory.
/// <summary>Upstream's <c>--ai-trace on the in-process transport</c> describe.</summary>
public sealed class AiTraceOnTheInProcessTransportTests : IAsyncLifetime
{
    private readonly string _output = Path.Combine(Path.GetTempPath(), "e2e-ai-trace-" + Guid.NewGuid().ToString("n"));
    private string? _path;
    private Exception? _error;

    public async Task InitializeAsync()
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("passed", "tapped once; the counter shows 1")
                : ModelResponses.Tap("button", "Increment")),
            BaseUrl = "https://counter.test",
            TestTitle = "default agent increments the counter",
            ResultsDirectory = Path.Combine(_output, "results"),
            ProjectRoot = _output,
            TargetName = "web",
            AiTrace = true,
            CacheEnabled = false,
        });
        try
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("increment the counter once and verify it shows 1");
            await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("1");
            session.Complete();
        }
        catch (Exception ex)
        {
            _error = ex;
        }

        _path = Path.Combine(_output, "ai-trace-" + Environment.ProcessId + ".json");
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_output, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public void Passes_and_writes_the_trace_next_to_the_results_directory()
    {
        Assert.Null(_error);
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Records_one_run_for_the_act_step_with_one_step_per_model_turn()
    {
        var document = Read();
        ExpectDevtoolsShape(document);

        var run = Assert.Single(document["runs"]!.AsArray())!;
        Assert.Equal(
            "default agent increments the counter · agent.act \"increment the counter once and verify it shows 1\"",
            (string?)run["function_id"]);
        var e2e = run["e2e"]!;
        Assert.Equal("default agent increments the counter", (string?)e2e["test"]);
        Assert.Equal("web", (string?)e2e["target"]);
        Assert.Equal(0, (int?)e2e["attempt"]);
        Assert.Equal("agent.act", (string?)e2e["api"]);
        var steps = document["steps"]!.AsArray().Where(step => (string?)step!["run_id"] == (string?)run["id"]).ToList();
        Assert.Equal([1, 2], steps.Select(step => (int?)step!["step_number"]));
        Assert.All(steps, step => Assert.Equal("scripted", (string?)step!["model_id"]));
        Assert.All(steps, step => Assert.Equal("Scripted", (string?)step!["provider"]));
    }

    private JsonNode Read() => JsonNode.Parse(File.ReadAllText(_path!))!;

    // The checks a viewer's devtools adapter runs before accepting a database.
    private static void ExpectDevtoolsShape(JsonNode document)
    {
        Assert.NotNull(document["runs"]?.AsArray());
        Assert.NotNull(document["steps"]?.AsArray());
        foreach (var run in document["runs"]!.AsArray())
        {
            Assert.IsType<string>((string?)run!["id"]);
        }

        foreach (var step in document["steps"]!.AsArray())
        {
            Assert.IsType<string>((string?)step!["run_id"]);
            Assert.IsType<string>((string?)step["input"]);
        }
    }
}
