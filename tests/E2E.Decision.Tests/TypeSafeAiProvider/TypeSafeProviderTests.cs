// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
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

    // Upstream's vi.resetModules() and re-import: a fresh load context re-runs the static initializer of TypeSafeAi.
    // The singleton takes no transport, so a loopback listener stands in for upstream's spy on globalThis.fetch and
    // the base URL points at it; the path after the host is upstream's.
    [Fact]
    public async Task Configures_the_default_provider_from_TYPESAFE_AI_BASE_URL_at_import_time()
    {
        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var served = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var path = context.Request.Url!.AbsolutePath;
            var body = Encoding.UTF8.GetBytes(NativeResponse.ToJsonString());
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
            return path;
        });
        var previousKey = Environment.GetEnvironmentVariable("TYPESAFE_AI_API_KEY");
        var previousUrl = Environment.GetEnvironmentVariable("TYPESAFE_AI_BASE_URL");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", $"http://127.0.0.1:{port}/v1/");
        var context = new AssemblyLoadContext("typesafe-ai-import", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(
                typeof(E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider).Assembly.Location
            );
            var providerType = assembly.GetType(typeof(E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider).FullName!)!;
            var provider = providerType.GetProperty("TypeSafeAi")!.GetValue(null);
            var model = providerType.GetMethod("DecisionModel")!.Invoke(provider, ["jev-latest"])!;

            var partType = assembly.GetType("E2E.Decision.Sdk.DecisionJsonPart")!;
            var statePartType = assembly.GetType("E2E.Decision.Sdk.DecisionStatePart")!;
            var questionType = assembly.GetType("E2E.Decision.Sdk.DecisionQuestion")!;
            var state = Array.CreateInstance(statePartType, 1);
            state.SetValue(
                Activator.CreateInstance(
                    partType,
                    JsonDocument.Parse("""{"text":"The request used the configured endpoint."}""").RootElement
                ),
                0
            );
            var question = Activator.CreateInstance(questionType)!;
            questionType.GetProperty("Type")!.SetValue(question, "boolean");
            var questions = (System.Collections.IDictionary)
                Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), questionType))!;
            questions["configured"] = question;
            var callType = assembly.GetType("E2E.Decision.Sdk.DecisionModelCall")!;
            var call = Activator.CreateInstance(callType)!;
            callType.GetProperty("State")!.SetValue(call, state);
            callType.GetProperty("Questions")!.SetValue(call, questions);

            var task = (Task)model.GetType().GetMethod("DoDecideAsync")!.Invoke(model, [call, CancellationToken.None])!;
            await task;

            Assert.Equal("/v1/systemone", await served);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", previousUrl);
            context.Unload();
        }
    }

    private static int GetFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try
        {
            return ((IPEndPoint)tcp.LocalEndpoint).Port;
        }
        finally
        {
            tcp.Stop();
        }
    }

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
