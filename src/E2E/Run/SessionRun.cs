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
    private static SessionStore? _store;

    /// <summary>The store of this run. The first call fixes where its directory is.</summary>
    public static SessionStore Store(string projectRoot)
    {
        lock (Gate)
        {
            return _store ??= SessionStore.Create(AttemptReport.RunId, Path.Combine(Path.GetFullPath(projectRoot), ".e2e", "sessions"));
        }
    }

    /// <summary>Deletes the run's session directory.</summary>
    public static void Cleanup()
    {
        lock (Gate)
        {
            _store?.Cleanup();
            _store = null;
        }
    }
}
