// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// The resolved <c>e2e.config.json</c>. The JSON shape follows upstream <c>e2e.config.ts</c>:
/// <c>targets[].app.url</c>, named <c>agents</c>, top-level timeouts in milliseconds,
/// <c>retries</c>, <c>cache</c>, <c>secrets</c>, and <c>reporters</c> (type names, .NET-only). Unknown keys fail with
/// <c>INVALID_CONFIG</c>. <c>E2ETest</c> finds and applies this file; a fixture can
/// still override each value. There is no default model.
/// </summary>
public sealed class E2EConfig
{
    /// <summary>The file name <see cref="Find"/> looks for.</summary>
    public const string FileName = "e2e.config.json";

    private static readonly Dictionary<string, string> MovedKeys = new(StringComparer.Ordinal)
    {
        ["app"] = "the app under test is declared on its target: targets: [{ \"app\": { \"url\": ... } }]",
        ["engine"] = "the engine is chosen by the fixture; declare the target as targets: [{ \"platform\": \"web\" }]",
        ["agent"] = "agents are named: agents: { \"default\": { \"model\": ... } }",
        ["timeouts"] = "timeouts are top-level milliseconds: timeout, launchTimeout, actionTimeout, assertionTimeout, cleanupTimeout",
    };

    // Upstream's REMOVED_TOP_LEVEL_KEYS: keys the runner used to accept, each mapped to what replaces it.
    private static readonly Dictionary<string, string> RemovedTopLevelKeys = new(StringComparer.Ordinal)
    {
        ["specVersion"] = "delete it; the runner version is the format version",
        ["limits"] = "set maxInputTokens on each agent (it was limits.maxModelTokensPerCall); the runner fixes maxAgentContextBytes, maxLedgerBytes, and maxEventsPerStep",
    };

    // Upstream's REMOVED_AGENT_KEYS: agent keys the runner used to accept, each mapped to what replaces it.
    private static readonly Dictionary<string, string> RemovedAgentKeys = new(StringComparer.Ordinal)
    {
        ["timeout"] = "use judgmentTimeout, the deadline of one assert, waitFor, or extract call",
        ["maxTurns"] = "use maxModelCalls, the model requests one agent call may make",
        ["maxModelTokensPerCall"] = "use maxInputTokens",
        ["limits"] = "set maxInputTokens on the agent; the runner fixes the other limits",
    };

    private static readonly string[] TopLevelKeys =
    [
        "targets", "timeout", "launchTimeout", "actionTimeout", "assertionTimeout", "cleanupTimeout",
        "retries", "failOnSkippedFailure", "maxDuration", "agents", "cache", "secrets", "credentials", "artifacts", "output", "trace", "video", "testAssembly", "reporters",
    ];

    // Upstream keys the .NET port does not implement. They fail loudly rather than being ignored.
    private static readonly string[] UnsupportedTopLevelKeys =
    [
        "projectId", "tests", "workers",
    ];

    private static readonly string[] ReporterKeys = ["type", "options"];

    private static readonly string[] TargetKeys = ["name", "platform", "app", "trace", "video"];

    private static readonly string[] UnsupportedTargetKeys = ["engine"];

    private static readonly string[] CredentialKeys = ["username", "password"];

    private static readonly string[] AppKeys = ["url", "command", "readyUrl", "identity", "environment", "permissions"];

    private static readonly string[] MobileAppKeys =
        ["bundleId", "appPath", "command", "readyUrl", "identity", "environment", "launchArguments", "permissions"];

    private static readonly string[] WindowsAppKeys =
        ["bundleId", "appPath", "command", "readyUrl", "identity", "environment", "launchArguments"];

    // A Windows app has no permissions to grant.
    private static readonly string[] UnsupportedAppKeys = ["permissions"];

    private static readonly string[] Environments = ["test", "staging", "production"];

    private static readonly string[] Platforms = ["web", "ios", "android", "windows"];

    private static readonly string[] AgentKeys =
        ["model", "judge", "provider", "system", "context", "baseUrl", "apiKeyEnv", "maxSteps", "maxModelCalls", "judgmentTimeout", "maxObservationBytes", "maxInputTokens", "providerOptions"];

    private static readonly string[] UnsupportedAgentKeys = ["tools", "executor"];

    private static readonly string[] CacheKeys = ["mode", "dir", "strict"];

    // A store is code: a JSON file cannot name one. Upstream's `cache.store` and `artifacts.store` are `E2EConfig.WithCacheStore` and `E2EConfig.WithArtifactStore`.
    private static readonly string[] UnsupportedCacheKeys = ["store"];

    private static readonly string[] ArtifactsKeys = [];

    private static readonly string[] UnsupportedArtifactsKeys = ["store"];

    // Upstream's VIDEO_MOVED: where a video fact lives now, for a config that still says it the old way.
    private const string VideoMoved =
        "video is its own option: video: 'on' in the config or on a target, video: 'retain-on-failure' to keep only failed attempts' recordings, or E2E_VIDEO=<mode> for one run";

    // What `WithCacheStore` re-reads the cache from: the `cache` value as written and the environment it was read against.
    private JsonElement? _rawCache;
    private CliOverrides _cli = new();

    private Func<string, string?> _environment = Environment.GetEnvironmentVariable;

    private E2EConfig()
    {
    }

    /// <summary>The file this config was read from, or null for the defaults.</summary>
    public string? ConfigPath { get; private init; }

    /// <summary>The config file's directory. Relative paths such as <c>cache.dir</c> resolve against it.</summary>
    public string ProjectRoot { get; private init; } = "";

    /// <summary>True when the <c>CI</c> environment variable is set to a true value.</summary>
    public bool Ci { get; private init; }

    public IReadOnlyList<TargetConfig> Targets { get; private set; } = [];

    /// <summary>
    /// The only target the config declares. .NET-only: a fixture, a host, or a command with no target of its own reads it. With several
    /// targets it throws <c>TARGET_REQUIRED</c> (upstream's <c>selectTarget</c>): name one from <see cref="Targets"/>.
    /// </summary>
    public TargetConfig Target => SelectTarget.Select(Targets, null, "name one of them from E2EConfig.Targets");

    /// <summary>Whole-test timeout. Upstream <c>timeout</c>, 120 s by default.</summary>
    public TimeSpan Timeout { get; private init; } = E2EDefaults.TestTimeout;

    /// <summary>Engine start timeout. Upstream <c>launchTimeout</c>, 60 s by default.</summary>
    public TimeSpan LaunchTimeout { get; private init; } = E2EDefaults.LaunchTimeout;

    public TimeSpan ActionTimeout { get; private init; } = E2EDefaults.ActionTimeout;

    public TimeSpan AssertionTimeout { get; private init; } = E2EDefaults.AssertionTimeout;

    /// <summary>Engine shutdown timeout. Upstream <c>cleanupTimeout</c>, 30 s by default.</summary>
    public TimeSpan CleanupTimeout { get; private init; } = E2EDefaults.CleanupTimeout;

    /// <summary>
    /// Upstream <c>failOnSkippedFailure</c>, false by default. Recorded and validated only: the port has no runner, so nothing reads it
    /// (upstream's runner decides the exit code from it). It stays out of <see cref="ResolvedConfig.ComputeConfigDigest"/>.
    /// </summary>
    public bool FailOnSkippedFailure { get; private init; }

    /// <summary>
    /// Upstream <c>maxDuration</c>: the run's duration limit, unset by default. Recorded and validated only: the port has no runner to stop
    /// (the test framework owns the run). It stays out of <see cref="ResolvedConfig.ComputeConfigDigest"/>.
    /// </summary>
    public TimeSpan? MaxDuration { get; private init; }

    /// <summary>0 to 10. Defaults to 1 in CI and 0 elsewhere. NUnit retries still come from <c>[Retry]</c>.</summary>
    public int Retries { get; private init; }

    public IReadOnlyDictionary<string, AgentConfig> Agents { get; private init; } = ReadOnlyDictionary<string, AgentConfig>.Empty;

    /// <summary>
    /// The agents unpinned tests run as (upstream's <c>agentNames</c>): <c>default</c>, or the names <c>--agent</c> gave, in order and
    /// deduplicated. Never empty.
    /// </summary>
    public IReadOnlyList<string> AgentNames { get; private init; } = ["default"];

    /// <summary>The first of <see cref="AgentNames"/>, <c>default</c> unless <c>--agent</c> picked another: the run's agent where exactly one is wanted. It always exists; its model may be null.</summary>
    public AgentConfig Agent => Agents[AgentNames[0]];

    /// <summary>
    /// The absolute path of the test assembly <c>e2e mcp</c> runs a test from (<c>open_session {test}</c>), or null. .NET-only: upstream
    /// collects tests from its <c>tests</c> globs. The config's <c>testAssembly</c> is relative to the config's directory.
    /// </summary>
    public string? TestAssembly { get; private init; }

    public CacheConfig Cache { get; private set; } = new();

    /// <summary>
    /// The reporters the config lists, in config order (upstream's <c>reporters</c>, <c>ResolvedConfig.customReporters</c>). .NET-only shape: an
    /// entry names a <see cref="Reporter"/> type, and the reporter is built when the run ends (see <see cref="Reporters"/>).
    /// </summary>
    internal IReadOnlyList<ReporterEntry> Reporters { get; private init; } = [];

    /// <summary>
    /// Where artifacts go, over the files under the results directory (upstream's <c>artifacts.store</c>, <c>ResolvedConfig.artifactStore</c>).
    /// None by default. A store is code, so the config file cannot name one: <see cref="WithArtifactStore"/> sets it.
    /// </summary>
    public IArtifactStore? ArtifactStore { get; private set; }

