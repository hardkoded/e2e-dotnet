// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Engine;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.ExecutorVision;

/// <summary>Pieces <c>executor-vision.test.ts</c> shares.</summary>
internal static class VisionTestHelpers
{
    /// <summary>Upstream's <c>PIXELS</c>: bytes no decoder reads, so the screenshot goes through untouched.</summary>
    public static ExecutorPixels Undecodable { get; } = new([1, 2, 3], "image/png", 800, 600, 1, 0);

    /// <summary>Builds the decision executor.</summary>
    public static IStepExecutor Build(
        IDecisionModel model,
        IAgentModel? text = null,
        bool vision = true,
        double minProbability = 0,
        double minConfidence = 0
    ) =>
        global::E2E.Decision.Executor.DecisionExecutor(
            new DecisionExecutorOptions
            {
                Model = model,
                TextModel = text,
                Vision = vision,
                MinProbability = minProbability,
                MinConfidence = minConfidence,
            }
        );

    public static Task<StepVerdict> Run(IStepExecutor executor, Fixture fixture) =>
        executor.RunStepAsync(fixture.Ctx, CancellationToken.None);

    /// <summary>Upstream's <c>scriptedOutputs(Array.from({ length }, () => ({ target })))</c>.</summary>
    public static ScriptedTextModel Targets(int length, string target = "the Add key") =>
        ScriptedOutputs(
            Enumerable.Range(0, length).Select(_ => (object?)new Dictionary<string, object?> { ["target"] = target })
        );

    public static IReadOnlyList<string> Criteria(EvalRequest request, string question = "operation") =>
        request.Questions[question].Keys;

    public static Func<ExecutorObservation, ExecutorObservation> WithPixels(ExecutorPixels pixels) =>
        observation => observation with { Pixels = pixels };

    /// <summary>One look of upstream's <c>observe.mockImplementation</c>: a text of <c>x</c>, the fixture viewport, a tree, and pixels.</summary>
    public static ExecutorObservation Look(int revision, IReadOnlyList<ExecutorNode> tree, ExecutorPixels pixels) =>
        new("x")
        {
            Revision = revision.ToString(CultureInfo.InvariantCulture),
            Viewport = new TraceViewport(800, 600),
            Tree = tree,
            Pixels = pixels,
        };

    public static JsonElement Prop(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            element = element.GetProperty(name);
        }

        return element;
    }

    public static Point TapPoint(Fixture fixture, int nth) => (Point)fixture.Actions.Of("tapAt").ElementAt(nth)[0]!;
}
