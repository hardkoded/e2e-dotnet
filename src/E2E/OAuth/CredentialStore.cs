// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.OAuth;

/// <summary>Where credentials live between runs, keyed by provider id.</summary>
public interface ICredentialStore
{
    Task<OAuthCredentials?> GetAsync(string providerId, CancellationToken cancellationToken = default);

    Task SetAsync(string providerId, OAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Forgets the login. True when one was stored.</summary>
    Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="next"/> only while the stored login is still <paramref name="expected"/>; false when a
    /// login or logout replaced it meanwhile, which then stands.
    /// </summary>
    Task<bool> ReplaceAsync(
        string providerId,
        OAuthCredentials expected,
        OAuthCredentials next,
        CancellationToken cancellationToken = default
    );

    /// <summary>Every provider id with stored credentials.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="refresh"/> while no other process refreshes <paramref name="providerId"/>'s login from this store, so a
    /// rotating refresh token is sent once however many workers need it.
    /// </summary>
    Task<T> ExclusiveAsync<T>(string providerId, Func<Task<T>> refresh, CancellationToken cancellationToken = default);
}

/// <summary>The stores the subscription models and the CLI use.</summary>
public static class CredentialStores
{
    /// <summary>Credentials JSON in the environment, standing in for the file on a machine the user cannot sign in on.</summary>
    public const string EnvironmentVariable = "E2E_OAUTH_CREDENTIALS";

    /// <summary>
    /// <c>$XDG_CONFIG_HOME/e2e/oauth.json</c>, or <c>~/.config/e2e/oauth.json</c>: the file upstream's
    /// <c>e2e login</c> writes, so a login made with either CLI serves both.
    /// </summary>
    public static string DefaultPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var root = string.IsNullOrEmpty(configHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : configHome;
        return Path.Combine(root, "e2e", "oauth.json");
    }

    /// <summary><see cref="EnvironmentVariable"/> when set, else the file at <see cref="DefaultPath"/>.</summary>
    public static ICredentialStore Default()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return string.IsNullOrEmpty(fromEnv)
            ? new FileCredentialStore(DefaultPath())
            : new EnvironmentCredentialStore(fromEnv);
    }

    /// <summary>Keys an earlier release stored a login under, to the provider id that reads it now. Kept as is so that release still finds the login.</summary>
    private static readonly Dictionary<string, string> LegacyKeys = new(StringComparer.Ordinal)
    {
        ["spacexai"] = "spacexsi",
    };

    internal static string CurrentId(string key) => LegacyKeys.TryGetValue(key, out var current) ? current : key;

    /// <summary>The key <paramref name="providerId"/>'s login is stored under: the id itself, else a legacy key that maps to it, else the id.</summary>
    internal static string StoredKey(Dictionary<string, JsonNode?> all, string providerId)
    {
        if (all.ContainsKey(providerId))
        {
            return providerId;
        }

        return all.Keys.FirstOrDefault(key => CurrentId(key) == providerId) ?? providerId;
    }

    internal static bool SameLogin(OAuthCredentials? stored, OAuthCredentials expected) =>
        stored is not null && stored.Access == expected.Access && stored.Refresh == expected.Refresh;

    internal static IReadOnlyList<string> StoredIds(Dictionary<string, JsonNode?> all) =>
        [
            .. all.Where(pair => OAuthCredentials.FromJson(pair.Value) is not null)
                .Select(pair => CurrentId(pair.Key))
                .Distinct(StringComparer.Ordinal),
        ];

    internal static Dictionary<string, JsonNode?> ParseFile(string text, string source)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new OAuthException(OAuthException.Misconfigured, source + " is not valid JSON", ex);
        }

        if (parsed is not JsonObject entries)
        {
            throw new OAuthException(
                OAuthException.Misconfigured,
                source + " must hold an object keyed by provider id"
            );
        }

        return entries.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
    }
}