    /// <summary>
    /// The absolute directory the runs write their results to: <c>output</c>, else <c>&lt;ProjectRoot&gt;/.e2e</c>. Results go to
    /// <c>results/</c> under it, the session stores to <c>sessions/</c>, and an <c>e2e mcp</c> recording to <c>videos/</c>. Upstream's
    /// <c>ResolvedConfig.output</c>. It is checked when read, not when the config loads, so <c>--output</c> can replace a configured value
    /// that is refused (upstream checks only the one that is used). Throws <c>INVALID_CONFIG</c> for a directory the run cannot own.
    /// </summary>
    public string Output => ResolveOutput(null);

    private JsonElement? _rawOutput;

    /// <summary>
    /// The config as written, the JSON object <see cref="Parse(string, string, Func{string, string?}?)"/> read. .NET-only: upstream's
    /// <c>computeConfigDigest</c> reads the declared <c>raw</c> config, and the port's resolved members hold defaults the file did not write.
    /// </summary>
    internal JsonElement RawConfig { get; private init; }

    public Secrets Secrets { get; private set; } = Secrets.Empty;

    /// <summary>The <c>credentials</c> entries by name, each with its password as the secret <c>&lt;name&gt;.password</c> (upstream's <c>ResolvedConfig.credentials</c>).</summary>
    public IReadOnlyDictionary<string, ResolvedCredential> Credentials { get; private set; } = ReadOnlyDictionary<string, ResolvedCredential>.Empty;

    private JsonElement? _rawCredentials;
    private JsonElement? _rawSecrets;
    private IReadOnlyDictionary<string, CredentialProvider> _credentialProviders = ReadOnlyDictionary<string, CredentialProvider>.Empty;
    private IReadOnlyDictionary<string, SecretProvider> _secretProviders = ReadOnlyDictionary<string, SecretProvider>.Empty;

    /// <summary>A <c>credentials</c> entry the code declares: the username (null reads <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> alone) and the provider of its password.</summary>
    private sealed record CredentialProvider(string? Username, SecretProvider Password);

    /// <summary>Every secret the run holds: each credential's password, then the <c>secrets</c> entries (upstream's <c>allSecrets</c>). Redaction reads it.</summary>
    internal IReadOnlyList<Secret> AllSecrets =>
    [
        .. Credentials.Values.Select(credential => credential.Password),
        .. Secrets.Names.Select(Secrets.Get),
    ];

    /// <summary>
    /// A copy of the config whose replay cache is <paramref name="store"/> (upstream's <c>cache.store</c>), over the file store at
    /// <c>Cache.Directory</c>. .NET-only: the config file cannot name a store, so code sets it. Like upstream, a store ends the
    /// <c>E2E_CACHE_DIR</c> override (a store has no directory), and CI does not turn an unset mode into <see cref="CacheMode.ReadOnly"/>:
    /// the store states its own trust.
    /// </summary>
    public E2EConfig WithCacheStore(IStepCache store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var copy = (E2EConfig)MemberwiseClone();
        copy.Cache = ReadCache(_rawCache, ProjectRoot, Ci, _environment, store, _cli);
        return copy;
    }

