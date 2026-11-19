// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;
using static E2E.Decision.Tests.TracingChannel.TracingChannelTestHelpers;

namespace E2E.Decision.Tests.TracingChannel;

/// <summary>The AI SDK's <c>tracing-channel.test.ts</c>, <c>describe('telemetry tracing channel publisher')</c>: the <c>it</c>s that run <c>decide</c>.</summary>
[Collection(TracingChannelCollection.Name)]
public sealed class TelemetryTracingChannelPublisherTests
{
    private static Task Decide(DecideOptions options) => ExperimentalDecide.RunAsync(options);

    private static DecideOptions Options(TelemetryOptions? telemetry, IReadOnlyDictionary<string, object?>? runtimeContext)
    {
        var (model, _) = Setup(
            new DecisionModelResult
            {
                Answers = new Dictionary<string, DecisionAnswer>
                {
                    ["refund"] = new() { Type = "boolean", Probability = 0.9 },
                },
            }
        );
        return new DecideOptions
        {
            Model = model,
            State = [new DecisionTextPart("Please refund me")],
            Questions = new Dictionary<string, DecisionQuestion> { ["refund"] = DecisionQuestion.Boolean("Refund?") },
            Telemetry = telemetry,
            RuntimeContext = runtimeContext,
        };
    }

    [Fact]
    public async Task Partial_includes_the_SDK_version_with_allowlisted_context_on_every_tracing_span()
    {
        var runtimeContext = new Dictionary<string, object?> { ["requestId"] = "request-1", ["accessToken"] = "secret-token" };
        var telemetry = new TelemetryOptions { IncludeRuntimeContext = new Dictionary<string, bool> { ["requestId"] = true } };

        var messages = await CollectTracingChannelStartMessages(() => Decide(Options(telemetry, runtimeContext)));

        var start = Assert.Single(messages, message => message.Type == TelemetryTracingEventType.ExperimentalDecide);
        var @event = Assert.IsType<DecideStartEvent>(start.Event);
        Assert.Equal(TelemetryDispatcher.AiVersion, @event.AiVersion);
        Assert.Equal(new Dictionary<string, object?> { ["requestId"] = "request-1" }, @event.RuntimeContext);
    }

    [Fact]
    public async Task Traces_the_current_decide_lifecycle_sequence()
    {
        var sequence = await CollectTracingChannelEventSequence(() => Decide(Options(null, null)));

        Assert.Equal(["bindStart experimental_decide", "asyncEnd experimental_decide"], sequence);
    }

    [Theory]
    [InlineData("an omitted allowlist")]
    [InlineData("an empty allowlist")]
    [InlineData("an explicitly excluded field")]
    public async Task Excludes_decide_runtime_context_with(string name)
    {
        TelemetryOptions? telemetry = name switch
        {
            "an omitted allowlist" => null,
            "an empty allowlist" => new TelemetryOptions(),
            _ => new TelemetryOptions { IncludeRuntimeContext = new Dictionary<string, bool> { ["secret"] = false } },
        };

        var messages = await CollectTracingChannelStartMessages(() =>
            Decide(Options(telemetry, new Dictionary<string, object?> { ["secret"] = "hidden" }))
        );

        var decideMessage = messages.Find(message => message.Type == TelemetryTracingEventType.ExperimentalDecide);
        Assert.Empty(Assert.IsType<DecideStartEvent>(decideMessage?.Event).RuntimeContext);
    }

    [Fact]
    public async Task Includes_only_allowlisted_decide_runtime_context_in_tracing()
    {
        var messages = await CollectTracingChannelStartMessages(() =>
            Decide(
                Options(
                    new TelemetryOptions { IncludeRuntimeContext = new Dictionary<string, bool> { ["requestId"] = true } },
                    new Dictionary<string, object?> { ["requestId"] = "request-1", ["secret"] = "hidden" }
                )
            )
        );

        var decideMessage = messages.Find(message => message.Type == TelemetryTracingEventType.ExperimentalDecide);
        Assert.Equal(
            new Dictionary<string, object?> { ["requestId"] = "request-1" },
            Assert.IsType<DecideStartEvent>(decideMessage?.Event).RuntimeContext
        );
    }
}
