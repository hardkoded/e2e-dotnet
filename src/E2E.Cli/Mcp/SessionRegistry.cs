// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Cli.Mcp;

/// <summary>What the registry reads of a live session.</summary>
internal interface INamedSession
{
    string Id { get; }

    string TargetName { get; }
}

/// <summary>
/// The bookkeeping behind <c>e2e mcp</c>'s sessions: which exist and in what phase, the limit, and the errors a
/// call that names a session gets. A session is one entry from the moment it is admitted until its attempt has
/// closed, so the limit and the config claim cover a session still booting or still tearing down, while a name
/// resolves only to a session that is live. The lifecycle itself, opening an attempt and closing it, belongs to
/// the host; this class only orders it.
/// </summary>
internal sealed class SessionRegistry<TSession>(int limit)
    where TSession : class, INamedSession
{
    /// <summary>How many ended sessions keep the reason they ended, for the error a call naming one gets.</summary>
    private const int EndedKept = 32;

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Why recent sessions ended, oldest first.</summary>
    private readonly List<(string Id, string Reason)> _ended = [];

    /// <summary>Set once the server shuts down: nothing new is admitted, ever.</summary>
    private bool _draining;

    /// <summary>Whether any session is live.</summary>
    public bool HasLive => LiveCount > 0;

    /// <summary>How many sessions are live.</summary>
    public int LiveCount
    {
        get
        {
            lock (_gate)
            {
                return Live().Count;
            }
        }
    }

    /// <summary>
    /// Admits a session and runs <paramref name="open"/> for it under a fresh id. A session whose open fails
    /// leaves once <paramref name="open"/> has settled, after the host tore down what it opened, so its claims
    /// hold until then.
    /// </summary>
    public Task<string> AdmitAsync(Func<string, Task<string>> open)
    {
        string id;
        Entry entry;
        lock (_gate)
        {
            if (_draining)
            {
                return Task.FromException<string>(new ConfigurationException("SESSION_OPEN", "the server is shutting down; no session can open"));
            }

            if (_entries.Count >= limit)
            {
                return Task.FromException<string>(LimitReached());
            }

            id = Guid.CreateVersion7().ToString();
            entry = new Entry();
            _entries[id] = entry;
        }

        var opened = OpenAsync(id, open);
        entry.Opened = opened;
        return opened;
    }

    /// <summary>Claims the config for an opening session before it loads, or refuses a config other than the one open sessions share.</summary>
    public void ClaimConfig(string id, string configPath)
    {
        lock (_gate)
        {
            var holder = _entries.FirstOrDefault(pair => pair.Key != id && pair.Value.ConfigPath is not null && pair.Value.ConfigPath != configPath);
            if (holder.Value is not null)
            {
                throw new ConfigurationException(
                    "CONFIG_IN_USE",
                    "session " + holder.Key + " is open on config " + holder.Value.ConfigPath + "; sessions open at once share one config, because the secrets they redact are shared process-wide; open this one on that config, or close every session on it first");
            }

            Require(id).ConfigPath = configPath;
        }
    }

    /// <summary>Makes an opened session live: from now on calls resolve to it.</summary>
    public void Activate(TSession session)
    {
        lock (_gate)
        {
            Require(session.Id).Phase = new LivePhase(session);
        }
    }

    /// <summary>Whether <paramref name="id"/> names a live session.</summary>
    public bool IsLive(string id)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out var entry) && entry.Phase is LivePhase;
        }
    }

    /// <summary>The session a call names, or the only live one when it names none.</summary>
    public TSession Resolve(string? id)
    {
        lock (_gate)
        {
            return ResolveLocked(id);
        }
    }

    /// <summary>
    /// Closes the session a call names, or the only live one, with <paramref name="teardown"/>; the session
    /// leaves, and its claims go, once <paramref name="teardown"/> settles. Closing a session already closing
    /// returns the same close.
    /// </summary>
    public Task<string> CloseAsync(string? id, string reason, Func<TSession, Task<string>> teardown)
    {
        lock (_gate)
        {
            if (id is not null && _entries.TryGetValue(id, out var existing) && existing.Phase is ClosingPhase closing)
            {
                return closing.Closed;
            }

            var session = ResolveLocked(id);
            var entry = Require(session.Id);
            var closed = TeardownAsync(session, reason, teardown);
            entry.Phase = new ClosingPhase(session, reason, closed);
            return closed;
        }
    }

    /// <summary>
    /// Closes every session once the opens still running have settled, and admits nothing from then on, so no
    /// session starts a browser while the server goes away. Returns what each close it ran reported.
    /// </summary>
    public async Task<IReadOnlyList<string>> CloseAllAsync(string reason, Func<TSession, Task<string>> teardown)
    {
        List<Task> opening;
        lock (_gate)
        {
            _draining = true;
            opening = [.. _entries.Values.Select(entry => entry.Opened)];
        }

        await Task.WhenAll(opening.Select(Settled)).ConfigureAwait(false);
        List<TSession> live;
        List<Task<string>> closing;
        lock (_gate)
        {
            live = Live();
            closing = [.. _entries.Values.Select(entry => entry.Phase).OfType<ClosingPhase>().Select(phase => phase.Closed)];
        }

        var summaries = await Task.WhenAll(live.Select(session => CloseAsync(session.Id, reason, teardown))).ConfigureAwait(false);
        await Task.WhenAll(closing.Select(Settled)).ConfigureAwait(false);
        return summaries;
    }

    private static async Task Settled(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Only that it settled matters here; the caller of the task sees its outcome.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private static string Describe(IEnumerable<INamedSession> sessions) =>
        string.Join(", ", sessions.Select(session => session.Id + " on \"" + session.TargetName + "\""));

    private async Task<string> OpenAsync(string id, Func<string, Task<string>> open)
    {
        try
        {
            return await open(id).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _entries.Remove(id);
            }

            throw;
        }
    }

    private async Task<string> TeardownAsync(TSession session, string reason, Func<TSession, Task<string>> teardown)
    {
        try
        {
            return await teardown(session).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _entries.Remove(session.Id);
                _ended.Add((session.Id, reason));
                if (_ended.Count > EndedKept)
                {
                    _ended.RemoveAt(0);
                }
            }
        }
    }

    private TSession ResolveLocked(string? id)
    {
        if (id is not null)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                if (entry.Phase is LivePhase named)
                {
                    return named.Session;
                }

                if (entry.Phase is ClosingPhase closing)
                {
                    throw new ConfigurationException("NO_SESSION", "session \"" + id + "\" is closing (" + closing.Reason + ")");
                }
            }

            var ended = _ended.FirstOrDefault(item => item.Id == id);
            if (ended.Id is not null)
            {
                throw new ConfigurationException("NO_SESSION", "session \"" + id + "\" ended: " + ended.Reason + "; call open_session for a new one");
            }

            var others = Live();
            throw new ConfigurationException(
                "NO_SESSION",
                "session \"" + id + "\" is not open; " + (others.Count == 0 ? "no session is open" : "open: " + Describe(others)));
        }

        var live = Live();
        if (live.Count == 0)
        {
            var previous = _ended.Count == 0 ? null : _ended[^1].Reason;
            throw new ConfigurationException("NO_SESSION", "no session is open; call open_session first" + (previous is null ? "" : " (the previous session ended: " + previous + ")"));
        }

        if (live.Count > 1)
        {
            throw new ConfigurationException(
                "SESSION_REQUIRED",
                live.Count.ToString(CultureInfo.InvariantCulture) + " sessions are open; pass session to name one: " + Describe(live));
        }

        return live[0];
    }

    /// <summary>The live sessions, in the order they were admitted.</summary>
    private List<TSession> Live() => [.. _entries.Values.Select(entry => entry.Phase).OfType<LivePhase>().Select(phase => phase.Session)];

    private Entry Require(string id) =>
        _entries.TryGetValue(id, out var entry) ? entry : throw new InvalidOperationException("session " + id + " is not admitted");

    private ConfigurationException LimitReached()
    {
        var slots = _entries.Values.Select(entry => entry.Phase switch
        {
            LivePhase live => Describe([live.Session]),
            ClosingPhase closing => Describe([closing.Session]) + " (closing)",
            _ => "one opening",
        });
        return new ConfigurationException(
            "SESSION_OPEN",
            "no session slot is free (e2e mcp --max-sessions " + limit.ToString(CultureInfo.InvariantCulture) + "): " + string.Join(", ", slots) + "; close_session one first");
    }

    private abstract record Phase;

    private sealed record OpeningPhase : Phase;

    private sealed record LivePhase(TSession Session) : Phase;

    private sealed record ClosingPhase(TSession Session, string Reason, Task<string> Closed) : Phase;

    private sealed class Entry
    {
        public Phase Phase { get; set; } = new OpeningPhase();

        /// <summary>The config the session loads: secrets resolve through one process-wide registry, so every session shares one config.</summary>
        public string? ConfigPath { get; set; }

        /// <summary>Settles when the open does, whatever its outcome.</summary>
        public Task Opened { get; set; } = Task.CompletedTask;
    }
}
