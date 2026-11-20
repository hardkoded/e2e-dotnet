// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>Upstream's <c>describe('HTTP errors')</c> of <c>typesafe-ai-decision-model.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class HttpErrorsTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(529)]
    public async Task Preserves_status_and_retryability_for_status(int status)
    {
        var (server, model) = Create();
        server.RespondError(Url, status, """{"detail":"Provider error"}""");

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal(status, error.StatusCode);
        Assert.Equal("Provider error", error.Message);
        Assert.Equal(status is 429 or 529, error.IsRetryable);
        Assert.Single(server.Calls);
    }

    [Theory]
    [InlineData(
        """{"detail":[{"loc":["body","questions"],"msg":"Invalid question"}]}""",
        """[{"loc":["body","questions"],"msg":"Invalid question"}]"""
    )]
    [InlineData("""{"error":{"message":"Overloaded"}}""", "Overloaded")]
    [InlineData("""{"error":"Overloaded"}""", "Overloaded")]
    [InlineData("""{"message":"Overloaded"}""", "Overloaded")]
    public async Task Handles_provider_error_envelopes(string body, string message)
    {
        var (server, model) = Create();
        server.RespondError(Url, 422, body);

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal(422, error.StatusCode);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Surfaces_a_bare_error_type_code_observed_in_production_as_max_tokens_exceeded()
    {
        var (server, model) = Create();
        server.RespondError(Url, 400, """{"error_type":"max_tokens_exceeded"}""");

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("max_tokens_exceeded", error.Message);
    }

    [Fact]
    public async Task Prefers_a_prose_field_over_the_error_type_code()
    {
        var (server, model) = Create();
        server.RespondError(
            Url,
            400,
            """{"error_type":"max_tokens_exceeded","message":"Input exceeds the model context window"}"""
        );

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal("Input exceeds the model context window", error.Message);
    }

    [Fact]
    public async Task Still_falls_back_when_the_body_carries_no_recognised_field()
    {
        var (server, model) = Create();
        server.RespondError(Url, 400, """{"unrecognised":"shape"}""");

        var error = await Assert.ThrowsAsync<ApiCallException>(() =>
            model.DoDecideAsync(Options(), CancellationToken.None)
        );

        Assert.Equal("TypeSafe request failed", error.Message);
    }

    [Fact]
    public async Task Rejects_malformed_successful_responses()
    {
        var (server, model) = Create();
        server.RespondJson(Url, """{"answers":{"test":{"type":"noul"}}}""");

        await Assert.ThrowsAsync<ApiCallException>(() => model.DoDecideAsync(Options(), CancellationToken.None));
    }
}
