// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>What a state is bound to (upstream's <c>StateIdentity</c>, <c>core/state.ts</c>): it restores only on a host of the same engine, platform, and app.</summary>
/// <param name="Engine">The engine's name.</param>
/// <param name="Platform">The platform label.</param>
/// <param name="AppIdentity">The digest of the app's identity, as <see cref="Sessions.TargetIdentity"/> computes it.</param>
/// <param name="App">The app as a message names it: its URL, bundle id, or declared identity. Null on an imported state, whose document holds the digest alone.</param>
internal sealed record StateIdentity(string Engine, string Platform, string AppIdentity, string? App = null);

/// <summary>The options of <see cref="SessionState.ExportAsync"/> and <see cref="SessionState.ImportAsync"/>: the AES-256-GCM key, 32 bytes the caller keeps. Upstream's <c>SessionStateKeyOptions</c>.</summary>
public sealed record SessionStateKeyOptions
{
    /// <summary>The 32-byte AES-256-GCM key.</summary>
    public required byte[] Key { get; init; }
}

/// <summary>
/// What a session's <c>SaveStateAsync</c> captures (the engine's cookies, storage, or app data), held in memory. Upstream's <c>SessionState</c>
/// (<c>core/state.ts</c>). It leaves the process only encrypted, under a key the caller holds, as a <c>session-state-1</c> document; there is no
/// plaintext export. Pass it to <see cref="Host.NewSessionAsync"/> to start a session from it, on a host of the same engine, platform, and app.
/// </summary>
public sealed class SessionState
{
    private const int KeyBytes = 32;

    private const int IvBytes = 12;

    private const int TagBytes = 16;

    private const string SchemaVersion = "session-state-1";

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    // A 12-byte IV and a 16-byte tag in base64, as schema/session-state-v1.schema.json pins them.
    private static readonly Regex IvPattern = new(@"^[A-Za-z0-9+/]{16}\z", RegexOptions.CultureInvariant, PatternTimeout);

    private static readonly Regex TagPattern = new(@"^[A-Za-z0-9+/]{22}==\z", RegexOptions.CultureInvariant, PatternTimeout);

    private readonly StateIdentity _identity;

    private readonly SavedSession _saved;

    private SessionState(StateIdentity identity, SavedSession saved)
    {
        _identity = identity;
        _saved = saved;
        Engine = identity.Engine;
        Platform = identity.Platform;
    }

    /// <summary>The engine that captured it, by name.</summary>
    public string Engine { get; }

    /// <summary>The platform it was captured on.</summary>
    public string Platform { get; }

    internal StateIdentity Identity => _identity;

    internal SavedSession Saved => _saved;

    /// <summary>
    /// Decrypts a document <see cref="ExportAsync"/> wrote. A document that is not <c>session-state-1</c>, or that fails authentication under
    /// the key (a wrong key, an edited byte), is <c>SESSION_INVALID</c>; an expired one is <c>SESSION_EXPIRED</c>.
    /// </summary>
    public static Task<SessionState> ImportAsync(byte[] data, SessionStateKeyOptions options)
    {
        try
        {
            return Task.FromResult(Import(data, options));
        }
        catch (Exception cause)
        {
            // Async so a bad key or document rejects instead of throwing, as upstream's async import does.
            return Task.FromException<SessionState>(cause);
        }
    }

    /// <summary>
    /// Encrypts the state with the key (AES-256-GCM, 32 bytes) into a <c>session-state-1</c> document, as UTF-8 bytes. The document names the
    /// engine, the platform, a digest of the app, and the names of the secrets in the clear, authenticated; everything the state holds,
    /// secret values included, is in the ciphertext.
    /// </summary>
    public Task<byte[]> ExportAsync(SessionStateKeyOptions options)
    {
        try
        {
            return Task.FromResult(Export(options));
        }
        catch (Exception cause)
        {
            return Task.FromException<byte[]>(cause);
        }
    }

    /// <summary>A state a session just captured. Upstream's <c>SessionState.captured</c>.</summary>
    internal static SessionState Captured(StateIdentity identity, EngineState state, IReadOnlyList<(string Name, string Value)> secrets) =>
        new(identity, new SavedSession(state, secrets));

