// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.OAuth;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.Store;

[Collection(ProcessEnvironment.Name)]
public sealed class FileCredentialStoreTests : IDisposable
{
    private static readonly OAuthCredentials Creds = new()
    {
        Access = "a",
        Refresh = "r",
        Expires = 123,
        Extra = new Dictionary<string, string> { ["accountId"] = "acct" },
    };

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "e2e-oauth-" + Guid.NewGuid().ToString("N"));

    private static OAuthCredentials Renewed(string access = "renewed") =>
        new()
        {
            Access = access,
            Refresh = "r",
            Expires = 123,
            Extra = new Dictionary<string, string> { ["accountId"] = "acct" },
        };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Creates_the_file_with_owner_only_permissions_and_round_trips_entries()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        Assert.Null(await store.GetAsync("spacexsi"));
        await store.SetAsync("spacexsi", Creds);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(file)!)
            );
        }

        var read = await store.GetAsync("spacexsi");
        Assert.Equal(("a", "r", 123L, "acct"), (read!.Access, read.Refresh, read.Expires, read.Get("accountId")));
        Assert.Equal(["spacexsi"], await store.ListAsync());
        await store.RemoveAsync("spacexsi");
        Assert.Empty(await store.ListAsync());
        Assert.Equal("{}\n", await File.ReadAllTextAsync(file));
        Assert.False(File.Exists(file + ".lock"));
    }

    [Fact]
    public async Task Reads_a_login_an_earlier_release_stored_under_a_legacy_key_and_keeps_it_under_that_key_so_that_release_still_finds_it()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("openai", Creds);
        await File.WriteAllTextAsync(file, new JsonObject { ["spacexai"] = Creds.ToJson() }.ToJsonString());
        Assert.Equal("a", (await store.GetAsync("spacexsi"))!.Access);
        Assert.Equal(["spacexsi"], await store.ListAsync());
        await store.SetAsync("spacexsi", Renewed());
        var stored = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        Assert.Equal(["spacexai"], stored.Select(pair => pair.Key));
        Assert.Equal("renewed", (string?)stored["spacexai"]!["access"]);
        Assert.True(await store.RemoveAsync("spacexsi"));
        Assert.Equal("{}\n", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Refreshes_a_login_stored_under_a_legacy_key_holding_the_lock_an_earlier_release_holds_for_it()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("openai", Creds);
        await File.WriteAllTextAsync(file, new JsonObject { ["spacexai"] = Creds.ToJson() }.ToJsonString());
        await store.ExclusiveAsync("spacexsi", () =>
        {
            Assert.True(File.Exists(file + ".spacexai.refresh.lock"));
            Assert.False(File.Exists(file + ".spacexsi.refresh.lock"));
            return Task.FromResult(true);
        });
    }

    [Fact]
    public async Task Replaces_a_login_only_while_it_is_still_the_one_stored_under_the_key_it_is_stored_under()
    {
        var file = Path.Combine(_directory, "oauth.json");
        var store = new FileCredentialStore(file);
        var renewed = new OAuthCredentials
        {
            Access = "renewed",
            Refresh = "r2",
            Expires = 123,
            Extra = Creds.Extra,
        };
        Assert.False(await store.ReplaceAsync("spacexsi", Creds, renewed));
        Assert.Empty(await store.ListAsync());
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            file,
            new JsonObject { ["spacexai"] = Creds.ToJson(), ["openai"] = Creds.ToJson() }.ToJsonString()
        );
        Assert.True(await store.ReplaceAsync("spacexsi", Creds, renewed));
        var expected = new JsonObject { ["spacexai"] = renewed.ToJson(), ["openai"] = Creds.ToJson() };
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(await File.ReadAllTextAsync(file))));

        // The stored login changed meanwhile: a new login stands.
        var stale = new OAuthCredentials
        {
            Access = "stale-renewal",
            Refresh = "r",
            Expires = 123,
            Extra = Creds.Extra,
        };
        Assert.False(await store.ReplaceAsync("spacexsi", Creds, stale));
        var stored = (await store.GetAsync("spacexsi"))!;
        Assert.True(JsonNode.DeepEquals(renewed.ToJson(), stored.ToJson()));
    }

    [Fact]
    public async Task Prefers_the_login_under_the_id_to_one_under_its_legacy_key()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("openai", Creds);
        await File.WriteAllTextAsync(
            file,
            new JsonObject { ["spacexai"] = Creds.ToJson(), ["spacexsi"] = Renewed("newer").ToJson() }.ToJsonString()
        );
        Assert.Equal("newer", (await store.GetAsync("spacexsi"))!.Access);
        Assert.Equal(["spacexsi"], await store.ListAsync());
        await store.RemoveAsync("spacexsi");
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task Reads_no_login_under_a_CLI_alias_since_no_release_stored_one_there()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("openai", Creds);
        await File.WriteAllTextAsync(file, new JsonObject { ["grok"] = Creds.ToJson() }.ToJsonString());
        Assert.Null(await store.GetAsync("spacexsi"));
    }

    [Fact]
    public async Task Holds_a_lock_for_each_login_while_it_refreshes()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("a", Creds);
        var finishA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = store.ExclusiveAsync("a", async () =>
        {
            await finishA.Task;
            return true;
        });
        var enteredSecond = false;
        var second = store.ExclusiveAsync("a", () =>
        {
            enteredSecond = true;
            return Task.FromResult(true);
        });
        Assert.Equal("b", await store.ExclusiveAsync("b", () => Task.FromResult("b")));
        Assert.False(enteredSecond);
        finishA.SetResult();
        await Task.WhenAll(first, second);
        Assert.True(enteredSecond);
    }

    [Fact]
    public async Task Gives_up_with_TIMEOUT_after_30_s_of_another_worker_refreshing()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FileCredentialStore(file, clock);
        await store.SetAsync("x", Creds);
        var lockPath = file + ".x.refresh.lock";
        await File.WriteAllTextAsync(lockPath, "elsewhere.example 1 other");
        // Touched in the future, so the lock stays fresh however far the fake clock moves.
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddHours(1));
        var pending = store.ExclusiveAsync("x", () => Task.FromResult(true));
        await AdvanceAsync(clock, TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        await AdvanceAsync(clock, TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<OAuthException>(() => pending);
        Assert.Equal(OAuthException.Timeout, error.Code);
        Assert.Equal(
            "another e2e process is still refreshing the x login after 30 s; run again once it finishes",
            error.Message
        );
    }

    [Fact]
    public async Task Drops_a_damaged_entry_instead_of_losing_every_login()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("x", Creds);
        await File.WriteAllTextAsync(
            file,
            """{"x":{"access":"a","refresh":"r","expires":123,"accountId":"acct"},"broken":{"access":1},"nan":{"access":"a","refresh":"r","expires":null}}"""
        );
        Assert.Equal(["x"], await store.ListAsync());
        Assert.Null(await store.GetAsync("broken"));
    }

    [Fact]
    public async Task Keeps_other_providers_when_one_is_written_even_from_concurrent_writers()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var a = new FileCredentialStore(file);
        var b = new FileCredentialStore(file);
        await Task.WhenAll(
            a.SetAsync("a", Creds),
            b.SetAsync("b", With(Creds, "b")),
            a.SetAsync("c", With(Creds, "c"))
        );
        Assert.Equal(["a", "b", "c"], (await a.ListAsync()).Order(StringComparer.Ordinal));
        Assert.Equal("b", (await b.GetAsync("b"))?.Access);
    }

    [Fact]
    public async Task Defers_a_writer_while_another_process_holds_the_lock()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("x", Creds);
        await File.WriteAllTextAsync(file + ".lock", "");
        var settled = false;
        var pending = store.SetAsync("y", Creds).ContinueWith(_ => settled = true, TaskScheduler.Default);
        await Task.Delay(150);
        Assert.False(settled);
        File.Delete(file + ".lock");
        await pending;
        Assert.Equal(["x", "y"], (await store.ListAsync()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Tightens_the_permissions_of_a_directory_and_file_that_already_existed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var file = Path.Combine(_directory, "nested", "oauth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.SetUnixFileMode(Path.GetDirectoryName(file)!, (UnixFileMode)Convert.ToInt32("755", 8));
        await File.WriteAllTextAsync(file, "{}");
        File.SetUnixFileMode(file, (UnixFileMode)Convert.ToInt32("644", 8));
        await new FileCredentialStore(file).SetAsync("x", Creds);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(file)!)
        );
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task Takes_over_a_stale_lock_left_by_a_dead_process()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("x", Creds);
        await File.WriteAllTextAsync(file + ".lock", "");
        File.SetLastWriteTimeUtc(file + ".lock", DateTime.UtcNow.AddSeconds(-60));
        await store.SetAsync("y", Creds);
        Assert.Equal(["x", "y"], (await store.ListAsync()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Defaults_to_the_XDG_config_directory()
    {
        using (ProcessEnvironment.Set("XDG_CONFIG_HOME", "/tmp/xdg"))
        {
            Assert.Equal(Path.Combine("/tmp/xdg", "e2e", "oauth.json"), CredentialStores.DefaultPath());
        }

        using (ProcessEnvironment.Set("XDG_CONFIG_HOME", null))
        {
            Assert.EndsWith(
                Path.Combine(".config", "e2e", "oauth.json"),
                CredentialStores.DefaultPath(),
                StringComparison.Ordinal
            );
        }
    }

    /// <summary>Steps the fake clock in small turns, with a real yield before each, so a waiter's delay is seen between steps.</summary>
    private static async Task AdvanceAsync(FakeTimeProvider clock, TimeSpan by)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < by; elapsed += TimeSpan.FromMilliseconds(25))
        {
            await Task.Delay(1);
            clock.Advance(TimeSpan.FromMilliseconds(25));
        }
    }

    private static OAuthCredentials With(OAuthCredentials credentials, string access) =>
        new()
        {
            Access = access,
            Refresh = credentials.Refresh,
            Expires = credentials.Expires,
            Extra = credentials.Extra,
        };
}
