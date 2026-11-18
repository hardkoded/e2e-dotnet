// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('model failures')</c> of <c>executor.test.ts</c>.</summary>
public sealed class ModelFailuresTests
{
    [Fact]
    public async Task Acts_on_a_committed_choice_without_a_distribution_even_under_a_probability_gate()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                new ScriptedAnswer(
                    id == "operation" ? (call == 0 ? "tap" : "done")
                        : id == "verdict" ? "holds"
                        : keys[0],
                    Bare: true
                )
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var verdict = await Run(model, fixture, minProbability: 0.9);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Equal(1, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Blocks_a_committed_choice_without_a_distribution_under_a_confidence_gate()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) => new ScriptedAnswer(id == "operation" ? "tap" : keys[0], Bare: true)
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var verdict = await Run(model, fixture, minConfidence: 0.5);
        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Equal("AUTOMATION_UNSUPPORTED", verdict.ErrorCode);
        Assert.Equal(0, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Fails_a_probability_outside_0_1()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id != "operation"
                    ? new ScriptedAnswer(keys[0])
                    : new ScriptedAnswer(
                        "tap",
                        keys.ToDictionary(
                            key => key,
                            key =>
                                key == "tap" ? 1.5
                                : key == "done" ? -0.5
                                : 0.0
                        )
                    )
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var error = await Assert.ThrowsAsync<AgentException>(() => Run(model, fixture));
        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
    }

    [Fact]
    public async Task Names_a_refusal_the_SDK_rejects_as_a_wrong_type_answer()
    {
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var error = await Assert.ThrowsAsync<AgentException>(() => Run(new RefusingModel(), fixture));
        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.Equal("The decision model refused to answer \"operation\".", error.Message);
    }

    [Fact]
    public async Task Maps_provider_failures_without_provider_text()
    {
        var (model, _) = ScriptedDecision(
            (_, _, _) => new ScriptedAnswer("tap"),
            throws: new Exception("upstream detail")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var first = await Assert.ThrowsAsync<AgentException>(() => Run(model, fixture));
        Assert.Equal("MODEL_PROVIDER_FAILED", first.Code);
        var second = await Assert.ThrowsAsync<AgentException>(() => Run(model, fixture));
        Assert.DoesNotContain("upstream", second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_the_API_key_variable_the_provider_reads()
    {
        var missing = new LoadApiKeyException(
            "TypeSafe AI API key is missing. Pass it using the 'apiKey' parameter or the TYPESAFE_AI_API_KEY environment variable."
        );
        var (model, _) = ScriptedDecision((_, _, _) => new ScriptedAnswer("tap"), throws: missing);
        var error = await Assert.ThrowsAsync<AgentException>(() =>
            Run(model, Context(new ContextOptions { Tree = Buttons }))
        );
        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        Assert.Equal("Set TYPESAFE_AI_API_KEY to the decision model API key.", error.Message);
        var (unnamed, _) = ScriptedDecision(
            (_, _, _) => new ScriptedAnswer("tap"),
            throws: new LoadApiKeyException("no key")
        );
        var other = await Assert.ThrowsAsync<AgentException>(() =>
            Run(unnamed, Context(new ContextOptions { Tree = Buttons }))
        );
        Assert.Equal("MODEL_UNAVAILABLE", other.Code);
        Assert.Equal("Set the decision model API key.", other.Message);
    }

    [Fact]
    public async Task Rethrows_InvalidArgumentError_as_our_own_bug()
    {
        var failure = new InvalidArgumentException("questions", "bad");
        var (model, _) = ScriptedDecision((_, _, _) => new ScriptedAnswer("tap"), throws: failure);
        var thrown = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            Run(model, Context(new ContextOptions { Tree = Buttons }))
        );
        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task Honors_abort()
    {
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        var (model, _) = ScriptedDecision((_, _, _) => new ScriptedAnswer("tap"));
        var fixture = Context(new ContextOptions { Tree = Buttons, Signal = controller.Token });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(model, fixture));
    }
}
