// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// What one test's session does with saved sessions: the session to restore, and for a setup test the staging that collects what it
/// saves. Upstream's <c>SessionPlan</c> and the setup arm of its <c>AttemptContext</c> (<c>run/execute.ts</c>).
/// </summary>
internal sealed record SessionPlan(SessionStore Store, SessionIdentity Identity, string? Restore, SessionStaging? Staging);

/// <summary>
/// The process's one <see cref="SessionStore"/>, made when the first test needs it (.NET-only: upstream's runner makes it for the run). The
/// key lives in memory only and the directory under <c>.e2e/sessions/&lt;run id&gt;</c> goes when the run ends, with
/// <c>E2ERunFixture</c>, as upstream's <c>cleanup</c> does.
/// </summary>
internal static class SessionRun
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, SessionStore> Stores = new(StringComparer.Ordinal);

    /// <summary>The store of this run for a project root and output, whose output directory (<c>.e2e</c> by default) holds it in <c>sessions/</c>.</summary>
    public static SessionStore Store(string projectRoot, string? output = null)
    {
        var root = Path.GetFullPath(projectRoot);
        var directory = OutputLayout.Of(output ?? Path.Combine(root, ".e2e")).Sessions;
        lock (Gate)
        {
            if (!Stores.TryGetValue(directory, out var store))
            {
                Stores[directory] = store = SessionStore.Create(AttemptReport.RunId, directory);
            }

            return store;
        }
    }

    /// <summary>Deletes the run's session directory.</summary>
    public static void Cleanup()
    {
        lock (Gate)
        {
            foreach (var store in Stores.Values)
            {
                store.Cleanup();
            }

            Stores.Clear();
        }
    }
}
