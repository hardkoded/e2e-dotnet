// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Engine;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>What the <c>executor.test.ts</c> tests of the gates, assertions, failures, observations, context, and grammar share.</summary>
internal static class ExecutorContextHelpers
{
    /// <summary>Upstream's <c>decisionExecutor({ ... }).runStep(ctx)</c>.</summary>
    public static Task<StepVerdict> Run(
        IDecisionModel model,
        Fixture fixture,
        IAgentModel? textModel = null,
        double minProbability = 0,
        double minConfidence = 0
    ) =>
        global::E2E
            .Decision.Executor.DecisionExecutor(
                new DecisionExecutorOptions
                {
                    Model = model,
                    TextModel = textModel,
                    MinProbability = minProbability,
                    MinConfidence = minConfidence,
                }
            )
            .RunStepAsync(fixture.Ctx, CancellationToken.None);

    /// <summary>The observation of look number <paramref name="look"/> over <paramref name="tree"/>, as the fixture's default builds it.</summary>
    public static ExecutorObservation Obs(IReadOnlyList<ExecutorNode> tree, int look, string? path = null) =>
        new("x")
        {
            Revision = (look + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Viewport = new TraceViewport(800, 600),
            Tree = tree,
            Path = path,
        };

    /// <summary>A tree with a ticking text node appended, so every look differs from the last.</summary>
    public static IReadOnlyList<ExecutorNode> Ticking(IReadOnlyList<ExecutorNode> tree, int look) =>
        [.. tree, new ExecutorNode { Id = "t" + (look + 1), Text = "tick " + (look + 1) }];

    public static IReadOnlyDictionary<string, JsonElement> ParseParams(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    /// <summary>Every string in a JSON value, keys and values.</summary>
    public static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var text in Strings(item))
                    {
                        yield return text;
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var text in Strings(property.Value))
                    {
                        yield return text;
                    }
                }

                break;
        }
    }

    public static Dictionary<string, double> Spread(IReadOnlyList<string> keys, string choice, double mass)
    {
        var rest = (1 - mass) / Math.Max(1, keys.Count - 1);
        return keys.ToDictionary(key => key, key => key == choice ? mass : rest);
    }

    /// <summary>The usual script: the operation question gets <paramref name="operation"/>, the verdict question <paramref name="verdict"/>, any other the first key.</summary>
    public static ScriptedAnswer Pick(
        string id,
        IReadOnlyList<string> keys,
        string operation,
        string verdict = "holds"
    ) =>
        new(
            id == "operation" ? operation
            : id == "verdict" ? verdict
            : keys.Count > 0 ? keys[0]
            : ""
        );

    public static EvalRequest VerdictRequest(IEnumerable<EvalRequest> requests) =>
        requests.First(request => request.Questions.ContainsKey("verdict"));

    /// <summary>A model that answers every question with a refusal, the way the SDK reports one.</summary>
    public sealed class RefusingModel : IDecisionModel
    {
        public string Provider => "scripted";

        public string ModelId => "scripted-1";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["choice"];

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
            Task.FromResult(
                new DecisionModelResult
                {
                    Answers = call.Questions.ToDictionary(
                        pair => pair.Key,
                        pair => new DecisionAnswer { Type = "refusal" }
                    ),
                    Usage = new DecisionUsage(1, 0),
                    Rounding = new DecisionRounding(6, 6),
                }
            );
    }
}
