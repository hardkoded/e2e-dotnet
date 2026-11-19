// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Internal;

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

    /// <summary>Retries of a call that fails with a retryable error. Null is the AI SDK's default, 2.</summary>
    public int? MaxRetries { get; init; }

    /// <summary>
    /// The download function URL file state needs (<c>experimental_download</c>). Use <see cref="CreateDownload.Create"/> with a transport that
    /// validates DNS at connection time for untrusted URLs. The model's own HTTP client is not used.
    /// </summary>
    public DownloadFunction? ExperimentalDownload { get; init; }

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The provider options, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>Optional telemetry configuration.</summary>
    public TelemetryOptions? Telemetry { get; init; }

    /// <summary>User-defined runtime context. Treat runtime context as immutable.</summary>
    public IReadOnlyDictionary<string, object?>? RuntimeContext { get; init; }

    /// <summary>Called when the decide operation begins.</summary>
    public Func<DecideStartEvent, Task>? OnStart { get; init; }

    /// <summary>Called when the decide operation completes.</summary>
    public Func<DecideEndEvent, Task>? OnEnd { get; init; }

    /// <summary>Internal. For test use only (<c>_internal.generateCallId</c>). May change without notice.</summary>
    internal Func<string>? GenerateCallId { get; init; }
}

/// <summary>
/// The AI SDK's <c>experimental_decide</c> (<c>ai/src/decide/decide.ts</c>) and its input and answer validation
/// (<c>validate-decision.ts</c>), which the SDK keeps in its <c>ai</c> package and the port has no such package for. .NET-only home.
/// </summary>
public static class ExperimentalDecide
{
    private const double Tolerance = 1e-6;

    /// <summary>The AI SDK's default <c>maxRetries</c>.</summary>
    private const int DefaultMaxRetries = 2;

    /// <summary>The AI SDK's first backoff, doubled for each retry.</summary>
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    private const int RetryBackoffFactor = 2;

    /// <summary>.NET-only: how long a telemetry integration may take to handle the error before the call's own error is rethrown.</summary>
    private static readonly TimeSpan ErrorCallbackTimeout = TimeSpan.FromSeconds(5);

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

        var maxRetries = options.MaxRetries ?? DefaultMaxRetries;
        if (maxRetries < 0)
        {
            throw new InvalidArgumentException("maxRetries", "maxRetries must be >= 0");
        }

