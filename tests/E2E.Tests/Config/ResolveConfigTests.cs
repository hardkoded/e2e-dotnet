// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

public sealed class ResolveConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

    [Fact]
    public void Secrets_that_share_an_override_variable_are_refused_without_naming_a_value()
    {
        string[] values = ["secret-value-a1", "secret-value-b2", "secret-value-c3"];
        var colliding = $$"""{ "secrets": { "api-key": "{{values[0]}}", "api_key": "{{values[1]}}" } }""";
        foreach (var environment in new[] { Env(), Env(("E2E_SECRET_API_KEY", "rotated-value-z9")) })
        {
            var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(colliding, Root, environment));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal(
                "secrets \"api-key\" and \"api_key\" share the override variable E2E_SECRET_API_KEY, so one value set there would replace all of them; rename all but one",
                error.Message);
            foreach (var value in values.Append("rotated-value-z9"))
            {
                Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
            }
        }

        var punctuation = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            $$"""{ "secrets": { "api.key": "{{values[0]}}", "API-KEY": "{{values[1]}}", "apikey": "{{values[2]}}" } }""", Root, Env()));
        Assert.StartsWith("secrets \"api.key\" and \"API-KEY\" share the override variable E2E_SECRET_API_KEY", punctuation.Message, StringComparison.Ordinal);

        var three = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            $$"""{ "secrets": { "a-b": "{{values[0]}}", "a_b": "{{values[1]}}", "a.b": "{{values[2]}}" } }""", Root, Env()));
        Assert.StartsWith("secrets \"a-b\", \"a_b\" and \"a.b\" share", three.Message, StringComparison.Ordinal);

        // Upstream also tests a provider-function secret; the port has only string secrets.
        var pk = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            $$"""{ "secrets": { "pk-live": "{{values[0]}}", "pk_live": "{{values[1]}}" } }""", Root, Env()));
        Assert.Contains("E2E_SECRET_PK_LIVE", pk.Message, StringComparison.Ordinal);
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }
}
