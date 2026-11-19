// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.TracingChannel;

/// <summary>Runs the tests that subscribe to the process-wide tracing channel one at a time, so no other test's calls reach a subscriber.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TracingChannelCollection
{
    public const string Name = "TracingChannel";
}

/// <summary>The AI SDK's <c>tracing-channel.test.ts</c> helpers (<c>collectTracingChannelStartMessages</c>, <c>collectTracingChannelEventSequence</c>).</summary>
internal static class TracingChannelTestHelpers
{
    public static async Task<List<TelemetryTracingChannelMessage>> CollectTracingChannelStartMessages(Func<Task> run)
    {
        var messages = new List<TelemetryTracingChannelMessage>();
        var subscribers = new TracingChannelSubscribers
        {
            Start = message =>
            {
                var telemetry = (TelemetryTracingChannelMessage)message;
                lock (messages)
                {
                    messages.Add(new TelemetryTracingChannelMessage { Type = telemetry.Type, Event = telemetry.Event });
                }
            },
        };
        var tracingChannel = DiagnosticsChannel.GetTracingChannel(TelemetryTracingChannel.AiSdkTelemetryTracingChannel);
        tracingChannel.Subscribe(subscribers);
        try
        {
            await run();
        }
        finally
        {
            tracingChannel.Unsubscribe(subscribers);
        }

        return messages;
    }

    public static async Task<List<string>> CollectTracingChannelEventSequence(Func<Task> run)
    {
        var events = new List<string>();
        var store = new AsyncLocal<TelemetryTracingChannelMessage?>();
        var tracingChannel = DiagnosticsChannel.GetTracingChannel(TelemetryTracingChannel.AiSdkTelemetryTracingChannel);
        var subscribers = new TracingChannelSubscribers
        {
            AsyncEnd = message => events.Add($"asyncEnd {((TelemetryTracingChannelMessage)message).Type}"),
        };
        tracingChannel.Start.BindStore(store, message =>
        {
            events.Add($"bindStart {((TelemetryTracingChannelMessage)message).Type}");
            return (TelemetryTracingChannelMessage)message;
        });
        tracingChannel.Subscribe(subscribers);
        try
        {
            await run();
        }
        finally
        {
            tracingChannel.Unsubscribe(subscribers);
            tracingChannel.Start.UnbindStore(store);
        }

        return events;
    }
}
