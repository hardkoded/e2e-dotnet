// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

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

    /// <summary>
    /// The fewest characters, counted as code points, a secret value may have. Redaction rewrites
    /// every occurrence of the value, so a shorter one would take ordinary text with it.
    /// </summary>
    public const int MinLength = 6;

    internal string Value { get; }

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

        var length = value.EnumerateRunes().Count();
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

        // The replay cache records a unique value as a slot between U+0001 marks.
        if (value.Contains('\u0001', StringComparison.Ordinal))
        {
            throw new TestException("INVALID_ARGUMENT", "A Values.Unique value cannot contain the slot marker U+0001.");
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

        if (Secret.Problem(password) is { } problem)
        {
            throw new TestException("INVALID_CONFIG", "E2E_USER_" + key + "_PASSWORD " + problem + ".");
        }

        return new UserCredential(username, Secret.Create("password", password, name + " password"));
    }
}
