// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ToolLoopToolChoice.FakeLoopModel;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// The "isForcedToolChoiceRejected" describe. The port's provider failure quotes the response body in its message
/// (<c>HTTP 400: {body}</c>), where upstream's error carries <c>responseBody</c> apart, so the body cases put the body in the message.
/// </summary>
public sealed class IsForcedToolChoiceRejectedTests
{
    /// <summary>Partial: the port has no retry-chain error with a <c>lastError</c>; <c>ModelHttp</c> throws the last attempt's failure itself, so that one check is left out.</summary>
    [Fact]
    public void Partial_Reads_the_provider_message_through_a_gateway_wrapper_and_a_spent_retry_chain()
    {
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Rejection));
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(new InvalidOperationException("wrapped", Rejection)));
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Http400("""{"error":{"message":"tool_choice is not supported"}}""")));
    }

    [Fact]
    public void Reads_DeepSeeks_refusal_in_thinking_mode_its_default()
    {
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Http400(
            """{"error":{"message":"Thinking mode does not support this tool_choice (request_id: 73b8c958)","type":"invalid_request_error","param":null,"code":"invalid_request_error"}}""")));
    }

    [Fact]
    public void Reads_Meta_Muse_Sparks_auto_only_refusal_as_the_gateway_and_OpenCode_Console_forward_it()
    {
        const string Message = "only `\"auto\"` is supported for `tool_choice`. `\"none\"`, `\"required\"`, and named function choices are not currently supported";
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Http400(Message)));
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Http400("{\"error\":{\"message\":" + System.Text.Json.JsonSerializer.Serialize(Message, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "}}")));
        Assert.True(ToolChoice.IsForcedToolChoiceRejected(Http400("only auto is supported for tool_choice")));
    }

    [Fact]
    public void Ignores_server_failures_and_unrelated_messages_that_mention_tools()
    {
        Assert.False(ToolChoice.IsForcedToolChoiceRejected(new AgentException("MODEL_PROVIDER_FAILED", "tool_choice is not supported") { ProviderStatus = 502 }));
        Assert.False(ToolChoice.IsForcedToolChoiceRejected(new InvalidOperationException("tool_choice must name a defined tool")));
        Assert.False(ToolChoice.IsForcedToolChoiceRejected(new InvalidOperationException("Rate limit reached")));
        Assert.False(ToolChoice.IsForcedToolChoiceRejected(null));
    }
}
