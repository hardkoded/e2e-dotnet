// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Internal;

namespace E2E.Decision;

/// <summary>One call's boolean answers and what it spent (upstream's <c>BooleanAnswers</c>).</summary>
public sealed class BooleanAnswers
{
    /// <summary>Each answer's probability of true, by question id; null where the model gave none.</summary>
    public required IReadOnlyDictionary<string, double?> Probabilities { get; init; }

    public required DecisionUsage Usage { get; init; }

    public JsonElement? ProviderMetadata { get; init; }
}

/// <summary>
/// One gated answer: the chosen option, its probability, and the provider confidence. A score answer reads its most probable level as the
/// choice, carries the probability-weighted level as <see cref="Score"/>, and its probability is the mass on the levels within half a level
/// of that score: a spread over two adjacent columns is a precise position, not an uncertain one.
/// </summary>
public sealed record Decision(string Choice, double Probability, double Confidence, double? Score = null);

/// <summary>Upstream's <c>decide.ts</c>.</summary>
public static class Decide
{
    /// <summary>Transport retries per decide call.</summary>
    private const int MaxRetries = 3;

    /// <summary>
    /// Upstream's <c>decideBooleans</c>. Asks <paramref name="questions"/>, instructions by id, as boolean questions in one decide call
    /// against <paramref name="state"/>, for the test filter.
    /// </summary>
    public static async Task<BooleanAnswers> DecideBooleansAsync(
        DecisionFilterOptions options,
        IReadOnlyList<DecisionStatePart> state,
        IReadOnlyDictionary<string, string> questions,
        CancellationToken abortSignal
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await ExperimentalDecide
            .RunAsync(
                new DecideOptions
                {
                    Model = options.Model!,
                    State = state,
                    Questions = questions.ToDictionary(
                        pair => pair.Key,
                        pair => DecisionQuestion.Boolean(pair.Value),
                        StringComparer.Ordinal
                    ),
                    ProviderOptions = options.ProviderOptions,
                    MaxRetries = MaxRetries,
                },
                abortSignal
            )
            .ConfigureAwait(false);
        return new BooleanAnswers
        {
            Probabilities = questions.Keys.ToDictionary(
                id => id,
                id => result.Answers.TryGetValue(id, out var answer) ? answer.Probability : null,
                StringComparer.Ordinal
            ),
            Usage = result.Usage,
            ProviderMetadata = result.ProviderMetadata,
        };
    }