    /// <summary>
    /// A copy of the config with the <c>secrets</c> entry <paramref name="name"/> computed by <paramref name="provider"/> on every fill, in place
    /// of a value baked at load (upstream's <c>secrets: { name: () =&gt; string | Promise&lt;string&gt; }</c>, <c>SecretConfig</c>). .NET-only: the
    /// config file cannot hold a function, so code sets it, as it sets <see cref="WithCacheStore"/>. It replaces a file entry of the same name.
    /// <c>E2E_SECRET_&lt;NAME&gt;</c> still wins, as it does over a file value, and then the secret is that static value. The provider's value
    /// must be a string of at least six code points when it is asked for: a shorter one fails the fill.
    /// </summary>
    public E2EConfig WithSecretProvider(string name, SecretProvider provider)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(provider);
        return WithProviders(_credentialProviders, new Dictionary<string, SecretProvider>(_secretProviders, StringComparer.Ordinal) { [name] = provider });
    }

    /// <summary>
    /// A copy of the config with the <c>credentials</c> entry <paramref name="name"/> whose password <paramref name="password"/> computes on
    /// every fill (upstream's <c>credentials: { name: { username, password: () =&gt; string | Promise&lt;string&gt; } }</c>,
    /// <c>CredentialConfig</c>; only the password may be a provider). .NET-only: the config file cannot hold a function. It replaces a file entry
    /// of the same name. <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> and <c>E2E_USER_&lt;NAME&gt;_PASSWORD</c> still win. A null
    /// <paramref name="username"/> leaves it to <c>E2E_USER_&lt;NAME&gt;_USERNAME</c>.
    /// </summary>
    public E2EConfig WithCredential(string name, string? username, SecretProvider password)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(password);
        return WithProviders(new Dictionary<string, CredentialProvider>(_credentialProviders, StringComparer.Ordinal) { [name] = new CredentialProvider(username, password) }, _secretProviders);
    }

    /// <summary>
    /// A copy of the config with <paramref name="target"/> in place of the target of the same name, or after the others when none has it.
    /// .NET-only: the config file cannot hold an engine, so code puts one in a target (internal until a public API exists, #1339).
    /// </summary>
    internal E2EConfig WithTarget(TargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var copy = (E2EConfig)MemberwiseClone();
        var targets = Targets.ToList();
        var at = targets.FindIndex(existing => string.Equals(existing.Name, target.Name, StringComparison.Ordinal));
        if (at >= 0)
        {
            targets[at] = target;
        }
        else
        {
            targets.Add(target);
        }

        copy.Targets = targets;
        return copy;
    }

    private E2EConfig WithProviders(IReadOnlyDictionary<string, CredentialProvider> credentialProviders, IReadOnlyDictionary<string, SecretProvider> secretProviders)
    {
        var copy = (E2EConfig)MemberwiseClone();
        copy._credentialProviders = credentialProviders;
        copy._secretProviders = secretProviders;
        copy.Credentials = ReadCredentials(_rawCredentials, _environment, credentialProviders);
        copy.Secrets = ReadSecrets(_rawSecrets, _environment, copy.Credentials, secretProviders);
        return copy;
    }

    /// <summary>
    /// A copy of the config whose artifacts go to <paramref name="store"/> as well (upstream's <c>artifacts.store</c>). .NET-only: the config
    /// file cannot name a store, so code sets it.
    /// </summary>
    public E2EConfig WithArtifactStore(IArtifactStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var copy = (E2EConfig)MemberwiseClone();
        copy.ArtifactStore = store;
        return copy;
    }

    /// <summary>Reads and validates a config file. Throws <see cref="ConfigurationException"/> with <c>INVALID_CONFIG</c>.</summary>
    public static E2EConfig Load(string path, Func<string, string?>? environment = null) => Load(path, environment, null);

    /// <summary>Upstream's <c>resolveConfig(raw, { cli })</c> for a file: the flags that replace config keys applied (<c>e2e cache --cache-dir</c>).</summary>
    internal static E2EConfig Load(string path, Func<string, string?>? environment, CliOverrides? cli)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        string json;
        try
        {
            json = File.ReadAllText(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException("INVALID_CONFIG", "Cannot read " + full + ": " + ex.Message, ex);
        }

        return Parse(json, Path.GetDirectoryName(full)!, full, environment, cli);
    }

    /// <summary>Validates config JSON. <paramref name="projectRoot"/> anchors relative paths.</summary>
    public static E2EConfig Parse(string json, string projectRoot, Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Parse(json, Path.GetFullPath(projectRoot), null, environment, null);
    }

    /// <summary>
    /// Upstream's <c>resolveConfig(raw, { cli })</c>: the config with the flags that replace config keys applied. The port has no
    /// command line to read them from, so only code (the tests) passes them.
    /// </summary>
    internal static E2EConfig Parse(string json, string projectRoot, Func<string, string?>? environment, CliOverrides cli)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Parse(json, Path.GetFullPath(projectRoot), null, environment, cli);
    }

    /// <summary>The config with no file: upstream defaults, a <c>web</c> target with no URL, and no model.</summary>
    public static E2EConfig Default(string projectRoot, Func<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Parse("{}", Path.GetFullPath(projectRoot), null, environment, null);
    }

    /// <summary>
    /// The nearest <see cref="FileName"/> in each start directory or one of its parents,
    /// tried in order, or null. A directory with a <c>.git</c> entry is the repository root: the search
    /// looks for the file there and goes no higher.
    /// </summary>
    public static string? Find(params string[] startDirectories)
    {
        ArgumentNullException.ThrowIfNull(startDirectories);
        foreach (var start in startDirectories)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                if (Path.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The failure for an explicit config path with no file behind it (upstream's <c>explicitConfigNotFound</c> in <c>config/load.ts</c>).
    /// A relative path names the <paramref name="cwd"/> it resolved against, which may not be the one the caller meant.
    /// </summary>
    internal static ConfigurationException ExplicitConfigNotFound(string cwd, string explicitPath, string absolute)
    {
        if (Path.IsPathRooted(explicitPath))
        {
            return new ConfigurationException("CONFIG_NOT_FOUND", "config file not found: " + absolute);
        }

        var baseDirectory = Path.GetFullPath(cwd);
        return new ConfigurationException(
            "CONFIG_NOT_FOUND",
            "config file not found: " + absolute + " (" + explicitPath + " resolved against the working directory " + baseDirectory + "); pass an absolute path, or a path relative to that directory");
    }

    /// <summary>
    /// Loads the nearest <see cref="FileName"/> above the test assembly directory, then
    /// above the working directory. Without one, returns <see cref="Default"/> for the working directory.
    /// </summary>
    public static E2EConfig Discover(Func<string, string?>? environment = null)
    {
        var path = Find(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        return path is null ? Default(Directory.GetCurrentDirectory(), environment) : Load(path, environment);
    }

    /// <summary>True when <c>CI</c> is set to anything but empty, <c>0</c>, or <c>false</c>.</summary>
    public static bool IsCi(Func<string, string?>? environment = null)
    {
        var value = (environment ?? Environment.GetEnvironmentVariable)("CI")?.Trim();
        return !string.IsNullOrEmpty(value)
            && value != "0"
            && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a flag variable's value is <c>1</c> or <c>true</c>.</summary>
    private static bool IsFlagOn(string? value) => value?.Trim() is { } trimmed && (trimmed == "1" || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase));

    private static E2EConfig Parse(string json, string projectRoot, string? configPath, Func<string, string?>? environment, CliOverrides? cli)
    {
        cli ??= new CliOverrides();
        environment ??= Environment.GetEnvironmentVariable;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw Invalid((configPath ?? "config") + " is not valid JSON: " + ex.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("the config must be a JSON object");
            }

            CheckKeys(root, "config", TopLevelKeys, UnsupportedTopLevelKeys, MovedKeys, RemovedTopLevelKeys);
            var ci = IsCi(environment);
            var trace = ReadRun(root, RecordingKind.Trace, environment);
            var video = ReadRun(root, RecordingKind.Video, environment);
            var credentials = ReadCredentials(Property(root, "credentials"), environment, ReadOnlyDictionary<string, CredentialProvider>.Empty);
            var cache = ReadCache(Property(root, "cache"), projectRoot, ci, environment, null, cli);
            ReadArtifacts(Property(root, "artifacts"));
            // The port's `--agent` is one name in E2E_AGENT; flags passed by code win over it.
            var selected = cli.Agents ?? (environment("E2E_AGENT")?.Trim() is { Length: > 0 } run ? [run] : null);
            var agents = ResolveAgents(Property(root, "agents"), selected);
            return new E2EConfig
            {
                ConfigPath = configPath,
                ProjectRoot = projectRoot,
                Ci = ci,
                Targets = ReadTargets(Property(root, "targets"), projectRoot, trace, video),
                Timeout = Milliseconds(root, "timeout", E2EDefaults.TestTimeout),
                LaunchTimeout = Milliseconds(root, "launchTimeout", E2EDefaults.LaunchTimeout),
                ActionTimeout = Milliseconds(root, "actionTimeout", E2EDefaults.ActionTimeout),
                AssertionTimeout = Milliseconds(root, "assertionTimeout", E2EDefaults.AssertionTimeout),
                CleanupTimeout = Milliseconds(root, "cleanupTimeout", E2EDefaults.CleanupTimeout),
                FailOnSkippedFailure = ReadFailOnSkippedFailure(Property(root, "failOnSkippedFailure")),
                MaxDuration = Command.PositiveMs(Property(root, "maxDuration"), "maxDuration"),
                Retries = Property(root, "retries") is { } retries ? Integer(retries, "retries", 0, 10) : ci ? 1 : 0,
                Agents = agents.Agents,
                AgentNames = agents.AgentNames,
                TestAssembly = OptionalString(root, "testAssembly", "testAssembly") is { } testAssembly ? Path.GetFullPath(testAssembly, projectRoot) : null,
                Cache = cache,
                Reporters = ReadReporters(Property(root, "reporters")),
                _rawOutput = Property(root, "output")?.Clone(),
                RawConfig = root.Clone(),
                Secrets = ReadSecrets(Property(root, "secrets"), environment, credentials, ReadOnlyDictionary<string, SecretProvider>.Empty),
                Credentials = credentials,
                _rawCredentials = Property(root, "credentials")?.Clone(),
                _rawSecrets = Property(root, "secrets")?.Clone(),
                _rawCache = Property(root, "cache")?.Clone(),
                _environment = environment,
                _cli = cli,
            };
        }
    }

    // Upstream's resolveReporters: `reporters` is an array. An entry is a type name, or { type, options }. The shape is checked at load; the type is resolved when the run ends (see ReporterEntry).
    private static List<ReporterEntry> ReadReporters(JsonElement? raw)
    {
        var entries = new List<ReporterEntry>();
        if (raw is null)
        {
            return entries;
        }

        if (raw.Value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("reporters must be an array of reporter type names and { \"type\": ..., \"options\": ... } objects");
        }

        var index = 0;
        foreach (var entry in raw.Value.EnumerateArray())
        {
            var at = "reporters[" + index++.ToString(CultureInfo.InvariantCulture) + "]";
            string? name;
            JsonElement? options = null;
            if (entry.ValueKind == JsonValueKind.String)
            {
                name = entry.GetString();
            }
            else if (entry.ValueKind == JsonValueKind.Object)
            {
                CheckKeys(entry, at, ReporterKeys, [], null);
                name = Property(entry, "type") is { ValueKind: JsonValueKind.String } type ? type.GetString() : null;
                if (Property(entry, "options") is { } rawOptions)
                {
                    options = rawOptions.ValueKind == JsonValueKind.Object ? rawOptions.Clone() : throw Invalid(at + ".options must be an object");
                }
            }
            else
            {
                throw Invalid(at + " must be a reporter type name or an object with a string \"type\"");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw Invalid(at + " must be a reporter type name or an object with a string \"type\"");
            }

            entries.Add(new ReporterEntry(name.Trim(), options));
        }

        return entries;
    }

    private static List<TargetConfig> ReadTargets(JsonElement? raw, string projectRoot, RunRecording trace, RunRecording video)
    {
        if (raw is null)
        {
            return [new TargetConfig { Name = "web", Platform = "web", App = new AppConfig(), Trace = trace.For(null), Video = video.For(null) }];
        }

        if (raw.Value.ValueKind != JsonValueKind.Array || raw.Value.GetArrayLength() == 0)
        {
            throw Invalid("targets must be a non-empty array");
        }

        var targets = new List<TargetConfig>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defaulted = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var target in raw.Value.EnumerateArray())
        {
            targets.Add(ReadTarget(target, index++, projectRoot, trace, video, seen, defaulted));
        }

        return targets;
    }

    private static TargetConfig ReadTarget(
        JsonElement target, int index, string projectRoot, RunRecording trace, RunRecording video, HashSet<string> seen, HashSet<string> defaulted)
    {
        var at = "targets[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        if (target.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(at + " must be an object");
        }

        CheckKeys(target, at, TargetKeys, UnsupportedTargetKeys, null);
        var platform = OptionalString(target, "platform", at + ".platform") ?? "web";
        if (!Platforms.Contains(platform, StringComparer.Ordinal))
        {
            throw Invalid(at + ".platform \"" + platform + "\" is not supported; the .NET port drives \"web\", \"ios\", \"android\", and \"windows\"");
        }

        var declaredName = OptionalString(target, "name", at + ".name");
        var name = declaredName ?? platform;
        if (!ValidName(name))
        {
            throw Invalid(at + ".name must use ASCII letters, numbers, \"_\", \"-\", and \".\", and cannot be only dots");
        }

        if (!seen.Add(name))
        {
            var hint = declaredName is null || defaulted.Contains(name)
                ? "; a target without a name is named after its platform, so name one of them"
                : "";
            throw Invalid("duplicate target name \"" + name + "\"" + hint);
        }

        if (declaredName is null)
        {
            defaulted.Add(name);
        }

        var ownTrace = ReadMode(Property(target, "trace"), at + ".trace", RecordingKind.Trace);
        var ownVideo = ReadMode(Property(target, "video"), at + ".video", RecordingKind.Video);
        var app = new AppConfig();
        if (Property(target, "app") is { } rawApp)
        {
            if (rawApp.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(at + ".app must be an object");
            }

            // A pinned-app target (a device or a Windows app) names its app by bundle id and build path, a web target by URL.
            var pinned = platform != "web";
            CheckKeys(rawApp, at + ".app", !pinned ? AppKeys : platform == "windows" ? WindowsAppKeys : MobileAppKeys, UnsupportedAppKeys, null);
            var where = "target \"" + name + "\" app";
            app = new AppConfig
            {
                Url = OptionalString(rawApp, "url", at + ".app.url") is { } url ? Urls.NormalizeBaseUrl(url).Href : null,
                BundleId = OptionalString(rawApp, "bundleId", at + ".app.bundleId"),
                AppPath = OptionalString(rawApp, "appPath", at + ".app.appPath"),
                Command = Property(rawApp, "command") is { } command ? Command.Normalize(command, "target \"" + name + "\" app.command") : null,
                ReadyUrl = OptionalString(rawApp, "readyUrl", at + ".app.readyUrl"),
                Identity = OptionalString(rawApp, "identity", at + ".app.identity"),
                Environment = ReadEnvironment(Property(rawApp, "environment"), where + ".environment"),
                LaunchArguments = ReadLaunchArguments(Property(rawApp, "launchArguments"), where + ".launchArguments"),
                Permissions = ReadPermissions(Property(rawApp, "permissions"), where + ".permissions"),
            };

            // Resolved once here so a bad command, a free port nothing starts on, or a {port} without a URL fails at load, as upstream's resolveConfig does.
            TargetApps.ResolveTargetApp(name, app, projectRoot);
        }

        return new TargetConfig { Name = name, Platform = platform, App = app, Trace = trace.For(ownTrace), Video = video.For(ownVideo) };
    }

    // The run's trace or video mode before any target speaks: the environment variable, the config root, and the default under both.
    // A failed attempt keeps its trace by default. The video is off.
    private static RunRecording ReadRun(JsonElement root, RecordingKind kind, Func<string, string?> environment)
    {
        var key = kind == RecordingKind.Trace ? "trace" : "video";
        var variable = "E2E_" + key.ToUpperInvariant();
        RecordingMode? flag = null;
        if (environment(variable) is { } text && !string.IsNullOrWhiteSpace(text))
        {
            flag = ReadMode(JsonSerializer.SerializeToElement(text.Trim()), variable, kind);
        }

        return new RunRecording(flag, ReadMode(Property(root, key), key, kind), kind == RecordingKind.Trace ? RecordingMode.RetainOnFailure : RecordingMode.Off);
    }

    // Checks one trace or video value: a mode, or null when the key is unset.
    private static RecordingMode? ReadMode(JsonElement? raw, string where, RecordingKind kind)
    {
        if (raw is not { } value)
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (RecordingModes.TryParse(text, out var mode))
        {
            return mode;
        }

        if (kind == RecordingKind.Trace)
        {
            var block = value.ValueKind == JsonValueKind.Object;
            var record = block ? (value.TryGetProperty("record", out var inner) && inner.ValueKind == JsonValueKind.String ? inner.GetString() : null) : text;
            if (RecordingModes.TryLegacyTraceSpelling(record, block, out var was, out var meant))
            {
                var spelled = where.StartsWith("E2E_", StringComparison.Ordinal) ? where + "=" + RecordingModes.Name(meant) : "trace: '" + RecordingModes.Name(meant) + "'";
                throw Invalid(where + " " + was + " is the old spelling of " + spelled + "; the modes are " + RecordingModes.List);
            }
        }

        throw Invalid(where + " must be one of " + RecordingModes.List + ", got " + value.GetRawText());
    }

    /// <summary>
    /// Upstream's <c>resolveAgents</c>: every named entry, and <c>default</c> even when the config names none. The run's agents are
    /// <c>default</c> alone unless <c>--agent</c> named others; an unknown name is a config error before anything starts.
    /// </summary>
    private static (ReadOnlyDictionary<string, AgentConfig> Agents, IReadOnlyList<string> AgentNames) ResolveAgents(JsonElement? raw, IReadOnlyList<string>? selected)
    {
        var agents = new Dictionary<string, AgentConfig>(StringComparer.Ordinal);
        if (raw is not null)
        {
            if (raw.Value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("agents must be an object of agents by name: agents: { \"default\": { \"model\": ... } }");
            }

            foreach (var property in raw.Value.EnumerateObject())
            {
                if (!ValidName(property.Name))
                {
                    throw Invalid("agent name \"" + property.Name + "\" must use ASCII letters, numbers, \"_\", \"-\", and \".\", and cannot be only dots");
                }

                var label = "agents." + property.Name;
                var value = property.Value;
                if (value.ValueKind != JsonValueKind.Object)
                {
                    throw Invalid(label + " must be an object");
                }

                CheckKeys(value, label, AgentKeys, UnsupportedAgentKeys, null, RemovedAgentKeys);
                var context = AnyString(value, "context", label + ".context");
                ResolvedAgent.CheckContext(context, label + ".context");
                var provider = OptionalString(value, "provider", label + ".provider") ?? AgentConfig.DefaultProvider;
                if (!AgentConfig.Providers.Contains(provider, StringComparer.Ordinal))
                {
                    throw Invalid(label + ".provider must be one of " + string.Join(", ", AgentConfig.Providers.Select(name => "\"" + name + "\"")));
                }

                var baseUrl = OptionalString(value, "baseUrl", label + ".baseUrl");
                var apiKeyEnv = OptionalString(value, "apiKeyEnv", label + ".apiKeyEnv");
                if (AgentConfig.IsSubscription(provider) && (baseUrl is not null || apiKeyEnv is not null))
                {
                    throw Invalid(label + ": the " + provider + " subscription reads its stored login, so baseUrl and apiKeyEnv do not apply");
                }

                if (provider == "openai-compatible" && baseUrl is null)
                {
                    throw Invalid(label + ".baseUrl is required for provider \"openai-compatible\"");
                }

                agents[property.Name] = new AgentConfig
                {
                    Model = OptionalString(value, "model", label + ".model"),
                    Judge = OptionalString(value, "judge", label + ".judge"),
                    Provider = provider,
                    System = AnyString(value, "system", label + ".system"),
                    Context = context,
                    BaseUrl = baseUrl,
                    ApiKeyEnv = apiKeyEnv,
                    MaxSteps = Property(value, "maxSteps") is { } steps ? Integer(steps, label + ".maxSteps", 1, ResolvedAgent.MaxBudget) : null,
                    MaxModelCalls = Property(value, "maxModelCalls") is { } calls ? Integer(calls, label + ".maxModelCalls", 1, ResolvedAgent.MaxBudget) : null,
                    JudgmentTimeout = Property(value, "judgmentTimeout") is { } judgment
                        ? TimeSpan.FromMilliseconds(Integer(judgment, label + ".judgmentTimeout", 1, int.MaxValue))
                        : E2EDefaults.JudgmentTimeout,
                    MaxObservationBytes = Property(value, "maxObservationBytes") is { } bytes
                        ? Integer(bytes, label + ".maxObservationBytes", ResolvedAgent.MinObservationBytes, ResolvedAgent.MaxObservationBytesLimit)
                        : E2EDefaults.MaxObservationBytes,
                    MaxInputTokens = Property(value, "maxInputTokens") is { } tokens
                        ? Integer(tokens, label + ".maxInputTokens", 1, ResolvedAgent.MaxInputTokensLimit)
                        : E2EDefaults.MaxInputTokens,
                    ProviderOptions = ReadProviderOptions(Property(value, "providerOptions"), label),
                };
            }
        }

        agents.TryAdd("default", new AgentConfig());
        // `--agent a --agent a` is one agent, not two results per test.
        IReadOnlyList<string> agentNames = selected is null || selected.Count == 0 ? ["default"] : [.. selected.Distinct(StringComparer.Ordinal)];
        foreach (var name in agentNames)
        {
            if (!agents.ContainsKey(name))
            {
                throw Invalid("unknown agent \"" + name + "\"; configured: " + string.Join(", ", agents.Keys) + Suggestions.DidYouMean(name, [.. agents.Keys]));
            }
        }

        return (new ReadOnlyDictionary<string, AgentConfig>(agents), agentNames);
    }

    private static ReadOnlyDictionary<string, JsonElement>? ReadProviderOptions(JsonElement? raw, string label)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw.Value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(label + ".providerOptions must be an object of provider options by provider");
        }

        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var provider in raw.Value.EnumerateObject())
        {
            if (provider.Value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(label + ".providerOptions." + provider.Name + " must be an object of provider options");
            }

            options[provider.Name] = provider.Value.Clone();
        }

        return new ReadOnlyDictionary<string, JsonElement>(options);
    }

    // The directories under the output a run clears or owns, which nothing else may live in (upstream's OUTPUT_OWNED_DIRS).
    private static readonly string[] OutputOwnedDirs = ["results", "sessions", "videos"];

    /// <summary>
    /// The output directory for a run, <paramref name="flag"/> (<c>--output</c>) over the config's <c>output</c>. Upstream's <c>resolveOutput</c>
    /// run on this config's cache.
    /// </summary>
    internal string ResolveOutput(string? flag) => ResolveOutput(_rawOutput, flag, ProjectRoot, Cache);

    // Upstream's resolveOutput: the results directory, `--output` over the config's `output`, from the project root. A run clears
    // `<output>/results` and writes over its reports, so the directory must be one it can own: a directory (or a path that does not exist
    // yet) inside the project root and not the root itself, not the cache directory or inside it, and not wrapping the cache in a directory
    // the run clears or owns. Every path is compared through the filesystem, with the symlinks of its nearest existing ancestor resolved and a
    // dangling one followed to where it points. Not ported: the check against the test globs (the port has no `tests` key).
    private static string ResolveOutput(JsonElement? configured, string? flag, string projectRoot, CacheConfig cache)
    {
        var where = flag is null ? "output" : "--output";
        string? value = flag;
        if (flag is null && configured is { } raw)
        {
            value = raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
            if (value is null)
            {
                throw Invalid(where + " must be a non-empty path relative to the project root, got " + raw.GetRawText());
            }
        }

        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(where + " must be a non-empty path relative to the project root, got " + JsonSerializer.Serialize(value));
        }

        var output = Path.GetFullPath(value ?? ".e2e", projectRoot);
        var named = value is null ? where + " \".e2e\" (the default)" : where + " " + JsonSerializer.Serialize(value);
        ConfigurationException Refuse(string reason) => Invalid(named + " " + reason);
        var realRoot = Paths.RealPathOfExisting(projectRoot);
        var real = Paths.RealPathOfExisting(output);
        var realCache = Paths.RealPathOfExisting(cache.Directory);
        if (real == realRoot)
        {
            throw Refuse("is the project root; the run clears <output>/results, so name a directory of its own, such as '.e2e'");
        }

        if (!IsWithin(real, realRoot))
        {
            throw Refuse("is outside the project root " + projectRoot + "; name a directory inside it");
        }

        var existing = NearestExisting(output);
        if (existing is not null && !Directory.Exists(existing))
        {
            throw Refuse(
                existing == output
                    ? "is a file; name a directory, which the run creates when it is missing"
                    : "is under the file " + Path.GetRelativePath(realRoot, Paths.RealPathOfExisting(existing)) + "; name a directory, which the run creates when it is missing");
        }

        var cacheName = Path.GetRelativePath(realRoot, realCache);
        if (IsWithin(real, realCache))
        {
            throw Refuse("is the cache directory " + cacheName + " or inside it; keep results and the replay cache apart");
        }

        foreach (var owned in OutputOwnedDirs)
        {
            if (IsWithin(realCache, Paths.RealPathOfExisting(Path.Combine(output, owned))))
            {
                throw Refuse("would hold " + cache.DirectorySource + " " + cacheName + " under " + owned + "/, which the run owns; move " + cache.DirectorySource + " or the output");
            }
        }

        return output;
    }

    // Whether `inner` is `outer` or a path below it.
    private static bool IsWithin(string inner, string outer)
    {
        var relative = Path.GetRelativePath(outer, inner);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    // The nearest of `target` and its ancestors that exists, or null when none does.
    private static string? NearestExisting(string target)
    {
        for (var current = target; ; current = Path.GetDirectoryName(current)!)
        {
            // A dangling symlink does not exist, as upstream's existsSync follows links.
            if (Directory.Exists(current) || (File.Exists(current) && new FileInfo(current).ResolveLinkTarget(true)?.Exists != false))
            {
                return current;
            }

            if (Path.GetDirectoryName(current) is null)
            {
                return null;
            }
        }
    }

    // Upstream's removedRecordingReplacement: what replaces the removed recording facts of `artifacts`, read together. The kinds list (bare or as
    // `kinds`, null when the config had none) and the `trace` block. A list holding `trace` beside `trace: { record: 'retries' }` meant retries
    // only, and a list without `trace` recorded none whatever the block said. Then the video option for a `video` kind, and the failure
    // screenshot a list without `screenshot` used to turn off.
    private static string RemovedRecordingReplacement(IReadOnlyList<string?>? kinds, JsonElement? trace)
    {
        var traced = kinds is null || kinds.Contains("trace");
        var mode = RecordingMode.On;
        if (traced && trace is { } value)
        {
            var block = value.ValueKind == JsonValueKind.Object;
            var record = block ? (value.TryGetProperty("record", out var inner) && inner.ValueKind == JsonValueKind.String ? inner.GetString() : null) : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (RecordingModes.TryLegacyTraceSpelling(record, block, out _, out var meant))
            {
                mode = meant;
            }
        }

        var parts = new List<string> { "write trace: '" + (traced ? RecordingModes.Name(mode) : "off") + "' at the config root instead (the modes are " + RecordingModes.List + ")" };
        if (kinds?.Contains("video") == true)
        {
            parts.Add(VideoMoved);
        }

        if (kinds is not null && !kinds.Contains("screenshot"))
        {
            parts.Add("failure screenshots are always captured now");
        }

        return string.Join("; ", parts);
    }

    // Upstream's resolveArtifactStore, which reads the `artifacts` key, `{ store }`. The port's key holds no store (it is code: see
    // `WithArtifactStore`), so what is left is the refusal of what is recorded: the old kinds list (bare or as `kinds`) and the `trace` block
    // are refused with the `trace` mode they meant, and `video` with the option that replaced it.
    private static void ReadArtifacts(JsonElement? raw)
    {
        if (raw is not { } value)
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            throw Invalid("artifacts no longer lists kinds: " + RemovedRecordingReplacement(KindsOf(value), null));
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("artifacts must be an object");
        }

        var removed = new[] { "kinds", "trace" }.Where(key => value.TryGetProperty(key, out _)).ToList();
        if (removed.Count > 0)
        {
            var kinds = value.TryGetProperty("kinds", out var list) ? KindsOf(list) : null;
            var keys = string.Join(" and ", removed.Select(key => "artifacts." + key));
            throw Invalid(keys + (removed.Count == 1 ? " was" : " were") + " removed: " + RemovedRecordingReplacement(kinds, Property(value, "trace")));
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Name == "video")
            {
                throw Invalid("artifacts.video was removed: " + VideoMoved);
            }
        }

        CheckKeys(value, "artifacts config", ArtifactsKeys, UnsupportedArtifactsKeys, null);
    }

    // The kinds a removed `artifacts` list names; a `kinds` value that is no list names none (upstream's `'kinds' in block ? []`).
    private static List<string?> KindsOf(JsonElement list) =>
        list.ValueKind == JsonValueKind.Array ? [.. list.EnumerateArray().Select(kind => kind.ValueKind == JsonValueKind.String ? kind.GetString() : null)] : [];

    private static CacheConfig ReadCache(JsonElement? raw, string projectRoot, bool ci, Func<string, string?> environment, IStepCache? store, CliOverrides cli)
    {
        // Upstream's resolveCacheDir: --cache-dir (the port's E2E_CACHE_DIR_OVERRIDE, or cli.CacheDir from code), else a non-blank E2E_CACHE_DIR, replaces cache.dir
        // and the default; the mode stays as configured. A store has no directory: the flag is refused beside it and the variable is ignored.
        var flag = cli.CacheDir ?? (environment("E2E_CACHE_DIR_OVERRIDE") is { Length: > 0 } overridden ? overridden : null);
        string? shared = null;
        var sharedSource = "E2E_CACHE_DIR";
        if (flag is not null)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                throw Invalid("--cache-dir must be a non-empty path");
            }

            if (store is not null)
            {
                throw Invalid("--cache-dir names a file store directory, but cache.store replaces the file store; drop one");
            }

            shared = flag;
            sharedSource = "--cache-dir";
        }
        else if (store is null && environment("E2E_CACHE_DIR") is { } variable && !string.IsNullOrWhiteSpace(variable))
        {
            shared = variable;
        }

        var sharedDirectory = shared is null ? null : Path.GetFullPath(Path.Combine(projectRoot, ExpandHome(shared)));
        var defaultDirectory = sharedDirectory ?? Path.Combine(projectRoot, ".e2e", "cache");

        // E2E_NO_CACHE is the port's --no-cache (upstream's `cli.cache = 'off'`, `noCache` of the run options): it turns the cache off whatever the config says, even a store.
        var noCache = IsFlagOn(environment("E2E_NO_CACHE"));
        if (raw is null)
        {
            return new CacheConfig { Mode = noCache ? CacheMode.Off : ci && store is null ? CacheMode.ReadOnly : CacheMode.ReadWrite, Directory = defaultDirectory, Strict = cli.CacheStrict, StrictSource = cli.CacheStrict ? new CacheStrictSource(false, true) : null, Store = store, DirectorySource = sharedDirectory is null ? "cache.dir" : sharedSource };
        }

        if (raw.Value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("cache must be an object");
        }

        var value = raw.Value;
        CheckKeys(value, "cache config", CacheKeys, UnsupportedCacheKeys, null);
        CacheMode mode;
        if (Property(value, "mode") is { } rawMode)
        {
            mode = (rawMode.ValueKind == JsonValueKind.String ? rawMode.GetString() : null) switch
            {
                "off" => CacheMode.Off,
                "read-only" => CacheMode.ReadOnly,
                "read-write" => CacheMode.ReadWrite,
                _ => throw Invalid("cache.mode must be \"off\", \"read-only\", or \"read-write\""),
            };
        }
        else
        {
            // An unset mode is read-write locally. CI treats recordings as untrusted input and only reads them.
            mode = ci && store is null ? CacheMode.ReadOnly : CacheMode.ReadWrite;
        }

        var directory = defaultDirectory;
        if (Property(value, "dir") is { } rawDir)
        {
            var dir = rawDir.ValueKind == JsonValueKind.String ? rawDir.GetString() : null;
            if (string.IsNullOrWhiteSpace(dir))
            {
                throw Invalid("cache.dir must be a non-empty path");
            }

            directory = sharedDirectory ?? Path.GetFullPath(Path.Combine(projectRoot, dir));
        }

        var strict = false;
        if (Property(value, "strict") is { } rawStrict)
        {
            strict = rawStrict.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw Invalid("cache.strict must be a boolean"),
            };
        }

        return new CacheConfig { Mode = noCache ? CacheMode.Off : mode, Directory = directory, Strict = strict || cli.CacheStrict, StrictSource = strict || cli.CacheStrict ? new CacheStrictSource(strict, cli.CacheStrict) : null, Store = store, DirectorySource = sharedDirectory is null ? "cache.dir" : sharedSource };
    }

    /// <summary>The value with a leading <c>~</c> or <c>~/</c> read as the home directory, which no shell expands in an env file.</summary>
    private static string ExpandHome(string value)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (value == "~")
        {
            return home;
        }

        return value.StartsWith("~/", StringComparison.Ordinal) || value.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? Path.Join(home, value[2..])
            : value;
    }

    /// <summary>
    /// The <c>credentials</c> entries, each with its password as the secret <c>&lt;name&gt;.password</c> (the credentials half of upstream's
    /// <c>resolveSecrets</c>). <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> and <c>E2E_USER_&lt;NAME&gt;_PASSWORD</c> win over the config. A
    /// message never holds a username or a password.
    /// </summary>
    private static Dictionary<string, ResolvedCredential> ReadCredentials(JsonElement? raw, Func<string, string?> environment, IReadOnlyDictionary<string, CredentialProvider> providers)
    {
        var credentials = new Dictionary<string, ResolvedCredential>(StringComparer.Ordinal);
        if (raw is not null && raw.Value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("credentials must be an object of { username, password } by name");
        }

        var (entries, fileNames) = ObjectEntries(raw);
        List<string> names = [.. fileNames, .. providers.Keys.Where(name => !entries.ContainsKey(name))];
        CheckEnvNameCollisions("E2E_USER", "credentials", names);
        foreach (var name in names)
        {
            var prefix = ConfigSecrets.EnvName("E2E_USER", name);
            var provided = providers.GetValueOrDefault(name);
            string? username;
            string? password;
            if (provided is null)
            {
                var entry = entries[name];
                CheckCredentialShape(name, entry);
                username = environment(prefix + "_USERNAME") ?? Property(entry, "username")?.GetString();
                // An env override always wins: the operator rotating a credential must not need to know how it was configured.
                password = environment(prefix + "_PASSWORD") ?? (Property(entry, "password") is { ValueKind: JsonValueKind.String } text ? text.GetString() : null);
            }
            else
            {
                username = environment(prefix + "_USERNAME") ?? provided.Username;
                password = environment(prefix + "_PASSWORD");
            }

            if (username is null)
            {
                throw Invalid("credential \"" + name + "\" has no username; set username or " + prefix + "_USERNAME");
            }

            var handle = ConfigSecrets.CredentialSecretName(name);
            if (provided is not null && password is null)
            {
                credentials[name] = new ResolvedCredential(name, username, Secret.Create(handle, provided.Password, "password"));
                continue;
            }

            if (!ConfigSecrets.IsSecretValue(password))
            {
                throw Invalid("credential \"" + name + "\" password " + ConfigSecrets.SecretValueProblem(password));
            }

            credentials[name] = new ResolvedCredential(name, username, Secret.Create(handle, password!, "password"));
        }

        return credentials;
    }

    /// <summary>
    /// A credential entry is <c>{ username, password }</c>, with <c>username</c> a string when set (<c>E2E_USER_&lt;NAME&gt;_USERNAME</c> may
    /// supply it). Upstream's <c>checkCredentialShape</c>; a JSON config has no provider functions, so its messages leave that out.
    /// </summary>
    private static void CheckCredentialShape(string name, JsonElement credential)
    {
        if (credential.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("credential \"" + name + "\" must be an object of { username, password }, got " + KindOf(credential));
        }

        foreach (var property in credential.EnumerateObject())
        {
            if (!CredentialKeys.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Invalid(
                    "unknown key \"" + property.Name + "\" in credential \"" + name + "\"; a credential has username and password" + Suggestions.DidYouMean(property.Name, CredentialKeys));
            }
        }

        if (Property(credential, "username") is { ValueKind: not JsonValueKind.String } username)
        {
            throw Invalid("credential \"" + name + "\" username must be a string, got " + KindOf(username) + "; only password may be a provider function");
        }
    }

    /// <summary>
    /// The kind of a JSON value, never its content: a credential entry written as a bare string may be the password. Upstream's
    /// <c>describeValue</c> quotes the value here; a message that holds a secret is worse than one that names the kind.
    /// </summary>
    private static string KindOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };

    /// <summary>
    /// Two entries of one namespace whose names map to the same override variable (<c>api-key</c> and <c>api_key</c> both read
    /// <c>E2E_SECRET_API_KEY</c>) would both take one rotated value, so the config load refuses them, whether or not the variable is set.
    /// The message names the entries and the variable, never a value. Upstream's <c>checkEnvNameCollisions</c>.
    /// </summary>
    private static void CheckEnvNameCollisions(string prefix, string namespaceName, IReadOnlyList<string> names)
    {
        var shared = names.GroupBy(name => ConfigSecrets.EnvName(prefix, name), StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (shared is null)
        {
            return;
        }

        var quoted = shared.Select(name => "\"" + name + "\"").ToArray();
        var variables = prefix == "E2E_USER"
            ? "the override variables " + shared.Key + "_USERNAME and " + shared.Key + "_PASSWORD"
            : "the override variable " + shared.Key;
        throw Invalid(
            namespaceName + " " + string.Join(", ", quoted[..^1]) + " and " + quoted[^1] + " share " + variables
            + ", so one value set there would replace all of them; rename all but one");
    }

    /// <summary>The properties of a JSON object by name and in file order. A name written twice stays in the list, so the variable collision check refuses it.</summary>
    private static (Dictionary<string, JsonElement> Entries, List<string> Names) ObjectEntries(JsonElement? raw)
    {
        Dictionary<string, JsonElement> entries = new(StringComparer.Ordinal);
        List<string> names = [];
        if (raw is { } value)
        {
            foreach (var property in value.EnumerateObject())
            {
                entries[property.Name] = property.Value;
                names.Add(property.Name);
            }
        }

        return (entries, names);
    }

    private static Secrets ReadSecrets(JsonElement? raw, Func<string, string?> environment, IReadOnlyDictionary<string, ResolvedCredential> credentials, IReadOnlyDictionary<string, SecretProvider> providers)
    {
        if (raw is not null && raw.Value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("secrets must be an object of secret values by name");
        }

        var (entries, fileNames) = ObjectEntries(raw);
        List<string> names = [.. fileNames, .. providers.Keys.Where(name => !entries.ContainsKey(name))];
        if (names.Count == 0)
        {
            return credentials.Count == 0 ? Secrets.Empty : new Secrets(new Dictionary<string, Secret>(StringComparer.Ordinal), credentials);
        }

        var values = new Dictionary<string, Secret>(StringComparer.Ordinal);
        if (names.Any(name => name.Trim().Length == 0))
        {
            throw Invalid("secret names must be non-empty");
        }

        CheckEnvNameCollisions("E2E_SECRET", "secrets", names);
        foreach (var name in names)
        {
            if (credentials.Values.FirstOrDefault(credential => credential.Password.Name == name) is { } owner)
            {
                throw Invalid("secret \"" + name + "\" has the name of credential \"" + owner.Name + "\"'s password handle; rename the secret");
            }

            var variable = Secrets.EnvironmentVariable(name);
            var provided = providers.GetValueOrDefault(name);
            string? entry = null;
            if (provided is null)
            {
                entry = entries[name].ValueKind switch
                {
                    JsonValueKind.String => entries[name].GetString(),
                    JsonValueKind.Null => null,
                    _ => throw Invalid("secret \"" + name + "\" must be a non-empty string, or null to read only " + variable),
                };
            }

            // An env override always wins over the file entry, a provider included.
            var value = environment(variable) ?? entry;
            if (value is null && provided is not null)
            {
                values[name] = Secret.Create(name, provided, "generic-secret");
                continue;
            }

            if (value is null)
            {
                throw Invalid("secret \"" + name + "\" has no value; set " + variable);
            }

            if (!ConfigSecrets.IsSecretValue(value))
            {
                throw Invalid("secret \"" + name + "\" " + ConfigSecrets.SecretValueProblem(value));
            }

            values[name] = Secret.Create(name, value, "generic-secret");
        }

        return new Secrets(values, credentials);
    }

    private static void CheckKeys(JsonElement value, string label, string[] allowed, string[] unsupported, Dictionary<string, string>? moved, Dictionary<string, string>? removed = null)
    {
        foreach (var property in value.EnumerateObject())
        {
            var key = property.Name;
            if (removed is not null && removed.TryGetValue(key, out var replacement))
            {
                throw Invalid((label == "config" ? key : label + "." + key) + " was removed: " + replacement);
            }

            if (allowed.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            var where = label == "config" ? "config key" : label + " key";
            if (unsupported.Contains(key, StringComparer.Ordinal))
            {
                throw Invalid(where + " \"" + key + "\" is not supported by the .NET port" + (key switch
                {
                    "executor" => ": an executor is code, so set AgentOptions.Executor",
                    "store" when label == "cache config" => ": a store is code, so call E2EConfig.WithCacheStore",
                    "store" => ": a store is code, so call E2EConfig.WithArtifactStore",
                    _ => "",
                }));
            }

            string hint;
            if (moved is not null && moved.TryGetValue(key, out var movedHint))
            {
                hint = "; " + movedHint;
            }
            else
            {
                hint = Suggestions.DidYouMean(key, allowed);
            }

            throw Invalid("unknown " + where + " \"" + key + "\"" + hint);
        }
    }

    private static JsonElement? Property(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property) ? property : null;
    }

    private static bool ReadFailOnSkippedFailure(JsonElement? value)
    {
        return value is not { } raw
            ? false
            : raw.ValueKind is JsonValueKind.True or JsonValueKind.False ? raw.GetBoolean() : throw Invalid("failOnSkippedFailure must be a boolean");
    }

    private static string? OptionalString(JsonElement value, string name, string label)
    {
        if (Property(value, name) is not { } property)
        {
            return null;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw Invalid(label + " must be a non-empty string");
        }

        return text;
    }

    // Checks app.environment: one of test, staging, production.
    private static string? ReadEnvironment(JsonElement? raw, string label)
    {
        if (raw is not { } value)
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text is not null && Environments.Contains(text, StringComparer.Ordinal)
            ? text
            : throw Invalid(label + " must be one of " + string.Join(", ", Environments) + ", got " + value.GetRawText());
    }

    // Checks app.launchArguments: an array of strings.
    private static ReadOnlyCollection<string>? ReadLaunchArguments(JsonElement? raw, string label)
    {
        if (raw is not { } value)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(argument => argument.ValueKind != JsonValueKind.String))
        {
            throw Invalid(label + " must be an array of strings");
        }

        return value.EnumerateArray().Select(argument => argument.GetString()!).ToList().AsReadOnly();
    }

    // Checks app.permissions: an object of permission name to grant, deny, or reset.
    private static ReadOnlyDictionary<string, AppPermissionState>? ReadPermissions(JsonElement? raw, string label)
    {
        if (raw is not { } value)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(label + " must be an object of permission name to grant, deny, or reset");
        }

        var permissions = new Dictionary<string, AppPermissionState>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            var state = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            permissions[property.Name] = state switch
            {
                "grant" => AppPermissionState.Grant,
                "deny" => AppPermissionState.Deny,
                "reset" => AppPermissionState.Reset,
                _ => throw Invalid(label + "." + property.Name + " must be grant, deny, or reset, got " + property.Value.GetRawText()),
            };
        }

        return new ReadOnlyDictionary<string, AppPermissionState>(permissions);
    }

    // A string that may be empty, as upstream system and context.
    private static string? AnyString(JsonElement value, string name, string label)
    {
        if (Property(value, name) is not { } property)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw Invalid(label + " must be a string");
        }

        return property.GetString();
    }

    private static TimeSpan Milliseconds(JsonElement root, string name, TimeSpan fallback)
    {
        return Property(root, name) is { } raw ? TimeSpan.FromMilliseconds(Integer(raw, name, 1, int.MaxValue)) : fallback;
    }

    private static int Integer(JsonElement value, string label, int min, int max)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < min || number > max)
        {
            throw Invalid(max == int.MaxValue
                ? label + " must be a positive integer"
                : label + " must be an integer from " + min.ToString(CultureInfo.InvariantCulture) + " through " + max.ToString(CultureInfo.InvariantCulture));
        }

        return number;
    }

    internal static bool ValidName(string name)
    {
        return name.Length > 0
            && name.Any(ch => ch != '.')
            && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.');
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", message);
}

