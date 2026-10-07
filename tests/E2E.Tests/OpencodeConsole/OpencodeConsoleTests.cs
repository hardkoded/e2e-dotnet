// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.OpencodeConsole;

public sealed class OpencodeConsoleTests
{
    /// <summary>The port's model names no provider, and its user agent is the port's own, so neither is checked.</summary>
    [Fact]
    public async Task Routes_a_Zen_model_to_the_protocol_the_workspace_config_names_naming_the_workspace_and_session_under_one_fixed_name()
    {
        var api = OpencodeConsoleWorkspace.Serve();
        var store = new MemoryCredentialStore(new() { ["opencode-console"] = OpencodeConsoleWorkspace.Login });
        var provider = new OpenCodeConsoleProvider(consoleUrl: OpencodeConsoleWorkspace.ConsoleUrl);
        using var model = new OpenCodeConsoleModel("claude-sonnet-5", provider, Subscriptions.Client(provider, store, inner: api));

        var result = await model.CompleteAsync(new ModelRequest { System = "", Tools = [], Messages = [new ModelMessage { Role = "user", Content = "hi" }] }, CancellationToken.None);

        Assert.Equal("hello", result.Content);
        var config = api.Requests.First();
        Assert.Equal("/console/api/config", config.Uri.AbsolutePath);
        Assert.Equal(("Bearer st_tok", "org_1"), (config.Header("authorization"), config.Header("x-org-id")));
        Assert.Null(config.Header("x-opencode-session"));
        var call = Assert.Single(api.Requests, request => request.Uri.AbsolutePath.StartsWith("/inference/", StringComparison.Ordinal));
        Assert.Equal("/inference/anthropic/v1/messages", call.Uri.AbsolutePath);
        Assert.Equal("Bearer st_tok", call.Header("authorization"));
        Assert.Equal("org_1", call.Header("x-opencode-org-id"));
        // The port writes the session id as 32 hex digits; upstream writes a dashed UUID.
        Assert.Matches(new Regex("^e2e_[0-9a-f]{32}$"), call.Header("x-opencode-session"));
        Assert.Null(call.Header("x-api-key"));
        Assert.Equal("claude-sonnet-5", JsonNode.Parse(call.Body)!["model"]!.GetValue<string>());
    }
}
