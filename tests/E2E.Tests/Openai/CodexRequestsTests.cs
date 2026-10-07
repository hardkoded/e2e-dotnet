// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.OAuth;

namespace E2E.Tests.Openai;

public sealed class CodexRequestsTests
{
    private const string Backend = "https://chatgpt.com/backend-api/codex/responses";

    /// <summary>The port's Codex model sends to the backend itself, so the request already has the backend URL.</summary>
    [Fact]
    public async Task Routes_a_Responses_request_at_the_Codex_backend_with_the_body_the_backend_requires()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Backend)
        {
            Content = new StringContent("""{ "model": "gpt-5.5", "input": [], "max_output_tokens": 100, "include": ["x"] }"""),
        };
        var credentials = new OAuthCredentials { Access = "a", Refresh = "r", Extra = new Dictionary<string, string> { ["accountId"] = "acct", ["residency"] = "eu" } };

        await new CodexProvider(originator: "e2e", apiUrl: Backend).PrepareAsync(request, credentials, CancellationToken.None);

        Assert.Equal(Backend, request.RequestUri!.AbsoluteUri);
        Assert.Equal("acct", request.Headers.GetValues("chatgpt-account-id").Single());
        Assert.Equal("e2e", request.Headers.GetValues("originator").Single());
        Assert.Equal("eu", request.Headers.GetValues("x-openai-internal-codex-residency").Single());
        var expected = JsonNode.Parse("""{ "model": "gpt-5.5", "input": [], "stream": true, "store": false, "instructions": "Follow the user request.", "include": ["x", "reasoning.encrypted_content"] }""");
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(await request.Content!.ReadAsStringAsync())), await request.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Leaves_other_paths_untouched_apart_from_the_headers()
    {
        using var other = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        await new CodexProvider(originator: "e2e", apiUrl: "https://c/x").PrepareAsync(other, new OAuthCredentials { Access = "a", Refresh = "r" }, CancellationToken.None);
        Assert.Equal("https://api.openai.com/v1/models", other.RequestUri!.AbsoluteUri);
        Assert.Equal("e2e", other.Headers.GetValues("originator").Single());
    }
}
