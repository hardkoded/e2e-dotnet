// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E.OAuth;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.Fetch;

/// <summary>
/// Upstream's <c>createOAuthFetch</c> is the port's <see cref="OAuthHandler"/>. "lets the provider send the request its
/// own way" is not ported: a port provider prepares a request but cannot send it itself.
/// </summary>
public sealed class CreateOAuthFetchTests
{
    private const string Url = "https://api.test/";

    [Fact]
    public async Task Does_not_refresh_or_send_a_request_whose_signal_was_already_aborted()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = 1,
                },
            }
        );
        var provider = new RotatingProvider();
        using var http = Client(provider, store, api);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.GetAsync(new Uri(Url), cancellation.Token));

        Assert.Empty(provider.Refreshes);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Cancels_one_refresh_waiter_without_cancelling_the_shared_refresh_another_request_needs()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, """{ "ok": true }"""));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = 1,
                },
            }
        );
        var provider = new GatedProvider();
        using var first = Client(provider, store, api);
        using var second = Client(provider, store, api);
        using var cancellation = new CancellationTokenSource();

        var abandoned = first.GetAsync(new Uri(Url), cancellation.Token);
        await provider.Started.Task;
        var active = second.GetAsync(new Uri(Url));
        await cancellation.CancelAsync();

        try
        {
            var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
            Assert.Equal(cancellation.Token, cancelled.CancellationToken);
            Assert.Empty(api.Requests);
        }
        finally
        {
            provider.Release.SetResult(
                new OAuthCredentials
                {
                    Access = "fresh",
                    Refresh = "rotated",
                    Expires = RotatingProvider.Later(),
                }
            );
        }

        using var response = await active;
        Assert.Equal("""{ "ok": true }""", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, provider.RefreshCount);
        var sent = Assert.Single(api.Requests);
        Assert.Equal("Bearer fresh", sent.Header("authorization"));
        var stored = await store.GetAsync("test");
        Assert.Equal(("fresh", "rotated"), (stored!.Access, stored.Refresh));
    }

    [Fact]
    public async Task Swaps_the_SDK_key_header_for_the_bearer_token_and_names_the_product()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, """{ "ok": true }"""));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "tok",
                    Refresh = "r",
                    Expires = 0,
                },
            }
        );
        using var http = Client(new RotatingProvider(), store, api);
        using var request = new HttpRequestMessage(HttpMethod.Post, Url + "v1/thing")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-api-key", "oauth");
        request.Headers.TryAddWithoutValidation("authorization", "Bearer oauth");

        using var response = await http.SendAsync(request);

        Assert.Equal("""{ "ok": true }""", await response.Content.ReadAsStringAsync());
        var sent = Assert.Single(api.Requests);
        Assert.Equal("Bearer tok", sent.Header("authorization"));
        Assert.Null(sent.Header("x-api-key"));
        Assert.Equal(E2E.Internal.ModelHttp.UserAgent, sent.Header("user-agent"));
        Assert.Equal("{}", sent.Body);
    }

    [Fact]
    public async Task Fails_with_NOT_LOGGED_IN_and_the_hint_when_nothing_is_stored()
    {
        using var http = Client(
            new RotatingProvider(),
            new MemoryCredentialStore(),
            new FakeApi(_ => (HttpStatusCode.OK, "{}")),
            "run e2e login test"
        );
        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));
        Assert.Equal(OAuthException.NotLoggedIn, error.Code);
        Assert.Contains("run e2e login test", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refreshes_an_expiring_token_once_across_model_instances_that_share_a_store()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = RotatingProvider.Soon(),
                },
            }
        );
        var provider = new RotatingProvider();
        using var first = Client(provider, store, api);
        using var second = Client(provider, store, api);

        await Task.WhenAll(first.GetAsync(new Uri(Url)), second.GetAsync(new Uri(Url)), first.GetAsync(new Uri(Url)));

        Assert.Equal(["rt-0"], provider.Refreshes);
        Assert.Equal(
            ["Bearer fresh-1", "Bearer fresh-1", "Bearer fresh-1"],
            api.Requests.Select(request => request.Header("authorization"))
        );
        var stored = await store.GetAsync("test");
        Assert.Equal(("fresh-1", "rt-1"), (stored!.Access, stored.Refresh));
    }

    [Fact]
    public async Task Shares_one_refresh_between_separate_file_stores_over_the_same_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-oauth-fetch-" + Guid.NewGuid().ToString("N"));
        try
        {
            var file = Path.Combine(directory, "oauth.json");
            await new FileCredentialStore(file).SetAsync(
                "test",
                new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = RotatingProvider.Soon(),
                }
            );
            var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
            var provider = new RotatingProvider();
            using var first = Client(provider, new FileCredentialStore(file), api);
            using var second = Client(provider, new FileCredentialStore(file), api);

            await Task.WhenAll(first.GetAsync(new Uri(Url)), second.GetAsync(new Uri(Url)));

            Assert.Equal(["rt-0"], provider.Refreshes);
            Assert.Equal(
                ["Bearer fresh-1", "Bearer fresh-1"],
                api.Requests.Select(request => request.Header("authorization"))
            );
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Refreshes_an_expired_login_once_for_workers_in_separate_processes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-oauth-fetch-" + Guid.NewGuid().ToString("N"));
        var children = new List<System.Diagnostics.Process>();
        try
        {
            var file = Path.Combine(directory, "oauth.json");
            await new FileCredentialStore(file).SetAsync(
                "test",
                new OAuthCredentials
                {
                    Access = "at-0",
                    Refresh = "rt-0",
                    Expires = 1,
                }
            );
            var gate = new object();
            var live = (Access: "at-0", Refresh: "rt-0");
            var issued = 0;
            var tokenRequests = 0;
            var port = AppProgram.FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            listener.Start();
            var serving = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await listener.GetContextAsync();
                    }
                    catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                    {
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        var response = context.Response;
                        try
                        {
                            if (context.Request.Url!.AbsolutePath != "/token")
                            {
                                bool fresh;
                                lock (gate)
                                {
                                    fresh = context.Request.Headers["Authorization"] == "Bearer " + live.Access;
                                }

                                await Reply(response, fresh ? 200 : 401, "{}");
                                return;
                            }

                            using var reader = new StreamReader(context.Request.InputStream);
                            var presented = System.Web.HttpUtility.ParseQueryString(await reader.ReadToEndAsync())[
                                "refresh_token"
                            ];
                            string? body = null;
                            lock (gate)
                            {
                                tokenRequests++;
                                if (presented != live.Refresh)
                                {
                                    body =
                                        """{ "error": "invalid_grant", "error_description": "refresh_token_reused" }""";
                                }
                                else
                                {
                                    issued++;
                                    live = ("at-" + issued, "rt-" + issued);
                                }
                            }

                            if (body is not null)
                            {
                                await Reply(response, 400, body);
                                return;
                            }

                            // Long enough for every worker to reach the token endpoint while the first refresh is still out.
                            await Task.Delay(500);
                            string issuedBody;
                            lock (gate)
                            {
                                issuedBody =
                                    "{ \"access_token\": \""
                                    + live.Access
                                    + "\", \"refresh_token\": \""
                                    + live.Refresh
                                    + "\", \"expires_in\": 3600 }";
                            }

                            await Reply(response, 200, issuedBody);
                        }
                        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                        {
                            // The test ended while this request was out: drop its connection.
                            response.Abort();
                        }
                    });
                }
            });

            var statuses = new List<Task<string>>();
            for (var index = 0; index < 4; index++)
            {
                var info = new System.Diagnostics.ProcessStartInfo(AppProgram.Executable)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (
                    var arg in new[]
                    {
                        "refresh-worker",
                        file,
                        "http://127.0.0.1:" + port + "/token",
                        "http://127.0.0.1:" + port + "/api",
                    }
                )
                {
                    info.ArgumentList.Add(arg);
                }

                var child = System.Diagnostics.Process.Start(info)!;
                children.Add(child);
                _ = child.StandardError.ReadToEndAsync();
                statuses.Add(child.StandardOutput.ReadToEndAsync());
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            foreach (var child in children)
            {
                await child.WaitForExitAsync(timeout.Token);
            }

            Assert.Equal(["200", "200", "200", "200"], await Task.WhenAll(statuses));
            lock (gate)
            {
                Assert.Equal(1, tokenRequests);
            }

            listener.Stop();
            await serving;
        }
        finally
        {
            // Killed when the test ends, even on a timeout, so a worker that hangs is not left running.
            foreach (var child in children)
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }

                child.Dispose();
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task Reply(HttpListenerResponse response, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    [Fact]
    public async Task Keeps_credentials_it_renewed_for_a_source_that_cannot_store_them()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var expires = RotatingProvider.Soon().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var store = new EnvironmentCredentialStore(
            """{ "test": { "access": "stale", "refresh": "rt-0", "expires": """ + expires + " } }"
        );
        var provider = new RotatingProvider();
        using var http = Client(provider, store, api);

        await http.GetAsync(new Uri(Url));
        await http.GetAsync(new Uri(Url));

        Assert.Equal(["rt-0"], provider.Refreshes);
        Assert.Equal(
            ["Bearer fresh-1", "Bearer fresh-1"],
            api.Requests.Select(request => request.Header("authorization"))
        );
    }

    [Fact]
    public async Task Fails_with_LOGIN_REQUIRED_and_the_hint_when_the_refresh_token_is_rejected_for_good()
    {
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = RotatingProvider.Soon(),
                },
            }
        );
        var provider = new RotatingProvider();
        provider.Refreshes.Add("rt-0");
        using var http = Client(
            provider,
            store,
            new FakeApi(_ => (HttpStatusCode.OK, "{}")),
            "run `npx e2e login test`"
        );

        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));

        Assert.Equal(OAuthException.LoginRequired, error.Code);
        Assert.Equal("refresh token rt-0 was already used; run `npx e2e login test`", error.Message);
    }

    [Fact]
    public async Task Uses_the_credentials_another_process_stored_when_its_own_refresh_token_was_already_rotated()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = RotatingProvider.Soon(),
                },
            }
        );
        var provider = new RotatingProvider();
        provider.Refreshes.Add("rt-0");
        using var http = Client(provider, store, api);

        // The other process rotated the token and stores it a moment after this one was rejected.
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await store.SetAsync(
                "test",
                new OAuthCredentials
                {
                    Access = "theirs",
                    Refresh = "rt-1",
                    Expires = RotatingProvider.Later(),
                }
            );
        });
        await http.GetAsync(new Uri(Url));

        Assert.Equal("Bearer theirs", api.Requests.First().Header("authorization"));
    }

    [Fact]
    public async Task Uses_the_token_another_process_stored_while_this_one_waited_for_the_lock_sending_no_refresh()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = StaleStore(expires: 1);
        // Another process refreshed and wrote a fresh token while this one waited for the lock.
        store.Exclusive = async (providerId, refresh) =>
        {
            await store.SetAsync(providerId, Stored("theirs", "rt-1", RotatingProvider.Later()));
            return await refresh();
        };
        var provider = new RotatingProvider();
        using var http = Client(provider, store, api);

        await http.GetAsync(new Uri(Url));

        Assert.Empty(provider.Refreshes);
        Assert.Equal(["Bearer theirs"], api.Requests.Select(request => request.Header("authorization")));
    }

    [Fact]
    public async Task Refreshes_a_token_another_process_stored_when_that_token_is_expiring_too()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = StaleStore(expires: 1);
        // Another process wrote a different token while this one waited for the lock, and that token lapses too.
        store.Exclusive = async (providerId, refresh) =>
        {
            await store.SetAsync(providerId, Stored("other", "rt-1", RotatingProvider.Soon()));
            return await refresh();
        };
        var provider = new RotatingProvider();
        using var http = Client(provider, store, api);

        await http.GetAsync(new Uri(Url));

        Assert.Equal(["rt-1"], provider.Refreshes);
        Assert.Equal(["Bearer fresh-1"], api.Requests.Select(request => request.Header("authorization")));
    }

    [Fact]
    public async Task Waits_for_another_process_to_store_a_rotated_token_only_after_releasing_the_refresh_lock()
    {
        var store = StaleStore(expires: 1);
        var held = false;
        var readsWhileHeld = 0;
        store.Exclusive = async (_, refresh) =>
        {
            held = true;
            try
            {
                return await refresh();
            }
            finally
            {
                held = false;
            }
        };
        store.OnGet = () =>
        {
            if (held)
            {
                readsWhileHeld++;
            }
        };
        var provider = new RotatingProvider();
        provider.Refreshes.Add("rt-0");
        using var http = Client(provider, store, new FakeApi(_ => (HttpStatusCode.OK, "{}")));

        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));

        Assert.Equal(OAuthException.LoginRequired, error.Code);
        // The re-read alone; the poll runs after release, so waiters on a revoked login do not wait it out in turn.
        Assert.Equal(1, readsWhileHeld);
    }

    [Fact]
    public async Task Fails_with_LOGIN_REQUIRED_when_the_token_another_process_stored_is_rejected_too()
    {
        var api = new FakeApi(_ => (HttpStatusCode.OK, "{}"));
        var store = StaleStore(expires: 1);
        store.Exclusive = async (providerId, refresh) =>
        {
            await store.SetAsync(providerId, Stored("other", "rt-1", RotatingProvider.Soon()));
            return await refresh();
        };
        var provider = new FailingProvider(token => new OAuthException(
            OAuthException.LoginRequired,
            "refresh token " + token + " was revoked"
        ));
        using var http = Client(provider, store, api);

        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));

        Assert.Equal(OAuthException.LoginRequired, error.Code);
        // The poll waits for a token other than the one this refresh sent, not the one it read before the lock.
        Assert.Equal(["rt-1"], provider.Refreshes);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Passes_on_any_other_refresh_failure_as_it_is_without_waiting_for_another_process()
    {
        var store = StaleStore(expires: 1);
        var reads = 0;
        store.OnGet = () => reads++;
        var provider = new FailingProvider(_ => new OAuthException(
            OAuthException.FlowFailed,
            "the token response is missing refresh_token"
        ));
        using var http = Client(
            provider,
            store,
            new FakeApi(_ => (HttpStatusCode.OK, "{}")),
            "run `npx e2e login test`"
        );

        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));

        Assert.Equal(OAuthException.FlowFailed, error.Code);
        Assert.Equal("the token response is missing refresh_token", error.Message);
        // The read before the refresh and the re-read inside the lock; no poll for a rotated token.
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task Retries_once_with_a_refreshed_token_after_a_401_also_for_a_Request_input()
    {
        var api = new FakeApi(request =>
            request.Header("authorization") == "Bearer stale"
                ? (HttpStatusCode.Unauthorized, """{ "error": "expired" }""")
                : (HttpStatusCode.OK, "{\"echo\":\"" + request.Body + "\"}")
        );
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "stale",
                    Refresh = "rt-0",
                    Expires = 0,
                },
            }
        );
        using var http = Client(new RotatingProvider(), store, api);

        using var response = await http.PostAsync(new Uri(Url), new StringContent("payload"));

        Assert.Equal("""{"echo":"payload"}""", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal("payload", api.Requests.Last().Body);
    }

    [Fact]
    public async Task Fails_with_LOGIN_REQUIRED_and_the_hint_on_a_401_for_a_token_that_cannot_be_refreshed_without_a_retry()
    {
        var api = new FakeApi(_ => (HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""));
        var store = new MemoryCredentialStore(
            new()
            {
                ["test"] = new OAuthCredentials
                {
                    Access = "gh",
                    Refresh = "",
                    Expires = 0,
                },
            }
        );
        using var http = Client(new RotatingProvider(), store, api, "run `npx e2e login test`");

        var error = await Assert.ThrowsAsync<OAuthException>(() => http.GetAsync(new Uri(Url)));

        Assert.Equal(OAuthException.LoginRequired, error.Code);
        Assert.Equal("Test rejected the stored token (401: Bad credentials); run `npx e2e login test`", error.Message);
        Assert.Single(api.Requests);
    }

    private static HttpClient Client(
        IOAuthProvider provider,
        ICredentialStore store,
        HttpMessageHandler api,
        string? loginHint = null
    ) => new(new OAuthHandler(provider, store, loginHint, api));

    private static MemoryCredentialStore StaleStore(long expires) =>
        new(new() { ["test"] = Stored("stale", "rt-0", expires) });

    private static OAuthCredentials Stored(string access, string refresh, long expires) =>
        new()
        {
            Access = access,
            Refresh = refresh,
            Expires = expires,
        };

    /// <summary>A provider whose refresh always fails with the error <paramref name="failure"/> builds from the refresh token it was sent.</summary>
    private sealed class FailingProvider(Func<string, OAuthException> failure) : IOAuthProvider
    {
        public List<string> Refreshes { get; } = [];

        public string Id => "test";

        public string Name => "Test";

        public Task<OAuthCredentials> LoginAsync(
            OAuthLoginCallbacks callbacks,
            OAuthLoginOptions options,
            CancellationToken cancellationToken
        ) => Task.FromResult(new OAuthCredentials { Access = "x", Refresh = "y" });

        public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken)
        {
            Refreshes.Add(credentials.Refresh);
            throw failure(credentials.Refresh);
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

    /// <summary>A provider whose refresh waits until the test releases it, so a second request can queue behind it.</summary>
    private sealed class GatedProvider : IOAuthProvider
    {
        private int _refreshCount;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<OAuthCredentials> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RefreshCount => _refreshCount;

        public string Id => "test";

        public string Name => "Test";

        public Task<OAuthCredentials> LoginAsync(
            OAuthLoginCallbacks callbacks,
            OAuthLoginOptions options,
            CancellationToken cancellationToken
        ) => Task.FromResult(new OAuthCredentials { Access = "x", Refresh = "y" });

        public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _refreshCount);
            Started.TrySetResult();
            return Release.Task;
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
