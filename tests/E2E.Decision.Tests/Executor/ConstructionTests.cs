// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('construction')</c> of <c>executor.test.ts</c>: construction-time validation, gates and model support.</summary>
public sealed class ConstructionTests
{
    private const string Example =
        "pass a decision model, e.g. DecisionExecutor(new DecisionExecutorOptions { Model = new OpenAIDecisionModel(...) })";

    private static IDecisionModel Done() => ScriptedDecision((_, _, _) => Pick("done")).Model;

    private static IStepExecutor Build(DecisionExecutorOptions options) =>
        global::E2E.Decision.Executor.DecisionExecutor(options);

    private static void AssertMessage(Action factory, string message)
    {
        var error = Assert.Throws<ConfigurationException>(factory);
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    [InlineData(double.NaN)]
    public void Rejects_gates_outside_0_1(double bad)
    {
        var model = Done();
        AssertInvalidConfig(() => Build(Options(model, minProbability: bad)), "between 0 and 1");
        AssertInvalidConfig(() => Build(Options(model, minConfidence: bad)), "between 0 and 1");
    }

    [Fact]
    public void Rejects_no_options_as_INVALID_CONFIG_naming_the_fix() =>
        AssertMessage(() => Build(null!), "DecisionExecutor() takes an options object; " + Example);

    [Fact]
    public void Rejects_no_model_as_INVALID_CONFIG_naming_the_fix() =>
        AssertMessage(() => Build(Options(null!)), "DecisionExecutor({ model }) got null; " + Example);

    [Fact]
    public void Rejects_a_v3_model_as_INVALID_CONFIG_naming_the_fix() =>
        AssertMessage(
            () =>
                Build(
                    Options(
                        new ScriptedDecisionModel(_ => throw new InvalidOperationException())
                        {
                            SpecificationVersion = "v3",
                        }
                    )
                ),
            "DecisionExecutor({ model }) got scripted/scripted-decision, a v3 model; the decision call decides with v4 decision models"
        );

    [Fact]
    public void Rejects_a_decision_model_without_choice_questions_as_INVALID_CONFIG_naming_the_fix() =>
        AssertMessage(
            () => Build(Options(ScriptedDecision((_, _, _) => Pick("done"), supported: ["boolean"]).Model)),
            "DecisionExecutor({ model }) got scripted/scripted-1, which does not answer choice questions"
        );

    [Fact]
    public void Accepts_a_decision_model_and_a_deprecated_evaluation_model()
    {
        Assert.Equal("decision", Build(Options(Done())).Name);
        var (legacy, _) = ScriptedLegacyDecision((_, _, _) => Pick("done"));
        Assert.Equal("decision", Build(new DecisionExecutorOptions { EvaluationModel = legacy }).Name);
    }

    [Fact]
    public void Rejects_providerOptions_that_are_a_provider_mapped_to_a_non_object()
    {
        var providerOptions = new Dictionary<string, JsonElement>
        {
            ["gateway"] = JsonSerializer.SerializeToElement(true),
        };
        AssertInvalidConfig(
            () => Build(Options(Done(), providerOptions: providerOptions)),
            "maps provider names to option objects"
        );
    }

    [Fact]
    public void Exposes_the_text_model_and_replays_cache()
    {
        var text = ScriptedText();
        var executor = Build(Options(Done(), textModel: text));
        Assert.Equal(ExecutorCache.Inherit, executor.Cache);
        Assert.Same(text, executor.Model);
        Assert.Equal("decision", executor.Name);
    }
}
