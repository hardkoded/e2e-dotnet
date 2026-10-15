// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.ProviderFailureMessage;

/// <summary>No upstream <c>it</c>: upstream's AI SDK hands the error to its own redaction. Here a failure the provider answers with 200 is redacted like an HTTP failure body.</summary>
public sealed class ResponsesFailureRedactionTests
{
    [Theory]
    [InlineData("text/event-stream", "data: {\"type\":\"response.failed\",\"response\":{\"error\":{\"message\":\"bad field: hunter2\"}}}\n\n")]
    [InlineData("application/json", "{\"error\":{\"message\":\"bad field: hunter2\"}}")]
    public async Task Redacts_secrets_in_a_failure_the_provider_answered_with_200(string contentType, string body)
    {
        using var http = new HttpClient(new FixedHandler(contentType, body));
        using var model = new OpenAiResponsesModel(new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" }, http);
        var request = new ModelRequest
        {
            System = "rules",
            Messages = [new ModelMessage { Role = "user", Content = "hi" }],
            Tools = [],
            Redactor = Redactor.For([("password", "hunter2")]),
        };

        var error = await Assert.ThrowsAsync<AgentException>(() => model.CompleteAsync(request, CancellationToken.None));

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("<secret:password>", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
    }

    private sealed class FixedHandler(string contentType, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, contentType) });
        }
    }
}