/// <summary>One entry of <c>targets</c>. The port drives the <c>web</c>, <c>ios</c>, and <c>android</c> platforms.</summary>
public sealed class TargetConfig
{
    public required string Name { get; init; }

    public required string Platform { get; init; }

    public required AppConfig App { get; init; }

    /// <summary>This target with another app: the one resolved on the port its command was started on.</summary>
    internal TargetConfig WithApp(AppConfig app) => new() { Name = Name, Platform = Platform, App = app, Trace = Trace, Video = Video, Engine = Engine };

    /// <summary>
    /// The engine this target runs, built in code (upstream's <c>engine</c> of a target, <c>defineEngine(...)</c>); null for a JSON target, whose engine
    /// the fixture's <c>CreateEngine</c> chooses. Internal until a public engine-in-config API exists (#1339): only <see cref="ResolvedConfig.Resolve"/>
    /// reads it, to check the secrets its options hold.
    /// </summary>
    internal IEngine? Engine { get; init; }

    /// <summary>Which attempts write <c>trace.md</c>: <c>E2E_TRACE</c>, else this target's <c>trace</c>, else the config's, else <c>retain-on-failure</c>.</summary>
    public ResolvedRecording Trace { get; init; } = new(RecordingMode.RetainOnFailure, RecordingSource.Default);