    /// <summary>
    /// Upstream's <c>decide</c>. Makes one decide call under the step budget and returns the answers. This is the only file that touches the
    /// decide API, so a rename there stays a one-file change.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, Decision>> DecideAsync(
        StepExecutorContext ctx,
        IDecisionModel model,
        DecisionRequest request,
        IReadOnlyDictionary<string, JsonElement>? providerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);
        var signal = cancellationToken.CanBeCanceled ? cancellationToken : ctx.Attempt.CancellationToken;
        // The screenshot rides beside the JSON state as a file part; a text-only provider rejects it before the request.
        List<DecisionStatePart> state = [new DecisionJsonPart(request.State)];
        if (request.Screenshot is { } screenshot)
        {
            state.Add(new DecisionFilePart(screenshot.MediaType, screenshot.Data));
        }

        DecisionResult result;
        try
        {
            result = await Budget
                .RecordedAsync(ctx, model.Provider, model.ModelId, signal, async () =>
                {
                    var decided = await ExperimentalDecide
                        .RunAsync(
                            new DecideOptions
                            {
                                Model = model,
                                State = state,
                                Questions = request.Questions,
                                ProviderOptions = providerOptions,
                                // Transport retries for a rate limit or a 5xx, as the runner's own adapter has; a bad answer never retries.
                                MaxRetries = MaxRetries,
                            },
                            signal
                        )
                        .ConfigureAwait(false);
                    return new Recorded<DecisionResult>
                    {
                        Value = decided,
                        InputTokens = decided.Usage.InputTokens,
                        OutputTokens = decided.Usage.OutputTokens,
                        ModelId = decided.ResponseModelId,
                    };
                })
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            signal.ThrowIfCancellationRequested();
            throw DecideError(error);
        }

        var decisions = new Dictionary<string, Decision>(StringComparer.Ordinal);
        foreach (var (id, answer) in result.Answers)
        {
            decisions[id] = Selected(answer) with { Confidence = ReportedConfidence(result.ProviderMetadata, id) };
        }

        return decisions;
    }

    /// <summary>
    /// Upstream's <c>selected</c>: the chosen option and its probability. An answer with no distribution, as a language model answering
    /// through a gateway fallback returns, is a committed choice: all the mass on it, and no confidence to report. Anything else without a
    /// valid distribution is <c>MODEL_OUTPUT_INVALID</c>.
    /// </summary>
    private static Decision Selected(DecisionAnswer? answer)
    {
        if (answer is null || answer.Type is not ("choice" or "score"))
        {
            throw new AgentException("MODEL_OUTPUT_INVALID", "The decision model returned no choice or score answer.");
        }

        if (answer.Type == "score")
        {
            if (answer.Score is not { } score || !double.IsFinite(score))
            {
                throw new AgentException(
                    "MODEL_OUTPUT_INVALID",
                    "The decision model returned a score answer without a score."
                );
            }

            if (answer.Probabilities is null)
            {
                return new Decision(score.ToString(CultureInfo.InvariantCulture), 1, 0, score);
            }

            var top = (Choice: "", Probability: -1.0);
            var nearby = 0.0;
            foreach (var (level, probability) in answer.Probabilities)
            {
                // On a tie the lower level wins, as JavaScript's ascending integer-key order does upstream.
                if (
                    probability > top.Probability
                    || (
                        probability == top.Probability
                        && double.Parse(level, CultureInfo.InvariantCulture)
                            < double.Parse(top.Choice, CultureInfo.InvariantCulture)
                    )
                )
                {
                    top = (level, probability);
                }

                if (Math.Abs(double.Parse(level, CultureInfo.InvariantCulture) - score) <= 0.5)
                {
                    nearby += probability;
                }
            }

            return new Decision(top.Choice, Math.Min(1, nearby), 0, score);
        }

        var choice = answer.Choice ?? "";
        if (answer.Probabilities is null)
        {
            return new Decision(choice, 1, 0);
        }

        var chosen = answer.Probabilities.TryGetValue(choice, out var value) ? value : 0;
        if (!double.IsFinite(chosen) || chosen < 0 || chosen > 1)
        {
            throw new AgentException(
                "MODEL_OUTPUT_INVALID",
                "The decision model returned a probability outside [0, 1]."
            );
        }

        return new Decision(choice, chosen, 0);
    }

    /// <summary>Upstream's <c>reportedConfidence</c>: the provider-reported confidence for one question, or 0 when absent.</summary>
    private static double ReportedConfidence(JsonElement? metadata, string id)
    {
        if (metadata is not { ValueKind: JsonValueKind.Object } sections)
        {
            return 0;
        }

        foreach (var section in sections.EnumerateObject())
        {
            if (
                section.Value.ValueKind == JsonValueKind.Object
                && section.Value.TryGetProperty("confidence", out var confidence)
                && confidence.ValueKind == JsonValueKind.Object
                && confidence.TryGetProperty(id, out var value)
                && value.ValueKind == JsonValueKind.Number
                && double.IsFinite(value.GetDouble())
            )
            {
                return value.GetDouble();
            }
        }

        return 0;
    }

    /// <summary>Upstream's <c>decideError</c>: maps decide failures onto the runner error codes without echoing provider text.</summary>
    private static Exception DecideError(Exception error)
    {
        switch (error)
        {
            case AgentException or ConfigurationException:
                return error;
            case LoadApiKeyException missing:
                return ApiKey.MissingKey("decision", missing);
            case InvalidArgumentException:
                return error;
            case DecisionRefusalException refusal:
                var which =
                    refusal.QuestionIds.Count == 0
                        ? "a question"
                        : string.Join(", ", refusal.QuestionIds.Select(id => JsonSerializer.Serialize(id)));
                return new AgentException(
                    "MODEL_OUTPUT_INVALID",
                    "The decision model refused to answer " + which + "."
                );
            case InvalidResponseDataException:
                return new AgentException("MODEL_OUTPUT_INVALID", "The decision model returned an invalid answer.");
            case ApiCallException { InnerException: AgentException provider }:
                return provider;
            case RetryError { LastError: { } last }:
                return DecideError(last);
            default:
                return new AgentException("MODEL_PROVIDER_FAILED", "The decision model call failed.");
        }
    }
}
