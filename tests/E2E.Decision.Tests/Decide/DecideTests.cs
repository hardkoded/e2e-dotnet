// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;

namespace E2E.Decision.Tests.Decide;

/// <summary>The AI SDK's <c>decide.test.ts</c>, the <c>it</c>s outside any <c>describe</c> that report the lifecycle events.</summary>
public sealed class DecideTests
{
    [Fact]
    public async Task Retries_transient_errors_with_the_configured_retry_limit()
    {
        var (model, calls) = Setup();
        var integration = new RecordingTelemetry();
        model.FailOnce(
            new ApiCallException("Rate limited", 429, new Dictionary<string, string> { ["retry-after-ms"] = "0" })
        );

        var result = await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("text")],
                Questions = QuestionMap,
                MaxRetries = 1,
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.NotNull(result);
        Assert.Equal(2, calls.Count);
        Assert.Single(integration.ModelCallStarts);
        Assert.Single(integration.ModelCallEnds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("provider-default")]
    [InlineData("none")]
    [InlineData("minimal")]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("xhigh")]
    [InlineData("max")]
    public async Task Forwards_reasoning_and_reports_it_in_lifecycle_callbacks(string? reasoning)
    {
        var (model, calls) = Setup();
        var starts = new List<DecideStartEvent>();
        var ends = new List<DecideEndEvent>();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("test")],
                Questions = QuestionMap,
                Reasoning = reasoning,
                OnStart = @event =>
                {
                    starts.Add(@event);
                    return Task.CompletedTask;
                },
                OnEnd = @event =>
                {
                    ends.Add(@event);
                    return Task.CompletedTask;
                },
            }
        );

        Assert.Equal(reasoning, Assert.Single(calls).Reasoning);
        Assert.Equal(reasoning, Assert.Single(starts).Reasoning);
        Assert.Equal(reasoning, Assert.Single(ends).Reasoning);
    }

    [Fact]
    public async Task Partial_preserves_structured_questions_for_provider_calls_and_all_lifecycle_events()
    {
        var (model, calls) = Setup();
        var integration = new RecordingTelemetry();
        var starts = new List<DecideStartEvent>();
        var ends = new List<DecideEndEvent>();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("package")],
                Questions = QuestionMap,
                OnStart = @event =>
                {
                    starts.Add(@event);
                    return Task.CompletedTask;
                },
                OnEnd = @event =>
                {
                    ends.Add(@event);
                    return Task.CompletedTask;
                },
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.Same(QuestionMap, Assert.Single(calls).Questions);
        Assert.Same(QuestionMap, Assert.Single(integration.ModelCallStarts).Questions);
        Assert.Same(QuestionMap, Assert.Single(integration.ModelCallEnds).Questions);
        Assert.Same(QuestionMap, Assert.Single(starts).Questions);
        Assert.Same(QuestionMap, Assert.Single(ends).Questions);
    }
}