    /// <summary>Which attempts record a video: <c>E2E_VIDEO</c>, else this target's <c>video</c>, else the config's, else <c>off</c>.</summary>
    public ResolvedRecording Video { get; init; } = new(RecordingMode.Off, RecordingSource.Default);
}

/// <summary>One kind's modes before any target speaks: the environment variable, the config root, and the default under both.</summary>
internal sealed record RunRecording(RecordingMode? Flag, RecordingMode? Config, RecordingMode Fallback)
{
    /// <summary>A target's effective mode and where it came from: the variable, else the target's own, else the config root's, else the default.</summary>
    public ResolvedRecording For(RecordingMode? own)
    {
        if (Flag is { } flag)
        {
            return new ResolvedRecording(flag, RecordingSource.Run);
        }

        if (own is { } target)
        {
            return new ResolvedRecording(target, RecordingSource.Target);
        }

        return Config is { } config ? new ResolvedRecording(config, RecordingSource.Run) : new ResolvedRecording(Fallback, RecordingSource.Default);
    }
}

public sealed class AppConfig
{
    /// <summary>
    /// Base URL for <c>App.OpenAsync</c>, normalized: a missing scheme becomes <c>https://</c>, or <c>http://</c> for a loopback host.
    /// Relative URLs resolve against it.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>The bundle id (iOS) or package (Android) of an installed app a device target launches.</summary>
    public string? BundleId { get; init; }

