// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The identity a saved session is keyed on (upstream's <c>SessionIdentity</c>, <c>run/sessions.ts</c>): the target and the engine and app
/// it ran against, so a session never restores into another engine, platform, or app.
/// </summary>
internal sealed record SessionIdentity(string TargetId, string EngineName, string EngineVersion, int SpiVersion, string Platform, string AppIdentity);

/// <summary>The identity-making function of upstream's <c>run/sessions.ts</c>.</summary>
internal static class Sessions
{
    /// <summary>
    /// The identity session and replay cache entries key on for one target (upstream's <c>targetIdentity</c>). It comes from the engine's
    /// name and version and the target's resolved app, so an engine swap never restores another engine's state. The app's declared
    /// identity (else its URL, bundle id, or build path) keys entries, so an ephemeral per-deploy origin (a PR preview) can share them
    /// with the app it is a deployment of. The environment always joins the digest: an identity must never bleed entries across
    /// environments.
    /// </summary>
    public static SessionIdentity TargetIdentity(string targetName, string platform, IEngine engine, ResolvedApp app)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(app);
        var identity = new JsonObject();
        if (app.Identity is not null)
        {
            identity["identity"] = app.Identity;
        }

        identity["environment"] = app.Environment;
        return new SessionIdentity(targetName, engine.Name, engine.Version, BuildReport.EngineSpiVersion, platform, Ids.CanonicalDigest(identity));
    }
}

/// <summary>What one saved session holds once decrypted (upstream's <c>SavedSession</c>).</summary>
internal sealed record SavedSession(EngineState State, SavedSecrecy Secrecy);

/// <summary>
/// Collects the states saved by one setup attempt and owns the setup session contract (upstream's <c>SessionStaging</c>,
/// <c>run/sessions.ts</c>): each declared name saved exactly once, nothing undeclared.
/// </summary>
internal sealed class SessionStaging(IReadOnlyList<string> declared)
{
    private readonly Dictionary<string, SavedSession> _staged = new(StringComparer.Ordinal);

    /// <summary>Stages one captured state with the secret values of the session it came from, rejecting a duplicate or undeclared name.</summary>
    public void Stage(string name, EngineState state, SavedSecrecy secrecy)
    {
        if (_staged.ContainsKey(name))
        {
            throw new TestException("SESSION_CONTRACT", $"session \"{name}\" saved twice");
        }

        if (!declared.Contains(name, StringComparer.Ordinal))
        {
            throw new TestException("SESSION_CONTRACT", $"session \"{name}\" was not declared by this setup test");
        }

        _staged.Add(name, new SavedSession(state, secrecy));
    }

    /// <summary>Declared names the attempt finished without saving.</summary>
    public IReadOnlyList<string> Missing() => [.. declared.Where(name => !_staged.ContainsKey(name))];

    public IEnumerable<KeyValuePair<string, SavedSession>> Entries() => _staged;
}

/// <summary>
/// The per-run encrypted session store (upstream's <c>SessionStore</c>, <c>run/sessions.ts</c>). It holds the run's AES-256-GCM key in
/// memory and owns the run's session directory. A session is one envelope file (<c>schema/session-v1.schema.json</c>): the identity, the
/// expiry, and the secrecy in the clear, all authenticated, and the engine state with the learned secret values inside the ciphertext.
/// </summary>
internal sealed class SessionStore
{
    private const string SchemaVersion = "session-1";

    private const int KeyLength = 32;

    private const int NonceLength = 12;

    private const int TagLength = 16;

    private const int FileNamePartMax = 40;

    private static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions EnvelopeOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly byte[] _key;

    private readonly string _runId;

    private readonly string _directory;

    private readonly TimeProvider _time;

    private readonly Lock _gate = new();

    /// <summary>Decrypted states by target and name; see <see cref="LoadAsync"/>.</summary>
    private readonly Dictionary<string, Task<SavedSession>> _loaded = [];

    private SessionStore(string runId, string sessionsRoot, byte[] key, TimeProvider time)
    {
        _runId = runId;
        _key = key;
        _time = time;
        _directory = Path.Combine(sessionsRoot, runId);
    }

    /// <summary>Opens the store for a new run, generating its key. <paramref name="time"/> is .NET-only: a test's clock.</summary>
    public static SessionStore Create(string runId, string sessionsRoot, TimeProvider? time = null) =>
        new(runId, sessionsRoot, RandomNumberGenerator.GetBytes(KeyLength), time ?? TimeProvider.System);

    /// <summary>Opens the store in a worker, reusing the key the runner generated.</summary>
    public static SessionStore ForWorker(string runId, string sessionsRoot, byte[] key, TimeProvider? time = null)
    {
        if (key.Length != KeyLength)
        {
            throw new E2EException("SESSION_KEY_INVALID", "session key must be 32 bytes");
        }

        return new SessionStore(runId, sessionsRoot, [.. key], time ?? TimeProvider.System);
    }

    /// <summary>Base64 key for transfer to worker processes over IPC (never disk/env).</summary>
    public string ExportKeyForWorker() => Convert.ToBase64String(_key);

