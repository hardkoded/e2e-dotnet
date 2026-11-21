// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.OAuth;

/// <summary>
/// Upstream's <c>tests/unit/oauth/helpers/refresh-worker.ts</c>: one worker process for the cross-process refresh test. It makes one
/// call over the login stored in the file, refreshing against the token endpoint, and prints the status the API answered.
/// </summary>
internal static class RefreshWorker
{
    public static async Task RunAsync(string file, string tokenUrl, string apiUrl)
    {
        using var http = new HttpClient(new OAuthHandler(new Provider(tokenUrl), new FileCredentialStore(file)));
        using var response = await http.GetAsync(new Uri(apiUrl));
        Console.Out.Write(((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class Provider(string tokenUrl) : IOAuthProvider
    {
        public string Id => "test";

        public string Name => "Test";

        public Task<OAuthCredentials> LoginAsync(
            OAuthLoginCallbacks callbacks,
            OAuthLoginOptions options,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException("not used");

        public async Task<OAuthCredentials> RefreshAsync(
            OAuthCredentials credentials,
            CancellationToken cancellationToken
        )
        {
            var tokens = await TokenEndpoint
                .RequestAsync(
                    OAuthHttp.Shared,
                    "Test",
                    tokenUrl,
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = credentials.Refresh,
                    },
                    OAuthException.LoginRequired,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new OAuthCredentials
            {
                Access = tokens.AccessToken,
                Refresh = tokens.RefreshToken ?? "",
                Expires = TokenEndpoint.ExpiryFrom(tokens.ExpiresIn),
            };
        }

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
}