    /// <summary>The build a device target installs, relative to the folder of <c>e2e.config.json</c>.</summary>
    public string? AppPath { get; init; }

    /// <summary>
    /// The command that starts the app (upstream's <c>app.command</c>), started before the first test and stopped when the process ends.
    /// <c>{port}</c> in <see cref="CommandConfig.Args"/> and <see cref="CommandConfig.Env"/> is the port the app is served on. A
    /// <see cref="Url"/> with port 0 asks for a free one, and needs this.
    /// </summary>
    public CommandConfig? Command { get; init; }

    /// <summary>The URL that answers once <see cref="Command"/> has started the app (upstream's <c>app.readyUrl</c>). Unset, <see cref="Url"/>.</summary>
    public string? ReadyUrl { get; init; }

    /// <summary>
    /// The stable identity that keys the replay cache and saved sessions (upstream's <c>app.identity</c>). Unset, the URL's origin and
    /// path as declared, else <see cref="BundleId"/>, else <see cref="AppPath"/>. An ephemeral per-deploy origin (a PR preview) can
    /// declare the identity of the app it is a deployment of, and so share its entries.
    /// </summary>
    public string? Identity { get; init; }

    /// <summary>
    /// What the app is: <c>test</c>, <c>staging</c>, or <c>production</c> (upstream's <c>app.environment</c>). Unset, <c>test</c> for a
    /// loopback, <c>.localhost</c>, or <c>.test</c> host, and <c>production</c> for any other host. The environment always joins the
    /// cache key, so an identity never bleeds entries across environments.
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>Arguments every fresh launch of the app takes: process arguments on iOS, <c>am start</c> arguments on Android, the command line on Windows.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }

    /// <summary>
    /// Permissions the app holds, by permission name (upstream's <c>app.permissions</c>). A device target sets each before every fresh
    /// launch and takes the names of <c>DevicePermission</c>. A web target takes <see cref="AppPermissionState.Grant"/> only, for the names its
    /// browser grants (Playwright's), and every page of the attempt holds the grant; headless Chromium refuses what is left out.
    /// </summary>
    public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }

    /// <summary>This app with another URL: the one on the port its command was started on.</summary>
    internal AppConfig WithUrl(string? url) => new()
    {
        Url = url,
        BundleId = BundleId,
        AppPath = AppPath,
        Identity = Identity,
        Environment = Environment,
        LaunchArguments = LaunchArguments,
        Permissions = Permissions,
        Command = Command,
        ReadyUrl = ReadyUrl,
    };
}

