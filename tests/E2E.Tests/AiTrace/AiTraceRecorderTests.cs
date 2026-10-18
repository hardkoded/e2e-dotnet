// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;
using static E2E.Tests.AiTrace.AiTraceTestSupport;

namespace E2E.Tests.AiTrace;

// Upstream's "registers through the SDK loader and tolerates a missing SDK" has no port: the port has no AI SDK to register with,
// and AiTraceModel feeds the recorder directly.
/// <summary>Upstream's <c>AiTraceRecorder</c> describe, with the AI SDK telemetry events as method calls.</summary>
public sealed class AiTraceRecorderTests
{
    [Fact]
    public async Task Records_one_run_per_agent_step_and_one_step_per_model_round_trip()
    {
        var recorder = new AiTraceRecorder();
        await InScope(Scope, () => Step("agent.act", "add a todo", () => Generation(recorder, "call-1")));
        var (runs, steps) = Drained(recorder);

        Assert.Single(runs);
        Assert.Single(steps);
        var run = runs[0];
        Assert.Equal("todos › adds one · agent.act \"add a todo\"", run.FunctionId);
        Assert.Null(run.ParentRunId);
        Assert.Equal(Scope with { Api = "agent.act", Label = "add a todo" }, run.E2e);
        var step = steps[0];
        Assert.Equal(run.Id, step.RunId);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal("google/gemini-3-flash", step.ModelId);
        Assert.Equal("gateway", step.Provider);
        Assert.Equal(321, step.DurationMs);
        Assert.Null(step.Error);
    }

    [Fact]
    public async Task Writes_the_prompt_as_sent_with_the_system_prompt_in_front_and_tool_schemas_attached()
    {
        var recorder = new AiTraceRecorder();
        await Generation(recorder, "call-1");
        var step = recorder.Drain().Steps[0];
        var input = Parse(step.Input);

        var prompt = input["prompt"]!.AsArray();
        Assert.Equal(["system", "user"], prompt.Select(message => (string?)message!["role"]));
        Assert.Equal("You are a testing agent.", (string?)prompt[0]!["content"]);

        // The model-call view (after the history is prepared) supersedes the step-start one.
        Assert.Contains("(compacted)", (string?)prompt[1]!["content"], StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(
            Json(A(O(("name", "tap"), ("description", "Tap or click one node."), ("parameters", O(("type", "object"), ("properties", O(("target", O(("type", "string"))))))))))
            , input["tools"]));
        Assert.True(JsonNode.DeepEquals(Json(O(("type", "required"))), input["toolChoice"]));
    }

    [Fact]
    public async Task Closes_an_open_step_with_the_error_when_the_generation_fails()
    {
        var recorder = new AiTraceRecorder();
        await Generation(recorder, "call-1", new InvalidOperationException("provider exploded"));
        var steps = recorder.Drain().Steps;

        var step = Assert.Single(steps);
        Assert.Equal("provider exploded", step.Error);
        Assert.Null(step.Output);
        Assert.False(recorder.Pending);
    }

