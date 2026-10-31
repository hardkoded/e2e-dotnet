// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.SessionEnvelope;

/// <summary><c>describe('session envelope')</c> of <c>tests/unit/session-envelope.test.ts</c>.</summary>
public sealed class SessionEnvelopeTests : SessionEnvelopeKit
{
    [Fact]
    public async Task Carries_learned_secret_values_naming_the_secrets_in_the_clear_and_the_values_only_inside_the_ciphertext()
    {
        var (store, raw, _, envelope) = await SaveEnvelope([("token", SecretValue), ("token", "rotated-token-0042")]);
        try
        {
            SessionSchema.AssertValidSessionEnvelope(envelope);
            Assert.Equal("A256GCM", (string?)envelope["state"]!["algorithm"]);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"names":["token"]}"""), envelope["secrecy"]));
            Assert.DoesNotContain(SecretValue, raw, StringComparison.Ordinal);
            Assert.DoesNotContain("rotated-token-0042", raw, StringComparison.Ordinal);
            var loaded = await store.LoadAsync("member", Identity);
            Assert.True(JsonNode.DeepEquals(Cookies().Data, loaded.State.Data));
            Assert.Equal([("token", SecretValue), ("token", "rotated-token-0042")], loaded.Secrets);
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Names_every_secret_the_config_accepts_a_long_name_and_many_names_validate_and_round_trip()
    {
        var longName = new string('x', 300);
        (string, string)[] secrets = [(longName, SecretValue), .. Enumerable.Range(0, 300).Select(index => ($"token-{index}", $"value-{index}-padded"))];
        var (store, _, _, envelope) = await SaveEnvelope(secrets);
        try
        {
            SessionSchema.AssertValidSessionEnvelope(envelope);
            Assert.Equal(301, envelope["secrecy"]!["names"]!.AsArray().Count);
            var loaded = await store.LoadAsync("member", Identity);
            Assert.Equal(secrets, loaded.Secrets);
        }
        finally
        {
            store.Cleanup();
        }
    }
}
