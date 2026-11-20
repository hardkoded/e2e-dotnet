// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.DeprecatedAliases;

/// <summary>
/// The AI SDK's <c>decide/deprecated-aliases.test.ts</c>, the <c>it</c>s on the function alias and the provider factories. The two
/// telemetry <c>it</c>s of that file belong to the deprecated telemetry callbacks (#1899).
/// </summary>
[Collection(DefaultProviderCollection.Name)]
public sealed class DeprecatedAliasesTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, DecisionQuestion> Questions = new Dictionary<
        string,
        DecisionQuestion
    >
    {
        ["route"] = DecisionQuestion.Choice("Team?", [new DecisionChoice("billing"), new DecisionChoice("support")]),
    };

    private static readonly IReadOnlyList<DecisionStatePart> State = [new DecisionTextPart("message")];

    private static readonly DecisionModelResult Result = new()
    {
        Answers = new Dictionary<string, DecisionAnswer>
        {
            ["route"] = new() { Type = "choice", Choice = "billing" },
        },
    };

    public void Dispose() => ResolveModel.AiSdkDefaultProvider = null;

    [Fact]
    public async Task Keeps_the_function_alias_and_accepts_existing_model_implementations()
    {
        Assert.Equal(
            new Func<DecideOptions, CancellationToken, Task<DecisionResult>>(ExperimentalDecide.RunAsync),
            ExperimentalDecide.ExperimentalEvaluate
        );
        var model = new LegacyModel();

        var output = await ExperimentalDecide.ExperimentalEvaluate(
            new DecideOptions
            {
                EvaluationModel = model,
                State = State,
                Questions = Questions,
            },
            CancellationToken.None
        );

        Assert.Equal(Result.Answers.Keys, output.Answers.Keys);
        Assert.Equal("billing", output.Answers["route"].Choice);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Resolves_deprecated_provider_factories_through_default_custom_and_registry_access()
    {
        var legacy = new LegacyProvider();
        ResolveModel.AiSdkDefaultProvider = legacy;
        var registry = ProviderRegistry.CreateProviderRegistry(
            new Dictionary<string, IDecisionProvider> { ["legacy"] = legacy },
            new ProviderRegistryOptions { Separator = "|" }
        );
        var custom = new CustomProvider(new CustomProviderOptions { FallbackProvider = legacy });
        var aliases = new CustomProvider(
            new CustomProviderOptions
            {
                EvaluationModels = new Dictionary<string, IEvaluationModel> { ["alias"] = new LegacyModel() },
            }
        );

        var models = new List<DecideOptions>
        {
            Options(modelId: "model"),
            Options(evaluationModel: registry.EvaluationModel("legacy|model")),
            Options(model: registry.DecisionModel("legacy|model")),
            Options(evaluationModel: custom.EvaluationModel("model")),
            Options(model: custom.DecisionModel("model")),
            Options(evaluationModel: aliases.EvaluationModel("alias")),
            Options(model: aliases.DecisionModel("alias")),
        };
        foreach (var options in models)
        {
            var output = await ExperimentalDecide.RunAsync(options);
            Assert.Equal("billing", output.Answers["route"].Choice);
        }

        var direct = await registry
            .EvaluationModel("legacy|model")
            .DoEvaluateAsync(new DecisionModelCall { State = State, Questions = Questions }, CancellationToken.None);
        Assert.Equal("billing", direct.Answers["route"].Choice);
    }

    [Fact]
    public async Task Prefers_new_model_maps_and_factories_and_supports_direct_deprecated_model_calls()
    {
        var model = new DecideTestHelpersModel();
        var provider = new CustomProvider(
            new CustomProviderOptions
            {
                DecisionModels = new Dictionary<string, IDecisionModel> { ["alias"] = model },
                EvaluationModels = new Dictionary<string, IEvaluationModel> { ["alias"] = new LegacyModel() },
            }
        );

        Assert.Same(model, provider.DecisionModel("alias"));
        var direct = await provider
            .EvaluationModel("alias")
            .DoEvaluateAsync(new DecisionModelCall { State = State, Questions = Questions }, CancellationToken.None);
        Assert.Equal("billing", direct.Answers["route"].Choice);

        var preferring = new PreferringProvider(model);
        ResolveModel.AiSdkDefaultProvider = preferring;
        await ExperimentalDecide.ExperimentalEvaluate(Options(modelId: "model"), CancellationToken.None);
        Assert.Equal(0, preferring.EvaluationModelCalls);
    }

    private static DecideOptions Options(
        string? modelId = null,
        IDecisionModel? model = null,
        IEvaluationModel? evaluationModel = null
    ) =>
        new()
        {
            ModelId = modelId,
            Model = model,
            EvaluationModel = evaluationModel,
            State = State,
            Questions = Questions,
        };

    private sealed class LegacyModel : IEvaluationModel
    {
        public int Calls { get; private set; }

        public string Provider => "legacy";

        public string ModelId => "legacy-model";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["choice"];

        public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class LegacyProvider : IDecisionProvider
    {
        private readonly LegacyModel _model = new();

        public IEvaluationModel? EvaluationModel(string modelId)
        {
            Assert.Equal("model", modelId);
            return _model;
        }
    }

    private sealed class DecideTestHelpersModel : IDecisionModel
    {
        public string Provider => "mock-provider";

        public string ModelId => "mock-model-id";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => ["choice", "score", "boolean"];

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class PreferringProvider(IDecisionModel model) : IDecisionProvider
    {
        public int EvaluationModelCalls { get; private set; }

        public IDecisionModel? DecisionModel(string modelId) => model;

        public IEvaluationModel? EvaluationModel(string modelId)
        {
            EvaluationModelCalls++;
            return null;
        }
    }
}

/// <summary>Tests that set <see cref="ResolveModel.AiSdkDefaultProvider"/> (upstream's <c>vi.stubGlobal('AI_SDK_DEFAULT_PROVIDER')</c>) do not run in parallel.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DefaultProviderCollection
{
    public const string Name = "DefaultProvider";
}
