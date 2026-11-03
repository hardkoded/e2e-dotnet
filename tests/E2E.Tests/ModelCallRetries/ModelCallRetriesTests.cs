// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace E2E.Tests.ModelCallRetries;

/// <summary>
/// .NET-only: upstream's <c>ModelCall.maxRetries</c> (<c>agent/model/adapter.ts</c>) reaches the AI SDK, which has no test of its own in the
/// port. <see cref="ModelRequest.MaxRetries"/> sets how often a model request is sent again after a retryable failure; without it a request
/// is sent at most six times.
/// </summary>
public sealed class ModelCallRetriesTests
{
    private sealed class Overloaded : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("overloaded") };
            response.Headers.TryAddWithoutValidation("retry-after-ms", "0");
            return Task.FromResult(response);
        }
    }

    private static ModelRequest Request(int? maxRetries) => new()
    {
        System = "s",
        Messages = [new ModelMessage { Role = "user", Content = "hi" }],
        Tools = [],
        MaxRetries = maxRetries,
    };

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 1)]
    [InlineData(null, 6)]
    public async Task Sends_a_request_again_only_as_often_as_the_call_allows(int? maxRetries, int sent)
    {
        var handler = new Overloaded();
        using var http = new HttpClient(handler);
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "gpt", ApiKey = "k" }, http);

        var error = await Assert.ThrowsAsync<AgentException>(() => model.CompleteAsync(Request(maxRetries), CancellationToken.None));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Equal(sent, handler.Requests);
    }
}