/// <summary>What a permission is set to before a launch (upstream's <c>AppPermissionState</c>): held, refused, or not asked for yet, so the OS asks again.</summary>
public enum AppPermissionState
{
    /// <summary>The app holds the permission.</summary>
    Grant,

    /// <summary>The permission is refused.</summary>
    Deny,

    /// <summary>The permission is not asked for yet.</summary>
    Reset,
}

/// <summary>
/// One entry of <c>agents</c>. Upstream <c>model</c> and <c>judge</c> are AI SDK instances; here they are model ids
/// for <see cref="Provider"/>, which share <see cref="BaseUrl"/> and <see cref="ApiKeyEnv"/>.
/// Each entry starts from the defaults; none inherits another's values.
/// </summary>
public sealed class AgentConfig
{
    public const string DefaultProvider = "openai";

    /// <summary>
    /// The <c>provider</c> values: API keys (<c>openai</c>, <c>openai-responses</c>, <c>azure</c>, <c>anthropic</c>,
    /// <c>google</c>, <c>bedrock</c>, <c>xai</c>, <c>openrouter</c>, <c>gateway</c>, <c>openai-compatible</c>) and
    /// subscription logins (<c>chatgpt</c>, <c>copilot</c>, <c>grok</c>, <c>opencode-console</c>).
    /// </summary>
    public static IReadOnlyList<string> Providers { get; } =
    [
        "openai", "openai-responses", "azure", "anthropic", "google", "bedrock", "xai", "openrouter", "gateway", "openai-compatible",
        "chatgpt", "copilot", "grok", "opencode-console",
    ];

    public string? Model { get; init; }

    /// <summary>The model id that judges <c>assert</c>, <c>waitFor</c>, and <c>extract</c>. Unset, the judge is <see cref="Model"/>.</summary>
    public string? Judge { get; init; }

    /// <summary>Who serves <see cref="Model"/> and <see cref="Judge"/>. One of <see cref="Providers"/>; <c>openai</c> by default.</summary>
    public string Provider { get; init; } = DefaultProvider;

    /// <summary>Text appended to the act rules. Judges never see it.</summary>
    public string? System { get; init; }

    /// <summary>Project context told to every model call, at most 16384 UTF-8 bytes.</summary>
    public string? Context { get; init; }

    /// <summary>The API root. Unset, the provider's own; for <c>azure</c> it is built from <c>AZURE_RESOURCE_NAME</c>.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>The environment variable holding the key. Unset, the one the provider reads, such as <c>ANTHROPIC_API_KEY</c>.</summary>
    public string? ApiKeyEnv { get; init; }

    /// <summary>1 through 100. Null when the entry leaves it out: <see cref="E2EDefaults.MaxSteps"/> applies, and <c>Explore</c> sets its own.</summary>
    public int? MaxSteps { get; init; }

    /// <summary>1 through 100. Null when the entry leaves it out: <see cref="E2EDefaults.MaxModelCalls"/> applies, and <c>Explore</c> sets its own.</summary>
    public int? MaxModelCalls { get; init; }

    public TimeSpan JudgmentTimeout { get; init; } = E2EDefaults.JudgmentTimeout;

    /// <summary>1024 through 16777216. See <see cref="AgentOptions.MaxObservationBytes"/>.</summary>
    public int MaxObservationBytes { get; init; } = E2EDefaults.MaxObservationBytes;

    /// <summary>1 through 1000000. See <see cref="AgentOptions.MaxInputTokens"/>.</summary>
    public int MaxInputTokens { get; init; } = E2EDefaults.MaxInputTokens;

    /// <summary>Provider options by provider. Each client adds the entry under its own key to its request body.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>The model for <see cref="Model"/>, or null when no model is set.</summary>
    public IAgentModel? CreateModel() => Create(Model);

    /// <summary>The model for <see cref="Judge"/>, or null when no judge is set.</summary>
    public IAgentModel? CreateJudge() => Create(Judge);

    /// <summary>The <see cref="AgentOptions"/> this entry describes, with its models created.</summary>
    public AgentOptions CreateOptions()
    {
        return new AgentOptions
        {
            Model = CreateModel(),
            Judge = CreateJudge(),
            System = System,
            Context = Context,
            MaxSteps = MaxSteps,
            MaxModelCalls = MaxModelCalls,
            JudgmentTimeout = JudgmentTimeout,
            MaxObservationBytes = MaxObservationBytes,
            MaxInputTokens = MaxInputTokens,
            ProviderOptions = ProviderOptions,
        };
    }

    internal static bool IsSubscription(string provider) => provider is "chatgpt" or "copilot" or "grok" or "opencode-console";

