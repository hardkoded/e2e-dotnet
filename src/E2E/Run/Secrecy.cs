// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>What one live session knows about secrets: the values to redact.</summary>
internal static class Secrecy
{
    /// <summary>
    /// Whether a text-like download of the session is rewritten through its ledger before it is kept: whenever the ledger holds a
    /// value, filled or not. A value reaches the app in ways the runner never sees (a URL or a <c>fill</c> the test spelled it into, the
    /// app's own config), so downloads are redacted like every report and observation, not only after a fill. Upstream's
    /// <c>redactsDownloads</c>; the port has no <c>SessionSecrecy</c>, so it takes the ledger alone.
    /// </summary>
    public static bool RedactsDownloads(Redactor ledger) => !ledger.IsEmpty;

    /// <summary>
    /// The plaintext of one secret an engine option carries. The value joins <paramref name="ledger"/> before it is
    /// returned, so it is redacted from the moment it exists, whoever it is handed to.
    /// </summary>
    public static string ResolveSecretValue(Secret secret, ICollection<(string Name, string Value)> ledger)
    {
        Register(ledger, secret.Name, secret.Value);
        return secret.Value;
    }

    /// <summary>
    /// Registers the forms an engine derived from one resolved secret (the base64 basic-auth credential) under the
    /// secret's name in <paramref name="ledger"/>, so what the app sees in place of the value is redacted wherever the
    /// value is. Registered before the engine hands a form to the app, since the resolve that computes them has not returned yet.
    /// </summary>
    public static void RegisterDerivedSecrets(string name, IEnumerable<string> derived, ICollection<(string Name, string Value)> ledger)
    {
        foreach (var value in derived.Where(value => value.Length > 0))
        {
            Register(ledger, name, value);
        }
    }

    private static void Register(ICollection<(string Name, string Value)> ledger, string name, string value)
    {
        if (!ledger.Contains((name, value)))
        {
            ledger.Add((name, value));
        }
    }
}

/// <summary>What a saved session carries of the secrecy of the attempt that saved it (upstream's <c>SavedSecrecy</c>, <c>run/secrecy.ts</c>).</summary>
/// <param name="Secrets">
/// Name and value pairs the saving session learned beyond the static config values; they travel only inside the encrypted payload.
/// </param>
/// <param name="Tainted">
/// Whether a secret was filled on the saving session, so its viewport, and any state restored from it, may carry one.
/// </param>
internal sealed record SavedSecrecy(IReadOnlyList<(string Name, string Value)> Secrets, bool Tainted);
