// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Vercel.AI.Operations;
using VercelGateway = Vercel.AI.Gateway;
using VercelPrompt = Vercel.AI.Prompt;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// .NET-only: the AI SDK's <c>createGateway</c> decision-model factory, served by the <c>Vercel.AI</c> package's
/// <see cref="VercelGateway.GatewayProvider"/> instead of a port inside E2E.Decision. It makes <c>decisionModel</c> (and the deprecated
/// <c>evaluationModel</c>) return an <see cref="IDecisionModel"/> that <see cref="ExperimentalDecide"/> and the decision executor accept.
/// The rest of the Gateway (language, realtime, batch, spend, credits) is <c>Vercel.AI</c>'s: reach it through <see cref="Gateway"/>.
/// </summary>
public sealed class GatewayDecisionProvider : IDecisionProvider
{
    /// <summary>Wraps <paramref name="gateway"/>, or a default Gateway (<c>AI_GATEWAY_API_KEY</c>, else <c>VERCEL_OIDC_TOKEN</c>).</summary>
    public GatewayDecisionProvider(VercelGateway.GatewayProvider? gateway = null) =>
        Gateway = gateway ?? VercelGateway.GatewayProvider.Create();

    /// <summary>The <c>Vercel.AI</c> Gateway provider the decision models call through.</summary>
    public VercelGateway.GatewayProvider Gateway { get; }

    /// <inheritdoc />
    public IDecisionModel? DecisionModel(string modelId) => new VercelAiDecisionModel(Gateway.DecisionModel(modelId));

    /// <inheritdoc />
    public IEvaluationModel? EvaluationModel(string modelId) =>
        ResolveModel.AsEvaluationModel(new VercelAiDecisionModel(Gateway.DecisionModel(modelId)));
}

/// <summary>
/// .NET-only: an <see cref="IDecisionModel"/> over a <c>Vercel.AI</c> decision model. The port and <c>Vercel.AI</c> each have their own
/// decision call types, so the call and its result are converted here and nothing else.
/// </summary>
internal sealed class VercelAiDecisionModel(IDecisionCaller model) : IDecisionModel
{
    public string Provider => model.Provider;

    public string ModelId => model.ModelId;

    public string SpecificationVersion => model.SpecificationVersion;

    public IReadOnlyList<string> SupportedQuestionTypes => model.SupportedQuestionTypes;

