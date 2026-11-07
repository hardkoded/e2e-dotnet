// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>One <c>config.credentials</c> entry, resolved (upstream's <c>ResolvedCredential</c>, <c>config/resolve.ts</c>): an account whose password is the secret <c>{name}.password</c>.</summary>
/// <param name="Name">The entry's name in <c>credentials</c>.</param>
/// <param name="Username">The username from <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> or the config.</param>
/// <param name="Password">The password from <c>E2E_USER_&lt;NAME&gt;_PASSWORD</c> or the config, named <c>&lt;name&gt;.password</c>.</param>
public sealed record ResolvedCredential(string Name, string Username, Secret Password);

/// <summary>
/// The engine of a resolved target as selection reads it: its name, the capabilities it declares, and the secrets its options hold (the slice of
/// upstream's <c>Engine</c> that <c>select</c> and <c>checkEngineSecrets</c> use). .NET-only name.
/// </summary>
internal sealed record ResolvedEngine(string Name, IReadOnlySet<string> Capabilities, IReadOnlyList<Secret>? Secrets = null);

/// <summary>A target as selection reads it (upstream's <c>ResolvedTarget</c>, <c>config/resolve.ts</c>); null <see cref="Engine"/> grades no capability.</summary>
internal sealed record ResolvedTarget(string Name, string Platform, AppConfig App, ResolvedEngine? Engine = null);

/// <summary>
/// The resolved config as selection and the test filters read it (upstream's <c>ResolvedConfig</c>, <c>config/resolve.ts</c>). The port has
/// no resolver yet, so only these members exist.
/// </summary>
internal sealed record ResolvedConfig
{
    public required string ProjectRoot { get; init; }

    /// <summary>The loaded config file; null for a config object.</summary>
    public string? ConfigPath { get; init; }

    public bool Ci { get; init; }

    public required IReadOnlyList<ResolvedTarget> Targets { get; init; }

    /// <summary>The test filters, in order; empty when unset. A run with <c>--changed-since</c> applies them.</summary>
    public IReadOnlyList<ITestFilter> Filters { get; init; } = [];

    /// <summary>Every secret the run holds, by name: the <c>config.secrets</c> entries and every credential's password.</summary>
    public IReadOnlyList<(string Name, string Value)> AllSecrets { get; init; } = [];

    /// <summary>
    /// The digest of the resolved config (upstream's <c>configDigest</c>, <c>config/resolve.ts</c>), SHA-256 over the canonical JSON of the
    /// declared config with credential material replaced by <c>{ secretName }</c> and env values by <c>{ envName }</c>. Empty when
    /// the resolver did not compute one.
    /// </summary>
    public string ConfigDigest { get; init; } = "";

    /// <summary>The longest a filter's name may be (upstream's <c>MAX_FILTER_NAME</c>, <c>config/resolve.ts</c>).</summary>
    internal const int MaxFilterName = 256;

    // Upstream's computeConfigDigest takes these out of `raw` before the plain rest is cloned: they are reduced below, or never enter.
    // `failOnSkippedFailure`, `maxDuration`, `filters`, `artifacts`, and `output` stay out, as do `trace` and `video` at the top
    // (a target's own enter nowhere, since a target digests by name, platform, and app).
    private static readonly HashSet<string> DigestedApart = new(StringComparer.Ordinal)
    {
        "failOnSkippedFailure", "maxDuration", "filters", "artifacts", "output", "trace", "video", "targets", "credentials", "secrets", "agents", "cache", "reporters",
    };

