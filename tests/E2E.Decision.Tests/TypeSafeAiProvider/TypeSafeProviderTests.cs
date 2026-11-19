// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.TypeSafeAi;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;
using Provider = E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider;

namespace E2E.Decision.Tests.TypeSafeAiProvider;

/// <summary>Upstream's <c>describe('TypeSafe provider')</c> of <c>typesafe-ai-provider.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class TypeSafeProviderTests
{
    [Fact]
    public void Provides_the_experimental_decision_capability()
    {
        var model = Provider.TypeSafeAi.DecisionModel("jev-latest");

        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("typesafe.decision", model.Provider);
        Assert.Equal("jev-latest", model.ModelId);
        Assert.Equal(["choice", "score", "boolean", "box"], model.SupportedQuestionTypes);
    }

    [Fact]
    public void Retains_the_deprecated_factory_alias()
    {
        Assert.Equal("typesafe.decision", Provider.TypeSafeAi.EvaluationModel("jev-latest").Provider);
    }

    [Theory]
    [InlineData("languageModel")]
    [InlineData("embeddingModel")]
    [InlineData("imageModel")]
    public void Rejects_unsupported_factories(string factory)
    {
        var provider = new Provider();
        var error = Assert.Throws<NoSuchModelException>(() =>
        {
            switch (factory)
            {
                case "languageModel":
                    provider.LanguageModel("unknown");
                    break;
                case "embeddingModel":
                    provider.EmbeddingModel("unknown");
                    break;
                default:
                    provider.ImageModel("unknown");
                    break;
            }
        });
        Assert.Equal(factory, error.ModelType);
        Assert.Equal("unknown", error.ModelId);
    }

    [Fact]
    public async Task Uses_the_default_base_URL_when_TYPESAFE_AI_BASE_URL_is_not_set()
    {
        using var environment = new EnvironmentOverride("TYPESAFE_AI_BASE_URL", null);

        var url = await RequestedUrl(new TypeSafeAiProviderSettings());

        Assert.Equal("https://api.typesafe.ai/v1/systemone", url);
    }

    [Fact]
    public async Task Uses_TYPESAFE_AI_BASE_URL_without_a_trailing_slash()
    {
        using var environment = new EnvironmentOverride("TYPESAFE_AI_BASE_URL", "https://proxy.example/v1/");

        var url = await RequestedUrl(new TypeSafeAiProviderSettings());

        Assert.Equal("https://proxy.example/v1/systemone", url);
    }

    [Fact]
    public async Task Prefers_the_baseURL_option_over_TYPESAFE_AI_BASE_URL()
    {
        using var environment = new EnvironmentOverride("TYPESAFE_AI_BASE_URL", "https://environment.example/v1");

        var url = await RequestedUrl(new TypeSafeAiProviderSettings { BaseUrl = "https://option.example/v1/" });

        Assert.Equal("https://option.example/v1/systemone", url);
    }

    private static async Task<string> RequestedUrl(TypeSafeAiProviderSettings settings)
    {
        var server = new TestServer();
        server.RespondJson("https://api.typesafe.ai/v1/systemone", NativeResponse.ToJsonString());
        server.RespondJson("https://proxy.example/v1/systemone", NativeResponse.ToJsonString());
        server.RespondJson("https://option.example/v1/systemone", NativeResponse.ToJsonString());
        var provider = new Provider(
            new TypeSafeAiProviderSettings
            {
                ApiKey = "test-api-key",
                BaseUrl = settings.BaseUrl,
                HttpClient = server.Client,
            }
        );

        await provider.DecisionModel("jev-latest").DoDecideAsync(Options(), CancellationToken.None);

        return Assert.Single(server.Calls).RequestUrl;
    }

    private sealed class EnvironmentOverride : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentOverride(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
