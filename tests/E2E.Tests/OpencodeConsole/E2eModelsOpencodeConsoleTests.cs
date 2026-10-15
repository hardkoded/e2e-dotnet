// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.OAuth;
using E2E.Tests.Fetch;
using E2E.Tests.Store;

namespace E2E.Tests.OpencodeConsole;

[Collection(ProcessEnvironment.Name)]
public sealed class E2eModelsOpencodeConsoleTests
{
    [Fact]
    public async Task Lists_the_Zen_and_Go_models_the_workspace_serves_Go_ids_prefixed_leaving_out_disabled_free_and_custom_provider_models()
    {
        var api = OpencodeConsoleWorkspace.Serve();
        var store = new MemoryCredentialStore(new() { ["opencode-console"] = OpencodeConsoleWorkspace.Login });
        var provider = new OpenCodeConsoleProvider(consoleUrl: OpencodeConsoleWorkspace.ConsoleUrl);
        using var http = Subscriptions.Client(provider, store, inner: api);

        var models = await provider.ListModelsAsync(http, CancellationToken.None);

        Assert.Equal("org_1", api.Requests.First().Header("x-org-id"));
        Assert.Equal(
            [
                ("claude-sonnet-5", "Claude Sonnet 5", "Zen, $2 in, $10 out per 1M, vision"),
                ("gemini-3.1-pro", null, "Zen, $2 in, $12 out per 1M"),
                ("gpt-5-nano", null, "Zen, $0.05 in, $0.4 out per 1M"),
                ("glm-5.3", null, "Zen, $1.4 in, $4.4 out per 1M"),
                ("go/deepseek-v4.1-flash", "DeepSeek V4.1 Flash", "Go, vision"),
            ],
            models.Select(model => (model.Id, model.Name, model.Detail)));
    }

    [Fact]
    public async Task Lists_ids_through_OPENCODE_API_KEY_when_no_login_is_stored_the_credential_opencodeConsole_uses()
    {
        var api = OpencodeConsoleWorkspace.Serve();
        using var _ = ProcessEnvironment.Set("OPENCODE_API_KEY", " oc_sk_env ");

        var models = await ListAsync(api, new MemoryCredentialStore());

        Assert.Equal("Bearer oc_sk_env", api.Requests.First().Header("authorization"));
        // A service key names no workspace, so there is no header to send.
        Assert.Null(api.Requests.First().Header("x-org-id"));
        Assert.Contains("go/deepseek-v4.1-flash", models.Select(model => model.Id));
    }

    [Fact]
    public async Task Reads_the_key_over_a_stored_login_as_opencodeConsole_does()
    {
        var api = OpencodeConsoleWorkspace.Serve();
        using var _ = ProcessEnvironment.Set("OPENCODE_API_KEY", "oc_sk_env");

        await ListAsync(api, new MemoryCredentialStore(new() { ["opencode-console"] = OpencodeConsoleWorkspace.Login }));

        Assert.Equal("Bearer oc_sk_env", api.Requests.First().Header("authorization"));
        Assert.Null(api.Requests.First().Header("x-org-id"));
    }

    [Fact]
    public async Task Reads_the_key_when_the_stored_logins_are_unreadable()
    {
        var api = OpencodeConsoleWorkspace.Serve();
        using var logins = ProcessEnvironment.Set(CredentialStores.EnvironmentVariable, "{not json");
        using var key = ProcessEnvironment.Set("OPENCODE_API_KEY", "oc_sk_env");

        await ListAsync(api, store: null);

        Assert.Equal("Bearer oc_sk_env", api.Requests.First().Header("authorization"));
    }

    [Fact]
    public async Task With_neither_credential_the_message_names_the_key_as_well_as_the_login()
    {
        using var _ = ProcessEnvironment.Set("OPENCODE_API_KEY", null);

        var error = await Assert.ThrowsAsync<OAuthException>(() => ListAsync(OpencodeConsoleWorkspace.Serve(), new MemoryCredentialStore()));

        Assert.Equal(OAuthException.NotLoggedIn, error.Code);
        Assert.Equal("no OpenCode Console login is stored; run `e2e login opencode-console` or set OPENCODE_API_KEY", error.Message);
    }

    [Fact]
    public async Task A_rejected_key_names_the_key_not_the_login_it_stands_in_for()
    {
        var api = new FakeApi(_ => (HttpStatusCode.Unauthorized, """{ "error": "invalid key" }"""));
        using var _ = ProcessEnvironment.Set("OPENCODE_API_KEY", "oc_sk_env");

        var error = await Assert.ThrowsAsync<OAuthException>(() => ListAsync(api, new MemoryCredentialStore(new() { ["opencode-console"] = OpencodeConsoleWorkspace.Login })));

        Assert.Equal(OAuthException.LoginRequired, error.Code);
        Assert.EndsWith("; check OPENCODE_API_KEY", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_the_other_providers_on_their_stored_login()
    {
        var api = new FakeApi(request => request.Uri.AbsolutePath.EndsWith("/models", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{ "models": [{ "slug": "gpt-6-astra", "display_name": "GPT-6 Astra", "visibility": "list", "priority": 0 }] }""")
            : (HttpStatusCode.NotFound, """{ "error": "not found" }"""));
        using var _ = ProcessEnvironment.Set("OPENCODE_API_KEY", "oc_sk_env");
        var store = new MemoryCredentialStore(new() { ["openai"] = new OAuthCredentials { Access = "oai_tok", Refresh = "rt_2", Expires = 0 } });

        var models = await OAuthProviders.ListModelsAsync(new CodexProvider(), store, api, CancellationToken.None);

        // The Console key is not a ChatGPT credential, so the listing runs on the stored login.
        Assert.Equal("Bearer oai_tok", api.Requests.Last().Header("authorization"));
        Assert.Equal(["gpt-6-astra"], models.Select(model => model.Id));
    }

    private static Task<IReadOnlyList<SubscriptionModel>> ListAsync(HttpMessageHandler api, ICredentialStore? store) =>
        OAuthProviders.ListModelsAsync(new OpenCodeConsoleProvider(consoleUrl: OpencodeConsoleWorkspace.ConsoleUrl), store, api, CancellationToken.None);
}