    private IAgentModel? Create(string? model)
    {
        if (model is null)
        {
            return null;
        }

        return Provider switch
        {
            "openai-responses" => new OpenAiResponsesModel(new OpenAiResponsesModelOptions
            {
                Model = model,
                BaseUrl = BaseUrl ?? "https://api.openai.com/v1",
                ApiKeyEnv = ApiKeyEnv ?? "OPENAI_API_KEY",
            }),
            "azure" => new OpenAiResponsesModel(new OpenAiResponsesModelOptions
            {
                Model = model,
                BaseUrl = BaseUrl ?? AzureBaseUrl(),
                Provider = "azure",
                ApiKeyEnv = ApiKeyEnv ?? "AZURE_API_KEY",
                ApiKeyHeader = "api-key",
                QueryParameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["api-version"] = "v1" },
            }),
            "anthropic" => new AnthropicModel(new AnthropicModelOptions
            {
                Model = model,
                BaseUrl = BaseUrl ?? "https://api.anthropic.com/v1",
                ApiKeyEnv = ApiKeyEnv ?? "ANTHROPIC_API_KEY",
            }),
            "google" => new GoogleModel(new GoogleModelOptions
            {
                Model = model,
                BaseUrl = BaseUrl ?? "https://generativelanguage.googleapis.com/v1beta",
                ApiKeyEnv = ApiKeyEnv ?? "GOOGLE_GENERATIVE_AI_API_KEY",
            }),
            "bedrock" => new BedrockModel(new BedrockModelOptions
            {
                Model = model,
                BaseUrl = BaseUrl,
                ApiKeyEnv = ApiKeyEnv ?? "AWS_BEARER_TOKEN_BEDROCK",
            }),
            "xai" => Compatible(model, "https://api.x.ai/v1", "xai", "XAI_API_KEY", null),
            "openrouter" => Compatible(model, "https://openrouter.ai/api/v1", "openrouter", "OPENROUTER_API_KEY", null),
            "gateway" => Compatible(model, "https://ai-gateway.vercel.sh/v1", "gateway", "AI_GATEWAY_API_KEY", "VERCEL_OIDC_TOKEN"),
            "openai-compatible" => Compatible(model, "http://127.0.0.1:11434/v1", "openai-compatible", "LLM_API_KEY", null),
            "chatgpt" => OAuth.Subscriptions.ChatGpt(model),
            "copilot" => OAuth.Subscriptions.Copilot(model),
            "grok" => OAuth.Subscriptions.Grok(model),
            "opencode-console" => OAuth.Subscriptions.OpenCodeConsole(model),
            _ => Compatible(model, "https://api.openai.com/v1", "openai", "OPENAI_API_KEY", null),
        };
    }

    private OpenAiCompatibleModel Compatible(string model, string baseUrl, string provider, string apiKeyEnv, string? fallbackEnv)
    {
        return new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions
        {
            Model = model,
            BaseUrl = BaseUrl ?? baseUrl,
            Provider = provider,
            ApiKeyEnv = ApiKeyEnv ?? apiKeyEnv,
            ApiKeyFallbackEnv = ApiKeyEnv is null ? fallbackEnv : null,
        });
    }

    private static string AzureBaseUrl()
    {
        var resource = Environment.GetEnvironmentVariable("AZURE_RESOURCE_NAME");
        return string.IsNullOrWhiteSpace(resource)
            ? throw new ConfigurationException("INVALID_CONFIG", "provider \"azure\" needs baseUrl or AZURE_RESOURCE_NAME")
            : "https://" + resource.Trim() + ".openai.azure.com/openai/v1";
    }
}

/// <summary>What turned <c>cache.strict</c> on for the run: the config key, the <c>--strict-cache</c> flag, or both (upstream's <c>CacheStrictSource</c>).</summary>
internal sealed record CacheStrictSource(bool Config, bool Flag);

/// <summary>Flags that replace config keys (upstream's <c>CliOverrides</c>). Only <c>cacheStrict</c>, <c>cacheDir</c>, and <c>agents</c> exist in the port.</summary>
internal sealed record CliOverrides
{
    /// <summary>Upstream's <c>--agent</c> (<c>agents</c>): the configured agents unpinned tests run as, instead of <c>default</c> alone.</summary>
    public IReadOnlyList<string>? Agents { get; init; }

    /// <summary>Upstream's <c>--strict-cache</c>: turns <c>cache.strict</c> on for the run.</summary>
    public bool CacheStrict { get; init; }

    /// <summary>Upstream's <c>--cache-dir</c> (<c>cacheDir</c>): the replay cache directory for the run, over <c>E2E_CACHE_DIR</c> and <c>cache.dir</c>.</summary>
    public string? CacheDir { get; init; }
}

public sealed class CacheConfig
{
    /// <summary>Unset, it is <see cref="CacheMode.ReadWrite"/>, or <see cref="CacheMode.ReadOnly"/> in CI.</summary>
    public CacheMode Mode { get; init; } = CacheMode.ReadWrite;

    /// <summary>Absolute cache directory. <c>cache.dir</c> resolves against the config directory; the default is <c>.e2e/cache</c>. <c>--cache-dir</c> (the environment variable <c>E2E_CACHE_DIR_OVERRIDE</c>), else a non-blank <c>E2E_CACHE_DIR</c>, replaces both.</summary>
    public string Directory { get; init; } = Path.Combine(".e2e", "cache");

    /// <summary>When true, a recording that no longer matches fails with <c>REPLAY_STALE</c> instead of running live.</summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Upstream's <c>ResolvedCacheConfig.strict</c> beyond the flag: which knobs turned <see cref="Strict"/> on, since a re-recording has to
    /// turn each of them off. Null when strict is off.
    /// </summary>
    internal CacheStrictSource? StrictSource { get; init; }

    /// <summary>What named <see cref="Directory"/>, for the errors that point at it (upstream's <c>CacheDirSource</c>): <c>cache.dir</c>, <c>--cache-dir</c>, or <c>E2E_CACHE_DIR</c>.</summary>
    internal string DirectorySource { get; init; } = "cache.dir";

    /// <summary>
    /// The store that replaces the file store at <see cref="Directory"/> (upstream's <c>cache.store</c>), or null. Set it with
    /// <see cref="E2EConfig.WithCacheStore"/>: the config file cannot name one.
    /// </summary>
    public IStepCache? Store { get; init; }
}

/// <summary>How the replay cache is used.</summary>
public enum CacheMode
{
    /// <summary>Never read or write recordings.</summary>
    Off,

    /// <summary>Replay recordings, never write or delete them.</summary>
    ReadOnly,

    /// <summary>Replay recordings, record verified acts, and delete unverified ones.</summary>
    ReadWrite,
}

/// <summary>
/// One artifact handed to <see cref="IArtifactStore.PutAsync"/> (upstream's <c>StoredArtifact</c>): the file's bytes, its digest, and the
/// identity the report files it under. <see cref="Path"/> is the report-relative path the record carries, stable across runs of the same
/// test, so a host may use it as its own key.
/// </summary>
public sealed record StoredArtifact
{
    /// <summary><see cref="ArtifactKind.Screenshot"/>, <see cref="ArtifactKind.Video"/>, <see cref="ArtifactKind.Download"/>, or <see cref="ArtifactKind.Log"/>.</summary>
    public required ArtifactKind Kind { get; init; }

    public required string MediaType { get; init; }

    public required ReadOnlyMemory<byte> Bytes { get; init; }

    public required long Size { get; init; }

    public required string Sha256 { get; init; }

    /// <summary>Report-relative path with <c>/</c> separators.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// How much of the file the runner masked, as the report records it. A <see cref="ArtifactKind.Download"/> is
    /// <see cref="ArtifactRedaction.Incomplete"/> unless the runner ran it as text through the session's secret values; a store that exports
    /// only what the runner vouches for reads this rather than the kind.
    /// </summary>
    public required ArtifactRedaction Redaction { get; init; }

    public required string RunId { get; init; }

    public required string TestId { get; init; }

    public required string AttemptId { get; init; }

    /// <summary>The step that produced it, when one was running.</summary>
    public string? StepId { get; init; }

    /// <summary>When a time-based artifact began recording (a video segment), so a host can align step timestamps with it without reading the report.</summary>
    public DateTimeOffset? StartedAt { get; init; }
}

/// <summary>One provider-hosted recording, handed to <see cref="IArtifactStore.PutLinkAsync"/> once the attempt stopped it (upstream's <c>StoredArtifactLink</c>).</summary>
public sealed record StoredArtifactLink
{
    /// <summary>Always <see cref="ArtifactKind.Video"/>: a hosted recording is the only link there is.</summary>
    public ArtifactKind Kind => ArtifactKind.Video;

    /// <summary>A recording masks nothing, so a link is always <see cref="ArtifactRedaction.Incomplete"/>.</summary>
    public ArtifactRedaction Redaction => ArtifactRedaction.Incomplete;

    /// <summary>The <c>http(s)</c> URL the service serves the recording from.</summary>
    public required string Url { get; init; }

    public required string MediaType { get; init; }

    public required string RunId { get; init; }

    public required string TestId { get; init; }

    public required string AttemptId { get; init; }

    /// <summary>When the recording started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>The step that was running when the recording was registered, if any.</summary>
    public string? StepId { get; init; }
}

/// <summary>
/// Where artifacts go (upstream's <c>ArtifactStore</c>, <c>artifacts.store</c>). The default keeps them under the artifacts directory, and
/// the report records their paths. A host-supplied store (object storage, an API) receives each artifact an attempt registers (a download, a video, an attached screenshot or log) as it is produced, not after the
/// run, and returns its own reference, which the report records as the artifact's <c>ref</c> beside the local path. The cloud seam for
/// evidence, the way <see cref="IStepCache"/> is for the replay cache. A failed <see cref="PutAsync"/> never fails the run: the record
/// simply carries no <c>ref</c>. Like every live value, a store never crosses a process boundary. Set it with
/// <see cref="E2ESessionOptions.ArtifactStore"/>, or override <c>E2EFixture.ArtifactStore</c>.
/// </summary>
public interface IArtifactStore
{
    /// <summary>Stores the artifact and returns the reference to record (upstream's <c>put</c>, which returns <c>{ ref }</c>). An empty reference is not recorded.</summary>
    Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a video a hosted service keeps (a browser or device provider's own recording): a link with no bytes, so <see cref="PutAsync"/>
    /// never sees it. Returns a reference the report records as the artifact's <c>ref</c> beside its <c>url</c>; the default returns none, so
    /// a link is only recorded (upstream's optional <c>putLink</c>). A passed attempt's link under <c>retain-on-failure</c> is never handed
    /// over, since the report leaves it out. A failed call never fails the run, like <see cref="PutAsync"/>.
    /// </summary>
    Task<string?> PutLinkAsync(StoredArtifactLink link, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
