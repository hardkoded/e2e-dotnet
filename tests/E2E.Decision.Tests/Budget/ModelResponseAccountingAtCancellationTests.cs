// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using DecisionBudget = global::E2E.Decision.Budget;

namespace E2E.Decision.Tests.Budget;

/// <summary><c>describe('model response accounting at cancellation')</c> of <c>budget.test.ts</c>.</summary>
public sealed class ModelResponseAccountingAtCancellationTests
{
    /// <summary>
    /// Partial: a <see cref="CancellationTokenSource"/> carries no reason, so the rejection is checked as an
    /// <see cref="OperationCanceledException"/> and not by the text <c>cancelled locally</c>.
    /// </summary>
    [Fact]
    public async Task Partial_records_returned_usage_and_actual_model_id_while_refusing_the_cancelled_value()
    {
        using var controller = new CancellationTokenSource();
        var (ctx, usage) = Helpers.Context(controller.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DecisionBudget.RecordedAsync(ctx, "local", "requested-model", async () =>
            {
                await controller.CancelAsync();
                return new Recorded<string>
                {
                    Value = "late answer",
                    InputTokens = 17,
                    OutputTokens = 9,
                    ModelId = "served-model",
                };
            })
        );

        var call = Assert.Single(usage);
        Assert.Equal("local", call.Provider);
        Assert.Equal("served-model", call.ModelId);
        Assert.Equal(17, call.InputTokens);
        Assert.Equal(9, call.OutputTokens);
    }

    /// <summary>Partial: as above, the rejection is an <see cref="OperationCanceledException"/>, not the text <c>cancelled before dispatch</c>.</summary>
    [Fact]
    public async Task Partial_does_not_start_or_account_a_call_cancelled_before_dispatch()
    {
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        var (ctx, usage) = Helpers.Context(controller.Token);
        var started = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DecisionBudget.RecordedAsync(ctx, "local", "requested-model", () =>
            {
                started = true;
                return Task.FromResult(new Recorded<string> { Value = "unused" });
            })
        );

        Assert.False(started);
        Assert.Empty(usage);
    }

    [Fact]
    public async Task Keeps_the_requested_model_and_no_invented_usage_when_a_call_fails()
    {
        var (ctx, usage) = Helpers.Context();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DecisionBudget.RecordedAsync<string>(ctx, "local", "requested-model", () =>
                throw new InvalidOperationException("local failure")
            )
        );

        Assert.Equal("local failure", error.Message);
        var call = Assert.Single(usage);
        Assert.Equal("local", call.Provider);
        Assert.Equal("requested-model", call.ModelId);
        Assert.Null(call.InputTokens);
    }
}
