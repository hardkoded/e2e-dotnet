// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RemoteChromeCleanup;

// .NET-only: the remote Chrome host is a test helper with no upstream `it`. These tests keep its temp profiles from piling up.
public sealed class RemoteChromeCleanupTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("remote-chrome-cleanup-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An indexer or scanner holding a file must not turn a passing test into an error; the temp root is reclaimed later.
        }
    }

    [Fact]
    public void Sweeps_a_stale_profile_and_keeps_a_fresh_one_and_unrelated_directories()
    {
        var stale = Profile("e2e-cdp-host-stale", age: TimeSpan.FromDays(3));
        var fresh = Profile("e2e-cdp-host-fresh", age: TimeSpan.FromMinutes(5));
        var unrelated = Profile("something-else", age: TimeSpan.FromDays(3));

        RemoteChrome.SweepStaleProfiles(_root, TimeSpan.FromDays(1));

        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(fresh));
        Assert.True(Directory.Exists(unrelated));
    }

    [Fact]
    public void Sweeping_a_missing_root_does_not_throw()
    {
        RemoteChrome.SweepStaleProfiles(Path.Combine(_root, "missing"), TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task Deletes_a_profile_with_nested_files_and_ignores_one_already_gone()
    {
        var profile = Profile("e2e-cdp-host-nested", age: TimeSpan.Zero);

        await RemoteChrome.DeleteProfileAsync(profile);
        await RemoteChrome.DeleteProfileAsync(profile);

        Assert.False(Directory.Exists(profile));
    }

    [Fact]
    public void Keeps_a_profile_whose_top_directory_is_old_but_whose_files_are_in_use()
    {
        var live = Profile("e2e-cdp-host-live", age: TimeSpan.FromDays(3));
        File.SetLastWriteTimeUtc(Path.Combine(live, "Default", "Preferences"), DateTime.UtcNow);

        RemoteChrome.SweepStaleProfiles(_root, TimeSpan.FromDays(1));

        Assert.True(Directory.Exists(live));
    }

    [Fact]
    public async Task Retries_while_a_helper_still_holds_a_file_and_deletes_once_it_lets_go()
    {
        var profile = Profile("e2e-cdp-host-held", age: TimeSpan.Zero);
        var held = new FileStream(Path.Combine(profile, "Default", "Preferences"), FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await held.DisposeAsync();
        });

        await RemoteChrome.DeleteProfileAsync(profile);

        Assert.False(Directory.Exists(profile));
    }

    private string Profile(string name, TimeSpan age)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(directory, "Default"));
        var file = Path.Combine(directory, "Default", "Preferences");
        File.WriteAllText(file, "{}");
        var written = DateTime.UtcNow - age;
        File.SetLastWriteTimeUtc(file, written);
        Directory.SetLastWriteTimeUtc(Path.Combine(directory, "Default"), written);
        Directory.SetLastWriteTimeUtc(directory, written);
        return directory;
    }
}