        var callId = (options.GenerateCallId ?? GenerateCallId)();
        var telemetryDispatcher = new RestrictedTelemetryDispatcher(options.Telemetry);
        var startEvent = new DecideStartEvent
        {
            CallId = callId,
            RuntimeContext = options.RuntimeContext ?? new Dictionary<string, object?>(),
            Provider = model.Provider,
            ModelId = model.ModelId,
            State = options.State,
            Questions = options.Questions,
            MaxOutputTokens = options.MaxOutputTokens,
            Reasoning = options.Reasoning,
            MaxRetries = maxRetries,
            Headers = options.Headers,
            ProviderOptions = options.ProviderOptions ?? new Dictionary<string, JsonElement>(),
        };
        await Notify
            .NotifyAsync(startEvent, [options.OnStart, telemetryDispatcher.OnStartAsync], cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var preparedState = await DecisionState
                .PrepareAsync(options.State, options.ExperimentalDownload, cancellationToken)
                .ConfigureAwait(false);
            var modelCallEvent = new DecisionModelCallStartEvent
            {
                CallId = callId,
                Provider = model.Provider,
                ModelId = model.ModelId,
                State = preparedState,
                Questions = options.Questions,
            };
            await Notify
                .NotifyAsync(modelCallEvent, [telemetryDispatcher.OnDecisionModelCallStartAsync], cancellationToken)
                .ConfigureAwait(false);
            var result = await RetryAsync(
                    maxRetries,
                    async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return await model
                            .DoDecideAsync(
                                new DecisionModelCall
                                {
                                    State = preparedState,
                                    Questions = options.Questions,
                                    MaxOutputTokens = options.MaxOutputTokens,
                                    Reasoning = options.Reasoning,
                                    Headers = options.Headers ?? new Dictionary<string, string>(),
                                    ProviderOptions = options.ProviderOptions,
                                },
                                cancellationToken
                            )
                            .ConfigureAwait(false);
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

            await Notify
                .NotifyAsync(
                    new DecisionModelCallEndEvent
                    {
                        CallId = callId,
                        Provider = model.Provider,
                        ModelId = model.ModelId,
                        State = preparedState,
                        Questions = options.Questions,
                        Answers = result.Answers,
                        Usage = result.Usage,
                        Warnings = result.Warnings,
                        Rounding = result.Rounding,
                        ProviderMetadata = result.ProviderMetadata,
                        ResponseModelId = result.ResponseModelId,
                    },
                    [telemetryDispatcher.OnDecisionModelCallEndAsync],
                    cancellationToken
                )
                .ConfigureAwait(false);

            var decisionResult = new DecisionResult
            {
                Answers = result.Answers,
                Usage = new DecisionUsage(result.Usage?.InputTokens, result.Usage?.OutputTokens),
                Warnings = result.Warnings,
                Rounding = result.Rounding,
                ProviderMetadata = result.ProviderMetadata,
                ResponseModelId = result.ResponseModelId ?? model.ModelId,
            };
            await Notify
                .NotifyAsync(
                    new DecideEndEvent
                    {
                        CallId = startEvent.CallId,
                        RuntimeContext = startEvent.RuntimeContext,
                        Provider = startEvent.Provider,
                        ModelId = startEvent.ModelId,
                        State = startEvent.State,
                        Questions = startEvent.Questions,
                        MaxOutputTokens = startEvent.MaxOutputTokens,
                        Reasoning = startEvent.Reasoning,
                        MaxRetries = startEvent.MaxRetries,
                        Headers = startEvent.Headers,
                        ProviderOptions = startEvent.ProviderOptions,
                        Answers = decisionResult.Answers,
                        Usage = decisionResult.Usage,
                        Warnings = decisionResult.Warnings,
                        Rounding = decisionResult.Rounding,
                        ProviderMetadata = decisionResult.ProviderMetadata,
                        ResponseModelId = decisionResult.ResponseModelId,
                        Timestamp = decisionResult.Timestamp,
                    },
                    [options.OnEnd, telemetryDispatcher.OnEndAsync],
                    cancellationToken
                )
                .ConfigureAwait(false);
            return decisionResult;
        }
        catch (Exception error)
        {
            // A failing integration must not hide the error the call failed with.
            await Notify
                .NotifyAsync(
                    error,
                    [failure => telemetryDispatcher.OnErrorAsync(callId, failure)],
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The AI SDK's <c>createIdGenerator({ prefix: 'call', size: 24 })</c>: <c>call-</c> and 24 random letters and digits.</summary>
    private static string GenerateCallId()
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        return "call-" + string.Create(24, alphabet, static (span, chars) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];
                }
            });
    }

    /// <summary>
    /// The AI SDK's <c>retryWithExponentialBackoffRespectingRetryHeaders</c>: runs <paramref name="call"/>, and sends it again, up to
    /// <paramref name="maxRetries"/> times, while it fails with a retryable <see cref="ApiCallException"/>. The first non-retryable failure
    /// is thrown as is; a failure after retries, or a spent budget, is a <see cref="RetryError"/>. Cancelling ends the wait between attempts.
    /// </summary>
    private static async Task<T> RetryAsync<T>(int maxRetries, Func<Task<T>> call, CancellationToken cancellationToken)
    {
        if (maxRetries == 0)
        {
            // As the AI SDK's retryWithExponentialBackoff: no retries, no wrapping.
            return await call().ConfigureAwait(false);
        }

        var errors = new List<Exception>();
        var backoff = InitialRetryDelay;
        while (true)
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                var message = ErrorMessage(error);
                errors.Add(error);
                var tries = errors.Count;
                if (tries > maxRetries)
                {
                    throw new RetryError(
                        "Failed after "
                            + tries.ToString(CultureInfo.InvariantCulture)
                            + " attempts. Last error: "
                            + message,
                        errors
                    );
                }

                if (error is ApiCallException { IsRetryable: true } api)
                {
                    await Task.Delay(RetryDelay(api, backoff), cancellationToken).ConfigureAwait(false);
                    backoff *= RetryBackoffFactor;
                    continue;
                }

                if (tries == 1)
                {
                    // Not wrapped when a non-retryable error occurs on the first try.
                    throw;
                }

                throw new RetryError(
                    "Failed after "
                        + tries.ToString(CultureInfo.InvariantCulture)
                        + " attempts with non-retryable error: '"
                        + message
                        + "'",
                    errors
                );
            }
        }
    }

    /// <summary>The AI SDK's <c>getErrorMessage</c>, which prints an error as JavaScript's <c>toString</c> does: its name, then its message.</summary>
    private static string ErrorMessage(Exception error) =>
        (error is ApiCallException ? "AI_APICallError" : "Error") + ": " + error.Message;

    /// <summary>
    /// The AI SDK's <c>getRetryDelayInMs</c>: the provider's <c>retry-after-ms</c>, else its <c>retry-after</c> (seconds or an HTTP date),
    /// when it is not negative and under a minute (or under the backoff); otherwise the exponential backoff.
    /// </summary>
    private static TimeSpan RetryDelay(ApiCallException error, TimeSpan backoff)
    {
        var headers = error.ResponseHeaders;
        if (headers is null)
        {
            return backoff;
        }

        double? ms = null;
        if (
            headers.TryGetValue("retry-after-ms", out var millis)
            && double.TryParse(millis, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMs)
        )
        {
            ms = parsedMs;
        }

        if (ms is null && headers.TryGetValue("retry-after", out var after))
        {
            if (double.TryParse(after, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                ms = seconds * 1000;
            }
            else if (DateTimeOffset.TryParse(after, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                ms = (date - DateTimeOffset.UtcNow).TotalMilliseconds;
            }
        }

        return
            ms is { } delay
            && double.IsFinite(delay)
            && delay >= 0
            && (delay < 60_000 || delay < backoff.TotalMilliseconds)
            ? TimeSpan.FromMilliseconds(delay)
            : backoff;
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
