// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.SessionEnvelope;

/// <summary><c>describe('session load')</c> of <c>tests/unit/session-envelope.test.ts</c>.</summary>
public sealed class SessionLoadTests : SessionEnvelopeKit
{
    /// <summary>Rewrites a saved envelope file through <paramref name="edit"/>, as a tamperer with write access to it would.</summary>
    private static async Task Tamper(string file, Action<JsonObject> edit)
    {
        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        edit(envelope);
        await File.WriteAllTextAsync(file, envelope.ToJsonString());
    }

    [Fact]
    public async Task Refuses_a_session_saved_for_another_app_engine_or_platform()
    {
        var (store, _, _, _) = await SaveEnvelope();
        try
        {
            foreach (var other in new[]
            {
                Identity with { AppIdentity = new string('b', 64) },
                Identity with { EngineName = "other" },
                Identity with { EngineVersion = "1.62.0" },
                Identity with { Platform = "android" },
            })
            {
                var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.LoadAsync("member", other));
                Assert.Equal("SESSION_MISMATCH", error.Code);
            }

            var loaded = await store.LoadAsync("member", Identity);
            Assert.True(JsonNode.DeepEquals(Cookies().Data, loaded.State.Data));
            Assert.Empty(loaded.Secrets);
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Refuses_a_session_past_its_expiry()
    {
        var time = new FakeTimeProvider();
        var (store, _, _, _) = await SaveEnvelope(time: time);
        try
        {
            time.Advance(TimeSpan.FromHours(24));
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.LoadAsync("member", Identity));
            Assert.Equal("SESSION_EXPIRED", error.Code);
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Reuses_a_valid_cached_state_but_refuses_it_at_its_expiry()
    {
        var time = new FakeTimeProvider();
        var (store, _, _, _) = await SaveEnvelope(time: time);
        try
        {
            var saved = await store.LoadAsync("member", Identity);
            var expiry = DateTimeOffset.Parse(saved.State.ExpiresAt!, CultureInfo.InvariantCulture);
            time.SetUtcNow(expiry.AddMilliseconds(-1));
            Assert.Same(saved, await store.LoadAsync("member", Identity));
            time.SetUtcNow(expiry);
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.LoadAsync("member", Identity));
            Assert.Equal("SESSION_EXPIRED", error.Code);
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Refuses_a_session_whose_ciphertext_secret_names_or_expiry_was_edited_or_that_is_not_JSON()
    {
        Action<JsonObject>[] edits =
        [
            envelope =>
            {
                var bytes = Convert.FromBase64String((string)envelope["state"]!["ciphertext"]!);
                bytes[0] ^= 1;
                envelope["state"]!["ciphertext"] = Convert.ToBase64String(bytes);
            },
            envelope => envelope["secrecy"]!["names"] = new JsonArray(),
            envelope => envelope["expiresAt"] = DateTimeOffset.Parse((string)envelope["expiresAt"]!, CultureInfo.InvariantCulture).AddDays(365).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        ];
        foreach (var edit in edits)
        {
            var (store, _, file, _) = await SaveEnvelope([("token", SecretValue)]);
            try
            {
                await Tamper(file, edit);
                var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.LoadAsync("member", Identity));
                Assert.Equal("SESSION_INVALID", error.Code);
            }
            finally
            {
                store.Cleanup();
            }
        }

        var (plain, _, plainFile, _) = await SaveEnvelope();
        try
        {
            await File.WriteAllTextAsync(plainFile, """{ "schemaVersion": "session-1", """);
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => plain.LoadAsync("member", Identity));
            Assert.Equal("SESSION_INVALID", error.Code);
        }
        finally
        {
            plain.Cleanup();
        }
    }
}
