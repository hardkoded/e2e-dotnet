// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E;

/// <summary>What the app did on its own, as the session passes it on (upstream's <c>AppEvent</c>): a log line, or where it went.</summary>
internal abstract record AppEvent
{
    private AppEvent()
    {
    }

    /// <summary>A line the app logged.</summary>
    public sealed record Log(AppLogEntry Entry) : AppEvent;

    /// <summary>Where the app went.</summary>
    public sealed record Navigation(string Line) : AppEvent;
}

/// <summary>App log entries one attempt keeps (upstream's <c>APP_LOG_LIMITS</c>): errors and warnings, and info apart, so chatter never crowds out an error.</summary>
internal static class AppLogLimits
{
    /// <summary>Errors and warnings an attempt keeps.</summary>
    public const int ErrorsAndWarnings = 200;

    /// <summary>Info lines an attempt keeps.</summary>
    public const int Info = 100;
}

/// <summary>
/// Where the engine's app log goes (upstream's <c>AppLogRoute</c> of <c>TargetSession</c>, built by <c>createAppLogRoute</c> in
/// <c>engine/session.ts</c>). The engine reports from the moment it starts, before any step records. What arrives before a sink is
/// routed waits, each kind within its own quota, so chatter before the first step never crowds out an error. The step recorder
/// enforces <see cref="AppLogLimits"/> on what it keeps; the route holds no more than both while no recorder takes its events.
/// </summary>
internal sealed class AppLogRoute
{
    /// <summary>Navigations held while no step recorder takes them, apart from the log lines so neither crowds out the other.</summary>
    internal const int MaxWaitingNavigations = 50;

    private readonly Lock _gate = new();
    private readonly List<(AppEvent Event, DateTimeOffset At)> _waiting = [];
    private Action<AppEvent, DateTimeOffset>? _sink;

    // How many of each kind wait: errors and warnings, info, navigations.
    private readonly int[] _held = new int[3];
    private static readonly int[] Quotas = [AppLogLimits.ErrorsAndWarnings, AppLogLimits.Info, MaxWaitingNavigations];

    /// <summary>Takes one entry from the engine. A malformed one, such as a blank line, is dropped.</summary>
    public void Push(AppLogEntry? entry)
    {
        if (entry is { Source: var source, Level: var level, Text: { } text }
            && Enum.IsDefined(source)
            && Enum.IsDefined(level)
            && !string.IsNullOrWhiteSpace(text))
        {
            Pass(new AppEvent.Log(new AppLogEntry(source, level, text)));
        }
    }

    /// <summary>Takes one line saying where the app went; a blank one is dropped.</summary>
    public void Navigated(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            Pass(new AppEvent.Navigation(line));
        }
    }

    /// <summary>Sends everything from now on, and everything still waiting, to <paramref name="sink"/>; null holds it again.</summary>
    public void Route(Action<AppEvent, DateTimeOffset>? sink)
    {
        lock (_gate)
        {
            _sink = sink;
            if (sink is null)
            {
                return;
            }

            foreach (var (waiting, at) in _waiting)
            {
                Deliver(sink, waiting, at);
            }

            _waiting.Clear();
            Array.Clear(_held);
        }
    }

    private void Pass(AppEvent appEvent)
    {
        lock (_gate)
        {
            var at = DateTimeOffset.UtcNow;
            if (_sink is { } sink)
            {
                Deliver(sink, appEvent, at);
                return;
            }

            var kind = appEvent switch
            {
                AppEvent.Navigation => 2,
                AppEvent.Log { Entry.Level: AppLogLevel.Info } => 1,
                _ => 0,
            };
            if (_held[kind] >= Quotas[kind])
            {
                return;
            }

            _held[kind]++;
            _waiting.Add((appEvent, at));
        }
    }

    // The recorder is a reader: a sink that throws never fails what the engine was doing.
    private static void Deliver(Action<AppEvent, DateTimeOffset> sink, AppEvent appEvent, DateTimeOffset at)
    {
        try
        {
            sink(appEvent, at);
        }
        catch (Exception)
        {
            // Dropped.
        }
    }
}
