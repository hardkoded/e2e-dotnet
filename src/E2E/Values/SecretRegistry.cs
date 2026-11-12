// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The accounts <see cref="Credentials.User"/> resolves against while a session runs (the credentials half of upstream's
/// <c>SecretRegistry</c> and <c>holdSecretRegistry</c>, <c>secrets.ts</c>). Several sessions may hold one at once: releasing one leaves
/// the newest registry still held, and releasing the last clears it. Upstream's <c>live</c> option is not ported: every session of the
/// run holds the same config's accounts.
/// </summary>
internal static class SecretRegistry
{
    private static readonly Lock Gate = new();
    private static readonly List<IReadOnlyDictionary<string, ResolvedCredential>> Holds = [];

    /// <summary>Installs <paramref name="credentials"/> until the returned release is disposed.</summary>
    public static IDisposable Hold(IReadOnlyDictionary<string, ResolvedCredential> credentials)
    {
        lock (Gate)
        {
            Holds.Add(credentials);
        }

        return new Release(credentials);
    }

    /// <summary>The named account of the newest registry held, or null when none is held or it has no such account.</summary>
    public static ResolvedCredential? Credential(string name)
    {
        lock (Gate)
        {
            return Holds.Count > 0 && Holds[^1].TryGetValue(name, out var credential) ? credential : null;
        }
    }

    private sealed class Release(IReadOnlyDictionary<string, ResolvedCredential> credentials) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            lock (Gate)
            {
                var index = Holds.LastIndexOf(credentials);
                if (index >= 0)
                {
                    Holds.RemoveAt(index);
                }
            }
        }
    }
}
