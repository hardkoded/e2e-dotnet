// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.Login;

public sealed class LoginAndLogoutDuringARefreshTests : IDisposable
{
    private static readonly OAuthLoginCallbacks Callbacks = new()
    {
        OnAuth = _ => { },
        OnPrompt = (_, _) => Task.FromResult(""),
    };

    private static readonly OAuthCredentials Old = new()
    {
        Access = "old",
        Refresh = "old-refresh",
        Expires = 1,
    };

    private static readonly OAuthCredentials SignedIn = new()
    {
        Access = "new-account",
        Refresh = "new-refresh",
        Expires = 0,
    };

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "e2e-oauth-login-" + Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Keeps_the_new_login_over_the_credentials_the_refresh_renews()
    {
        var (store, provider, request) = await RefreshInFlightAsync();
        await OAuthProviders.LoginAsync(
            new SignInProvider(provider.Id, SignedIn),
            Callbacks,
            null,
            store,
            CancellationToken.None
        );
        provider.Release.SetResult(Renewed());
        using var _ = await request;
        Assert.Equal("new-account", (await store.GetAsync(provider.Id))!.Access);
        Assert.Equal("new-refresh", (await store.GetAsync(provider.Id))!.Refresh);
    }

    [Fact]
    public async Task Keeps_the_login_removed_when_the_refresh_finishes_after_logout()
    {
        var (store, provider, request) = await RefreshInFlightAsync();
        Assert.True(await OAuthProviders.LogoutAsync(provider.Id, store));
        provider.Release.SetResult(Renewed());
        using var _ = await request;
        Assert.Null(await store.GetAsync(provider.Id));
    }

    private static OAuthCredentials Renewed() =>
        new()
        {
            Access = "old-renewed",
            Refresh = "old-refresh-2",
            Expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000,
        };

    /// <summary>A file store holding an expired login, and a request that has started refreshing it and waits on the provider's release.</summary>
    private async Task<(
        FileCredentialStore Store,
        RefreshProvider Provider,
        Task<HttpResponseMessage> Request
    )> RefreshInFlightAsync()
    {
        var store = new FileCredentialStore(Path.Combine(_directory, "oauth.json"));
        var provider = new RefreshProvider("github-copilot");
        await store.SetAsync(provider.Id, Old);
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var http = new HttpClient(new OAuthHandler(provider, store, null, api));
        var request = http.GetAsync(new Uri("https://api.test/"));
        await provider.Started.Task;
        return (store, provider, request);
    }

    private class SignInProvider(string id, OAuthCredentials credentials) : IOAuthProvider
    {
        public string Id => id;

        public string Name => id;

        public Task<OAuthCredentials> LoginAsync(
            OAuthLoginCallbacks callbacks,
            OAuthLoginOptions options,
            CancellationToken cancellationToken
        ) => Task.FromResult(credentials);

        public virtual Task<OAuthCredentials> RefreshAsync(
            OAuthCredentials credentials,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task PrepareAsync(
            HttpRequestMessage request,
            OAuthCredentials credentials,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(
            HttpClient http,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyList<SubscriptionModel>>([]);
    }

    private sealed class RefreshProvider(string id) : SignInProvider(id, Old)
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<OAuthCredentials> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<OAuthCredentials> RefreshAsync(
            OAuthCredentials credentials,
            CancellationToken cancellationToken
        )
        {
            Started.TrySetResult();
            return Release.Task;
        }
    }
}
