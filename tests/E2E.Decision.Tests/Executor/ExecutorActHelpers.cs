// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Engine;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Shorthands the executor tests of <c>executor.test.ts</c> share.</summary>
internal static class ExecutorActHelpers
{
    public static DecisionExecutorOptions Options(
        IDecisionModel model,
        double minProbability = 0,
        double minConfidence = 0,
        IReadOnlyDictionary<string, JsonElement>? providerOptions = null,
        IAgentModel? textModel = null
    ) =>
        new()
        {
            Model = model,
            MinProbability = minProbability,
            MinConfidence = minConfidence,
            ProviderOptions = providerOptions,
            TextModel = textModel,
        };

    public static Task<StepVerdict> RunAsync(
        IDecisionModel model,
        Fixture fixture,
        DecisionExecutorOptions? options = null
    ) =>
        global::E2E
            .Decision.Executor.DecisionExecutor(options ?? Options(model))
            .RunStepAsync(fixture.Ctx, CancellationToken.None);

    public static ScriptedAnswer Pick(string choice) => new(choice);

    /// <summary>The usual script: the operation by call number, holds for a verdict, the first key otherwise.</summary>
    public static Func<string, IReadOnlyList<string>, int, ScriptedAnswer> Script(Func<int, string> operation) =>
        (id, keys, call) =>
            new ScriptedAnswer(
                id == "operation" ? operation(call)
                : id == "verdict" ? "holds"
                : keys.FirstOrDefault() ?? ""
            );

    public static void AssertVerdict(StepVerdict verdict, StepVerdictStatus status, string? code = null)
    {
        Assert.Equal(status, verdict.Status);
        if (code is not null)
        {
            Assert.Equal(code, verdict.ErrorCode);
        }
    }

    public static ExecutorObservation Look(IReadOnlyList<ExecutorNode> tree, string text = "#save button") =>
        new(text)
        {
            Revision = "1",
            Viewport = new TraceViewport(800, 600),
            Tree = tree,
        };
}
