// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;
using static E2E.Tests.AiTrace.AiTraceTestSupport;

namespace E2E.Tests.AiTrace;

/// <summary>.NET-only: upstream records through the AI SDK's telemetry, so it has no seam of its own to test. The port feeds the recorder from <c>AiTraceModel</c>.</summary>
public sealed class AiTraceModelTests
{
    private const string Password = "hunter2-correct-horse";

    private static ModelRequest Request(Redactor redactor, params ModelMessage[] messages) =>
        new() { System = "rules", Messages = messages, Tools = [], Redactor = redactor };

    [Fact]
    public async Task Redacts_the_secrets_of_the_request_the_response_and_a_failure()
    {
        var redactor = Redactor.ForValue(Password);
        var model = new ScriptedModel(_ => new ModelResponse
        {
            Content = "typed " + Password,
            ToolCalls = [new ModelToolCall { Id = "c1", Name = "fill", Arguments = JsonSerializer.SerializeToElement(new { value = Password }) }],
        });
        var recorder = new AiTraceRecorder();

        await AiTraceModel.CompleteAsync(recorder, model, Request(redactor, new ModelMessage { Role = "user", Content = "the password is " + Password }), CancellationToken.None);
        var failing = new ScriptedModel(_ => throw new InvalidOperationException("provider echoed " + Password));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AiTraceModel.CompleteAsync(recorder, failing, Request(redactor), CancellationToken.None));
        var document = Internal.AiTrace.Stringify(recorder.Drain());

        Assert.DoesNotContain(Password, document, StringComparison.Ordinal);
        Assert.Contains("[redacted]", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Records_the_provider_metadata_of_a_tool_call_redacted()
    {
        var redactor = Redactor.ForValue(Password);
        var metadata = new Dictionary<string, JsonElement> { ["google"] = JsonSerializer.SerializeToElement(new { thoughtSignature = "sig-" + Password }) };
        var call = new ModelToolCall { Id = "c1", Name = "tap", Arguments = JsonSerializer.SerializeToElement(new { }), ProviderMetadata = metadata };
        var model = new ScriptedModel(_ => new ModelResponse { ToolCalls = [call] });
        var recorder = new AiTraceRecorder();

        await AiTraceModel.CompleteAsync(recorder, model, Request(redactor, new ModelMessage { Role = "assistant", ToolCalls = [call] }), CancellationToken.None);
        var step = recorder.Drain().Steps[0];
        var output = JsonDocument.Parse(step.Output!).RootElement;

        Assert.Equal("sig-[redacted]", output.GetProperty("content")[0].GetProperty("providerMetadata").GetProperty("google").GetProperty("thoughtSignature").GetString());
        Assert.Equal("sig-[redacted]", output.GetProperty("response").GetProperty("messages")[0].GetProperty("content")[0].GetProperty("providerOptions").GetProperty("google").GetProperty("thoughtSignature").GetString());
        Assert.Contains("providerOptions", step.Input, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, step.Input + step.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Records_the_provider_metadata_of_a_step_redacted()
    {
        var redactor = Redactor.ForValue(Password);
        var metadata = new Dictionary<string, JsonElement> { ["gateway"] = JsonSerializer.SerializeToElement(new { cost = "0.0001", generationId = "gen-" + Password }) };
        var model = new ScriptedModel(_ => new ModelResponse { Content = "ok", ProviderMetadata = metadata });
        var recorder = new AiTraceRecorder();

        await AiTraceModel.CompleteAsync(recorder, model, Request(redactor), CancellationToken.None);
        var step = recorder.Drain().Steps[0];
        var gateway = JsonDocument.Parse(step.Output!).RootElement.GetProperty("providerMetadata").GetProperty("gateway");

        Assert.Equal("0.0001", gateway.GetProperty("cost").GetString());
        Assert.Equal("gen-[redacted]", gateway.GetProperty("generationId").GetString());
        Assert.DoesNotContain(Password, step.Input + step.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Omits_the_provider_metadata_of_a_tool_call_that_has_none()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));
        var recorder = new AiTraceRecorder();

        await AiTraceModel.CompleteAsync(recorder, model, Request(Redactor.None), CancellationToken.None);
        var step = recorder.Drain().Steps[0];

        Assert.DoesNotContain("providerMetadata", step.Output!, StringComparison.Ordinal);
        Assert.DoesNotContain("providerOptions", step.Output!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attributes_a_generation_a_tool_starts_to_the_step_that_called_it()
    {
        var recorder = new AiTraceRecorder();
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));

        using (var turn = await AiTraceModel.BeginAsync(recorder, model, Request(Redactor.None), CancellationToken.None))
        {
            await turn.ExecuteToolAsync(() => AiTraceModel.CompleteAsync(recorder, model, Request(Redactor.None), CancellationToken.None));
        }

        var document = recorder.Drain();

        Assert.Equal(2, document.Runs.Count);
        var (outer, inner) = (document.Runs[0], document.Runs[1]);
        Assert.Equal(outer.Id, inner.ParentRunId);
        Assert.Equal(document.Steps.Single(step => step.RunId == outer.Id).Id, inner.ParentStepId);
    }

    [Fact]
    public async Task Records_a_screenshot_as_a_byte_count()
    {
        var recorder = new AiTraceRecorder();
        var image = new ModelImage(new byte[4096], "image/png", 10, 10);
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));

        await AiTraceModel.CompleteAsync(recorder, model, Request(Redactor.None, new ModelMessage { Role = "user", Content = "look", Images = [image] }), CancellationToken.None);
        var step = recorder.Drain().Steps[0];

        Assert.Contains("[binary 4096 bytes omitted]", step.Input, StringComparison.Ordinal);
        Assert.True(step.Input.Length < 1_000);
    }

    [Fact]
    public async Task Keeps_the_attribution_of_tests_that_run_at_the_same_time()
    {
        var recorder = new AiTraceRecorder();
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Test(string title) => InScope(Scope with { Test = title }, () => Step("agent.act", title, async () =>
        {
            await gate.Task;
            await AiTraceModel.CompleteAsync(recorder, model, Request(Redactor.None), CancellationToken.None);
        }));

        var both = Task.WhenAll(Test("first"), Test("second"));
        gate.SetResult();
        await both;
        var document = recorder.Drain();

        Assert.Equal(["first · agent.act \"first\"", "second · agent.act \"second\""], document.Runs.Select(run => run.FunctionId).Order(StringComparer.Ordinal));
    }
}
