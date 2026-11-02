// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E;

/// <summary>The names the config gives secrets and credentials (upstream's <c>config/secrets.ts</c>).</summary>
internal static class ConfigSecrets
{
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

/// <summary>One <c>config.credentials</c> entry, resolved (upstream's <c>ResolvedCredential</c>): an account whose password is the secret <c>&lt;name&gt;.password</c>.</summary>
/// <param name="Name">The entry's name in <c>credentials</c>.</param>
/// <param name="Username">The username from <c>E2E_USER_&lt;NAME&gt;_USERNAME</c> or the config.</param>
/// <param name="Password">The password from <c>E2E_USER_&lt;NAME&gt;_PASSWORD</c> or the config, named <c>&lt;name&gt;.password</c>.</param>
public sealed record ResolvedCredential(string Name, string Username, Secret Password);
