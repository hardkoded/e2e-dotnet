// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E;

/// <summary>
/// A value the model must not see. Prompts, transcripts, and reports receive
/// <c>&lt;secret:name&gt;</c>. The agent fills the real value itself.
/// </summary>
public sealed class Secret
{
    private Secret(string name, string value, string? purpose)
    {
        Name = name;
        Value = value;
        Purpose = purpose;
    }

    public string Name { get; }

    public string? Purpose { get; }

    internal string Value { get; }

    public static Secret Create(string name, string value, string? purpose = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return new Secret(name, value, purpose);
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
        ArgumentNullException.ThrowIfNull(value);
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
/// Reads <c>E2E_USER_{NAME}_USERNAME</c> and <c>E2E_USER_{NAME}_PASSWORD</c>.
/// The name is uppercased and dashes become underscores.
/// </summary>
public static class Credentials
{
    public static UserCredential User(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var key = name.ToUpperInvariant().Replace('-', '_');
        var username = Environment.GetEnvironmentVariable("E2E_USER_" + key + "_USERNAME");
        var password = Environment.GetEnvironmentVariable("E2E_USER_" + key + "_PASSWORD");
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new TestException(
                "AUTH_CREDENTIAL_UNAVAILABLE",
                "Credential '" + name + "' is not configured. Set E2E_USER_" + key + "_USERNAME and E2E_USER_" + key + "_PASSWORD.");
        }

        return new UserCredential(username, Secret.Create("password", password, name + " password"));
    }
}

/// <summary>
/// The secrets declared in <c>e2e.config.json</c>. Each value comes from
/// <c>E2E_SECRET_{NAME}</c> when it is set, else from the config. The name is
/// uppercased and every character other than A-Z and 0-9 becomes an underscore.
/// </summary>
public sealed class Secrets
{
    /// <summary>The shortest secret value, in code points.</summary>
    public const int MinimumLength = 6;

    private readonly IReadOnlyDictionary<string, Secret> _values;

    internal Secrets(IReadOnlyDictionary<string, Secret> values)
    {
        _values = values;
    }

    public static Secrets Empty { get; } = new(new Dictionary<string, Secret>(StringComparer.Ordinal));

    public IEnumerable<string> Names => _values.Keys;

    /// <summary>The override variable for a secret name, such as <c>E2E_SECRET_STRIPE_KEY</c> for <c>stripe-key</c>.</summary>
    public static string EnvironmentVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder("E2E_SECRET_");
        foreach (var ch in name.ToUpperInvariant())
        {
            builder.Append(ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? ch : '_');
        }

        return builder.ToString();
    }

    /// <summary>The configured secret. A name the config does not declare throws <c>SECRET_UNAVAILABLE</c>.</summary>
    public Secret Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_values.TryGetValue(name, out var secret))
        {
            return secret;
        }

        throw new TestException("SECRET_UNAVAILABLE", "Secret '" + name + "' is not declared in e2e.config.json secrets.");
    }
}
