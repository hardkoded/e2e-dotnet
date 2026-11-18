// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.DecisionModels;

/// <summary>
/// .NET-only: the decide call (<c>ExperimentalDecide</c>, the AI SDK's <c>experimental_decide</c>), <c>Decide.DecideAsync</c>, and
/// <c>Model.CheckModel</c>. Upstream covers them only through the decision executor and filter, which wait for #1770 and #1786; these
/// stand in until those tests port.
/// </summary>
public sealed class DecideTests
{
    private static readonly IReadOnlyDictionary<string, DecisionQuestion> Operation = new Dictionary<
        string,
        DecisionQuestion
    >
    {
        ["operation"] = DecisionQuestion.Choice(null, [new DecisionChoice("tap"), new DecisionChoice("done")]),
    };

    private static DecisionRequest Request() =>
        new() { State = JsonDocument.Parse("{}").RootElement, Questions = Operation };

    private static DecisionModelResult Answer(DecisionAnswer answer, string? metadata = null) =>
        new()
        {
            Answers = new Dictionary<string, DecisionAnswer> { ["operation"] = answer },
            Rounding = new DecisionRounding(2, 2),
            ProviderMetadata = metadata is null ? null : JsonDocument.Parse(metadata).RootElement,
            Usage = new DecisionUsage(10, 2),
        };

    [Fact]
    public async Task Reads_the_chosen_probability_and_the_provider_confidence_and_records_the_call()
    {
        var model = new ScriptedDecisionModel(_ =>
            Answer(
                new DecisionAnswer
                {
                    Type = "choice",
                    Choice = "tap",
                    Probabilities = new Dictionary<string, double> { ["tap"] = 0.9, ["done"] = 0.1 },
                },
                """{"openai":{"confidence":{"operation":0.7}}}"""
            )
        );
        var (ctx, usage) = Context();

        var decisions = await Decide.DecideAsync(ctx, model, Request());

        Assert.Equal(new Decision("tap", 0.9, 0.7), decisions["operation"]);
        Assert.Equal(3, model.Calls[0].MaxRetries);
        var call = Assert.Single(usage);
        Assert.Equal(("scripted", 10, 2), (call.Provider, call.InputTokens, call.OutputTokens));
    }

