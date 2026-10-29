// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>
/// No upstream equivalent: upstream's recorder is an AI SDK telemetry integration, so every model call reaches it without a call site
/// knowing. The port has no AI SDK, so each place that asks a model for a turn goes through here, and the telemetry events the SDK would
/// have fired are fired from this one seam. The text is redacted on its way in, as the model's own request was.
/// </summary>
internal static class AiTraceModel
{
    /// <summary>Asks <paramref name="model"/> for one turn, and records it when <paramref name="recorder"/> is set.</summary>
    public static async Task<ModelResponse> CompleteAsync(AiTraceRecorder? recorder, IAgentModel model, ModelRequest request, CancellationToken cancellationToken)
    {
        var turn = await BeginAsync(recorder, model, request, cancellationToken).ConfigureAwait(false);
        turn.End();
        return turn.Response;
    }

    /// <summary>
    /// Asks <paramref name="model"/> for one turn and leaves the generation open, as the AI SDK does while a turn's tools run: the caller
    /// runs each tool through <see cref="AiTraceTurn.ExecuteToolAsync"/>, so a generation a tool starts is a child of this step, and then
    /// calls <see cref="AiTraceTurn.End"/>.
    /// </summary>
    public static async Task<AiTraceTurn> BeginAsync(AiTraceRecorder? recorder, IAgentModel model, ModelRequest request, CancellationToken cancellationToken)
    {
        if (recorder is null)
        {
            return new AiTraceTurn(await model.CompleteAsync(request, cancellationToken).ConfigureAwait(false), null, null, null);
        }

        var redactor = request.Redactor;
        var callId = Guid.NewGuid().ToString("n");
        var typeName = model.GetType().Name;
        var provider = typeName.Length > "Model".Length && typeName.EndsWith("Model", StringComparison.Ordinal) ? typeName[..^"Model".Length] : null;
        var instructions = redactor.Redact(request.System);
        var messages = request.Messages.Select(message => Message(message, redactor)).ToList();
        var toolChoice = Obj(null, ("type", "auto"));
        recorder.OnStart(new AiTraceStartEvent(callId, "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent(callId, 0, provider, model.Name, instructions, messages, toolChoice));
        recorder.OnLanguageModelCallStart(new AiTraceModelCallStartEvent(callId, instructions, messages, request.Tools.Select(Tool).ToList(), toolChoice));
        var started = Stopwatch.GetTimestamp();
        ModelResponse response;
        try
        {
            response = await model.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            recorder.OnError(new AiTraceErrorEvent(callId, ex is OperationCanceledException ? "the call was cancelled" : redactor.Redact(ex.Message)));
            throw;
        }

        recorder.OnLanguageModelCallEnd(new AiTraceModelCallEndEvent(callId, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        var content = new List<object?>();
        if (!string.IsNullOrEmpty(response.Content))
        {
            content.Add(Obj("text", ("text", redactor.Redact(response.Content))));
        }

        content.AddRange(response.ToolCalls.Select(call => ToolCall(call, redactor)));
        var stepEnd = new AiTraceStepEndEvent(
            callId,
            0,
            content,
            response.ToolCalls.Count > 0 ? "tool-calls" : "stop",
            response.Usage is null ? null : Obj(null, ("inputTokens", response.Usage.InputTokens), ("outputTokens", response.Usage.OutputTokens)),
            new AiTraceResponse(ModelId: model.Name, Timestamp: DateTimeOffset.UtcNow, Messages: [Obj(null, ("role", "assistant"), ("content", content))]));
        return new AiTraceTurn(response, recorder, callId, stepEnd);
    }

    private static Dictionary<string, object?> Message(ModelMessage message, Redactor redactor)
    {
        switch (message.Role)
        {
            case "assistant":
                var parts = new List<object?>();
                if (!string.IsNullOrEmpty(message.Content))
                {
                    parts.Add(Obj("text", ("text", redactor.Redact(message.Content))));
                }

                parts.AddRange((message.ToolCalls ?? []).Select(call => ToolCall(call, redactor)));
                return Obj(null, ("role", "assistant"), ("content", parts));
            case "tool":
                var result = Obj(
                    "tool-result",
                    ("toolCallId", message.ToolCallId),
                    ("toolName", message.Name),
                    ("output", Obj("text", ("value", message.Content is null ? "" : redactor.Redact(message.Content)))));
                return Obj(null, ("role", "tool"), ("content", new List<object?> { result }));
            default:
                var text = message.Content is null ? null : redactor.Redact(message.Content);
                if (message.Images is not { Count: > 0 } images)
                {
                    return Obj(null, ("role", message.Role), ("content", text));
                }

                var content = new List<object?>();
                if (text is not null)
                {
                    content.Add(Obj("text", ("text", text)));
                }

                content.AddRange(images.Select(image => Obj("image", ("image", image.Data), ("mediaType", image.MediaType))));
                return Obj(null, ("role", message.Role), ("content", content));
        }
    }

    private static Dictionary<string, object?> ToolCall(ModelToolCall call, Redactor redactor) =>
        Obj("tool-call", ("toolCallId", call.Id), ("toolName", call.Name), ("input", Tree(call.Arguments, redactor)));

    private static Dictionary<string, object?> Tool(ModelTool tool) =>
        Obj(null, ("name", tool.Name), ("description", tool.Description), ("inputSchema", tool.Parameters.ValueKind == JsonValueKind.Undefined ? null : tool.Parameters.Clone()));

    // The JSON a model produced or was sent, with every string redacted: a secret the model echoed stops here.
    private static object? Tree(JsonElement element, Redactor redactor) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => Tree(property.Value, redactor), StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(item => Tree(item, redactor)).ToList(),
        JsonValueKind.String => redactor.Redact(element.GetString()!),
        _ => element.Clone(),
    };

    private static Dictionary<string, object?> Obj(string? type, params (string Key, object? Value)[] fields)
    {
        var value = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (type is not null)
        {
            value["type"] = type;
        }

        foreach (var (key, field) in fields)
        {
            value[key] = field;
        }

        return value;
    }
}

/// <summary>
/// No upstream equivalent (see <see cref="AiTraceModel"/>): one model turn whose generation is still open. The AI SDK ends the step and
/// the generation after the turn's tools ran, and runs each tool under <c>executeTool</c> with the generation's call id.
/// </summary>
internal sealed class AiTraceTurn(ModelResponse response, AiTraceRecorder? recorder, string? callId, AiTraceStepEndEvent? stepEnd) : IDisposable
{
    private int _ended;

    public ModelResponse Response { get; } = response;

    /// <summary>Runs a tool of this turn, so a generation it starts is attributed to the step that called it.</summary>
    public Task ExecuteToolAsync(Func<Task> execute) =>
        recorder is null || callId is null ? execute() : recorder.ExecuteTool(callId, execute);

    /// <summary>Ends the step and the generation, as <see cref="End"/> does.</summary>
    public void Dispose() => End();

    /// <summary>Ends the step and the generation. Safe to call more than once.</summary>
    public void End()
    {
        if (recorder is null || callId is null || stepEnd is null || Interlocked.Exchange(ref _ended, 1) != 0)
        {
            return;
        }

        recorder.OnStepEnd(stepEnd);
        recorder.OnEnd(new AiTraceEndEvent(callId));
    }
}
