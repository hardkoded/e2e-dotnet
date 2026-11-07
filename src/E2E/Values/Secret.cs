// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>
/// A value the model must not see. Prompts, transcripts, reports, and the names
/// and test ids an act records in its replay cache entry receive
/// <c>&lt;secret:name&gt;</c>. The agent fills the real value itself.
/// </summary>
public sealed class Secret
{
    private readonly SecretProvider? _provider;

    private Secret(string name, string value, string? purpose, SecretProvider? provider = null)
    {
        Name = name;
        Value = value;
        Purpose = purpose;
        _provider = provider;
    }

    public string Name { get; }

    public string? Purpose { get; }

    /// <summary>
    /// The fewest characters, counted as code points, a secret value may have. Redaction rewrites
    /// every occurrence of the value, so a shorter one would take ordinary text with it.
    /// </summary>
    public const int MinLength = 6;

    /// <summary>The plaintext of a static secret. A provider-backed secret has none until <see cref="ResolveAsync"/> computes one, and reads empty.</summary>
    internal string Value { get; }

    /// <summary>The function that computes the plaintext at fill time, or null for a static secret (upstream's <c>ResolvedSecret.value</c> as a <c>SecretProvider</c>).</summary>
    internal SecretProvider? Provider => _provider;

    /// <summary>
    /// The code an unresolvable secret fails with: a credential's password is a credential problem, any other secret a secret problem
    /// (upstream's <c>unavailableCode</c>, <c>secrets.ts</c>).
    /// </summary>
    internal string UnavailableCode => Purpose == "password" ? "AUTH_CREDENTIAL_UNAVAILABLE" : "SECRET_UNAVAILABLE";

    /// <summary>
    /// A secret whose plaintext <paramref name="provider"/> computes at fill time, on every fill. .NET-only: upstream's registry holds
    /// the provider beside the name, and the handle looks it up; the port's handle carries it.
    /// </summary>
    internal static Secret Create(string name, SecretProvider provider, string? purpose = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);
        return new Secret(name, string.Empty, purpose, provider);
    }

    /// <summary>
    /// The secret with its plaintext known: this one when it is static, else a static copy holding what the provider returned now.
    /// The copy is what joins redaction before it is used. A value that is not a string of at least <see cref="MinLength"/> code points
    /// fails with <see cref="UnavailableCode"/>. Upstream's <c>resolveSecretValue</c> (<c>run/secrecy.ts</c>) without its ledger.
    /// </summary>
    internal async Task<Secret> ResolveAsync(CancellationToken cancellationToken)
    {
        if (_provider is null)
        {
            return this;
        }

        var plaintext = _provider(cancellationToken) is { } pending ? await pending.ConfigureAwait(false) : null;
        if (plaintext is null || ConfigSecrets.SecretLength(plaintext) < MinLength)
        {
            throw new ConfigurationException(
                UnavailableCode,
                "secret \"" + Name + "\" provider did not return a string of at least " + MinLength.ToString(CultureInfo.InvariantCulture) + " characters (code points)");
        }

        return new Secret(Name, plaintext, Purpose);
    }

    public static Secret Create(string name, string value, string? purpose = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        if (Problem(value) is { } problem)
        {
            throw new TestException("INVALID_ARGUMENT", "Secret '" + name + "' " + problem + ".");
        }

        return new Secret(name, value, purpose);
    }

    internal static string? Problem(string value)
    {
        if (value.Length == 0)
        {
            return "must be a non-empty string";
        }

        var length = ConfigSecrets.SecretLength(value);
        return length >= MinLength
            ? null
            : "must be at least " + MinLength.ToString(CultureInfo.InvariantCulture) + " characters (code points), not " +
                length.ToString(CultureInfo.InvariantCulture) + "; a shorter value cannot be redacted without rewriting unrelated text";
    }

    public override string ToString() => "<secret:" + Name + ">";
}

