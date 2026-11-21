// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.TypeSafeAi;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>
/// .NET-only: the TypeSafe decision model sends its requests through the TypeSafe .NET SDK, and upstream has no <c>it</c> for what that adds: the key
/// taken from the <c>Authorization</c> header, the base URL the SDK extends, and the SDK's failures read as the model's own.
/// </summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class SdkTransportTests
{
    [Fact]
    public async Task Requests_a_base_URL_without_v1_with_v1_added()
    {
        var server = new TestServer();
        server.RespondJson("https://custom.example/typesafe/v1/systemone", NativeResponse.ToJsonString());
        var model = new E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider(
            new TypeSafeAiProviderSettings
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://custom.example/typesafe",
                HttpClient = server.Client,
            }
        ).DecisionModel("jev-latest");

        await model.DoDecideAsync(Options(), CancellationToken.None);

        Assert.Equal("https://custom.example/typesafe/v1/systemone", Assert.Single(server.Calls).RequestUrl);
    }

    [Fact]
    public async Task Sends_the_bearer_of_a_call_header_that_replaces_the_provider_key()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(headers: new Dictionary<string, string> { ["Authorization"] = "Bearer call-key" }),
            CancellationToken.None
        );

        Assert.Equal("Bearer call-key", Assert.Single(server.Calls).RequestHeaders["authorization"]);
    }

    [Fact]
    public async Task Fails_without_an_Authorization_header_as_a_missing_key()
    {
        var server = new TestServer();
        server.RespondJson(Url, NativeResponse.ToJsonString());
        var model = ModelWithHeaders(server, new Dictionary<string, string?> { ["x-custom"] = "value" });

        await Assert.ThrowsAsync<LoadApiKeyException>(() => model.DoDecideAsync(Options(), CancellationToken.None));

        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Rejects_an_Authorization_header_that_is_not_a_bearer()
    {
        var server = new TestServer();
        server.RespondJson(Url, NativeResponse.ToJsonString());
        var model = ModelWithHeaders(
            server,
            new Dictionary<string, string?> { ["Authorization"] = "Basic dXNlcjpwdw==" }
        );

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal("headers.Authorization", error.Parameter);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Rejects_a_request_the_SDK_refuses_before_sending_it()
    {
        var (server, model) = Create();

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(Options(questions: new Dictionary<string, DecisionQuestion>()), CancellationToken.None)
        );

        Assert.Equal("request", error.Parameter);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Reads_an_answer_the_SDK_cannot_read_as_a_failed_successful_response()
    {
        var (server, model) = Create();
        server.RespondJson(Url, """{"answers":{"test":{"type":"noul"}}}""");

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal("Failed to process successful response", error.Message);
        Assert.Equal(200, error.StatusCode);
        Assert.False(error.IsRetryable);
    }

    [Fact]
    public async Task Reads_a_failed_connection_as_a_retryable_call_error()
    {
        var model = ModelOver((_, _) => throw new HttpRequestException("connection refused"));

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.StartsWith("Cannot connect to API: ", error.Message, StringComparison.Ordinal);
        Assert.True(error.IsRetryable);
        Assert.Null(error.StatusCode);
    }

    [Fact]
    public async Task Surfaces_a_caller_cancel_as_cancellation_not_as_a_call_error()
    {
        var started = new TaskCompletionSource();
        var model = ModelOver(
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        using var cancellation = new CancellationTokenSource();

        var call = model.DoDecideAsync(Options(), cancellation.Token);
        await started.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    private static DecisionTypeSafeAiModel ModelWithHeaders(
        TestServer server,
        IReadOnlyDictionary<string, string?> headers
    ) =>
        new(
            "jev-latest",
            new TypeSafeAiDecisionModelConfig
            {
                Provider = "typesafe.decision",
                BaseUrl = "https://api.typesafe.ai/v1",
                Headers = _ => Task.FromResult(headers),
                HttpClient = server.Client,
            }
        );

    private static DecisionTypeSafeAiModel ModelOver(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send
    ) =>
        new(
            "jev-latest",
            new TypeSafeAiDecisionModelConfig
            {
                Provider = "typesafe.decision",
                BaseUrl = "https://api.typesafe.ai/v1",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?> { ["Authorization"] = "Bearer test-api-key" }
                    ),
                HttpClient = new HttpClient(new FuncHandler(send)),
            }
        );

    private sealed class FuncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => send(request, cancellationToken);
    }
}