    /// <summary>
    /// Upstream's <c>computeConfigDigest</c>. Only plain data enters: a live value is reduced to its identity first. <c>agents</c> digest
    /// their tools by name, an executor by its name, version, cache, and models, and a model anywhere by its identity; <c>cache.store</c> digests as
    /// whether it is writable; targets digest by declaration; credentials and secrets keep their names and lose their values. An <c>artifacts</c> store,
    /// <c>output</c>, <c>trace</c> and <c>video</c> (at the top or on a target), and the reporters that are no type name never enter, so
    /// adding one leaves the replays valid. Differences: the config is the one <see cref="E2EConfig.RawConfig"/> holds, and
    /// <paramref name="agents"/> are the agents' options in code (the fixture's), else the <c>agents</c> the file declares; the port has no
    /// <c>projectId</c> or engine on a target, so neither enters.
    /// </summary>
    internal static string ComputeConfigDigest(E2EConfig config, IReadOnlyDictionary<string, AgentOptions>? agents = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var raw = config.RawConfig;
        var sanitized = new JsonObject();
        JsonElement credentials = default;
        JsonElement secrets = default;
        JsonElement declaredAgents = default;
        JsonElement cache = default;
        JsonElement reporters = default;
        if (raw.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raw.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "credentials":
                        credentials = property.Value;
                        break;
                    case "secrets":
                        secrets = property.Value;
                        break;
                    case "agents":
                        declaredAgents = property.Value;
                        break;
                    case "cache":
                        cache = property.Value;
                        break;
                    case "reporters":
                        reporters = property.Value;
                        break;
                    default:
                        if (!DigestedApart.Contains(property.Name))
                        {
                            sanitized[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                        }

                        break;
                }
            }
        }

        // A reporter object changes nothing about what a run records, so only a type name enters: the built-in ids digest as they always have.
        if (reporters.ValueKind == JsonValueKind.Array)
        {
            sanitized["reporters"] = new JsonArray([.. reporters.EnumerateArray().Where(reporter => reporter.ValueKind == JsonValueKind.String).Select(reporter => (JsonNode?)JsonValue.Create(reporter.GetString()))]);
        }

        if (agents is not null)
        {
            sanitized["agents"] = DigestAgents(agents);
        }
        else if (declaredAgents.ValueKind != JsonValueKind.Undefined)
        {
            sanitized["agents"] = JsonNode.Parse(declaredAgents.GetRawText());
        }

        if (cache.ValueKind != JsonValueKind.Undefined || config.Cache.Store is not null)
        {
            sanitized["cache"] = DigestCache(cache, config.Cache);
        }

        if (credentials.ValueKind == JsonValueKind.Object)
        {
            // The indexer, not the constructor: JSON tolerates a repeated key, and the last one wins as it does when the config is read.
            var digested = new JsonObject();
            foreach (var credential in credentials.EnumerateObject())
            {
                digested[credential.Name] = DigestCredential(credential);
            }

            sanitized["credentials"] = digested;
        }

        if (secrets.ValueKind == JsonValueKind.Object)
        {
            var digested = new JsonObject();
            foreach (var secret in secrets.EnumerateObject())
            {
                digested[secret.Name] = SecretName(secret.Name);
            }

            sanitized["secrets"] = digested;
        }