    // The port quotes the body from the model itself, not from a parsed SDK error, so this goes through the real provider path.
    [Fact]
    public async Task Records_the_status_and_the_body_of_a_provider_error_the_SDK_could_not_parse()
    {
        const string body = """{"detail":"Unsupported service_tier: fast"}""";
        using var http = new HttpClient(new RejectingHandler(HttpStatusCode.BadRequest, body));
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "fake-model", ApiKey = "key", BaseUrl = "https://provider.test/v1" }, http);
        var recorder = new AiTraceRecorder();
        var request = new ModelRequest { System = "rules", Messages = [new ModelMessage { Role = "user", Content = "hi" }], Tools = [], Redactor = Redactor.None };

        await Assert.ThrowsAsync<AgentException>(() => AiTraceModel.CompleteAsync(recorder, model, request, CancellationToken.None));

        Assert.Equal("The model provider failed: HTTP 400: " + body, recorder.Drain().Steps[0].Error);
    }

    [Fact]
    public async Task Names_a_run_after_the_scope_outside_a_scope_it_keeps_the_SDK_function_id()
    {
        var recorder = new AiTraceRecorder();
        recorder.OnStart(new AiTraceStartEvent("x", "ai.generateText", "my-agent"));
        recorder.OnEnd(new AiTraceEndEvent("x"));
        await InScope(Scope with { Attempt = 1 }, () => Step("agent.assert", new string('a', 200), () => Generation(recorder, "y")));
        var names = recorder.Drain().Runs.Select(run => run.FunctionId).ToList();

        Assert.Equal("my-agent", names[0]);
        Assert.Matches(@"^todos › adds one · attempt 2 · agent\.assert ""a{80}…""$", names[1]);
    }

    [Fact]
    public async Task Continues_one_run_across_the_generations_of_a_single_step_and_starts_a_new_run_per_step()
    {
        var recorder = new AiTraceRecorder();
        await InScope(Scope, async () =>
        {
            // A polling judgment: two generations in one step.
            await Step("agent.waitFor", "users appear", async () =>
            {
                await Generation(recorder, "poll-1");
                await Generation(recorder, "poll-2");
            });
            await Step("agent.assert", "saved", () => Generation(recorder, "judge"));
        });

        // Outside any step, every generation is its own run.
        await Generation(recorder, "loose-1");
        await Generation(recorder, "loose-2");
        var (runs, steps) = Drained(recorder);

        Assert.Equal(
            ["todos › adds one · agent.waitFor \"users appear\"", "todos › adds one · agent.assert \"saved\"", null, null],
            runs.Select(run => run.FunctionId));
        Assert.Equal([1, 2], steps.Where(step => step.RunId == runs[0].Id).Select(step => step.StepNumber));
        Assert.Equal([1], steps.Where(step => step.RunId == runs[1].Id).Select(step => step.StepNumber));
        Assert.Equal(5, steps.Count);
    }

    [Fact]
    public async Task Numbers_the_steps_of_generations_that_overlap_inside_one_step_without_collisions()
    {
        var recorder = new AiTraceRecorder();
        void Start(string callId) => recorder.OnStart(new AiTraceStartEvent(callId, "ai.generateText"));
        void StepStart(string callId, int stepNumber) => recorder.OnStepStart(new AiTraceStepStartEvent(callId, stepNumber, "p", "m", "s", []));
        void StepEnd(string callId, int stepNumber) => recorder.OnStepEnd(new AiTraceStepEndEvent(callId, stepNumber, A(), "stop", O(), new AiTraceResponse()));
        await InScope(Scope, () => Step("agent.act", "parallel brain", () =>
        {
            // A custom executor firing two multi-step generations at once.
            Start("a");
            Start("b");
            StepStart("a", 0);
            StepStart("b", 0);
            StepEnd("a", 0);
            StepStart("a", 1);
            StepEnd("b", 0);
            StepStart("b", 1);
            StepEnd("a", 1);
            StepEnd("b", 1);
            recorder.OnEnd(new AiTraceEndEvent("a"));
            recorder.OnEnd(new AiTraceEndEvent("b"));
            return Task.CompletedTask;
        }));
        var (runs, steps) = Drained(recorder);

        Assert.Single(runs);
        Assert.All(steps, step => Assert.Equal(runs[0].Id, step.RunId));
        Assert.Equal([1, 2, 3, 4], steps.Select(step => step.StepNumber).Order());
    }

    [Fact]
    public async Task Attributes_a_nested_generation_to_the_tool_that_made_it_even_when_tools_overlap()
    {
        var recorder = new AiTraceRecorder();
        var innerBDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var callId in new[] { "A", "B" })
        {
            recorder.OnStart(new AiTraceStartEvent(callId, "ai.generateText"));
            recorder.OnStepStart(new AiTraceStepStartEvent(callId, 0, "p", "m", null, []));
        }

        // Tool A starts first but its nested generation lands after tool B's.
        await Task.WhenAll(
            recorder.ExecuteTool("A", async () =>
            {
                await innerBDone.Task;
                await Generation(recorder, "innerA");
            }),
            recorder.ExecuteTool("B", async () =>
            {
                await Generation(recorder, "innerB");
                innerBDone.SetResult();
            }));
        var (runs, steps) = Drained(recorder, all: true);

        var (outerA, outerB, innerB, innerA) = (runs[0], runs[1], runs[2], runs[3]);
        Assert.Equal(outerB.Id, innerB.ParentRunId);
        Assert.Equal(outerA.Id, innerA.ParentRunId);
        Assert.Equal(steps.Single(step => step.RunId == outerA.Id).Id, innerA.ParentStepId);
        Assert.Equal(steps.Single(step => step.RunId == outerB.Id).Id, innerB.ParentStepId);
    }

    [Fact]
    public void Ignores_operations_that_are_not_text_generations()
    {
        var recorder = new AiTraceRecorder();
        recorder.OnStart(new AiTraceStartEvent("e", "ai.embed"));

        Assert.Empty(recorder.Drain().Runs);
    }

    [Fact]
    public void Keeps_open_steps_until_they_close_and_takes_them_at_a_final_drain()
    {
        var recorder = new AiTraceRecorder();
        recorder.OnStart(new AiTraceStartEvent("c", "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent("c", 0, "p", "m", "sys", []));

        Assert.Empty(recorder.Drain().Steps);
        Assert.True(recorder.Pending);
        var final = recorder.Drain(all: true);
        var step = Assert.Single(final.Steps);
        Assert.Matches("run ended", step.Error);
        Assert.False(recorder.Pending);
    }

    [Fact]
    public async Task Records_nothing_once_disposed()
    {
        var recorder = new AiTraceRecorder();
        recorder.Dispose();
        await Generation(recorder, "late");

        var document = recorder.Drain(all: true);
        Assert.Empty(document.Runs);
        Assert.Empty(document.Steps);
    }

    private static (List<AiTraceRun> Runs, List<AiTraceStep> Steps) Drained(AiTraceRecorder recorder, bool all = false)
    {
        var document = recorder.Drain(all);
        return ([.. document.Runs], [.. document.Steps]);
    }

    private sealed class RejectingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                ReasonPhrase = "",
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