/// <summary>
/// A value that changes every run, such as a timestamp or a fresh email.
/// The replay cache treats every <see cref="UniqueValue"/> as the same param
/// and substitutes the current value when it types.
/// </summary>
public sealed class UniqueValue
{
    public UniqueValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TestException("INVALID_ARGUMENT", "Values.Unique takes a non-empty string.");
        }

        // The replay cache records a unique value as a {{param:<pointer>}} placeholder; a value that spells one could not be told from it.
        if (value.Contains(CacheKeys.PlaceholderPrefix, StringComparison.Ordinal))
        {
            throw new TestException("INVALID_ARGUMENT", "A Values.Unique value cannot spell a placeholder (" + CacheKeys.PlaceholderPrefix + ").");
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>Wraps a fresh value so the replay cache can still match the step.</summary>
public static class Values
{
    public static UniqueValue Unique(string value) => new(value);
}

/// <summary>Username plus a <see cref="Secret"/> password loaded from the environment.</summary>
public sealed class UserCredential
{
    public UserCredential(string username, Secret password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        Username = username;
        Password = password;
    }

    public string Username { get; }

    public Secret Password { get; }
}

/// <summary>
/// The accounts of the config's <c>credentials</c> (upstream's <c>credentials</c>, <c>secrets.ts</c>): <c>E2E_USER_{NAME}_USERNAME</c> and
/// <c>E2E_USER_{NAME}_PASSWORD</c> replace the config's values. While a session runs, only a configured name resolves.
/// </summary>
public static class Credentials
{
    /// <summary>
    /// The named account. A name the session's config declares in <c>credentials</c> resolves with its password as the secret
    /// <c>&lt;name&gt;.password</c>. .NET-only: a name the config does not declare, or a call outside a session, reads the
    /// <c>E2E_USER_*</c> variables alone, as the port did before it read the config. The name maps to a variable as in the config
    /// (uppercased, every character outside A-Z and 0-9 as <c>_</c>).
    /// </summary>
    public static UserCredential User(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (SecretRegistry.Credential(name) is { } configured)
        {
            return new UserCredential(configured.Username, configured.Password);
        }

        // The same mapping as the config path (upstream's envName): uppercase, every character outside A-Z0-9 as '_'.
        var prefix = ConfigSecrets.EnvName("E2E_USER", name);
        var username = Environment.GetEnvironmentVariable(prefix + "_USERNAME");
        var password = Environment.GetEnvironmentVariable(prefix + "_PASSWORD");
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new TestException(
                "AUTH_CREDENTIAL_UNAVAILABLE",
                "Credential '" + name + "' is not configured. Set " + prefix + "_USERNAME and " + prefix + "_PASSWORD.");
        }

        if (Secret.Problem(password) is { } problem)
        {
            throw new TestException("INVALID_CONFIG", prefix + "_PASSWORD " + problem + ".");
        }

        return new UserCredential(username, Secret.Create(ConfigSecrets.CredentialSecretName(name), password, "password"));
    }
}

/// <summary>
/// The secrets declared in <c>e2e.config.json</c>. Each value comes from
/// <c>E2E_SECRET_{NAME}</c> when it is set, else from the config. The name is
/// uppercased and every character other than A-Z and 0-9 becomes an underscore.
/// </summary>
public sealed class Secrets
{
    private readonly IReadOnlyDictionary<string, Secret> _values;
    private readonly IReadOnlyDictionary<string, ResolvedCredential> _credentials;

    internal Secrets(IReadOnlyDictionary<string, Secret> values, IReadOnlyDictionary<string, ResolvedCredential>? credentials = null)
    {
        _values = values;
        _credentials = credentials ?? new Dictionary<string, ResolvedCredential>(StringComparer.Ordinal);
    }

    public static Secrets Empty { get; } = new(new Dictionary<string, Secret>(StringComparer.Ordinal));

    public IEnumerable<string> Names => _values.Keys;

    /// <summary>The override variable for a secret name, such as <c>E2E_SECRET_STRIPE_KEY</c> for <c>stripe-key</c>.</summary>
    public static string EnvironmentVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ConfigSecrets.EnvName("E2E_SECRET", name);
    }

    /// <summary>
    /// The configured secret (upstream's <c>secrets.get</c>, <c>secrets.ts</c>). A name the config does not declare throws <c>SECRET_UNAVAILABLE</c>,
    /// naming the <c>E2E_SECRET_&lt;NAME&gt;</c> variable and, when the name is a credential or its password handle, that the password is
    /// <c>Credentials.User(name).Password</c> (upstream's <c>credentialNamed</c> hint). The message holds names only, never a value.
    /// Differences: the port has no registry slot, so the credentials come with the <see cref="Secrets"/> the config built.
    /// </summary>
    public Secret Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_values.TryGetValue(name, out var secret))
        {
            return secret;
        }

        var credential = ConfigSecrets.CredentialNamed(name, _credentials);
        var hint = credential is null ? "" : "; credential \"" + credential.Name + "\"'s password is credentials.user(\"" + credential.Name + "\").password";
        throw new TestException("SECRET_UNAVAILABLE", "secret \"" + name + "\" is not configured; add it to config.secrets or set " + ConfigSecrets.EnvName("E2E_SECRET", name) + hint);
    }
}
