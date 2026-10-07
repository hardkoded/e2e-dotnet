// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.Copilot;

public sealed class CopilotRequestsTests
{
    [Fact]
    public async Task Marks_agent_turns_and_image_requests_and_routes_an_enterprise_login_at_its_host()
    {
        var provider = new CopilotProvider();
        using var withImage = new HttpRequestMessage(HttpMethod.Post, CopilotProvider.ApiUrl + "/chat/completions")
        {
            Content = new StringContent("""{ "messages": [{ "role": "user", "content": [{ "type": "text", "text": "x" }, { "type": "image_url", "image_url": { "url": "data:image/png;base64,AA==" } }] }, { "role": "assistant", "content": "ok" }] }"""),
        };
        await provider.PrepareAsync(withImage, new OAuthCredentials { Access = "a", Extra = new Dictionary<string, string> { ["enterpriseUrl"] = "gh.acme.com" } }, CancellationToken.None);
        Assert.Equal("https://copilot-api.gh.acme.com/chat/completions", withImage.RequestUri!.AbsoluteUri);
        Assert.Equal("agent", withImage.Headers.GetValues("x-initiator").Single());
        Assert.Equal("true", withImage.Headers.GetValues("copilot-vision-request").Single());
        Assert.Equal("conversation-edits", withImage.Headers.GetValues("openai-intent").Single());

        using var plain = new HttpRequestMessage(HttpMethod.Post, CopilotProvider.ApiUrl + "/chat/completions")
        {
            Content = new StringContent("""{ "messages": [{ "role": "user", "content": "hi" }] }"""),
        };
        await provider.PrepareAsync(plain, new OAuthCredentials { Access = "a" }, CancellationToken.None);
        Assert.Equal("https://api.githubcopilot.com/chat/completions", plain.RequestUri!.AbsoluteUri);
        Assert.Equal("user", plain.Headers.GetValues("x-initiator").Single());
        Assert.False(plain.Headers.Contains("copilot-vision-request"));
    }

    /// <summary>The port's model names no provider, so the check that the delegate switches from chat to Responses is not ported.</summary>
    [Fact]
    public async Task Calls_a_model_Copilot_lists_only_over_Responses_through_the_Responses_API_with_no_server_storage()
    {
        var api = new FakeApi(request => request.Uri.AbsolutePath == "/models"
            ? (HttpStatusCode.OK, """{ "data": [{ "id": "gpt-6-luna", "vendor": "OpenAI", "supported_endpoints": ["/responses", "ws:/responses"], "capabilities": { "type": "chat" } }] }""")
            : (HttpStatusCode.OK, """
                { "id": "resp_1", "created_at": 1, "model": "gpt-6-luna",
                  "output": [{ "type": "message", "id": "msg_1", "role": "assistant", "content": [{ "type": "output_text", "text": "luna", "annotations": [] }] }],
                  "usage": { "input_tokens": 7, "output_tokens": 3, "total_tokens": 10 } }
                """));
        var store = new MemoryCredentialStore(new() { ["github-copilot"] = new OAuthCredentials { Access = "gho_x", Refresh = "", Expires = 0 } });
        using var model = new CopilotModel("gpt-6-luna", Subscriptions.Client(new CopilotProvider(), store, inner: api));

        var result = await model.CompleteAsync(new ModelRequest { System = "", Tools = [], Messages = [new ModelMessage { Role = "user", Content = "color?" }] }, CancellationToken.None);

        Assert.Equal("luna", result.Content);
        var seen = api.Requests.Last();
        Assert.Equal("/responses", seen.Uri.AbsolutePath);
        Assert.Equal("Bearer gho_x", seen.Header("authorization"));
        var body = JsonNode.Parse(seen.Body)!.AsObject();
        Assert.IsType<JsonArray>(body["input"]);
        Assert.False(body.ContainsKey("messages"));
        Assert.False(body["store"]!.GetValue<bool>());
    }
}
