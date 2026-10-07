// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;

namespace E2E.Tests.Store;

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
        Assert.Null(await store.GetAsync("spacexai"));
        await store.SetAsync("spacexai", Creds);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(file)!));
        }

        var read = await store.GetAsync("spacexai");
        Assert.Equal(("a", "r", 123L, "acct"), (read!.Access, read.Refresh, read.Expires, read.Get("accountId")));
        Assert.Equal(["spacexai"], await store.ListAsync());
        await store.RemoveAsync("spacexai");
        Assert.Empty(await store.ListAsync());
        Assert.Equal("{}\n", await File.ReadAllTextAsync(file));
        Assert.False(File.Exists(file + ".lock"));
    }

    [Fact]
    public async Task Drops_a_damaged_entry_instead_of_losing_every_login()
    {
        var file = Path.Combine(_directory, "nested", "oauth.json");
        var store = new FileCredentialStore(file);
        await store.SetAsync("x", Creds);
        await File.WriteAllTextAsync(file, """{"x":{"access":"a","refresh":"r","expires":123,"accountId":"acct"},"broken":{"access":1},"nan":{"access":"a","refresh":"r","expires":null}}""");
        Assert.Equal(["x"], await store.ListAsync());
        Assert.Null(await store.GetAsync("broken"));
    }
}
