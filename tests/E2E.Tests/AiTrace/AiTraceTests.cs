// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.AiTrace.AiTraceTestSupport;

namespace E2E.Tests.AiTrace;

/// <summary>The <c>it</c> of upstream's <c>ai-trace.test.ts</c> that sit outside any <c>describe</c>, with the AI SDK telemetry events as method calls.</summary>
public sealed class AiTraceTests
{
    /// <summary>Partial: a .NET dictionary is an ordinary one, and a <see cref="Uri"/> serializes to its text, so the null-prototype and <c>toJSON</c> shapes stand in as a plain dictionary and a <see cref="Uri"/>.</summary>
    [Fact]
    public void Partial_Redacts_serialized_tool_values_in_trace_input_and_output_with_the_live_ledger()
    {
        const string token = "synthetic-trace-secret-271828";
        var known = false;
        var recorder = new AiTraceRecorder(text => known ? text.Replace(token, "<secret:key>", StringComparison.Ordinal) : text);
        recorder.OnStart(new AiTraceStartEvent("redact", "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent("redact", 0, "fake", "fake", null, []));
        var results = A(
            O(("type", "tool-result"), ("output", O(("type", "json"), ("value", O((token, token)))))),
            O(("type", "tool-result"), ("output", O(("type", "json"), ("value", new Uri($"https://example.test/?key={token}"))))),
            O(("type", "tool-result"), ("output", O(("type", "text"), ("value", $"custom {token}")))));
        recorder.OnLanguageModelCallStart(new AiTraceModelCallStartEvent("redact", null, A(O(("role", "tool"), ("content", results)))));
        known = true;
        var response = new AiTraceResponse(Messages: A(O(("role", "tool"), ("content", A(O(("type", "tool-result"), ("output", O(("type", "text"), ("value", token)))))))));
        recorder.OnStepEnd(new AiTraceStepEndEvent("redact", 0, A(O(("type", "tool-result"), ("output", token))), "stop", O(), response));
        recorder.OnEnd(new AiTraceEndEvent("redact"));
        var step = recorder.Drain().Steps[0];

        Assert.Contains("<secret:key>", step.Input, StringComparison.Ordinal);
        Assert.Contains("<secret:key>", step.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(token, step.Input, StringComparison.Ordinal);
        Assert.DoesNotContain(token, step.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_run_names_and_scope_metadata_before_label_truncation()
    {
        const string token = "synthetic-metadata-secret-314159";
        var recorder = new AiTraceRecorder(text => text.Replace(token, "<secret:key>", StringComparison.Ordinal));
        var scope = Scope with { Test = $"test {token}", TestId = $"id-{token}", Target = token, Agent = token, Api = token, Label = new string('x', 75) + token };
        await InScope(scope, () =>
        {
            recorder.OnStart(new AiTraceStartEvent("scope", "ai.generateText"));
            recorder.OnEnd(new AiTraceEndEvent("scope"));
            return Task.CompletedTask;
        });
        recorder.OnStart(new AiTraceStartEvent("function", "ai.generateText", $"function {token}"));
        recorder.OnEnd(new AiTraceEndEvent("function"));
        var runs = recorder.Drain().Runs;

        Assert.Equal(
            scope with { Test = "test <secret:key>", TestId = "id-<secret:key>", Target = "<secret:key>", Agent = "<secret:key>", Api = "<secret:key>", Label = new string('x', 75) + "<secret:key>" },
            runs[0].E2e);
        Assert.Contains("test <secret:key>", runs[0].FunctionId, StringComparison.Ordinal);
        Assert.DoesNotContain(token[..5], runs[0].FunctionId, StringComparison.Ordinal);
        Assert.Equal("function <secret:key>", runs[1].FunctionId);
    }
}
