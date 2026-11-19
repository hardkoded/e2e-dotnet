// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.TypesafeAiProvider;

/// <summary>Ports <c>describe('TypeSafe provider')</c> of <c>packages/typesafe-ai/src/typesafe-ai-provider.test.ts</c> of the AI SDK (<c>vercel/ai</c>).</summary>
[Collection("TypesafeAi")]
public sealed class TypesafeAiProviderTests
{
    private const string Answer = """{"model":"jev-latest","answers":{"configured":{"type":"noul","noul":1}}}""";

    private static readonly DecisionModelCall Options = new()
    {
        State = [new DecisionTextPart("The request used the configured endpoint.")],
        Questions = new Dictionary<string, DecisionQuestion>
        {
            ["configured"] = DecisionQuestion.Boolean("Did the request use the configured endpoint?"),
        },
    };

    [Fact]
    public void Provides_the_experimental_decision_capability()
    {
        var model = TypeSafeAi.Default.DecisionModel("jev-latest");

        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("typesafe.decision", model.Provider);
        Assert.Equal("jev-latest", model.ModelId);
        Assert.Equal(["choice", "score", "boolean", "box"], model.SupportedQuestionTypes);
    }

    [Fact]
    public void Retains_the_deprecated_factory_alias()
    {
#pragma warning disable CS0618
        Assert.Equal("typesafe.decision", TypeSafeAi.Default.EvaluationModel("jev-latest").Provider);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task Uses_the_default_base_URL_when_TYPESAFE_AI_BASE_URL_is_not_set()
    {
        Assert.Equal(
            "https://api.typesafe.ai/v1/systemone",
            await UrlOf(null, new TypeSafeAiProviderSettings { ApiKey = "test-api-key" })
        );
    }

    [Fact]
    public async Task Uses_TYPESAFE_AI_BASE_URL_without_a_trailing_slash()
    {
        Assert.Equal(
            "https://proxy.example/v1/systemone",
            await UrlOf("https://proxy.example/v1/", new TypeSafeAiProviderSettings { ApiKey = "test-api-key" })
        );
    }

    [Fact]
    public async Task Prefers_the_baseURL_option_over_TYPESAFE_AI_BASE_URL()
    {
        Assert.Equal(
            "https://option.example/v1/systemone",
            await UrlOf(
                "https://environment.example/v1",
                new TypeSafeAiProviderSettings { ApiKey = "test-api-key", BaseUrl = "https://option.example/v1/" }
            )
        );
    }

    private static async Task<string> UrlOf(string? environment, TypeSafeAiProviderSettings settings)
    {
        var handler = new Recorder();
        var previous = Environment.GetEnvironmentVariable("TYPESAFE_AI_BASE_URL");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", environment);
        try
        {
            using var http = new HttpClient(handler);
            var provider = TypeSafeAi.Create(
                new TypeSafeAiProviderSettings
                {
                    ApiKey = settings.ApiKey,
                    BaseUrl = settings.BaseUrl,
                    HttpClient = http,
                }
            );
            await provider.DecisionModel("jev-latest").DoDecideAsync(Options, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", previous);
        }

        return handler.Url!;
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public string? Url { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Url = request.RequestUri!.ToString();
            return Task.FromResult(
                new HttpResponseMessage
                {
                    Content = new StringContent(Answer, System.Text.Encoding.UTF8, "application/json"),
                }
            );
        }
    }
}
