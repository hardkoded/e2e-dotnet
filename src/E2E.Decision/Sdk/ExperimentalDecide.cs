// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>What <see cref="ExperimentalDecide.RunAsync"/> takes (the arguments of the AI SDK's <c>experimental_decide</c>).</summary>
public sealed class DecideOptions
{
    public required IDecisionModel Model { get; init; }

    public required IReadOnlyList<DecisionStatePart> State { get; init; }

    public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }

    /// <summary>The most output tokens, for models that support it. At least 1.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Reasoning effort, for models that support it.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Retries of transient provider failures. Null keeps the provider's own.</summary>
    public int? MaxRetries { get; init; }

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The provider options, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
}

/// <summary>
/// The AI SDK's <c>experimental_decide</c> (<c>ai/src/decide/decide.ts</c>) and its input and answer validation
/// (<c>validate-decision.ts</c>), which the SDK keeps in its <c>ai</c> package and the port has no such package for. .NET-only home.
/// </summary>
public static class ExperimentalDecide
{
    private const double Tolerance = 1e-6;

    /// <summary>Decides answers to typed questions against one shared state.</summary>
    public static async Task<DecisionResult> RunAsync(
        DecideOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var model = options.Model;
        if (options.MaxOutputTokens is < 1)
        {
            throw new InvalidArgumentException("maxOutputTokens", "must be an integer >= 1");
        }

        if (model.SpecificationVersion != "v4")
        {
            throw new InvalidArgumentException(
                "model",
                "got "
                    + model.Provider
                    + "/"
                    + model.ModelId
                    + ", a "
                    + model.SpecificationVersion
                    + " model; only v4 decision models decide"
            );
        }

        ValidateInput(options.State, options.Questions);
        foreach (var (id, question) in options.Questions)
        {
            if (!model.SupportedQuestionTypes.Contains(question.Type, StringComparer.Ordinal))
            {
                throw new DecisionUnsupportedQuestionTypeException(id, question.Type, model.Provider, model.ModelId);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await model
            .DoDecideAsync(
                new DecisionModelCall
                {
                    State = options.State,
                    Questions = options.Questions,
                    MaxOutputTokens = options.MaxOutputTokens,
                    Reasoning = options.Reasoning,
                    MaxRetries = options.MaxRetries,
                    Headers = options.Headers ?? new Dictionary<string, string>(),
                    ProviderOptions = options.ProviderOptions,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAnswers(options.Questions, result.Answers, result.Rounding);
        var refused = options.Questions.Keys.Where(id => result.Answers[id].Type == "refusal").ToList();
        if (refused.Count > 0)
        {
            throw new DecisionRefusalException(refused, model.Provider, model.ModelId);
        }

        return new DecisionResult
        {
            Answers = result.Answers,
            Usage = new DecisionUsage(result.Usage?.InputTokens, result.Usage?.OutputTokens),
            Warnings = result.Warnings,
            Rounding = result.Rounding,
            ProviderMetadata = result.ProviderMetadata,
            ResponseModelId = result.ResponseModelId ?? model.ModelId,
        };
    }

    /// <summary>The AI SDK's <c>validateDecisionInput</c>: the state and the questions, before any request.</summary>
    internal static void ValidateInput(
        IReadOnlyList<DecisionStatePart>? state,
        IReadOnlyDictionary<string, DecisionQuestion>? questions
    )
    {
        if (
            state is null
            || state.Any(part => part is null or DecisionJsonPart { Value.ValueKind: JsonValueKind.Undefined })
        )
        {
            throw new InvalidArgumentException("state", "must be text, a JSON value, or a list of parts");
        }

        if (questions is null || questions.Count == 0)
        {
            throw new InvalidArgumentException("questions", "must be a nonempty question map");
        }

        foreach (var (id, question) in questions)
        {
            var parameter = "questions." + id;
            if (question is null)
            {
                throw new InvalidArgumentException(parameter, "question must not be null");
            }

            switch (question.Type)
            {
                case "box":
                    break;
                case "choice":
                    if (question.Choices is not { Count: > 0 })
                    {
                        throw new InvalidArgumentException(parameter, "choice criteria must be a nonempty option map");
                    }

                    if (
                        question.Choices.Select(choice => choice.Value).Distinct(StringComparer.Ordinal).Count()
                        != question.Choices.Count
                    )
                    {
                        throw new InvalidArgumentException(parameter, "choice option ids must be unique");
                    }

                    break;
                case "score":
                    if (question.Levels is not { Count: > 0 })
                    {
                        throw new InvalidArgumentException(
                            parameter,
                            "score criteria must contain at least one ordered level"
                        );
                    }

                    break;
                case "boolean":
                    if (
                        question.Instructions is null
                        && question.TrueCriteria is null
                        && question.FalseCriteria is null
                    )
                    {
                        throw new InvalidArgumentException(
                            parameter,
                            "boolean questions require instructions or at least one non-null criterion"
                        );
                    }

                    break;
                default:
                    throw new InvalidArgumentException(
                        parameter,
                        "question type must be choice, score, boolean, or box"
                    );
            }
        }
    }

    /// <summary>The AI SDK's <c>validateDecisionAnswers</c>: one answer per question, each of the right type, in range, and consistent.</summary>
    internal static void ValidateAnswers(
        IReadOnlyDictionary<string, DecisionQuestion> questions,
        IReadOnlyDictionary<string, DecisionAnswer>? answers,
        DecisionRounding? rounding
    )
    {
        var probabilityError = RoundingError(rounding?.ProbabilityDecimals, answers);
        var scoreError = RoundingError(rounding?.ScoreDecimals, answers);
        if (answers is null || !SameKeys(answers.Keys, questions.Keys))
        {
            throw new InvalidResponseDataException(
                "Decision must return exactly one answer for every question.",
                answers
            );
        }

        foreach (var (id, question) in questions)
        {
            var answer = answers[id];
            if (answer is null || (answer.Type != question.Type && answer.Type != "refusal"))
            {
                throw new InvalidResponseDataException(
                    "Question \"" + id + "\" returned an answer with the wrong type.",
                    answers
                );
            }

            if (answer.Type == "refusal")
            {
                continue;
            }

            switch (question.Type)
            {
                case "choice":
                    ValidateChoice(id, question, answer, answers, probabilityError);
                    break;
                case "score":
                    ValidateScore(id, question, answer, answers, probabilityError, scoreError);
                    break;
                case "box":
                    var box = answer.Box;
                    if (
                        box is null
                        || !IsProbability(box.Left)
                        || !IsProbability(box.Right)
                        || !IsProbability(box.Top)
                        || !IsProbability(box.Bottom)
                        || !(box.Left < box.Right)
                        || !(box.Top < box.Bottom)
                    )
                    {
                        throw new InvalidResponseDataException(
                            "Question \""
                                + id
                                + "\" must return box edges in [0, 1] with left < right and top < bottom.",
                            answers
                        );
                    }

                    break;
                default:
                    if (answer.Probability is not { } probability || !IsProbability(probability))
                    {
                        throw new InvalidResponseDataException(
                            "Question \"" + id + "\" must return P(true) as a finite probability in [0, 1].",
                            answers
                        );
                    }

                    break;
            }
        }
    }

    private static void ValidateChoice(
        string id,
        DecisionQuestion question,
        DecisionAnswer answer,
        IReadOnlyDictionary<string, DecisionAnswer> answers,
        double probabilityError
    )
    {
        var options = question.Choices!.Select(choice => choice.Value).ToList();
        if (answer.Choice is null || !options.Contains(answer.Choice, StringComparer.Ordinal))
        {
            throw new InvalidResponseDataException("Question \"" + id + "\" selected an unknown option.", answers);
        }

        if (answer.Probabilities is not { } probabilities)
        {
            return;
        }

        ValidateDistribution(probabilities, options, answers, id, probabilityError);
        var selected = probabilities[answer.Choice];
        if (probabilities.Values.Any(probability => probability > selected + Tolerance + (2 * probabilityError)))
        {
            throw new InvalidResponseDataException(
                "Question \"" + id + "\" did not select a highest-probability option.",
                answers
            );
        }
    }

    private static void ValidateScore(
        string id,
        DecisionQuestion question,
        DecisionAnswer answer,
        IReadOnlyDictionary<string, DecisionAnswer> answers,
        double probabilityError,
        double scoreError
    )
    {
        var levels = question.Levels!.Count;
        if (answer.Score is not { } score || !double.IsFinite(score) || score < 0 || score > levels - 1)
        {
            throw new InvalidResponseDataException(
                "Question \""
                    + id
                    + "\" score must be in [0, "
                    + (levels - 1).ToString(CultureInfo.InvariantCulture)
                    + "].",
                answers
            );
        }

        if (answer.Probabilities is not { } probabilities)
        {
            return;
        }

        var keys = Enumerable.Range(0, levels).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToList();
        ValidateDistribution(probabilities, keys, answers, id, probabilityError);
        var mean = probabilities.Sum(item => int.Parse(item.Key, CultureInfo.InvariantCulture) * item.Value);
        var meanError = keys.Sum(index => int.Parse(index, CultureInfo.InvariantCulture) * probabilityError);
        if (Math.Abs(mean - score) > Tolerance + meanError + scoreError)
        {
            throw new InvalidResponseDataException(
                "Question \""
                    + id
                    + "\" score must equal the probability-weighted mean within the declared rounding precision.",
                answers
            );
        }
    }

    private static void ValidateDistribution(
        IReadOnlyDictionary<string, double> value,
        IReadOnlyList<string> keys,
        object answers,
        string id,
        double roundingError
    )
    {
        if (!SameKeys(value.Keys, keys) || value.Values.Any(probability => !IsProbability(probability)))
        {
            throw new InvalidResponseDataException(
                "Question \"" + id + "\" must have a complete distribution of finite probabilities in [0, 1].",
                answers
            );
        }

        if (Math.Abs(value.Values.Sum() - 1) > Tolerance + (keys.Count * roundingError))
        {
            throw new InvalidResponseDataException(
                "Question \"" + id + "\" probabilities must sum to 1 within the declared rounding precision.",
                answers
            );
        }
    }

    private static double RoundingError(int? decimals, object? answers)
    {
        if (decimals is null)
        {
            return 0;
        }

        if (decimals is < 0 or > 15)
        {
            throw new InvalidResponseDataException(
                "Decision rounding decimals must be integers between 0 and 15.",
                answers
            );
        }

        return 0.5 * Math.Pow(10, -decimals.Value);
    }

    private static bool IsProbability(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool SameKeys(IEnumerable<string> left, IEnumerable<string> right)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightList = right.ToList();
        return leftSet.Count == rightList.Count && rightList.All(leftSet.Contains);
    }
}
