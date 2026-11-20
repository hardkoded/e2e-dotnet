// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Reflection;
using System.Runtime.Loader;
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

    // Upstream resets its module registry and imports typesafe-ai-provider again. The port's singleton is a static, so the
    // equivalent is a fresh AssemblyLoadContext with its own copy of the assembly, where the static initializer runs again.
    // The singleton has no injectable client (upstream spies on global fetch), so a loopback listener sees the request instead.
    [Fact]
    public async Task Configures_the_default_provider_from_TYPESAFE_AI_BASE_URL_at_import_time()
    {
        using var listener = new HttpListener();
        var port = GetFreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var served = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var url = context.Request.Url!.ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(NativeResponse.ToJsonString());
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
            return url;
        });
        var previousKey = Environment.GetEnvironmentVariable("TYPESAFE_AI_API_KEY");
        var previousBaseUrl = Environment.GetEnvironmentVariable("TYPESAFE_AI_BASE_URL");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", $"http://127.0.0.1:{port}/v1/");
        var context = new FreshAssemblyLoadContext();
        try
        {
            var probe = context
                .LoadFromAssemblyPath(typeof(TypeSafeProviderTests).Assembly.Location)
                .GetType(typeof(ImportTimeProbe).FullName!, throwOnError: true)!;
            await (Task)probe.GetMethod(nameof(ImportTimeProbe.DecideAsync))!.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
            Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("TYPESAFE_AI_BASE_URL", previousBaseUrl);
        }

        Assert.Equal($"http://127.0.0.1:{port}/v1/systemone", await served.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    private static int GetFreePort()
    {
        var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        try
        {
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }
        finally
        {
            socket.Stop();
        }
    }

    /// <summary>Loads its own copy of the library and of this test assembly; everything else comes from the default context.</summary>
    private sealed class FreshAssemblyLoadContext() : AssemblyLoadContext("typesafe-import-time", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name is "E2E.Decision"
                ? LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "E2E.Decision.dll"))
                : null;
    }

    /// <summary>Runs inside the fresh context, where <c>TypeSafeAi</c> is built from the environment at its first use.</summary>
    internal static class ImportTimeProbe
    {
        public static async Task DecideAsync() =>
            await E2E
                .Decision.Sdk.TypeSafeAi.TypeSafeAiProvider.TypeSafeAi.DecisionModel("jev-latest")
                .DoDecideAsync(Options(), CancellationToken.None);
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
