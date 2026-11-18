// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests;

/// <summary>Upstream's <c>tests/helpers.ts</c> pieces the decision tests share.</summary>
internal static class Helpers
{
    /// <summary>Upstream's <c>context</c>: a step context whose budgets keep what each model call recorded.</summary>
    public static (StepExecutorContext Ctx, List<ExecutorModelCall> Usage) Context(CancellationToken signal = default)
    {
        var usage = new List<ExecutorModelCall>();
        return (new HandBuiltContext(new RecordingBudgets(usage), signal), usage);
    }

    /// <summary>A decision model whose answers come from a function of the call.</summary>
    public sealed class ScriptedDecisionModel(Func<DecisionModelCall, DecisionModelResult> script) : IDecisionModel
    {
        public List<DecisionModelCall> Calls { get; } = [];

        public string Provider => "scripted";

        public string ModelId => "scripted-decision";

        public string SpecificationVersion { get; init; } = "v4";

        public IReadOnlyList<string> SupportedQuestionTypes { get; init; } = ["choice", "boolean", "score"];

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            Calls.Add(call);
            return Task.FromResult(script(call));
        }
    }

    private sealed class HandBuiltContext : StepExecutorContext
    {
        [SetsRequiredMembers]
        public HandBuiltContext(IExecutorBudgets budgets, CancellationToken signal)
        {
            Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = "Do the thing" };
            Attempt = new ExecutorAttempt
            {
                TestId = "test",
                AttemptId = "attempt",
                CancellationToken = signal,
            };
            Target = new ExecutorTargetInfo { Name = "web", Platform = "web" };
            Budgets = budgets;
            Actions = null!;
        }
    }

    private sealed class RecordingBudgets(List<ExecutorModelCall> usage) : IExecutorBudgets
    {
        public int MaxActions => 25;

        public int MaxModelCalls => 25;

        public int ActionsUsed() => 0;

        public int RemainingMs() => 60_000;

        public void RecordModelCall(ExecutorModelCall? call = null) => usage.Add(call ?? new ExecutorModelCall());

        public Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body) => body();
    }
}