    public async Task<E2E.Decision.Sdk.DecisionModelResult> DoDecideAsync(
        E2E.Decision.Sdk.DecisionModelCall call,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(call);
        var result = await model
            .DoDecideAsync(ToVercel(call, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        return FromVercel(result);
    }

    private static Vercel.AI.Operations.DecisionModelCall ToVercel(
        E2E.Decision.Sdk.DecisionModelCall call,
        CancellationToken cancellationToken
    ) =>
        new(
            [.. call.State.Select(ToVercel)],
            call.Questions.ToDictionary(question => question.Key, question => ToVercel(question.Value)),
            call.ProviderOptions is null ? default : JsonSerializer.SerializeToElement(call.ProviderOptions),
            call.Headers,
            cancellationToken,
            call.MaxOutputTokens,
            call.Reasoning
        );

    private static DecisionModelStatePart ToVercel(DecisionStatePart part) =>
        part switch
        {
            DecisionTextPart text => new DecisionModelTextPart(text.Text),
            DecisionJsonPart json => new DecisionModelJsonPart(json.Value),
            DecisionFileUrlPart url => new DecisionModelFilePart(
                url.MediaType,
                new VercelPrompt.LanguageModelFilePart("url", url.MediaType) { Url = url.Url }
            ),
            DecisionFilePart file => ToVercel(file),
            _ => throw new ArgumentException("Unknown decision state part: " + part.GetType().Name, nameof(part)),
        };

    private static DecisionModelFilePart ToVercel(DecisionFilePart file)
    {
        var data = new VercelPrompt.LanguageModelFilePart(file.DataType, file.MediaType);
        switch (file.DataType)
        {
            case "url":
                data.Url = file.Url;
                break;
            case "text":
                data.Text = file.Text;
                break;
            case "reference":
                data.Reference = file.Reference is { ValueKind: JsonValueKind.Object } reference
                    ? reference
                        .EnumerateObject()
                        .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "")
                    : null;
                break;
            default:
                data.Base64 = file.GetBase64();
                break;
        }

        return new DecisionModelFilePart(file.MediaType, data)
        {
            FileName = file.Filename,
            ProviderOptions = file.ProviderOptions,
        };
    }

    private static Vercel.AI.Operations.DecisionQuestion ToVercel(E2E.Decision.Sdk.DecisionQuestion question)
    {
        var instructions = ToVercel(question.Instructions);
        DecisionCriteria? criteria = question.Type switch
        {
            "choice" when question.Choices is { } choices => new DecisionChoiceCriteria(
                choices.ToDictionary(choice => choice.Value, choice => ToVercel(choice.Description))
            ),
            "score" when question.Levels is { } levels => new DecisionScoreCriteria([.. levels.Select(ToVercel)]),
            "boolean" when question.TrueCriteria is not null || question.FalseCriteria is not null => BooleanCriteria(
                question
            ),
            _ => null,
        };
        return new Vercel.AI.Operations.DecisionQuestion(question.Type, instructions!, criteria!)
        {
            HasInstructions = question.Instructions is not null,
            HasCriteria = criteria is not null || question.NullCriteria,
        };
    }

    private static DecisionBooleanCriteria BooleanCriteria(E2E.Decision.Sdk.DecisionQuestion question)
    {
        var trueCriteria = ToVercel(question.TrueCriteria);
        var falseCriteria = ToVercel(question.FalseCriteria);
        return (question.TrueCriteria is not null, question.FalseCriteria is not null) switch
        {
            (true, true) => new DecisionBooleanCriteria { True = trueCriteria, False = falseCriteria },
            (true, false) => new DecisionBooleanCriteria { True = trueCriteria },
            _ => new DecisionBooleanCriteria { False = falseCriteria },
        };
    }

    // The port's DecisionInput.Null is a JSON null (an explicit null field); Vercel.AI writes a null input as null.
    private static Vercel.AI.Operations.DecisionInput? ToVercel(E2E.Decision.Sdk.DecisionInput? input) =>
        input is null || ReferenceEquals(input, E2E.Decision.Sdk.DecisionInput.Null) ? null
        : input.Text is { } text ? Vercel.AI.Operations.DecisionInput.From(text)
        : Vercel.AI.Operations.DecisionInput.FromJson(input.Json!.Value);

    private static E2E.Decision.Sdk.DecisionModelResult FromVercel(Vercel.AI.Operations.DecisionModelResult result) =>
        new()
        {
            Answers = result.Answers.ToDictionary(answer => answer.Key, answer => FromVercel(answer.Value)),
            Usage = result.Usage is { } usage ? new DecisionUsage(usage.InputTokens, usage.OutputTokens) : null,
            Rounding = result.Rounding is { } rounding
                ? new E2E.Decision.Sdk.DecisionRounding(rounding.ProbabilityDecimals, rounding.ScoreDecimals)
                : null,
            ProviderMetadata = result.ProviderMetadata,
            ResponseId = result.Response?.Id,
            ResponseTimestamp = result.Response?.Timestamp is { } timestamp ? new DateTimeOffset(timestamp) : null,
            ResponseModelId = result.Response?.ModelId,
            ResponseHeaders = result.Response?.Headers,
            ResponseBody = result.Response?.Body,
            Warnings = [.. result.Warnings.Select(Warning)],
        };

    private static E2E.Decision.Sdk.DecisionAnswer FromVercel(Vercel.AI.Operations.DecisionAnswer answer) =>
        new()
        {
            Type = answer.Type,
            Choice = answer.Choice,
            Score = answer.Score,
            Probability = answer.Probability,
            Probabilities = answer.Probabilities,
            Box = answer.Box is { } box
                ? new E2E.Decision.Sdk.DecisionBox(box.Top, box.Bottom, box.Left, box.Right)
                : null,
        };

    // The port's warnings are text, as OpenAIDecisionModel writes them: "unsupported setting: <feature>".
    private static string Warning(OperationWarning warning) =>
        warning.Message
        ?? (
            warning.Type == "unsupported"
                ? "unsupported setting: "
                    + warning.Feature
                    + (warning.Details is null ? "" : " (" + warning.Details + ")")
                : warning.Type
        );
}