    /// <summary>
    /// Encrypts and atomically persists one captured engine state. The secret values the saving session learned ride inside the ciphertext,
    /// beside the state they may be stored in, so the session that restores it redacts them too; the envelope names them and carries the
    /// taint in the clear, authenticated.
    /// </summary>
    public async Task SaveAsync(string name, SessionIdentity identity, SavedSession saved, CancellationToken cancellationToken = default)
    {
        EnsureDirectory();
        var now = _time.GetUtcNow();
        var expiresAt = now + MaxSessionAge;
        if (saved.State.ExpiresAt is { } declared)
        {
            if (!TryParseTimestamp(declared, out var stateExpiry) || stateExpiry <= now)
            {
                throw new TestException("SESSION_EXPIRED", "state expiry is at or before creation");
            }

            if (stateExpiry < expiresAt)
            {
                expiresAt = stateExpiry;
            }
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var envelope = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["runId"] = _runId,
            ["name"] = name,
            ["targetId"] = identity.TargetId,
            ["engine"] = new JsonObject
            {
                ["name"] = identity.EngineName,
                ["version"] = identity.EngineVersion,
                ["spiVersion"] = identity.SpiVersion,
            },
            ["platform"] = identity.Platform,
            ["appIdentity"] = identity.AppIdentity,
            ["createdAt"] = Ids.Timestamp(now),
            ["expiresAt"] = Ids.Timestamp(expiresAt),
            ["secrecy"] = new JsonObject
            {
                ["tainted"] = saved.Secrecy.Tainted,
                ["names"] = new JsonArray([.. saved.Secrecy.Secrets.Select(secret => secret.Name).Distinct(StringComparer.Ordinal).Select(each => (JsonNode)each)]),
            },
            ["state"] = new JsonObject
            {
                ["format"] = saved.State.Format,
                ["version"] = saved.State.Version,
                ["algorithm"] = "A256GCM",
                ["iv"] = Convert.ToBase64String(nonce),
            },
        };

        var payload = new JsonObject
        {
            ["data"] = saved.State.Data?.DeepClone(),
            ["secrets"] = new JsonArray([.. saved.Secrecy.Secrets.Select(secret => (JsonNode)new JsonArray(secret.Name, secret.Value))]),
        };
        var plaintext = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var cipher = new AesGcm(_key, TagLength))
        {
            cipher.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(Ids.CanonicalJson(envelope)));
        }

        var state = envelope["state"]!.AsObject();
        state["tag"] = Convert.ToBase64String(tag);
        state["ciphertext"] = Convert.ToBase64String(ciphertext);
        await AtomicWrite.WriteFileAtomicAsync(FilePath(identity.TargetId, name), envelope.ToJsonString(EnvelopeOptions) + "\n", UnixFileMode.UserRead | UnixFileMode.UserWrite, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates identity and expiry, then decrypts one session state. A session is immutable for the life of the run that produced it, so the
    /// decrypted state is memoized per store: a worker running many consumers of one session reads and decrypts its file once. Its expiry is
    /// still checked on every load: an immutable state can expire between consumers.
    /// </summary>
    public async Task<SavedSession> LoadAsync(string name, SessionIdentity identity, CancellationToken cancellationToken = default)
    {
        var memoKey = identity.TargetId + "\0" + name;
        Task<SavedSession> loading;
        lock (_gate)
        {
            if (!_loaded.TryGetValue(memoKey, out loading!))
            {
                loading = LoadUncachedAsync(name, identity);
                _loaded[memoKey] = loading;
            }
        }

        SavedSession saved;
        try
        {
            // The shared read is not tied to one caller's token: a cancelled caller stops waiting, and the others still get the state.
            saved = await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (loading.IsFaulted)
        {
            // A failed load is never kept, so the next consumer reads the file again.
            lock (_gate)
            {
                if (_loaded.TryGetValue(memoKey, out var current) && ReferenceEquals(current, loading))
                {
                    _loaded.Remove(memoKey);
                }
            }

            throw;
        }

        AssertSessionNotExpired(name, saved.State.ExpiresAt);
        return saved;
    }

    /// <summary>Deletes the run's session directory.</summary>
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static bool TryParseTimestamp(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);

    /// <summary>Refuses an expired saved session, whether freshly decrypted or already cached.</summary>
    private void AssertSessionNotExpired(string name, string? expiresAt)
    {
        if (expiresAt is not null && (!TryParseTimestamp(expiresAt, out var expiry) || expiry <= _time.GetUtcNow()))
        {
            throw new ConfigurationException("SESSION_EXPIRED", $"session \"{name}\" is expired");
        }
    }

    private void EnsureDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private string FilePath(string targetId, string name)
    {
        var hash = Ids.Sha256Hex(targetId + "\0" + name)[..16];
        return Path.Combine(_directory, $"{FileNamePart(targetId)}--{FileNamePart(name)}-{hash}.json");
    }

    /// <summary>
    /// The readable prefix of a session file name for one of its parts: every character outside <c>[A-Za-z0-9_]</c>, <c>-</c> and <c>.</c>
    /// included, becomes <c>%XX</c>, so the part carries no path separator or <c>..</c> segment, then the result is cut to a fixed length.
    /// The hash <see cref="FilePath"/> appends is what tells two pairs apart.
    /// </summary>
    private static string FileNamePart(string value)
    {
        var part = new StringBuilder();
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            if (octet is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'_')
            {
                part.Append((char)octet);
            }
            else
            {
                part.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return part.Length > FileNamePartMax ? part.ToString(0, FileNamePartMax) : part.ToString();
    }

    private async Task<SavedSession> LoadUncachedAsync(string name, SessionIdentity identity)
    {
        string rawText;
        try
        {
            rawText = await File.ReadAllTextAsync(FilePath(identity.TargetId, name)).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException("SESSION_UNAVAILABLE", $"session \"{name}\" was not produced", cause);
        }

        var envelope = ParseEnvelope(rawText, name);
        try
        {
            return Open(name, identity, envelope);
        }
        catch (Exception cause) when (cause is JsonException or InvalidOperationException or FormatException or InvalidCastException or ArgumentException)
        {
            throw new ConfigurationException("SESSION_INVALID", $"session \"{name}\" has an unexpected shape", cause);
        }
    }

    private SavedSession Open(string name, SessionIdentity identity, JsonObject envelope)
    {
        var engine = envelope["engine"]!.AsObject();
        var state = envelope["state"]!.AsObject();
        if (Text(envelope, "schemaVersion") != SchemaVersion
            || Text(envelope, "runId") != _runId
            || Text(envelope, "targetId") != identity.TargetId
            || Text(engine, "name") != identity.EngineName
            || Text(engine, "version") != identity.EngineVersion
            || (int?)engine["spiVersion"] != identity.SpiVersion
            || Text(envelope, "platform") != identity.Platform
            || Text(envelope, "appIdentity") != identity.AppIdentity)
        {
            throw new ConfigurationException("SESSION_MISMATCH", $"session \"{name}\" does not match the current run/target/engine identity");
        }

        var expiresAt = Text(envelope, "expiresAt");
        AssertSessionNotExpired(name, expiresAt);

        var tag = Convert.FromBase64String(Text(state, "tag"));
        var ciphertext = Convert.FromBase64String(Text(state, "ciphertext"));
        var nonce = Convert.FromBase64String(Text(state, "iv"));
        var authenticated = (JsonObject)envelope.DeepClone();
        authenticated["state"]!.AsObject().Remove("tag");
        authenticated["state"]!.AsObject().Remove("ciphertext");
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var decipher = new AesGcm(_key, TagLength);
            decipher.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(Ids.CanonicalJson(authenticated)));
        }
        catch (Exception cause) when (cause is CryptographicException or ArgumentException)
        {
            throw new ConfigurationException("SESSION_INVALID", $"session \"{name}\" failed authentication", cause);
        }

        var payload = ParsePayload(Encoding.UTF8.GetString(plaintext), name);
        var tainted = envelope["secrecy"] is JsonObject secrecy && secrecy["tainted"] is JsonValue flag && flag.TryGetValue<bool>(out var isTainted) && isTainted;
        return new SavedSession(
            new EngineState(Text(state, "format"), state["version"]?.GetValue<int>() ?? throw new InvalidOperationException("version is not a number"), payload["data"]?.DeepClone(), expiresAt),
            new SavedSecrecy([.. payload["secrets"]!.AsArray().Select(entry => ((string)entry![0]!, (string)entry[1]!))], tainted));
    }

    private static string Text(JsonObject owner, string key) => (string?)owner[key] ?? throw new InvalidOperationException(key + " is not text");

    /// <summary>Parses an envelope file, classifying a corrupt or truncated one as SESSION_INVALID.</summary>
    private static JsonObject ParseEnvelope(string rawText, string name)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(rawText);
        }
        catch (JsonException cause)
        {
            throw new ConfigurationException("SESSION_INVALID", $"session \"{name}\" is not valid JSON", cause);
        }

        if (parsed is not JsonObject envelope || envelope["state"] is not JsonObject || envelope["engine"] is not JsonObject)
        {
            throw new ConfigurationException("SESSION_INVALID", $"session \"{name}\" has an unexpected shape");
        }

        return envelope;
    }

    /// <summary>Parses an authenticated payload; one this runner did not write is SESSION_INVALID.</summary>
    private static JsonObject ParsePayload(string text, string name)
    {
        var parsed = JsonNode.Parse(text);
        if (parsed is not JsonObject payload
            || !payload.ContainsKey("data")
            || payload["secrets"] is not JsonArray secrets
            || !secrets.All(entry => entry is JsonArray { Count: 2 } pair && pair.All(part => part is JsonValue value && value.TryGetValue<string>(out _))))
        {
            throw new ConfigurationException("SESSION_INVALID", $"session \"{name}\" has an unexpected payload");
        }

        return payload;
    }
}
