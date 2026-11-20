// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using Publisher = E2E.Decision.Sdk.TracingChannelPublisher;

namespace E2E.Decision.Tests.TracingChannelPublisher;

/// <summary>The AI SDK's <c>tracing-channel-publisher.test.ts</c>, <c>describe('runWithTracingChannelSpan runtime switch')</c>.</summary>
[Collection(TracingChannel.TracingChannelCollection.Name)]
public sealed class RunWithTracingChannelSpanRuntimeSwitchTests
{
    [Fact]
    public async Task Traces_through_the_diagnostics_channel_when_running_on_Node()
    {
        var message = new TelemetryTracingChannelMessage
        {
            Type = TelemetryTracingEventType.LanguageModelCall,
            Event = new Dictionary<string, object?> { ["callId"] = "call-1" },
        };
        var starts = new List<TelemetryTracingChannelMessage>();
        var subscribers = new TracingChannelSubscribers
        {
            Start = started => starts.Add((TelemetryTracingChannelMessage)started),
        };
        var channel = DiagnosticsChannel.GetTracingChannel(TelemetryTracingChannel.AiSdkTelemetryTracingChannel);
        channel.Subscribe(subscribers);
        try
        {
            var result = await Publisher.RunWithTracingChannelSpanAsync(message, () => Task.FromResult("result"));

            Assert.Equal("result", result);
            var start = Assert.Single(starts);
            Assert.Equal(message.Type, start.Type);
            Assert.Equal(message.Event, start.Event);
        }
        finally
        {
            channel.Unsubscribe(subscribers);
        }
    }
}
