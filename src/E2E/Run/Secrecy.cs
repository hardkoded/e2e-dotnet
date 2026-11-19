// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>What one live session knows about secrets: the values to redact.</summary>
internal static class Secrecy
{
    private static readonly Lock SharedGate = new();

    /// <summary>Every value shared now, by name and value, with how many owners share it. Upstream's <c>SharedSecrets.values</c>.</summary>
    private static readonly Dictionary<(string Name, string Value), SharedValue> Shared = [];

    private static int _revision;

    /// <summary>
    /// Counts the times a value appeared or disappeared; one more owner of a value already shared changes nothing. Upstream's
    /// <c>SecretsBeside.revision</c>. The port keeps it in a static, where upstream holds it on <c>globalThis</c> so that two copies of the
    /// package agree: one process loads one copy of this assembly.
    /// </summary>
    public static int BesideRevision
    {
        get
        {
            lock (SharedGate)
            {
                return _revision;
            }
        }
    }

    /// <summary>Every value shared now. Upstream's <c>SecretsBeside.values</c>.</summary>
    public static IReadOnlyList<(string Name, string Value)> Beside()
    {
        lock (SharedGate)
        {
            return [.. Shared.Keys];
        }
    }

    /// <summary>
    /// Shares the static values of <paramref name="secrets"/> until the returned action is called: what a <c>launch()</c> host does while it is
    /// open, so a session beside it redacts them before the host opens a session of its own, and between two. Upstream's <c>shareStaticSecrets</c>.
    /// </summary>
    public static Action ShareStaticSecrets(IEnumerable<Secret> secrets)
    {
        var list = secrets.ToList();
        OpenSession.SeedProcess(list);
        return Share(list.Select(secret => (secret.Name, secret.Value))).Withdraw;
    }

    /// <summary>Shares <paramref name="entries"/> with every redactor in the process, on behalf of one owner. Upstream's <c>share</c>.</summary>
    public static Sharing Share(IEnumerable<(string Name, string Value)> entries)
    {
        var sharing = new Sharing();
        foreach (var (name, value) in entries)
        {
            sharing.Add(name, value);
        }

        return sharing;
    }

    /// <summary>
    /// Whether a text-like download of the session is rewritten through its ledger before it is kept: whenever the ledger holds a
    /// value, filled or not. A value reaches the app in ways the runner never sees (a URL or a <c>fill</c> the test spelled it into, the
    /// app's own config), so downloads are redacted like every report and observation, not only after a fill. Upstream's
    /// <c>redactsDownloads</c>; the port has no <c>SessionSecrecy</c>, so it takes the ledger alone.
    /// </summary>
    public static bool RedactsDownloads(Redactor ledger) => !ledger.IsEmpty;

    /// <summary>
    /// The plaintext of one secret an engine option carries, a provider's value computed fresh. The value joins <paramref name="ledger"/>
    /// before it is returned, so it is redacted from the moment it exists, whoever it is handed to. Upstream's <c>resolveSecretValue</c>.
    /// </summary>
    public static async Task<string> ResolveSecretValueAsync(
        Secret secret,
        ICollection<(string Name, string Value)> ledger,
        CancellationToken cancellationToken
    )
    {
        var resolved = await secret.ResolveAsync(cancellationToken).ConfigureAwait(false);
        Register(ledger, resolved.Name, resolved.Value);
        return resolved.Value;
    }

    /// <summary>
    /// Registers the forms an engine derived from one resolved secret (the base64 basic-auth credential) under the
    /// secret's name in <paramref name="ledger"/>, so what the app sees in place of the value is redacted wherever the
    /// value is. Registered before the engine hands a form to the app, since the resolve that computes them has not returned yet.
    /// </summary>
    public static void RegisterDerivedSecrets(
        string name,
        IEnumerable<string> derived,
        ICollection<(string Name, string Value)> ledger
    )
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

    private sealed class SharedValue
    {
        public int Owners;
    }

    /// <summary>One owner's share of values: more are added as it learns them, and all leave when it withdraws. Upstream's <c>Sharing</c>.</summary>
    internal sealed class Sharing
    {
        private readonly HashSet<(string Name, string Value)> _mine = [];

        public void Add(string name, string value)
        {
            lock (SharedGate)
            {
                if (!_mine.Add((name, value)))
                {
                    return;
                }

                if (Shared.TryGetValue((name, value), out var held))
                {
                    held.Owners++;
                    return;
                }

                Shared[(name, value)] = new SharedValue { Owners = 1 };
                _revision++;
            }
        }

        public void Withdraw()
        {
            lock (SharedGate)
            {
                foreach (var key in _mine)
                {
                    if (!Shared.TryGetValue(key, out var held))
                    {
                        continue;
                    }

                    held.Owners--;
                    if (held.Owners > 0)
                    {
                        continue;
                    }

                    Shared.Remove(key);
                    _revision++;
                }

                _mine.Clear();
            }
        }
    }
}
