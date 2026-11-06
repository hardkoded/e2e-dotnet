// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E;

/// <summary>
/// Resolves a secret's plaintext at fill time (a vault lookup, a freshly computed TOTP) instead of a value baked at config load (upstream's
/// <c>SecretProvider</c>, <c>config/secrets.ts</c>: <c>() =&gt; string | Promise&lt;string&gt;</c>). Called on every fill after the full
/// authorization policy passes; the resolved value goes straight to the trusted driver, joins redaction, and is never logged, cached, or
/// sent to a model. The token is .NET's cancellation: upstream's provider takes none.
/// </summary>
/// <param name="cancellationToken">Cancels the lookup when the step is cancelled or times out.</param>
public delegate Task<string> SecretProvider(CancellationToken cancellationToken);

/// <summary>The names the config gives secrets and credentials (upstream's <c>config/secrets.ts</c>).</summary>
internal static class ConfigSecrets
{
    /// <summary>A value's length as a reader counts it: in code points, so an emoji is one character, not two (upstream's <c>secretLength</c>).</summary>
    public static int SecretLength(string value) => value.EnumerateRunes().Count();

    /// <summary>Whether a static value is a secret value: a string of at least <see cref="Secret.MinLength"/> code points (upstream's <c>isSecretValue</c>; a provider function needs no check).</summary>
    public static bool IsSecretValue(string? value) => value is not null && SecretLength(value) >= Secret.MinLength;

    /// <summary>
    /// Which rule a value that is not a secret value broke, for the <c>INVALID_CONFIG</c> message that names it (upstream's
    /// <c>secretValueProblem</c>). A static value is a string of at least <see cref="Secret.MinLength"/> code points; a provider
    /// function needs no check. Null stands for a value that is not a string.
    /// </summary>
    public static string SecretValueProblem(string? value) =>
        value is { Length: > 0 }
            ? $"must be at least {Secret.MinLength} characters (code points), not {SecretLength(value)}; a shorter value cannot be redacted without rewriting unrelated text"
            : "must be a non-empty string or a provider function";

    /// <summary>
    /// The credential a <c>Secrets.Get(name)</c> that names no <c>secrets</c> entry most likely meant: the one named <paramref name="name"/>,
    /// or the one whose password <paramref name="name"/> is (upstream's <c>credentialNamed</c>). The config load and a running test both name it in their error.
    /// </summary>
    public static ResolvedCredential? CredentialNamed(string name, IReadOnlyDictionary<string, ResolvedCredential> credentials) =>
        credentials.Values.FirstOrDefault(credential => credential.Name == name || credential.Password.Name == name);

    /// <summary>
    /// The name of a credential's password handle: <c>admin.password</c> (upstream's <c>credentialSecretName</c>). Credentials and secrets
    /// are separate namespaces, but the agent, the replay cache, and the reports know a secret by its name alone.
    /// </summary>
    public static string CredentialSecretName(string credential) => credential + ".password";

    /// <summary><c>E2E_USER_ADMIN</c>, <c>E2E_SECRET_STRIPE_KEY</c>: the name uppercased, everything outside A-Z0-9 as <c>_</c> (upstream's <c>envName</c>).</summary>
    public static string EnvName(string prefix, string name)
    {
        var builder = new StringBuilder(prefix).Append('_');
        foreach (var ch in name.ToUpperInvariant())
        {
            builder.Append(ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? ch : '_');
        }

        return builder.ToString();
    }
}
