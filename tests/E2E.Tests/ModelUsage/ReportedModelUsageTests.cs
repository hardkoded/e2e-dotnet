// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;
using E2E.Tests.AgentAct;
using E2E.Tests.Traces;

namespace E2E.Tests.ModelUsage;

/// <summary>
/// Upstream's <c>tests/integration/model-usage.test.ts</c>, <c>describe('reported model usage')</c>. Both tests are partial: the
/// port has no <c>runExisting</c> harness or report schema validator, the cost comes from <c>ModelCallUsage.EstimatedCostUsd</c>
/// (upstream reads the AI Gateway metadata), and a <c>ModelCallUsage</c> token count is an <c>int</c>, so the first test sums at the
/// run level over steps whose tokens are set directly, and the second sends only <c>-1</c>, not <c>0.5</c> or <c>NaN</c>.
/// </summary>
public sealed class ReportedModelUsageTests
{
    private static async Task<E2ESession> JudgeAsync(ModelCallUsage usage, params string[] statements)
    {
        var model = new ScriptedModel(_ => new ModelResponse { ToolCalls = ModelResponses.Done("passed", "ready").ToolCalls, Usage = usage });
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "usage > case",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        await session.Context.App.OpenAsync("/");
        foreach (var statement in statements)
        {
            await session.Context.Agent.AssertAsync(statement);
        }

        session.Complete(null);
        return session;
    }

    [Fact]
    public async Task Partial_Keeps_run_totals_representable_when_separate_steps_overflow_their_sum()
    {
        await using var session = await JudgeAsync(new ModelCallUsage { InputTokens = 1, OutputTokens = 0, EstimatedCostUsd = double.MaxValue }, "ready", "still ready");

        var steps = session.Steps.Where(step => step.Api == "agent.assert").ToList();
        Assert.Equal([double.MaxValue, double.MaxValue], steps.Select(step => step.Model?.EstimatedCostUsd).ToArray());
        foreach (var step in steps)
        {
            step.Model = step.Model! with { InputTokens = E2E.ModelUsage.MaxSafeInteger };
        }

        var usage = BuildReport.ComputeUsage([ReportBuilders.Result(attempts: [ReportBuilders.Attempt(steps: steps)])], 1);

        Assert.Equal(E2E.ModelUsage.MaxSafeInteger, usage.ModelTokens);
        Assert.Null(usage.EstimatedCostUsd);
    }

    [Fact]
    public async Task Partial_Sanitizes_malformed_judgment_counters_before_recording_events()
    {
        await using var session = await JudgeAsync(new ModelCallUsage { InputTokens = -1, OutputTokens = 3, EstimatedCostUsd = 0.25 }, "ready");

        var step = session.Steps.Single(item => item.Api == "agent.assert");
        Assert.Equal(3, step.Events.First(item => item.Kind == StepEventKind.Model).Count);
        var model = step.Model!;
        Assert.Equal(1, model.Calls);
        Assert.Equal(0, model.InputTokens);
        Assert.Equal(3, model.OutputTokens);
        Assert.Equal(3, model.PeakTokensPerCall);
        Assert.Equal(TokenAccounting.AdapterUpperBound, model.TokenAccounting);
        Assert.Equal(0.25, model.EstimatedCostUsd);
    }
}