    /// <summary>
    /// What a host restores from <paramref name="state"/>, checked against the host's own identity: another engine, platform, or app is
    /// <c>SESSION_MISMATCH</c>, an expired state <c>SESSION_EXPIRED</c>. Upstream's <c>restoredStateFor</c>.
    /// </summary>
    internal static SavedSession RestoredStateFor(object? state, StateIdentity host)
    {
        if (state is not SessionState held)
        {
            throw new TestException("INVALID_ARGUMENT", "NewSessionAsync state must be a SessionState from SaveStateAsync() or SessionState.ImportAsync()");
        }

        var identity = held._identity;
        if (!string.Equals(identity.Engine, host.Engine, StringComparison.Ordinal) || !string.Equals(identity.Platform, host.Platform, StringComparison.Ordinal))
        {
            throw new ConfigurationException(
                "SESSION_MISMATCH",
                $"the session state was saved on engine {identity.Engine} ({identity.Platform}), and this host runs {host.Engine} ({host.Platform}); a state restores only on the engine and platform that saved it");
        }

        if (!string.Equals(identity.AppIdentity, host.AppIdentity, StringComparison.Ordinal))
        {
            throw new ConfigurationException(
                "SESSION_MISMATCH",
                $"the session state was saved for {identity.App ?? "another app"}, and this host drives {host.App ?? "an app with no URL or bundle id"}; a state restores only on the app it was saved for. "
                + "When both are one app at two addresses, give both hosts the same app identity");
        }

        if (IsExpired(held._saved.State.ExpiresAt))
        {
            throw new ConfigurationException("SESSION_EXPIRED", "the session state is expired");
        }

        return held._saved;
    }

    private static SessionState Import(byte[] data, SessionStateKeyOptions options)
    {
        var key = RequireKey(options, "SessionState.ImportAsync");
        if (data is null)
        {
            throw new TestException("INVALID_ARGUMENT", "SessionState.ImportAsync data must be a byte array");
        }

        var document = ParseDocument(Encoding.UTF8.GetString(data));
        var expiresAt = document["expiresAt"]?.GetValue<string>();
        if (IsExpired(expiresAt))
        {
            throw new ConfigurationException("SESSION_EXPIRED", "the session state is expired");
        }

        var state = document["state"]!.AsObject();
        var tag = Convert.FromBase64String(state["tag"]!.GetValue<string>());
        var ciphertext = Convert.FromBase64String(state["ciphertext"]!.GetValue<string>());
        var iv = Convert.FromBase64String(state["iv"]!.GetValue<string>());
        var sealedDocument = document.DeepClone().AsObject();
        sealedDocument["state"]!.AsObject().Remove("tag");
        sealedDocument["state"]!.AsObject().Remove("ciphertext");
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var cipher = new AesGcm(key, TagBytes);
            cipher.Decrypt(iv, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(Ids.CanonicalJson(sealedDocument)));
        }
        catch (CryptographicException cause)
        {
            throw new ConfigurationException("SESSION_INVALID", "the session state failed authentication: a different key, or the document was changed", cause);
        }

