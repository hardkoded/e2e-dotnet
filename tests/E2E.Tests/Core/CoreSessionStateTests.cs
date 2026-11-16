// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Tests.Core;

/// <summary>Upstream's <c>describe('SessionState')</c> (<c>tests/unit/core-state.test.ts</c>).</summary>
public sealed class CoreSessionStateTests
{
    private static readonly StateIdentity Identity = new("web", "web", new string('a', 64));

    private static readonly byte[] Key = [.. Enumerable.Repeat((byte)1, 32)];

    private static SessionState Captured(string? expiresAt = null) =>
        SessionState.Captured(
            Identity,
            new EngineState(
                "storage",
                1,
                new JsonObject
                {
                    ["cookies"] = new JsonArray(new JsonObject { ["name"] = "sid", ["value"] = "cookie-value" }),
                },
                expiresAt
            ),
            [("token", "provider-token-value")]
        );

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static async Task<ConfigurationException> Invalid(byte[] bytes, byte[] key) =>
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            SessionState.ImportAsync(bytes, new SessionStateKeyOptions { Key = key })
        );

    [Fact]
    public async Task Exports_a_session_state_1_document_that_names_the_host_in_the_clear_and_holds_nothing_else_readable()
    {
        var bytes = await Captured("2099-01-01T00:00:00Z").ExportAsync(new SessionStateKeyOptions { Key = Key });

        var document = JsonNode.Parse(Text(bytes))!.AsObject();
        Assert.True(JsonSchemaCheck.FromFile("session-state-v1.schema.json").IsValid(document));
        Assert.Equal("web", (string?)document["engine"]);
        Assert.Equal("web", (string?)document["platform"]);
        Assert.Equal("2099-01-01T00:00:00.000Z", (string?)document["expiresAt"]);
        Assert.Equal(["token"], document["secrecy"]!["names"]!.AsArray().Select(name => (string?)name));
        Assert.DoesNotContain("cookie-value", Text(bytes), StringComparison.Ordinal);
        Assert.DoesNotContain("provider-token-value", Text(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Round_trips_under_the_same_key_and_restores_on_a_matching_host()
    {
        var restored = await SessionState.ImportAsync(
            await Captured().ExportAsync(new SessionStateKeyOptions { Key = Key }),
            new SessionStateKeyOptions { Key = Key }
        );

        Assert.Equal("web", restored.Engine);
        var saved = SessionState.RestoredStateFor(restored, Identity);
        Assert.Equal("storage", saved.State.Format);
        Assert.Equal(1, saved.State.Version);
        Assert.Null(saved.State.ExpiresAt);
        Assert.Equal("""{"cookies":[{"name":"sid","value":"cookie-value"}]}""", saved.State.Data!.ToJsonString());
        Assert.Equal([("token", "provider-token-value")], saved.Secrets);
    }

    [Fact]
    public async Task Refuses_another_key_an_edited_document_and_something_that_is_not_one_as_SESSION_INVALID()
    {
        var bytes = await Captured().ExportAsync(new SessionStateKeyOptions { Key = Key });

        Assert.Equal("SESSION_INVALID", (await Invalid(bytes, [.. Enumerable.Repeat((byte)2, 32)])).Code);
        var edited = JsonNode.Parse(Text(bytes))!.AsObject();
        edited["platform"] = "ios";
        Assert.Equal("SESSION_INVALID", (await Invalid(Encoding.UTF8.GetBytes(edited.ToJsonString()), Key)).Code);
        Assert.Equal("SESSION_INVALID", (await Invalid(Encoding.UTF8.GetBytes("{}"), Key)).Code);
        Assert.Equal("SESSION_INVALID", (await Invalid(Encoding.UTF8.GetBytes("not json"), Key)).Code);
        foreach (var field in new[] { "iv", "tag" })
        {
            var malformed = JsonNode.Parse(Text(bytes))!.AsObject();
            malformed["state"]![field] = "";
            Assert.Equal(
                "SESSION_INVALID",
                (await Invalid(Encoding.UTF8.GetBytes(malformed.ToJsonString()), Key)).Code
            );
        }
    }

    [Fact]
    public async Task Takes_a_32_byte_key_only()
    {
        var short16 = await Assert.ThrowsAsync<TestException>(() =>
            Captured().ExportAsync(new SessionStateKeyOptions { Key = new byte[31] })
        );
        Assert.Equal("INVALID_ARGUMENT", short16.Code);
        var none = await Assert.ThrowsAsync<TestException>(() => Captured().ExportAsync(null!));
        Assert.Equal("INVALID_ARGUMENT", none.Code);
    }

    [Fact]
    public async Task Refuses_an_expired_state_and_restores_only_on_the_engine_platform_and_app_it_was_captured_on()
    {
        var expired = Captured("2001-01-01T00:00:00.000Z");

        Assert.Equal(
            "SESSION_EXPIRED",
            (await Invalid(await expired.ExportAsync(new SessionStateKeyOptions { Key = Key }), Key)).Code
        );
        Assert.Equal(
            "SESSION_EXPIRED",
            Assert.Throws<ConfigurationException>(() => SessionState.RestoredStateFor(expired, Identity)).Code
        );
        Assert.Equal(
            "SESSION_MISMATCH",
            Assert
                .Throws<ConfigurationException>(() =>
                    SessionState.RestoredStateFor(Captured(), Identity with { AppIdentity = new string('b', 64) })
                )
                .Code
        );
        Assert.Equal(
            "SESSION_MISMATCH",
            Assert
                .Throws<ConfigurationException>(() =>
                    SessionState.RestoredStateFor(Captured(), Identity with { Engine = "mobile" })
                )
                .Code
        );
        Assert.Equal(
            "INVALID_ARGUMENT",
            Assert
                .Throws<TestException>(() =>
                    SessionState.RestoredStateFor(new { Engine = "web", Platform = "web" }, Identity)
                )
                .Code
        );
    }
}
