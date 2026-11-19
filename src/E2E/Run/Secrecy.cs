// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using SharedSecrets = System.Tuple<System.Collections.Generic.Dictionary<(string Name, string Value), int[]>, int[]>;

namespace E2E;

/// <summary>What one live session knows about secrets: the values to redact.</summary>
internal static class Secrecy
{
    private const int Owners = 0;
    private const int Permanent = 1;
    private const int Revision = 0;
    private const int Hosts = 1;

    private static readonly RealmSlot<SharedSecrets> SharedSlot = new(Brands.SharedSecretsBrand);

    /// <summary>
    /// What the sessions in one process share, held in a process-wide slot and not in a static: a host can load a second copy of this
    /// assembly (a plugin in its own load context), and a host of one copy must still reach the sessions of the other. Plain framework
    /// data, so two copies, even two versions, agree on its shape. Upstream's <c>SharedSecrets</c>. The tuple is, in order: every value
    /// shared now, by name and value, with its counts (<c>[owners, permanent]</c>), and the counters (<c>[revision, hosts]</c>), where
    /// <c>hosts</c> is the hosts open now (<see cref="ShareStaticSecrets"/>). A session's learned values are visible beside it only while
    /// one is: upstream runs one test at a time in a worker, so the sessions beside a test's are the ones it opened, where a .NET test
    /// framework runs tests side by side in one process and one test must not redact another's values. Its dictionary is the lock.
    /// </summary>
    private static SharedSecrets Shared()
    {
        lock (RealmSlot<SharedSecrets>.Gate)
        {
            var state = SharedSlot.Get();
            if (state is null)
            {
                state = Tuple.Create(new Dictionary<(string Name, string Value), int[]>(), new int[2]);
                SharedSlot.Set(state);
            }

            return state;
        }
    }

    /// <summary>
    /// Counts the times a value appeared or disappeared; one more owner of a value already shared changes nothing. Upstream's
    /// <c>SecretsBeside.revision</c>.
    /// </summary>
    public static int BesideRevision
    {
        get
        {
            var state = Shared();
            lock (state.Item1)
            {
                return state.Item2[Revision];
            }
        }
    }

    /// <summary>Every value shared now. Upstream's <c>SecretsBeside.values</c>.</summary>
    public static IReadOnlyList<(string Name, string Value)> Beside()
    {
        var state = Shared();
        lock (state.Item1)
        {
            return
            [
                .. state
                    .Item1.Where(pair => pair.Value[Permanent] > 0 || state.Item2[Hosts] > 0)
                    .Select(pair => pair.Key),
            ];
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
        var sharing = Share(list.Select(secret => (secret.Name, secret.Value)));
        var state = Shared();
        lock (state.Item1)
        {
            if (state.Item2[Hosts]++ == 0)
            {
                state.Item2[Revision]++;
            }
        }

        var withdrawn = 0;
        return () =>
        {
            if (Interlocked.Exchange(ref withdrawn, 1) != 0)
            {
                return;
            }

            sharing.Withdraw();
            lock (state.Item1)
            {
                if (--state.Item2[Hosts] == 0)
                {
                    state.Item2[Revision]++;
                }
            }
        };
    }

    /// <summary>Shares <paramref name="entries"/> with every redactor in the process, on behalf of one owner. Upstream's <c>share</c>.</summary>
    public static Sharing Share(IEnumerable<(string Name, string Value)> entries, bool permanent = false)
    {
        var sharing = new Sharing(permanent);
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

    /// <summary>One owner's share of values: more are added as it learns them, and all leave when it withdraws. Upstream's <c>Sharing</c>.</summary>
    internal sealed class Sharing(bool permanent = false)
    {
        private readonly HashSet<(string Name, string Value)> _mine = [];
        private readonly SharedSecrets _state = Shared();

        public void Add(string name, string value)
        {
            lock (_state.Item1)
            {
                if (!_mine.Add((name, value)))
                {
                    return;
                }

                if (_state.Item1.TryGetValue((name, value), out var held))
                {
                    held[Owners]++;
                    if (permanent && held[Permanent]++ == 0 && _state.Item2[Hosts] == 0)
                    {
                        _state.Item2[Revision]++;
                    }

                    return;
                }

                _state.Item1[(name, value)] = [1, permanent ? 1 : 0];
                if (permanent || _state.Item2[Hosts] > 0)
                {
                    _state.Item2[Revision]++;
                }
            }
        }

        public void Withdraw()
        {
            lock (_state.Item1)
            {
                foreach (var key in _mine)
                {
                    if (!_state.Item1.TryGetValue(key, out var held))
                    {
                        continue;
                    }

                    held[Owners]--;
                    if (permanent)
                    {
                        held[Permanent]--;
                    }

                    if (held[Owners] > 0)
                    {
                        continue;
                    }

                    _state.Item1.Remove(key);
                    _state.Item2[Revision]++;
                }

                _mine.Clear();
            }
        }
    }
}