        var (data2, secrets) = ParsePayload(Encoding.UTF8.GetString(plaintext));
        return new SessionState(
            new StateIdentity(document["engine"]!.GetValue<string>(), document["platform"]!.GetValue<string>(), document["appIdentity"]!.GetValue<string>()),
            new SavedSession(new EngineState(state["format"]!.GetValue<string>(), state["version"]!.GetValue<int>(), data2, expiresAt), secrets));
    }

    private byte[] Export(SessionStateKeyOptions options)
    {
        var key = RequireKey(options, "SessionState.ExportAsync");
        var iv = RandomNumberGenerator.GetBytes(IvBytes);
        var unsealed = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["engine"] = _identity.Engine,
            ["platform"] = _identity.Platform,
            ["appIdentity"] = _identity.AppIdentity,
            ["createdAt"] = Ids.Timestamp(DateTimeOffset.UtcNow),
        };
        if (_saved.State.ExpiresAt is { } expiresAt)
        {
            unsealed["expiresAt"] = Ids.Timestamp(DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }

        unsealed["secrecy"] = new JsonObject
        {
            ["names"] = new JsonArray([.. _saved.Secrets.Select(secret => secret.Name).Distinct(StringComparer.Ordinal).Select(name => (JsonNode)name)]),
        };
        unsealed["state"] = new JsonObject
        {
            ["format"] = _saved.State.Format,
            ["version"] = _saved.State.Version,
            ["algorithm"] = "A256GCM",
            ["iv"] = Convert.ToBase64String(iv),
        };

        var payload = new JsonObject
        {
            ["data"] = _saved.State.Data?.DeepClone(),
            ["secrets"] = new JsonArray([.. _saved.Secrets.Select(secret => (JsonNode)new JsonArray(secret.Name, secret.Value))]),
        };
        var plaintext = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using (var cipher = new AesGcm(key, TagBytes))
        {
            cipher.Encrypt(iv, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(Ids.CanonicalJson(unsealed)));
        }

        var state = unsealed["state"]!.AsObject();
        state["tag"] = Convert.ToBase64String(tag);
        state["ciphertext"] = Convert.ToBase64String(ciphertext);
        return Encoding.UTF8.GetBytes(unsealed.ToJsonString() + "\n");
    }

    private static bool IsExpired(string? expiresAt) =>
        expiresAt is not null && DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) && at <= DateTimeOffset.UtcNow;

    private static byte[] RequireKey(SessionStateKeyOptions? options, string api)
    {
        if (options?.Key is not { Length: KeyBytes } key)
        {
            throw new TestException("INVALID_ARGUMENT", $"{api} key must be a byte array of {KeyBytes} bytes, such as RandomNumberGenerator.GetBytes({KeyBytes})");
        }

        return [.. key];
    }

    // Parses a document's clear part; anything ExportAsync would not have written is SESSION_INVALID.
    private static JsonObject ParseDocument(string text)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (JsonException cause)
        {
            throw new ConfigurationException("SESSION_INVALID", "the session state is not valid JSON", cause);
        }

        if (parsed is JsonObject document
            && document["schemaVersion"] is JsonValue schema && schema.TryGetValue<string>(out var version) && version == SchemaVersion
            && IsString(document["engine"]) && IsString(document["platform"]) && IsString(document["appIdentity"]) && IsString(document["createdAt"])
            && (document["expiresAt"] is null || (IsString(document["expiresAt"]) && DateTimeOffset.TryParse(document["expiresAt"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)))
            && document["secrecy"] is JsonObject { } secrecy && secrecy["names"] is JsonArray names && names.All(IsString)
            && document["state"] is JsonObject state
            && IsString(state["format"]) && IsInteger(state["version"]) && state["algorithm"] is JsonValue algorithm && algorithm.TryGetValue<string>(out var name) && name == "A256GCM"
            && IsString(state["iv"]) && IvPattern.IsMatch(state["iv"]!.GetValue<string>())
            && IsString(state["tag"]) && TagPattern.IsMatch(state["tag"]!.GetValue<string>())
            && IsString(state["ciphertext"]) && IsBase64(state["ciphertext"]!.GetValue<string>()))
        {
            return document;
        }

        throw new ConfigurationException("SESSION_INVALID", "the session state is not a session-state-1 document");
    }

    // Parses an authenticated payload; one ExportAsync did not write is SESSION_INVALID.
    private static (JsonNode? Data, IReadOnlyList<(string Name, string Value)> Secrets) ParsePayload(string text)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (JsonException cause)
        {
            throw new ConfigurationException("SESSION_INVALID", "the session state has an unexpected payload", cause);
        }

        if (parsed is JsonObject payload && payload.ContainsKey("data") && payload["secrets"] is JsonArray secrets
            && secrets.All(entry => entry is JsonArray { Count: 2 } pair && IsString(pair[0]) && IsString(pair[1])))
        {
            return (payload["data"]?.DeepClone(), [.. secrets.Select(entry => (entry![0]!.GetValue<string>(), entry[1]!.GetValue<string>()))]);
        }

        throw new ConfigurationException("SESSION_INVALID", "the session state has an unexpected payload");
    }

    private static bool IsString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out _);

    private static bool IsInteger(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out _);

    private static bool IsBase64(string text) => Convert.TryFromBase64String(text, new byte[text.Length], out _);
}