        // Targets enter as resolved: see DigestTargets.
        sanitized["targets"] = DigestTargets(config.Targets);
        return Ids.CanonicalDigest(sanitized);
    }

    // A credential keeps its username as written and loses its password for the name of the secret.
    private static JsonObject DigestCredential(JsonProperty credential)
    {
        var digest = new JsonObject();
        if (credential.Value.ValueKind == JsonValueKind.Object && credential.Value.TryGetProperty("username", out var username))
        {
            digest["username"] = JsonNode.Parse(username.GetRawText());
        }

        digest["password"] = SecretName(credential.Name);
        return digest;
    }

    private static JsonObject SecretName(string name) => new() { ["secretName"] = name };

    /// <summary>
    /// The targets as they enter the config digest (upstream's <c>digestTargets</c>, <c>config/targets.ts</c>): name, platform, and the app
    /// as declared, env values reduced to their names and the run's port never. Differences: a port target has no engine handle, so
    /// no engine declaration enters.
    /// </summary>
    internal static JsonArray DigestTargets(IReadOnlyList<TargetConfig> targets) =>
        new([.. targets.Select(target => (JsonNode?)new JsonObject
        {
            ["name"] = target.Name,
            ["platform"] = target.Platform,
            ["app"] = TargetApps.DigestTargetApp(target.App),
        })]);

    /// <summary>
    /// The agents as the digest records them (upstream's <c>digestAgents</c>): the plain options, the tools named, and the executor reduced
    /// to what identifies it. Differences: an option the entry leaves unset enters at its default, since a property cannot tell a default from a
    /// written value.
    /// </summary>
    internal static JsonObject DigestAgents(IReadOnlyDictionary<string, AgentOptions> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var digest = new JsonObject();
        foreach (var (name, entry) in agents)
        {
            var options = new JsonObject
            {
                ["judgmentTimeout"] = (long)entry.JudgmentTimeout.TotalMilliseconds,
                ["maxObservationBytes"] = entry.MaxObservationBytes,
                ["maxInputTokens"] = entry.MaxInputTokens,
            };
            if (entry.System is not null)
            {
                options["system"] = entry.System;
            }

            if (entry.Context is not null)
            {
                options["context"] = entry.Context;
            }

            if (entry.MaxSteps is { } maxSteps)
            {
                options["maxSteps"] = maxSteps;
            }

            if (entry.MaxModelCalls is { } maxModelCalls)
            {
                options["maxModelCalls"] = maxModelCalls;
            }

            if (entry.Model is not null)
            {
                options["model"] = ModelIdentity(entry.Model);
            }

            if (entry.Judge is not null)
            {
                options["judge"] = ModelIdentity(entry.Judge);
            }

            if (entry.ProviderOptions is not null)
            {
                var providerOptions = new JsonObject();
                foreach (var (provider, value) in entry.ProviderOptions)
                {
                    providerOptions[provider] = value.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(value.GetRawText());
                }

                options["providerOptions"] = providerOptions;
            }

            if (entry.Tools is not null)
            {
                options["tools"] = new JsonArray([.. entry.Tools.Keys.Order(StringComparer.Ordinal).Select(tool => (JsonNode?)JsonValue.Create(tool))]);
            }

            if (entry.Executor is { } executor)
            {
                var identity = new JsonObject { ["name"] = executor.Name };
                if (executor.Version is not null)
                {
                    identity["version"] = executor.Version;
                }

                identity["cache"] = executor.Cache == ExecutorCache.Off ? "off" : "inherit";
                if (executor.Model is not null)
                {
                    identity["model"] = ModelIdentity(executor.Model);
                }

                if (executor.Judge is not null)
                {
                    identity["judge"] = ModelIdentity(executor.Judge);
                }

                options["executor"] = identity;
            }

            digest[name] = options;
        }

        return digest;
    }

    /// <summary>
    /// <c>cache</c> as the digest records it (upstream's <c>digestCache</c>): the options as written, with the store reduced to whether it writes.
    /// Differences: a store has no <c>writable</c> member here, so the cache mode decides, as it does for the run.
    /// </summary>
    internal static JsonObject DigestCache(JsonElement declared, CacheConfig cache)
    {
        var digest = declared.ValueKind == JsonValueKind.Object ? (JsonObject)JsonNode.Parse(declared.GetRawText())! : [];
        if (cache.Store is not null)
        {
            digest["store"] = new JsonObject { ["writable"] = cache.Mode == CacheMode.ReadWrite };
        }

        return digest;
    }

    /// <summary>
    /// The digest-stable identity of a live model (upstream's <c>modelIdentity</c>). Differences: a model here is its name alone
    /// (<see cref="IAgentModel.Name"/>), where upstream's carries a provider, a model id, and a specification version.
    /// </summary>
    internal static JsonObject ModelIdentity(IAgentModel model) => new() { ["name"] = model.Name };

    /// <summary>
    /// Resolves a config whose targets may hold an engine (upstream's <c>resolve</c>, <c>config/resolve.ts</c>, the part that builds
    /// <c>ResolvedTarget</c> from each target's <c>engine</c> and runs <see cref="CheckEngineSecrets"/> at load). .NET-only name and place beside
    /// <c>HostTestFilters.Resolve</c>: the port has no full resolver yet, and this builds only the targets and the checked secrets. Differences:
    /// a target's engine comes from <see cref="TargetConfig.Engine"/>, set in code; a JSON target has none.
    /// </summary>
    internal static ResolvedConfig Resolve(E2EConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var targets = new List<ResolvedTarget>(config.Targets.Count);
        foreach (var target in config.Targets)
        {
            var engine = target.Engine is { } held
                ? new ResolvedEngine(held.Name, CapabilityNames(held.Capabilities), held.Secrets)
                : null;
            targets.Add(new ResolvedTarget(target.Name, target.Platform, target.App, engine));
        }

        CheckEngineSecrets(targets, config.Secrets, config.Credentials);
        return new ResolvedConfig
        {
            ProjectRoot = config.ProjectRoot,
            ConfigPath = config.ConfigPath,
            Ci = config.Ci,
            Targets = targets,
            ConfigDigest = ComputeConfigDigest(config),
            AllSecrets = [.. config.AllSecrets.Select(secret => (secret.Name, secret.Value))],
        };
    }

    private static HashSet<string> CapabilityNames(EngineCapabilities capabilities) =>
        [.. Enum.GetValues<EngineCapabilities>().Where(flag => flag != EngineCapabilities.None && capabilities.HasFlag(flag)).Select(flag => char.ToLowerInvariant(flag.ToString()[0]) + flag.ToString()[1..])];

    /// <summary>
    /// Every secret an engine's options hold (a <c>Secrets.Get</c> the config evaluated before any run existed) names a configured secret, so a
    /// typo fails at load with the nearest name, not mid-run (upstream's <c>checkEngineSecrets</c>, <c>config/resolve.ts</c>). A credential's
    /// password is not one: an engine option takes a <c>secrets</c> entry. <see cref="Resolve"/> calls it, as upstream's <c>resolve</c> does.
    /// </summary>
    internal static void CheckEngineSecrets(IReadOnlyList<ResolvedTarget> targets, Secrets secrets, IReadOnlyDictionary<string, ResolvedCredential> credentials)
    {
        foreach (var target in targets)
        {
            foreach (var secret in target.Engine?.Secrets ?? [])
            {
                if (secrets.Names.Contains(secret.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                var credential = ConfigSecrets.CredentialNamed(secret.Name, credentials);
                var hint = credential is null
                    ? Suggestions.DidYouMean(secret.Name, [.. secrets.Names])
                    : "; credential " + LocatorExpect.Quote(credential.Name) + " is not a secrets entry, and an engine option takes one: declare the value under config.secrets";
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    "target " + LocatorExpect.Quote(target.Name) + " engine " + target.Engine!.Name + " uses secrets.get(" + LocatorExpect.Quote(secret.Name) + "), which is not configured; add it to config.secrets" + hint);
            }
        }
    }

    /// <summary>
    /// Resolves the test filters (upstream's <c>resolveTestFilters</c>, <c>config/resolve.ts</c>): the list in declaration order, empty when
    /// null. A filter's name is how the report and errors tell it apart, so each name follows the target name rules, is at most
    /// <see cref="MaxFilterName"/> characters, and is unique. A null item (upstream's hole, or an item that is no <c>TestFilter</c>) is
    /// refused. Differences: upstream's refusals of a value that is not a list, of a function in the list, and of an item without a
    /// <c>filter</c> function have no port, since the list and its items are typed.
    /// </summary>
    internal static IReadOnlyList<ITestFilter> ResolveTestFilters(IReadOnlyList<ITestFilter?>? raw)
    {
        if (raw is null)
        {
            return [];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var filters = new List<ITestFilter>(raw.Count);
        for (var index = 0; index < raw.Count; index++)
        {
            if (raw[index] is not { Name: { } name } value)
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filters[{index}] must be a TestFilter: {{ name, filter(tests, context) }}");
            }

            if (!E2EConfig.ValidName(name))
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"invalid filter name {LocatorExpect.Quote(name)}: names are ASCII letters, numbers, \"_\", \"-\", or \".\", and cannot be only dots");
            }

            if (name.Length > MaxFilterName)
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filter name {LocatorExpect.Quote(ReportFormat.Ellipsize(name, 32))} is longer than {MaxFilterName} characters");
            }

            if (!names.Add(name))
            {
                throw new ConfigurationException("INVALID_CONFIG", $"filters has two filters named {LocatorExpect.Quote(name)}; the report tells filters apart by name");
            }

            filters.Add(value);
        }

        return filters;
    }
}
