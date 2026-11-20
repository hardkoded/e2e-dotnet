// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.TypeSafeAi;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;

namespace E2E.Decision.Tests.TypeSafeAiProvider;

/// <summary>Upstream's <c>describe('TypeSafe provider')</c> of <c>typesafe-ai-provider.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class TypeSafeProviderTests
{
    [Fact]
    public void Provides_the_experimental_decision_capability()
    {
        var provider = E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider.TypeSafeAi;
        var model = provider.DecisionModel("jev-latest");

        Assert.Equal("v4", provider.SpecificationVersion);
        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("typesafe.decision", model.Provider);
        Assert.Equal("jev-latest", model.ModelId);
        Assert.Equal(["choice", "score", "boolean", "box"], model.SupportedQuestionTypes);
    }

    [Fact]
    public void Partial_Retains_the_deprecated_factory_alias()
    {
        var provider = new E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider();

#pragma warning disable CS0618 // The test is of the deprecated alias.
        Assert.Equal("typesafe.decision", provider.EvaluationModel("jev-latest").Provider);
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("languageModel")]
    [InlineData("embeddingModel")]
    [InlineData("imageModel")]
    public void Rejects_unsupported_factories(string factory)
    {
        var provider = new E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider();

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
    public Task Uses_the_default_base_URL_when_TYPESAFE_AI_BASE_URL_is_not_set() =>
        RequestUrlAsync(environmentBaseUrl: null, optionBaseUrl: null, "https://api.typesafe.ai/v1/systemone");

    [Fact]
    public Task Uses_TYPESAFE_AI_BASE_URL_without_a_trailing_slash() =>
        RequestUrlAsync(
            environmentBaseUrl: "https://proxy.example/v1/",
            optionBaseUrl: null,
            "https://proxy.example/v1/systemone"
        );

    [Fact]
    public Task Prefers_the_baseURL_option_over_TYPESAFE_AI_BASE_URL() =>
        RequestUrlAsync(
            environmentBaseUrl: "https://environment.example/v1",
            optionBaseUrl: "https://option.example/v1/",
            "https://option.example/v1/systemone"
        );

    private static async Task RequestUrlAsync(string? environmentBaseUrl, string? optionBaseUrl, string expectedUrl)
    {
        var server = new TestServer();
        server.RespondJson(expectedUrl, NativeResponse.ToJsonString());
        var previous = Environment.GetEnvironmentVariable("TYPESAFE_AI_BASE_URL");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", environmentBaseUrl);
        try
        {
            await new E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider(
                new TypeSafeAiProviderSettings
                {
                    ApiKey = "test-api-key",
                    BaseUrl = optionBaseUrl,
                    HttpClient = server.Client,
                }
            ).DecisionModel("jev-latest").DoDecideAsync(Options(), CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", previous);
        }

        Assert.Equal(expectedUrl, Assert.Single(server.Calls).RequestUrl);
    }
}
