// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Tests.Config;

/// <summary>
/// <c>describe('resolveConfig')</c> of <c>config.test.ts</c>.
/// </summary>
public sealed class ResolveConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

    [Fact]
    public void Validates_the_opt_in_skip_failure_policy_without_invalidating_recordings()
    {
        Assert.False(E2EConfig.Parse("{}", Root, Env()).FailOnSkippedFailure);
        Assert.True(E2EConfig.Parse("""{ "failOnSkippedFailure": true }""", Root, Env()).FailOnSkippedFailure);
        Assert.False(E2EConfig.Parse("""{ "failOnSkippedFailure": false }""", Root, Env()).FailOnSkippedFailure);
        Assert.Equal(Digest("{}"), Digest("""{ "failOnSkippedFailure": true }"""));
        foreach (var value in new[] { "\"true\"", "1", "null", "{}" })
        {
            var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse($$"""{ "failOnSkippedFailure": {{value}} }""", Root, Env()));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Contains("failOnSkippedFailure must be a boolean", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Validates_maxDuration_as_milliseconds_without_invalidating_recordings()
    {
        Assert.Null(E2EConfig.Parse("{}", Root, Env()).MaxDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(900_000), E2EConfig.Parse("""{ "maxDuration": 900000 }""", Root, Env()).MaxDuration);
        Assert.Equal(Digest("{}"), Digest("""{ "maxDuration": 900000 }"""));
        foreach (var value in new[] { "0", "-1", "2.5", "\"15m\"" })
        {
            var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse($$"""{ "maxDuration": {{value}} }""", Root, Env()));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Contains("maxDuration must be a positive safe integer of milliseconds", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), E2EConfig.Parse("""{ "maxDuration": 2147483647 }""", Root, Env()).MaxDuration);
        var tooLong = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "maxDuration": 2147483648 }""", Root, Env()));
        Assert.Equal("maxDuration must be at most 2147483647 milliseconds, got 2147483648", tooLong.Message);
    }

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

        var pk = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            $$"""{ "secrets": { "pk_live": "{{values[0]}}" } }""", Root, Env()).WithSecretProvider("pk-live", _ => Task.FromResult("from-provider")));
        Assert.Contains("E2E_SECRET_PK_LIVE", pk.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the port has no engine to inherit a platform from, so only the platform names a target.</summary>
    [Fact]
    public void Defaults_a_web_target_name_to_its_platform()
    {
        Assert.Equal(["ios", "android"], E2EConfig.Parse("""{ "targets": [{ "platform": "ios" }, { "platform": "android" }] }""", Root, Env()).Targets.Select(target => target.Name));
        Assert.Equal("web", E2EConfig.Parse("""{ "targets": [{ "platform": "web" }] }""", Root, Env()).Target.Name);
        Assert.Equal("web", E2EConfig.Parse("{}", Root, Env()).Target.Name);
    }

    /// <summary>Partial: the port reports an invalid name with its own message, and a platform it does not drive fails before its name is checked.</summary>
    [Theory]
    [InlineData("bad name")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public void Refuses_an_invalid_target_name(string name)
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse($$"""{ "targets": [{ "name": "{{name}}", "platform": "web" }] }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("targets[0].name must use ASCII letters, numbers, \"_\", \"-\", and \".\", and cannot be only dots", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_target_names_that_start_with_or_hold_a_dot()
    {
        Assert.Equal(
            [".hidden", "v1.2"],
            E2EConfig.Parse("""{ "targets": [{ "name": ".hidden", "platform": "web" }, { "name": "v1.2", "platform": "web" }] }""", Root, Env()).Targets.Select(target => target.Name));
    }

    [Theory]
    [InlineData("""[{ "name": "web", "platform": "web" }, { "name": "web", "platform": "web" }]""", "duplicate target name \"web\"")]
    [InlineData("""[{ "platform": "ios" }, { "platform": "ios" }]""", "duplicate target name \"ios\"; a target without a name is named after its platform, so name one of them")]
    [InlineData("""[{ "name": "web", "platform": "ios" }, { "platform": "web" }]""", "duplicate target name \"web\"; a target without a name is named after its platform, so name one of them")]
    [InlineData("""[{ "platform": "web" }, { "name": "web", "platform": "ios" }]""", "duplicate target name \"web\"; a target without a name is named after its platform, so name one of them")]
    public void Refuses_a_duplicate_target_name_defaulted_names_included(string targets, string message)
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("{ \"targets\": " + targets + " }", Root, Env()));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Points_at_the_declared_entry_of_a_later_target()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "platform": "web" }, { "platform": "web", "name": "b", "app": { "url": 5 } }] }""", Root, Env()));
        Assert.Contains("targets[1].app.url must be a non-empty string", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Needs_a_target_when_the_config_declares_several()
    {
        var config = E2EConfig.Parse("""{ "targets": [{ "name": "a", "platform": "web" }, { "name": "b", "platform": "web" }] }""", Root, Env());
        Assert.Equal(["a", "b"], config.Targets.Select(target => target.Name));
        var error = Assert.Throws<ConfigurationException>(() => config.Target);
        Assert.Equal("TARGET_REQUIRED", error.Code);
        Assert.Contains("the config declares several targets (\"a\", \"b\")", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the port has no engine to inherit a platform from, so only the unknown key is reported.</summary>
    [Fact]
    public void Points_at_the_entry_when_a_target_has_an_unknown_key()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "platform": "web", "browser": "x" }] }""", Root, Env()));
        Assert.Contains("targets[0]", error.Message, StringComparison.Ordinal);
        Assert.Contains("\"browser\"", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the port requires <c>targets</c> to be a non-empty array when it is present, and defaults the web target when it is absent.</summary>
    [Fact]
    public void Requires_targets_to_be_a_non_empty_array_and_defaults_the_web_target_without_them()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": {} }""", Root, Env()));
        Assert.Contains("targets must be a non-empty array", error.Message, StringComparison.Ordinal);
        var config = E2EConfig.Parse("{}", Root, Env());
        Assert.Equal("web", config.Target.Name);
        Assert.Equal("web", config.Target.Platform);
    }

    /// <summary>Partial: the port checks the two app shapes it has, the object and its <c>url</c>.</summary>
    [Fact]
    public void Checks_the_shape_of_the_app_fields_the_port_has()
    {
        var url = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "app": { "url": 5 } }] }""", Root, Env()));
        Assert.Contains("targets[0].app.url must be a non-empty string", url.Message, StringComparison.Ordinal);
        var app = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "app": "https://app.test" }] }""", Root, Env()));
        Assert.Contains("targets[0].app must be an object", app.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the port suggests the nearest key and points the keys that moved at their new place. It has no <c>tests</c> or engine hints.</summary>
    [Fact]
    public void Suggests_the_nearest_key_and_points_a_moved_key_at_where_it_lives()
    {
        var near = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "target": [] }""", Root, Env()));
        Assert.Contains("unknown config key \"target\"; did you mean \"targets\"?", near.Message, StringComparison.Ordinal);
        var moved = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "app": { "url": "http://127.0.0.1:4000" } }""", Root, Env()));
        Assert.Contains("unknown config key \"app\"; the app under test is declared on its target", moved.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "timeout": "30s" }""", "timeout must be a positive safe integer of milliseconds, got \"30s\"")]
    [InlineData("""{ "launchTimeout": 0 }""", "launchTimeout must be a positive safe integer of milliseconds, got 0")]
    public void Names_the_unit_and_the_offending_value_for_durations(string json, string message)
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, Env()));
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_config_timeouts_a_timer_cannot_wait_for()
    {
        foreach (var key in new[] { "timeout", "launchTimeout", "actionTimeout", "assertionTimeout", "cleanupTimeout" })
        {
            var config = E2EConfig.Parse($$"""{ "{{key}}": 2147483647 }""", Root, Env());
            var value = key switch
            {
                "timeout" => config.Timeout,
                "launchTimeout" => config.LaunchTimeout,
                "actionTimeout" => config.ActionTimeout,
                "assertionTimeout" => config.AssertionTimeout,
                _ => config.CleanupTimeout,
            };
            Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), value);
            var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse($$"""{ "{{key}}": 2147483648 }""", Root, Env()));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal(key + " must be at most 2147483647 milliseconds, got 2147483648", error.Message);
        }
    }

    /// <summary>
    /// The provider is set in code, by <see cref="E2EConfig.WithSecretProvider"/>, since a JSON config holds no function. Upstream's title
    /// names "origin narrowing", but the <c>it</c> has no origin step: it asserts the resolved name, purpose, and value only.
    /// </summary>
    [Fact]
    public void Resolves_config_secrets_as_generic_secrets_values_providers_origin_narrowing_and_E2E_SECRET_overrides()
    {
        SecretProvider provider = _ => Task.FromResult("fresh");
        var config = E2EConfig.Parse(
            """{ "secrets": { "stripe-key": "sk_test_123", "rotated": "stale" } }""",
            Root,
            Env(("E2E_SECRET_ROTATED", "fresh-from-env"))).WithSecretProvider("totp", provider);

        var stripe = config.Secrets.Get("stripe-key");
        Assert.Equal("stripe-key", stripe.Name);
        Assert.Equal("generic-secret", stripe.Purpose);
        Assert.Equal("sk_test_123", stripe.Value);
        Assert.Null(stripe.Provider);
        Assert.Same(provider, config.Secrets.Get("totp").Provider);
        Assert.Equal("generic-secret", config.Secrets.Get("totp").Purpose);
        Assert.Equal("fresh-from-env", config.Secrets.Get("rotated").Value);
        Assert.Null(config.Secrets.Get("rotated").Provider);
    }

    /// <summary>.NET-only: JSON allows a name twice, which a TypeScript object cannot hold; the config refuses it as <c>INVALID_CONFIG</c>, not as a crash.</summary>
    [Fact]
    public void Refuses_a_secret_or_credential_written_twice_in_the_file()
    {
        var secret = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "secrets": { "key": "value-one", "key": "value-two" } }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", secret.Code);
        var credential = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "a": { "username": "u", "password": "password-1" }, "a": { "username": "u", "password": "password-2" } } }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", credential.Code);
    }

    [Fact]
    public async Task Asks_a_provider_for_its_value_each_time_and_refuses_one_under_6_code_points()
    {
        var calls = 0;
        var config = E2EConfig.Parse("{}", Root, Env()).WithSecretProvider("totp", _ => Task.FromResult("fresh-" + Interlocked.Increment(ref calls)));
        var totp = config.Secrets.Get("totp");

        Assert.Equal("fresh-1", (await totp.ResolveAsync(CancellationToken.None)).Value);
        Assert.Equal("fresh-2", (await totp.ResolveAsync(CancellationToken.None)).Value);

        var short4 = E2EConfig.Parse("{}", Root, Env()).WithSecretProvider("pin", _ => Task.FromResult("1234")).Secrets.Get("pin");
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => short4.ResolveAsync(CancellationToken.None));
        Assert.Equal("SECRET_UNAVAILABLE", error.Code);
        Assert.DoesNotContain("1234", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A JSON number and object stand for upstream's <c>42 as never</c> and <c>{ value }</c>. The port also names the override variable
    /// for them, which upstream's pattern (<c>secret "key" must be a non-empty string</c>) does not check.
    /// </summary>
    [Fact]
    public void Rejects_an_empty_or_non_string_secret_and_a_secret_named_like_a_password_handle_but_lets_a_secret_share_a_credential_name()
    {
        (string Json, string? EmptyOverride, string Message)[] refused =
        [
            ("""{ "secrets": { "key": "" } }""", null, "secret \"key\" must be a non-empty string or a provider function"),
            ("""{ "secrets": { "key": 42 } }""", null, "secret \"key\" must be a non-empty string, or null to read only E2E_SECRET_KEY"),
            ("""{ "secrets": { "key": { "value": "v" } } }""", null, "secret \"key\" must be a non-empty string, or null to read only E2E_SECRET_KEY"),
            ("""{ "secrets": { "key": "valid-value" } }""", "E2E_SECRET_KEY", "secret \"key\" must be a non-empty string or a provider function"),
        ];
        foreach (var (json, emptyOverride, message) in refused)
        {
            var environment = emptyOverride is null ? Env() : Env((emptyOverride, ""));
            var refusal = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, environment));
            Assert.Equal(message, refusal.Message);
            Assert.StartsWith("secret \"key\" must be a non-empty string", refusal.Message, StringComparison.Ordinal);
        }

        var shared = E2EConfig.Parse("""{ "credentials": { "admin": { "username": "u", "password": "password-1" } }, "secrets": { "admin": "value-1" } }""", Root, Env());
        Assert.Equal("value-1", shared.Secrets.Get("admin").Value);
        var password = shared.Credentials["admin"].Password;
        Assert.Equal("admin.password", password.Name);
        Assert.Equal("password", password.Purpose);
        Assert.Equal("password-1", password.Value);
        Assert.Equal(["admin.password", "admin"], shared.AllSecrets.Select(secret => secret.Name));
        Assert.Equal(["admin"], shared.Secrets.Names);

        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "u", "password": "password-1" } }, "secrets": { "admin.password": "value-1" } }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Matches("secret \"admin.password\" has the name of credential \"admin\"'s password handle; rename the secret", error.Message);
        Assert.DoesNotContain("value-1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>The provider is set in code, by <see cref="E2EConfig.WithCredential"/>; a JSON number stands for upstream's <c>42 as never</c>.</summary>
    [Fact]
    public void Accepts_a_provider_backed_credential_password_env_override_wins_over_it()
    {
        SecretProvider provider = _ => Task.FromResult("fresh-totp");
        var withProvider = E2EConfig.Parse("{}", Root, Env()).WithCredential("admin", "admin", provider);
        var password = withProvider.Credentials["admin"].Password;
        Assert.Equal("password", password.Purpose);
        Assert.Same(provider, password.Provider);

        var overridden = E2EConfig.Parse("{}", Root, Env(("E2E_USER_ADMIN_PASSWORD", "rotated"))).WithCredential("admin", "admin", provider);
        Assert.Equal("rotated", overridden.Credentials["admin"].Password.Value);
        Assert.Null(overridden.Credentials["admin"].Password.Provider);

        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "credentials": { "admin": { "username": "admin", "password": 42 } } }""", Root, Env()));
        Assert.Matches("password must be a non-empty string or a provider function", error.Message);
    }

    [Fact]
    public async Task Names_a_provider_backed_credential_password_for_its_credential_and_fails_it_as_a_credential()
    {
        var config = E2EConfig.Parse("""{ "credentials": { "admin": { "username": "file-user", "password": "file-password" } } }""", Root, Env())
            .WithCredential("admin", "admin", _ => Task.FromResult("abc"));
        var credential = config.Credentials["admin"];

        Assert.Equal("admin", credential.Username);
        Assert.Equal("admin.password", credential.Password.Name);
        Assert.Equal(["admin.password"], config.AllSecrets.Select(secret => secret.Name));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => credential.Password.ResolveAsync(CancellationToken.None));
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);

        var envUser = E2EConfig.Parse("{}", Root, Env(("E2E_USER_ADMIN_USERNAME", "from-env"))).WithCredential("admin", null, _ => Task.FromResult("provider-secret"));
        Assert.Equal("from-env", envUser.Credentials["admin"].Username);
        var missing = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("{}", Root, Env()).WithCredential("admin", null, _ => Task.FromResult("provider-secret")));
        Assert.Matches("credential \"admin\" has no username; set username or E2E_USER_ADMIN_USERNAME", missing.Message);
    }

    [Fact]
    public void Checks_every_secret_an_engine_option_holds_against_the_configured_secrets_never_a_credential()
    {
        var config = E2EConfig.Parse(
            """{ "secrets": { "stagingPassword": "staging-pass" }, "credentials": { "admin": { "username": "admin", "password": "admin-pass" } } }""", Root, Env());
        void Check(string name)
        {
            var held = config.WithTarget("web", new SecretHoldingEngine(Secret.Create(name, "synthetic-value")));
            ResolvedConfig.Resolve(held);
            HostTestFilters.Resolve(held, []);
        }

        Check("stagingPassword");
        foreach (var name in new[] { "admin", "admin.password" })
        {
            var credential = Assert.Throws<ConfigurationException>(() => Check(name));
            Assert.Equal("INVALID_CONFIG", credential.Code);
            Assert.Contains(
                $"uses secrets.get(\"{name}\"), which is not configured; add it to config.secrets; credential \"admin\" is not a secrets entry, and an engine option takes one: declare the value under config.secrets",
                credential.Message,
                StringComparison.Ordinal);
        }

        var typo = Assert.Throws<ConfigurationException>(() => Check("stagingPasword"));
        Assert.Equal("INVALID_CONFIG", typo.Code);
        Assert.Equal(
            "target \"web\" engine fake uses secrets.get(\"stagingPasword\"), which is not configured; add it to config.secrets; did you mean \"stagingPassword\"?",
            typo.Message);
    }

    [Fact]
    public void A_target_engine_enters_the_config_digest_and_the_run_uses_it()
    {
        var plain = E2EConfig.Parse("{}", Root, Env());
        var engine = new SecretHoldingEngine(Secret.Create("stagingPassword", "synthetic-value"));
        var held = E2EConfig.Parse("""{ "secrets": { "stagingPassword": "staging-pass" } }""", Root, Env()).WithTarget("web", engine);

        Assert.Same(engine, held.Target.Engine);
        Assert.Same(engine, E2E.Engine.TargetEngines.Create(held.Target, null, null));
        Assert.NotEqual(Digest(plain), Digest(held));
        Assert.Equal(Digest(held), HostTestFilters.Resolve(held, []).ConfigDigest);
        Assert.Single(held.Targets);
    }

    [Fact]
    public void The_run_path_checks_the_engine_secrets_of_a_target_the_run_does_not_use()
    {
        var config = E2EConfig.Parse("""{ "secrets": { "stagingPassword": "staging-pass" } }""", Root, Env())
            .WithTarget("other", new SecretHoldingEngine(Secret.Create("stagingPasword", "synthetic-value")));
        var error = Assert.Throws<ConfigurationException>(() => HostTestFilters.Resolve(config, [], target: config.Targets[0]));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("did you mean \"stagingPassword\"?", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_two_credentials_that_share_E2E_USER_variables_naming_the_entries_and_never_a_value()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "svc-a": { "username": "user@example.test", "password": "password-one-1" }, "svc_a": { "username": "user@example.test", "password": "password-two-2" } } }""",
            Root,
            Env()));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(
            "credentials \"svc-a\" and \"svc_a\" share the override variables E2E_USER_SVC_A_USERNAME and E2E_USER_SVC_A_PASSWORD, so one value set there would replace all of them; rename all but one",
            error.Message);
        Assert.DoesNotMatch("password-one-1|password-two-2|user@example\\.test", error.Message);
    }

    [Fact]
    public void Keeps_E2E_USER_and_E2E_SECRET_apart_and_accepts_punctuation_that_maps_to_a_distinct_variable()
    {
        var config = E2EConfig.Parse(
            """
            {
              "credentials": { "svc-a": { "username": "u", "password": "password-1" }, "svc-b": { "username": "u", "password": "password-2" } },
              "secrets": { "svc-a": "secret-value-1", "stripe.key": "secret-value-2", "stripe-key-2": "secret-value-3" }
            }
            """,
            Root,
            Env(("E2E_SECRET_STRIPE_KEY", "rotated-stripe"), ("E2E_USER_SVC_A_PASSWORD", "rotated-password")));

        Assert.Equal("rotated-stripe", config.Secrets.Get("stripe.key").Value);
        Assert.Equal("secret-value-3", config.Secrets.Get("stripe-key-2").Value);
        Assert.Equal("secret-value-1", config.Secrets.Get("svc-a").Value);
        Assert.Equal("rotated-password", config.AllSecrets.Single(secret => secret.Name == "svc-a.password").Value);
        Assert.Equal("password-2", config.AllSecrets.Single(secret => secret.Name == "svc-b.password").Value);
    }

    [Fact]
    public void Resolves_E2E_USER_environment_credentials_over_config_values()
    {
        var config = E2EConfig.Parse(
            """{ "credentials": { "member": { "username": "config-user", "password": "config-pass" } } }""",
            Root,
            Env(("E2E_USER_MEMBER_USERNAME", "env-user"), ("E2E_USER_MEMBER_PASSWORD", "env-pass")));

        Assert.Equal("env-user", config.Credentials["member"].Username);
        Assert.Equal("env-pass", config.AllSecrets.Single(secret => secret.Name == "member.password").Value);
    }

    [Fact]
    public void Takes_a_username_missing_from_the_config_from_E2E_USER_NAME_USERNAME_and_rejects_one_missing_from_both()
    {
        const string json = """{ "credentials": { "admin": { "password": "admin-pass" } } }""";
        Assert.Equal("from-env", E2EConfig.Parse(json, Root, Env(("E2E_USER_ADMIN_USERNAME", "from-env"))).Credentials["admin"].Username);
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, Env()));
        Assert.Matches("credential \"admin\" has no username; set username or E2E_USER_ADMIN_USERNAME", error.Message);
    }

    /// <summary>Partial: a username is a string in code (<see cref="E2EConfig.WithCredential"/>) and cannot be a function, so a JSON number stands for the function and the message says <c>got a number</c> where upstream says <c>got a function</c>. The message names the kind of the value (<c>a number</c>, <c>a string</c>), where upstream quotes it, because a bare string entry may be the password.</summary>
    [Fact]
    public void Partial_Rejects_a_credential_username_that_is_not_a_string_an_unknown_credential_key_and_a_non_object_credential()
    {
        var username = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": 42, "password": "admin-pass" } } }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", username.Code);
        Assert.Equal("credential \"admin\" username must be a string, got a number; only password may be a provider function", username.Message);

        var key = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "admin", "password": "admin-pass", "usename": "admin" } } }""", Root, Env()));
        Assert.Matches("unknown key \"usename\" in credential \"admin\"; a credential has username and password; did you mean \"username\"\\?", key.Message);

        var nonObject = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "credentials": { "admin": null } }""", Root, Env()));
        Assert.Matches("credential \"admin\" must be an object of \\{ username, password \\}, got null", nonObject.Message);

        var bare = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "credentials": { "admin": "hunter2-pass" } }""", Root, Env()));
        Assert.Equal("credential \"admin\" must be an object of { username, password }, got a string", bare.Message);
    }

    [Fact]
    public void Rejects_an_empty_credential_password_at_config_time_including_an_empty_env_override()
    {
        var empty = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "admin", "password": "" } } }""", Root, Env()));
        Assert.Contains("password must be a non-empty string", empty.Message, StringComparison.Ordinal);

        var overridden = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "admin", "password": "configured" } } }""", Root, Env(("E2E_USER_ADMIN_PASSWORD", ""))));
        Assert.Contains("password must be a non-empty string", overridden.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_static_secret_or_password_under_6_code_points_from_config_or_the_environment_naming_the_minimum()
    {
        var short5 = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "secrets": { "pin": "12345" } }""", Root, Env()));
        Assert.Equal("INVALID_CONFIG", short5.Code);
        Assert.Contains(
            "secret \"pin\" must be at least 6 characters (code points), not 5; a shorter value cannot be redacted without rewriting unrelated text",
            short5.Message,
            StringComparison.Ordinal);
        E2EConfig.Parse("""{ "secrets": { "pin": "123456" } }""", Root, Env());
        var overridden = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "secrets": { "pin": "long-enough" } }""", Root, Env(("E2E_SECRET_PIN", "7"))));
        Assert.Contains("secret \"pin\" must be at least 6 characters (code points), not 1", overridden.Message, StringComparison.Ordinal);

        var password5 = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "u", "password": "short" } } }""", Root, Env()));
        Assert.Matches("credential \"admin\" password must be at least 6 characters \\(code points\\), not 5", password5.Message);
        var passwordOverridden = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "u", "password": "long-enough" } } }""", Root, Env(("E2E_USER_ADMIN_PASSWORD", "pw"))));
        Assert.Matches("credential \"admin\" password must be at least 6 characters \\(code points\\), not 2", passwordOverridden.Message);

        // A provider's value is checked when it is asked for, not at load.
        E2EConfig.Parse("{}", Root, Env()).WithSecretProvider("pin", _ => Task.FromResult("1"));
    }

    [Fact]
    public void Counts_a_secret_in_code_points_so_three_emoji_are_three_characters_and_not_six()
    {
        Assert.Equal(6, "🔐🔐🔐".Length);
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "secrets": { "pin": "🔐🔐🔐" } }""", Root, Env()));
        Assert.Contains("secret \"pin\" must be at least 6 characters (code points), not 3", error.Message, StringComparison.Ordinal);
        var credential = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(
            """{ "credentials": { "admin": { "username": "u", "password": "🔐🔐🔐" } } }""", Root, Env()));
        Assert.Matches("credential \"admin\" password must be at least 6 characters \\(code points\\), not 3", credential.Message);
        E2EConfig.Parse("""{ "secrets": { "pin": "🔐🔐🔐🔐🔐🔐" } }""", Root, Env());
    }

    /// <summary>Partial: upstream "uses CI defaults for retries and workers, and the same trace and video as locally" also checks workers. The port has no workers, so only retries, trace, and video are checked.</summary>
    [Fact]
    public void Uses_CI_defaults_for_retries_and_the_same_trace_and_video_as_locally()
    {
        var config = E2EConfig.Parse("{}", Root, Env(("CI", "1")));
        Assert.Equal(1, config.Retries);
        Assert.Equal(new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Default), config.Target.Trace);
        Assert.Equal(new ResolvedRecording(RecordingMode.Off, RecordingSource.Default), config.Target.Video);
    }

    [Fact]
    public void Accepts_a_command_only_beside_a_URL_to_poll_and_defaults_readyUrl_to_it()
    {
        var noUrl = Assert.Throws<ConfigurationException>(() => App("""{ "command": { "executable": "node" } }"""));
        Assert.Contains("without a URL to poll", noUrl.Message, StringComparison.Ordinal);
        var withUrl = Resolved("""{ "url": "http://localhost:3000", "command": { "executable": "node" } }""");
        Assert.Equal("node", withUrl.Command!.Executable);
        Assert.Equal("http://localhost:3000/", withUrl.ReadyUrl);
        Assert.Equal("http://localhost:9/health", Resolved("""{ "command": { "executable": "node" }, "readyUrl": "http://localhost:9/health" }""").ReadyUrl);
        var empty = Assert.Throws<ConfigurationException>(() => App("""{ "url": "http://localhost:3000", "command": { "executable": "" } }"""));
        Assert.Contains("command.executable is required", empty.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: JSON has no <c>secrets.get()</c> handle, so the message that names one for a string is not reachable. A value that is not a string is quoted as written.</summary>
    [Fact]
    public void Partial_Takes_only_strings_in_a_commands_args_and_env()
    {
        const string url = """ "url": "http://localhost:3000" """;
        var arg = Assert.Throws<ConfigurationException>(() => App($$"""{ {{url}}, "command": { "executable": "node", "args": ["server.mjs", 3000] } }"""));
        Assert.Equal("INVALID_CONFIG", arg.Code);
        Assert.Equal("target \"web\" app.command.args[1] must be a string, got 3000", arg.Message);

        // A null value is a variable that is unset: the process starts without it.
        var env = Resolved($$"""{ {{url}}, "command": { "executable": "node", "env": { "PORT": "3000", "UNSET": null } } }""").Command!.Env;
        Assert.Equal("3000", Assert.Single(env!).Value);
        Assert.Equal("PORT", Assert.Single(env!).Key);
        var list = Assert.Throws<ConfigurationException>(() => App($$"""{ {{url}}, "command": { "executable": "node", "env": ["PORT=3000"] } }"""));
        Assert.Equal("target \"web\" app.command.env must be an object of variable name to string", list.Message);
        var number = Assert.Throws<ConfigurationException>(() => App($$"""{ {{url}}, "command": { "executable": "node", "env": { "PORT": 3000 } } }"""));
        Assert.Equal("INVALID_CONFIG", number.Code);
        Assert.Equal("target \"web\" app.command.env.PORT must be a string, got 3000", number.Message);
    }

    [Fact]
    public void Takes_inheritEnv_names_and_trailing_star_prefixes_refusing_a_bare_star_and_a_star_anywhere_else()
    {
        static string Declare(string patterns) => $$"""{ "url": "http://localhost:3000", "command": { "executable": "node", "inheritEnv": {{patterns}} } }""";
        Assert.Equal(["DATABASE_URL", "STRIPE_*"], Resolved(Declare("""["DATABASE_URL", "STRIPE_*"]""")).Command!.InheritEnv);
        var notArray = Assert.Throws<ConfigurationException>(() => App(Declare("\"DATABASE_URL\"")));
        Assert.Equal("INVALID_CONFIG", notArray.Code);
        Assert.Equal("target \"web\" app.command.inheritEnv must be an array of variable names", notArray.Message);
        var bare = Assert.Throws<ConfigurationException>(() => App(Declare("""["*"]""")));
        Assert.Equal("target \"web\" app.command.inheritEnv[0] cannot be \"*\": it would pass every variable to the app, model keys included; list the names or prefixes it needs", bare.Message);
        foreach (var (entry, shown) in new[] { ("", "\"\""), ("STR*PE", "\"STR*PE\""), ("*_KEY", "\"*_KEY\""), ("STRIPE_**", "\"STRIPE_**\""), ("A=B", "\"A=B\""), (" DATABASE_URL", "\" DATABASE_URL\"") })
        {
            var error = Assert.Throws<ConfigurationException>(() => App(Declare($$"""["DATABASE_URL", "{{entry}}"]""")));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"target \"web\" app.command.inheritEnv[1] must be a variable name or a prefix ending in * (such as \"STRIPE_*\"), got {shown}", error.Message);
        }

        var number = Assert.Throws<ConfigurationException>(() => App(Declare("[42]")));
        Assert.Equal("target \"web\" app.command.inheritEnv[0] must be a string, got 42", number.Message);
    }

    /// <summary>Partial: a misspelled key of the app is named in the port's own words, <c>unknown targets[0].app key</c>, where upstream says <c>target "web" app has unknown key</c>. The command's own message is upstream's.</summary>
    [Fact]
    public void Partial_Rejects_an_app_or_command_key_the_contract_does_not_know_naming_the_nearest()
    {
        const string url = "http://localhost:3000";
        var app = Assert.Throws<ConfigurationException>(() => App($$"""{ "url": "{{url}}", "readyURL": "{{url}}" }"""));
        Assert.Equal("INVALID_CONFIG", app.Code);
        Assert.Equal("unknown targets[0].app key \"readyURL\"; did you mean \"readyUrl\"?", app.Message);
        var arg = Assert.Throws<ConfigurationException>(() => App($$"""{ "url": "{{url}}", "command": { "executable": "node", "arg": ["server.mjs"] } }"""));
        Assert.Equal("INVALID_CONFIG", arg.Code);
        Assert.Equal("target \"web\" app.command has unknown key \"arg\"; did you mean \"args\"?", arg.Message);
        var ready = Assert.Throws<ConfigurationException>(() => App($$"""{ "url": "{{url}}", "command": { "executable": "node", "readyUrl": "{{url}}" } }"""));
        Assert.EndsWith(
            "app.command has unknown key \"readyUrl\"; expected one of executable, args, cwd, env, inheritEnv, startupTimeout, shutdownTimeout, log, reuseExisting",
            ready.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_declared_readyUrl_that_is_not_an_http_s_URL()
    {
        foreach (var bad in new[] { "not a url", "ftp://x/" })
        {
            var error = Assert.Throws<ConfigurationException>(() => App($$"""{ "url": "http://localhost:3000", "readyUrl": "{{bad}}" }"""));
            Assert.Contains("app.readyUrl must be an http(s) URL", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(
            "http://localhost:3000/health",
            Resolved("""{ "url": "http://localhost:3000", "command": { "executable": "x" }, "readyUrl": "http://localhost:3000/health" }""").ReadyUrl);
    }

    [Fact]
    public void Defaults_environment_to_test_for_loopback_localhost_test_hosts_and_production_elsewhere()
    {
        Assert.Equal("test", Resolved("""{ "url": "https://app.test" }""").Environment);
        Assert.Equal("test", Resolved("""{ "url": "http://localhost:3000" }""").Environment);
        Assert.Equal("production", Resolved("""{ "url": "https://staging.example.com" }""").Environment);
        Assert.Equal("staging", Resolved("""{ "url": "https://staging.example.com", "environment": "staging" }""").Environment);
        Assert.Equal("staging", Resolved("""{ "environment": "staging" }""").Environment);
        var error = Assert.Throws<ConfigurationException>(() => App("""{ "url": "https://app.test", "environment": "prod" }"""));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("app.environment must be one of", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keys_identity_on_the_declared_identity_else_the_URL_origin_and_path_else_the_bundle_id_or_build_else_nothing()
    {
        Assert.Equal("checkout-app", Resolved("""{ "url": "https://app.test", "identity": "checkout-app" }""").Identity);
        Assert.Equal("https://app.test/shop/", Resolved("""{ "url": "https://app.test/shop/" }""").Identity);
        Assert.Equal("com.example.app", Resolved("""{ "identity": "com.example.app" }""").Identity);
        Assert.Equal("dev.shop.app", ResolvedOn("ios", """{ "bundleId": "dev.shop.app", "appPath": "./build/Shop.app" }""").Identity);
        Assert.Equal("./build/shop.apk", ResolvedOn("android", """{ "appPath": "./build/shop.apk" }""").Identity);
        Assert.Null(Resolved("{}").Identity);
        var error = Assert.Throws<ConfigurationException>(() => App("""{ "url": "https://app.test", "identity": "  " }"""));
        Assert.Contains("app.identity must be a non-empty string", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the port names the field as <c>targets[0].app.url</c>, not <c>target "web" app.url</c>, an <c>app</c> that is not an object likewise, and it has no engine to be missing, so "declares app without an engine" is not reachable.</summary>
    [Fact]
    public void Partial_Checks_the_shape_of_every_app_field()
    {
        Assert.Contains("app.url must be a non-empty string", Assert.Throws<ConfigurationException>(() => App("""{ "url": 5 }""")).Message, StringComparison.Ordinal);
        Assert.Contains("app.bundleId must be a non-empty string", Assert.Throws<ConfigurationException>(() => AppOn("ios", """{ "bundleId": "" }""")).Message, StringComparison.Ordinal);
        Assert.Contains("app.appPath must be a non-empty string", Assert.Throws<ConfigurationException>(() => AppOn("ios", """{ "appPath": " " }""")).Message, StringComparison.Ordinal);
        Assert.Contains("app.launchArguments must be an array of strings", Assert.Throws<ConfigurationException>(() => AppOn("ios", """{ "bundleId": "dev.shop.app", "launchArguments": ["-a", 1] }""")).Message, StringComparison.Ordinal);
        var permission = Assert.Throws<ConfigurationException>(() => AppOn("ios", """{ "bundleId": "dev.shop.app", "permissions": { "camera": "allow" } }"""));
        Assert.Contains("app.permissions.camera must be grant, deny, or reset, got \"allow\"", permission.Message, StringComparison.Ordinal);
        var notAnObject = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "app": "https://app.test" }] }""", Root, Env()));
        Assert.Contains("app must be an object", notAnObject.Message, StringComparison.Ordinal);

        var app = ResolvedOn("ios", """{ "bundleId": "dev.shop.app", "launchArguments": ["-e2e"], "permissions": { "camera": "grant" } }""");
        Assert.Equal("dev.shop.app", app.BundleId);
        Assert.Equal(["-e2e"], app.LaunchArguments);
        Assert.Equal(new Dictionary<string, AppPermissionState> { ["camera"] = AppPermissionState.Grant }, app.Permissions);
    }

    [Fact]
    public void Derives_the_site_from_the_base_URL_or_none_without_one()
    {
        Assert.Equal("localhost", Resolved("""{ "url": "http://localhost:3000/app" }""").Site);
        Assert.Equal("example.com", Resolved("""{ "url": "https://app.staging.example.com" }""").Site);
        Assert.Null(Resolved("{}").Site);
    }

    /// <summary>The bundle id cases declare an ios target, since the port's web app takes no <c>bundleId</c>.</summary>
    [Fact]
    public void Digests_the_app_a_target_declares_replacing_command_env_values_by_name()
    {
        static string Declare(string platform, string app) => Digest(AppOn(platform, app));
        var a = Declare("web", """{ "url": "http://localhost:3000", "command": { "executable": "x", "env": { "TOKEN": "aaa" } } }""");
        var b = Declare("web", """{ "url": "http://localhost:3000", "command": { "executable": "x", "env": { "TOKEN": "bbb" } } }""");
        Assert.Equal(a, b);
        Assert.NotEqual(a, Declare("web", """{ "url": "http://localhost:3000", "command": { "executable": "x", "env": { "OTHER": "aaa" } } }"""));
        Assert.NotEqual(Declare("web", """{ "url": "http://localhost:3000" }"""), Declare("web", """{ "url": "http://localhost:4000" }"""));
        Assert.NotEqual(Declare("ios", """{ "bundleId": "a" }"""), Declare("ios", """{ "bundleId": "b" }"""));
        Assert.NotEqual(Declare("ios", """{ "bundleId": "a", "launchArguments": ["-x"] }"""), Declare("ios", """{ "bundleId": "a" }"""));
    }

    /// <summary>
    /// Partial: the port's target names its platform (the field is required), so a named target cannot inherit its engine's; the test checks that the
    /// engine's declared platform enters the digest, the target's own held constant. The engine is set in code on the internal <see cref="TargetConfig.Engine"/> (#1339).
    /// </summary>
    [Fact]
    public void Partial_Digests_the_platform_an_engine_declares_which_a_named_target_inherits()
    {
        static string Of(string platform) => ResolvedConfig.Resolve(E2EConfig.Parse("{}", Root, Env()).WithTarget(
            new TargetConfig { Name = "app", Platform = "web", App = new AppConfig(), Engine = new SecretHoldingEngine(platform: platform) })).ConfigDigest;
        Assert.Equal(Of("web"), Of("web"));
        Assert.NotEqual(Of("web"), Of("ios"));
    }

    [Fact]
    public void Replaces_secret_values_in_the_config_digest()
    {
        var a = Digest("""{ "secrets": { "key": "secret-1" } }""");
        var b = Digest("""{ "secrets": { "key": "secret-2" } }""");
        Assert.Equal(a, b);
        var c = Digest("""{ "secrets": { "other": "secret-1" } }""");
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Replaces_credential_passwords_in_the_config_digest()
    {
        var a = Digest("""{ "credentials": { "member": { "username": "u", "password": "secret-1" } } }""");
        var b = Digest("""{ "credentials": { "member": { "username": "u", "password": "secret-2" } } }""");
        Assert.Equal(a, b);
        var c = Digest("""{ "credentials": { "member": { "username": "other", "password": "secret-1" } } }""");
        Assert.NotEqual(a, c);
    }

    /// <summary>The engine is set in code on the internal <see cref="TargetConfig.Engine"/> (#1339), holding the secret the way <c>defineEngine({ secrets })</c> does.</summary>
    [Fact]
    public void Keeps_an_engine_options_secret_out_of_the_config_digest()
    {
        static string Of(string value)
        {
            var config = E2EConfig.Parse("""{ "secrets": { "key": "configured" } }""", Root, Env());
            return ResolvedConfig.Resolve(config.WithTarget(
                new TargetConfig { Name = "web", Platform = "web", App = new AppConfig(), Engine = new SecretHoldingEngine(Secret.Create("key", value)) })).ConfigDigest;
        }

        Assert.Equal(Of("first-value"), Of("second-value"));
    }

    [Fact]
    public void Keeps_reporter_objects_and_artifact_stores_out_of_the_config_digest_whatever_they_hold()
    {
        // A client with a cycle is what an SDK-backed reporter or store carries; JSON cannot clone it.
        var plain = E2EConfig.Parse("""{ "reporters": ["list"] }""", Root, Env());
        Assert.Equal(Digest(plain), Digest(plain.WithReporter(new UploadReporter())));
        var store = E2EConfig.Parse("{}", Root, Env());
        Assert.Equal(Digest(store), Digest(store.WithArtifactStore(new CyclicStore())));
    }

    /// <summary>.NET-only: a reporter in the config file is a type name or <c>{ type, options }</c> of plain data.</summary>
    [Fact]
    public void Digests_a_reporter_type_name_and_leaves_out_its_options()
    {
        Assert.Equal(Digest("""{ "reporters": ["list"] }"""), Digest("""{ "reporters": ["list", { "type": "upload", "options": { "bucket": "b" } }] }"""));
        Assert.NotEqual(Digest("""{ "reporters": ["list"] }"""), Digest("""{ "reporters": ["list", "github"] }"""));
    }

    /// <summary>.NET-only: <c>WithReporter</c> adds a live reporter after the ones the file names, and refuses one with no name.</summary>
    [Fact]
    public void WithReporter_adds_a_live_reporter_after_the_ones_the_file_names()
    {
        var config = E2EConfig.Parse("""{ "reporters": [] }""", Root, Env());
        var first = new UploadReporter();
        var second = new UploadReporter();
        var both = config.WithReporter(first).WithReporter(second);
        Assert.Equal([first, second], E2E.Reporters.Create(both));
        Assert.Empty(E2E.Reporters.Create(config));
        Assert.Equal("INVALID_CONFIG", Assert.Throws<ConfigurationException>(() => config.WithReporter(new UploadReporter { Label = "" })).Code);
    }


    [Fact]
    public void Reduces_every_model_instance_in_an_agent_entry_to_its_identity_judge_and_a_custom_executors_own_included()
    {
        // A provider client with a cycle: JSON cannot clone it, and its settings must never enter the digest anyway.
        var config = E2EConfig.Parse("{}", Root, Env());
        string Of(AgentOptions entry) => ResolvedConfig.ComputeConfigDigest(config, new Dictionary<string, AgentOptions> { ["default"] = entry });
        AgentOptions Models(string model, string judge) => new() { Model = new ClientModel(model), Judge = new ClientModel(judge) };
        var withJudge = Of(Models("actor", "verifier"));
        Assert.Equal(withJudge, Of(Models("actor", "verifier")));
        Assert.NotEqual(withJudge, Of(Models("actor", "other")));
        AgentOptions Brain(string judge) => new()
        {
            Executor = new E2E.Tests.AgentAct.TestExecutor("brain", (_, _) => throw new InvalidOperationException("not called")) { Model = new ClientModel("actor"), Judge = new ClientModel(judge) },
        };
        var asExecutor = Of(Brain("verifier"));
        Assert.Equal(asExecutor, Of(Brain("verifier")));
        Assert.NotEqual(asExecutor, Of(Brain("other")));
    }

    /// <summary>The digest of the config <paramref name="json"/> declares (upstream's <c>resolve(...).configDigest</c>).</summary>
    internal static string Digest(string json, Func<string, string?>? environment = null, CliOverrides? cli = null) =>
        Digest(E2EConfig.Parse(json, Root, environment ?? Env(), cli ?? new CliOverrides()));

    internal static string Digest(E2EConfig config) => ResolvedConfig.ComputeConfigDigest(config);

    /// <summary>A model holding a client that points back at itself, the way a provider's does: JSON cannot clone it.</summary>
    private sealed class ClientModel(string name) : IAgentModel
    {
        public string Name => name;

        public Cycle Client { get; } = new();

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("not called");
    }

    internal sealed class Cycle
    {
        public Cycle Self => this;
    }

    private sealed class UploadReporter : Reporter
    {
        public Cycle Client { get; } = new();

        public string Label { get; init; } = "upload";

        public override string Name => Label;
    }

    private sealed class CyclicStore : IArtifactStore
    {
        public Cycle Client { get; } = new();

        public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default) => Task.FromResult("r");
    }

    /// <summary>The config with one target of <paramref name="platform"/> declaring <paramref name="app"/>.</summary>
    internal static E2EConfig AppOn(string platform, string app) =>
        E2EConfig.Parse($$"""{ "targets": [{ "platform": "{{platform}}", "app": {{app}} }] }""", Root, Env());

    /// <summary>The target's app on <paramref name="platform"/>, resolved.</summary>
    internal static ResolvedApp ResolvedOn(string platform, string app) =>
        TargetApps.ResolveTargetApp("web", AppOn(platform, app).Target.App, Root);

    /// <summary>The config with one web target declaring <paramref name="app"/>.</summary>
    internal static E2EConfig App(string app, string? root = null) =>
        E2EConfig.Parse($$"""{ "targets": [{ "app": {{app}} }] }""", root ?? Root, Env());

    /// <summary>The one web target's app resolved, as <c>commandOf</c> and <c>resolveApp</c> of upstream's test read it.</summary>
    internal static ResolvedApp Resolved(string app, string? root = null, int? port = null) =>
        TargetApps.ResolveTargetApp("web", App(app, root).Target.App, root ?? Root, port);

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    private sealed class SecretHoldingEngine(Secret? secret = null, string platform = "web") : E2E.Engine.IEngine
    {
        public string Name => "fake";

        public string Platform => platform;

        public string Version => "0";

        public E2E.Engine.EngineCapabilities Capabilities => E2E.Engine.EngineCapabilities.None;

        public IReadOnlyList<Secret> Secrets => secret is null ? [] : [secret];

        public Task<E2E.Engine.IEngineSession> StartAsync(E2E.Engine.EngineStartOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
