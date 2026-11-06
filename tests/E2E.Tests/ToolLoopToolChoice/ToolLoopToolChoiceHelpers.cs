// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>One request the loop sent, as upstream's <c>loopCalls</c> keeps it (<c>helpers/fake-loop-model.ts</c>): read when it was sent, since the history is kept by reference.</summary>
/// <param name="Turn">The number of the request, a refused one included (upstream's <c>turn</c>).</param>
/// <param name="ToolChoice"><c>auto</c>, <c>required</c>, or <c>tool:&lt;name&gt;</c> (upstream's <c>toolChoice</c>).</param>
/// <param name="System">The instructions (upstream's <c>system</c>).</param>
/// <param name="Prompt">What the history held (upstream's <c>prompt</c>).</param>
/// <param name="LastPrompt">The text of the newest message (upstream's <c>lastPrompt</c>).</param>
/// <param name="UserMessages">How many user messages the history held (upstream's <c>userMessages</c>).</param>
/// <param name="ToolNames">The tools offered (upstream's <c>toolNames</c>).</param>
internal sealed record LoopCall(int Turn, string ToolChoice, string System, string Prompt, string LastPrompt, int UserMessages, IReadOnlyList<string> ToolNames);

/// <summary>The port's counterpart of upstream's <c>installFakeLoopModel</c> and <c>loopCalls</c>.</summary>
internal sealed class FakeLoopModel
{
    public FakeLoopModel(Func<LoopCall, ModelResponse> respond)
    {
        Model = new ScriptedModel(request =>
        {
            var call = new LoopCall(
                Calls.Count + 1,
                request.ToolChoice is null ? "auto" : request.ToolChoice.Type == "tool" ? "tool:" + request.ToolChoice.ToolName : request.ToolChoice.Type,
                request.System ?? "",
                string.Join('\n', request.Messages.Select(E2E.MessageShape.ShapeOf)),
                request.Messages[^1].Content ?? "",
                request.Messages.Count(message => message.Role == "user"),
                [.. request.Tools.Select(tool => tool.Name)]);
            Calls.Add(call);
            return respond(call);
        });
    }

    /// <summary>What Anthropic answers, and the gateway forwards, when a model does not take a forced tool choice.</summary>
    public static AgentException Rejection { get; } = Http400("tool_choice: type \"tool\" and \"any\" are not supported for this model.");

    public ScriptedModel Model { get; }

    public List<LoopCall> Calls { get; } = [];

    /// <summary>A model that refuses every forced choice and otherwise follows the script (upstream's <c>refusingForcedChoice</c>).</summary>
    public static Func<LoopCall, ModelResponse> RefusingForcedChoice(Func<LoopCall, ModelResponse> respond) =>
        call => call.ToolChoice != "auto" ? throw Rejection : respond(call);

    /// <summary>An HTTP 400 answer, as <c>ModelHttp</c> fails one.</summary>
    public static AgentException Http400(string body) =>
        new("MODEL_PROVIDER_FAILED", "The model provider failed: HTTP 400: " + body) { ProviderStatus = 400 };
}