/// <summary>
/// One JSON file keyed by provider id, mode 0600. Writes hold an advisory lock and replace the file atomically,
/// so two runs refreshing at once neither tear the file nor lose each other's entries. Entries this store does
/// not understand are kept as they are.
/// </summary>
public sealed class FileCredentialStore : ICredentialStore
{
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);

    /// <summary>A token request has no deadline of its own, so a waiter gives up on its own.</summary>
    private static readonly TimeSpan RefreshLockWait = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _clock;

    public FileCredentialStore(string path)
        : this(path, TimeProvider.System) { }

    internal FileCredentialStore(string path, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
        _clock = clock;
    }

    public string FilePath { get; }

    public Task<OAuthCredentials?> GetAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var all = Read();
        return Task.FromResult(
            all.TryGetValue(CredentialStores.StoredKey(all, providerId), out var entry)
                ? OAuthCredentials.FromJson(entry)
                : null
        );
    }

    public Task SetAsync(string providerId, OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return UpdateAsync(
            all => all[CredentialStores.StoredKey(all, providerId)] = credentials.ToJson(),
            cancellationToken
        );
    }

    public async Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await UpdateAsync(
                all =>
                {
                    foreach (var key in all.Keys.Where(key => CredentialStores.CurrentId(key) == providerId).ToList())
                    {
                        removed |= all.Remove(key);
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return removed;
    }

    public async Task<bool> ReplaceAsync(
        string providerId,
        OAuthCredentials expected,
        OAuthCredentials next,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(next);
        var replaced = false;
        await UpdateAsync(
                all =>
                {
                    var key = CredentialStores.StoredKey(all, providerId);
                    replaced =
                        all.TryGetValue(key, out var entry)
                        && CredentialStores.SameLogin(OAuthCredentials.FromJson(entry), expected);
                    if (replaced)
                    {
                        all[key] = next.ToJson();
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return replaced;
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CredentialStores.StoredIds(Read()));
    }

    /// <summary>Runs <paramref name="refresh"/> holding this login's lock file, so worker processes refresh it one at a time.</summary>
    public Task<T> ExclusiveAsync<T>(
        string providerId,
        Func<Task<T>> refresh,
        CancellationToken cancellationToken = default
    )
    {
        var lockPath = FilePath + "." + CredentialStores.StoredKey(Read(), providerId) + ".refresh.lock";
        return FileLock.WithFileLockAsync(
            lockPath,
            RefreshLockWait,
            () =>
            {
                var waited =
                    RefreshLockWait.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " s";
                var message = "another e2e process is still refreshing the " + providerId + " login after " + waited;
                return new OAuthException(OAuthException.Timeout, message + "; run again once it finishes");
            },
            refresh,
            cancellationToken,
            _clock
        );
    }

    private Dictionary<string, JsonNode?> Read()
    {
        string text;
        try
        {
            text = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        }

        return CredentialStores.ParseFile(text, FilePath);
    }

    private async Task UpdateAsync(Action<Dictionary<string, JsonNode?>> change, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var lockPath = FilePath + ".lock";
        await FileLock
            .WithFileLockAsync(
                lockPath,
                LockWait,
                () =>
                    new OAuthException(
                        OAuthException.Misconfigured,
                        lockPath + " is held by another process; remove it if that process is gone"
                    ),
                async () =>
                {
                    var all = Read();
                    change(all);
                    var json = new JsonObject();
                    foreach (var (id, entry) in all)
                    {
                        json[id] = entry;
                    }

                    var temp = FilePath + "." + Environment.ProcessId + "." + DateTime.UtcNow.Ticks + ".tmp";
                    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                    if (!OperatingSystem.IsWindows())
                    {
                        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    }

                    try
                    {
                        await using (var stream = new FileStream(temp, options))
                        await using (var writer = new StreamWriter(stream))
                        {
                            await writer
                                .WriteAsync(
                                    json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n"
                                )
                                .ConfigureAwait(false);
                        }

                        File.Move(temp, FilePath, overwrite: true);
                    }
                    catch
                    {
                        File.Delete(temp);
                        throw;
                    }

                    return true;
                },
                cancellationToken,
                _clock
            )
            .ConfigureAwait(false);
    }
}

/// <summary>Credentials from <see cref="CredentialStores.EnvironmentVariable"/>: readable everywhere, changed nowhere.</summary>
public sealed class EnvironmentCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, JsonNode?> _entries;

    public EnvironmentCredentialStore(string json)
    {
        _entries = CredentialStores.ParseFile(json, CredentialStores.EnvironmentVariable);
    }

    public Task<OAuthCredentials?> GetAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _entries.TryGetValue(CredentialStores.StoredKey(_entries, providerId), out var entry)
                ? OAuthCredentials.FromJson(entry)
                : null
        );
    }

    /// <summary>Throws <see cref="OAuthException.Misconfigured"/>: this store never takes a write. Lets a login fail before it starts.</summary>
    internal void AssertWritable()
    {
        throw ReadOnly();
    }

    public Task SetAsync(string providerId, OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    public Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    public Task<bool> ReplaceAsync(
        string providerId,
        OAuthCredentials expected,
        OAuthCredentials next,
        CancellationToken cancellationToken = default
    )
    {
        throw ReadOnly();
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CredentialStores.StoredIds(_entries));
    }

    /// <summary>Each process holds its own copy of the variable, so there is nothing to share a refresh with.</summary>
    public Task<T> ExclusiveAsync<T>(
        string providerId,
        Func<Task<T>> refresh,
        CancellationToken cancellationToken = default
    )
    {
        return refresh();
    }

    private static OAuthException ReadOnly()
    {
        return new OAuthException(
            OAuthException.Misconfigured,
            CredentialStores.EnvironmentVariable
                + " is set, so logins come from the environment and cannot be changed here; unset it to sign in on this machine"
        );
    }
}