    [Fact]
    public async Task Counts_an_answer_without_a_distribution_as_a_committed_choice()
    {
        var model = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "choice", Choice = "done" }));

        var decisions = await Decide.DecideAsync(Context().Ctx, model, Request());

        Assert.Equal(new Decision("done", 1, 0), decisions["operation"]);
    }

    [Fact]
    public async Task Names_a_refusal_as_MODEL_OUTPUT_INVALID()
    {
        var model = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "refusal" }));

        var error = await Assert.ThrowsAsync<AgentException>(() => Decide.DecideAsync(Context().Ctx, model, Request()));

        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.Equal("The decision model refused to answer \"operation\".", error.Message);
    }

    [Fact]
    public async Task Maps_a_missing_key_to_MODEL_UNAVAILABLE_naming_the_variable()
    {
        var model = new ScriptedDecisionModel(_ =>
            throw new LoadApiKeyException(
                "OpenAI API key is missing. Pass it using the 'apiKey' parameter or the OPENAI_API_KEY environment variable."
            )
        );

        var error = await Assert.ThrowsAsync<AgentException>(() => Decide.DecideAsync(Context().Ctx, model, Request()));

        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        Assert.Equal("Set OPENAI_API_KEY to the decision model API key.", error.Message);
    }

    [Fact]
    public async Task Rejects_an_answer_that_breaks_its_distribution_without_echoing_it()
    {
        var model = new ScriptedDecisionModel(_ =>
            Answer(
                new DecisionAnswer
                {
                    Type = "choice",
                    Choice = "tap",
                    Probabilities = new Dictionary<string, double> { ["tap"] = 0.2, ["done"] = 0.2 },
                }
            )
        );

        var error = await Assert.ThrowsAsync<AgentException>(() => Decide.DecideAsync(Context().Ctx, model, Request()));

        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.Equal("The decision model returned an invalid answer.", error.Message);
    }

    [Fact]
    public async Task Reads_a_score_answer_as_its_most_probable_level_with_the_mass_near_the_score()
    {
        var model = new ScriptedDecisionModel(_ => new DecisionModelResult
        {
            Answers = new Dictionary<string, DecisionAnswer>
            {
                ["operation"] = new()
                {
                    Type = "score",
                    Score = 1.5,
                    Probabilities = new Dictionary<string, double>
                    {
                        ["0"] = 0,
                        ["1"] = 0.5,
                        ["2"] = 0.5,
                    },
                },
            },
        });
        var request = new DecisionRequest
        {
            State = JsonDocument.Parse("{}").RootElement,
            Questions = new Dictionary<string, DecisionQuestion>
            {
                ["operation"] = DecisionQuestion.Score(null, ["a", "b", "c"]),
            },
        };

        var decisions = await Decide.DecideAsync(Context().Ctx, model, request);

        Assert.Equal(new Decision("1", 1, 0, 1.5), decisions["operation"]);
    }

    [Fact]
    public async Task Resolves_a_score_tie_to_the_lower_level()
    {
        var model = new ScriptedDecisionModel(_ => new DecisionModelResult
        {
            Answers = new Dictionary<string, DecisionAnswer>
            {
                ["operation"] = new()
                {
                    Type = "score",
                    Score = 1.5,
                    Probabilities = new Dictionary<string, double>
                    {
                        ["2"] = 0.5,
                        ["1"] = 0.5,
                        ["0"] = 0,
                    },
                },
            },
        });
        var request = new DecisionRequest
        {
            State = JsonDocument.Parse("{}").RootElement,
            Questions = new Dictionary<string, DecisionQuestion>
            {
                ["operation"] = DecisionQuestion.Score(null, ["a", "b", "c"]),
            },
        };

        var decisions = await Decide.DecideAsync(Context().Ctx, model, request);

        Assert.Equal("1", decisions["operation"].Choice);
    }

    [Fact]
    public async Task Maps_a_model_own_timeout_to_MODEL_PROVIDER_FAILED_while_the_attempt_is_live()
    {
        var model = new ScriptedDecisionModel(_ => throw new TaskCanceledException("timed out"));

        var error = await Assert.ThrowsAsync<AgentException>(() => Decide.DecideAsync(Context().Ctx, model, Request()));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
    }

    [Fact]
    public async Task Rejects_a_v3_model_a_repeated_choice_id_and_undefined_state_before_dispatch()
    {
        var v3 = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "choice", Choice = "tap" }))
        {
            SpecificationVersion = "v3",
        };
        var repeated = new Dictionary<string, DecisionQuestion>
        {
            ["operation"] = DecisionQuestion.Choice(null, [new DecisionChoice("tap"), new DecisionChoice("tap")]),
        };
        var ok = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "choice", Choice = "tap" }));

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = v3,
                    State = [new DecisionTextPart("x")],
                    Questions = Operation,
                }
            )
        );
        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = ok,
                    State = [new DecisionTextPart("x")],
                    Questions = repeated,
                }
            )
        );
        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = ok,
                    State = [new DecisionJsonPart(default)],
                    Questions = Operation,
                }
            )
        );
        Assert.Empty(v3.Calls);
        Assert.Empty(ok.Calls);
    }

    [Fact]
    public async Task Surfaces_a_caller_cancel_as_cancellation()
    {
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        var model = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "choice", Choice = "tap" }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Decide.DecideAsync(Context(controller.Token).Ctx, model, Request())
        );
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task Partial_rejects_unsupported_types_before_deciding_any_answer()
    {
        var model = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "choice", Choice = "tap" }))
        {
            SupportedQuestionTypes = ["boolean"],
        };

        var error = await Assert.ThrowsAsync<DecisionUnsupportedQuestionTypeException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = model,
                    State = [new DecisionTextPart("x")],
                    Questions = Operation,
                }
            )
        );
        Assert.Equal("operation", error.QuestionId);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public void Checks_a_model_at_config_load_naming_the_fix()
    {
        var needs = new ModelNeeds("DecisionExecutor", "choice", "e.g. new OpenAIDecisionModel(\"gpt-6-luna\")");
        var booleans = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "refusal" }))
        {
            SupportedQuestionTypes = ["boolean"],
        };
        var v3 = new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "refusal" }))
        {
            SpecificationVersion = "v3",
        };

        Model.CheckModel(new ScriptedDecisionModel(_ => Answer(new DecisionAnswer { Type = "refusal" })), needs);
        Assert.Equal(
            "DecisionExecutor({ model }) got the string \"gpt-6-luna\"; e.g. new OpenAIDecisionModel(\"gpt-6-luna\")",
            Assert.Throws<ConfigurationException>(() => Model.CheckModel("gpt-6-luna", needs)).Message
        );
        Assert.Equal(
            "DecisionExecutor({ model }) got scripted/scripted-decision, which does not answer choice questions",
            Assert.Throws<ConfigurationException>(() => Model.CheckModel(booleans, needs)).Message
        );
        Assert.Equal(
            "DecisionExecutor({ model }) got scripted/scripted-decision, a v3 model; the decision call decides with v4 decision models",
            Assert.Throws<ConfigurationException>(() => Model.CheckModel(v3, needs)).Message
        );
        Assert.All(
            new Action[]
            {
                () => Model.CheckModel(null, needs),
                () => Model.CheckModel(new object(), needs),
                () => Model.CheckModel((Func<int>)(() => 1), needs),
            },
            check => Assert.Equal("INVALID_CONFIG", Assert.Throws<ConfigurationException>(check).Code)
        );
    }

    [Fact]
    public void Checks_provider_options_map_provider_names_to_objects()
    {
        Model.CheckProviderOptions(null, "DecisionExecutor");
        Model.CheckProviderOptions(
            new Dictionary<string, JsonElement>
            {
                ["gateway"] = JsonDocument.Parse("""{"zeroDataRetention":true}""").RootElement,
            },
            "DecisionExecutor"
        );

        var error = Assert.Throws<ConfigurationException>(() =>
            Model.CheckProviderOptions(
                new Dictionary<string, JsonElement> { ["gateway"] = JsonDocument.Parse("true").RootElement },
                "DecisionExecutor"
            )
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
    }
}
