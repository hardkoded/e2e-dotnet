// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Tests.AiTrace;

/// <summary>The helpers of upstream's <c>ai-trace.test.ts</c> and <c>ai-trace-media.test.ts</c>, with the SDK values written as plain objects.</summary>
internal static class AiTraceTestSupport
{
    public static readonly AiTraceScope Scope = new() { Test = "todos › adds one", TestId = "t1", Target = "web", Agent = "default", Attempt = 0 };

    /// <summary>A JSON object, in the order the fields are written.</summary>
    public static Dictionary<string, object?> O(params (string Key, object? Value)[] fields)
    {
        var value = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, field) in fields)
        {
            value[key] = field;
        }

        return value;
    }

    public static List<object?> A(params object?[] items) => [.. items];

    /// <summary>The JSON value as the trace writes it, to compare with a parsed column.</summary>
    public static JsonNode? Json(object? value) => JsonNode.Parse(Internal.AiTrace.Stringify(value));

    public static JsonNode Parse(string? json) => JsonNode.Parse(json!)!;

    public static bool Same(object? expected, string? actual) => JsonNode.DeepEquals(Json(expected), JsonNode.Parse(actual!));

    public static Task<int> Step(string api, string label, Func<Task> work) =>
        AiTraceScopes.WithAiTraceStep(api, label, async () =>
        {
            await work();
            return 0;
        });

    public static Task<int> InScope(AiTraceScope scope, Func<Task> work) =>
        AiTraceScopes.WithAiTraceScope(scope, async () =>
        {
            await work();
            return 0;
        });

    /// <summary>Drives one two-step generation through the recorder the way the SDK does.</summary>
    public static Task Generation(AiTraceRecorder recorder, string callId, Exception? fail = null)
    {
        recorder.OnStart(new AiTraceStartEvent(callId, "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent(
            callId,
            0,
            "gateway",
            "google/gemini-3-flash",
            "You are a testing agent.",
            [O(("role", "user"), ("content", "Execute this test step: add a todo"))]));
        recorder.OnLanguageModelCallStart(new AiTraceModelCallStartEvent(
            callId,
            "You are a testing agent.",
            [O(("role", "user"), ("content", "Execute this test step: add a todo (compacted)"))],
            [
                O(
                    ("type", "function"),
                    ("name", "tap"),
                    ("description", "Tap or click one node."),
                    ("inputSchema", O(("type", "object"), ("properties", O(("target", O(("type", "string")))))))),
            ],
            O(("type", "required"))));
        recorder.OnLanguageModelCallEnd(new AiTraceModelCallEndEvent(callId, 321));
        if (fail is not null)
        {
            recorder.OnError(new AiTraceErrorEvent(callId, fail));
            return Task.CompletedTask;
        }

        var call = O(("type", "tool-call"), ("toolCallId", "c1"), ("toolName", "tap"), ("input", O(("target", "n1"))));
        recorder.OnStepEnd(new AiTraceStepEndEvent(
            callId,
            0,
            A(call),
            "tool-calls",
            O(("inputTokens", 100), ("outputTokens", 20), ("inputTokenDetails", O(("cacheReadTokens", 40)))),
            new AiTraceResponse(
                "r1",
                "google/gemini-3-flash",
                DateTimeOffset.UnixEpoch,
                [
                    O(("role", "assistant"), ("content", A(call))),
                    O(("role", "tool"), ("content", A(O(("type", "tool-result"), ("toolCallId", "c1"), ("toolName", "tap"), ("output", O(("type", "text"), ("value", "Tapped #n1."))))))),
                ]),
            O(("gateway", O(("cost", "0.0001"))))));
        recorder.OnEnd(new AiTraceEndEvent(callId));
        return Task.CompletedTask;
    }
}
