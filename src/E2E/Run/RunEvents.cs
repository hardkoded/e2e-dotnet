// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;

namespace E2E;

/// <summary>One consumer of the run's events (upstream's <c>RunEventSubscriber</c>): a name for the diagnostic and the sink.</summary>
internal sealed record RunEventSubscriber(string Name, Action<RunEvent> OnEvent);

/// <summary>
/// The single writer of a run's events (upstream's <c>createRunEventEmitter</c>, <c>run/events.ts</c>): it stamps <c>seq</c> and <c>at</c>, and hands each event to
/// every subscriber in order. Subscribers are quarantined independently, so one broken sink cannot silence another, and each quarantine is one
/// stderr line naming the sink, the event, and the error. .NET-only: it is thread-safe and takes subscribers while it runs, since sessions of
/// one test process emit from whatever thread their test runs on, where upstream's emitter is called from one thread; delivery holds one lock,
/// so every sink sees the events in <c>seq</c> order. Not ported: async sinks, since the port's sinks are synchronous.
/// </summary>
internal sealed partial class RunEventEmitter
{
    private readonly Lock _gate = new();
    private readonly List<RunEventSubscriber> _subscribers = [];
    private readonly HashSet<RunEventSubscriber> _quarantined = [];
    private long _seq;

    /// <summary>Whether anyone listens, so a producer can skip building a fact nobody reads.</summary>
    public bool HasSubscribers
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count > 0;
            }
        }
    }

    /// <summary>Adds a sink that sees the events emitted from now on. Disposing the result removes it.</summary>
    public IDisposable Subscribe(RunEventSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (_gate)
        {
            _subscribers.Add(subscriber);
        }

        return new Subscription(this, subscriber);
    }

    /// <summary>Stamps the fact and hands it to every sink that has not been quarantined.</summary>
    public void Emit(RunEventFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        lock (_gate)
        {
            if (_subscribers.Count == 0)
            {
                return;
            }

            var runEvent = new RunEvent(++_seq, DateTimeOffset.UtcNow, fact);
            foreach (var subscriber in _subscribers.ToArray())
            {
                if (_quarantined.Contains(subscriber))
                {
                    continue;
                }

                try
                {
                    subscriber.OnEvent(runEvent);
                }
                catch (Exception cause)
                {
                    // Several events may fail at one sink; the first names it, the rest find it quarantined.
                    _quarantined.Add(subscriber);
                    Console.Error.WriteLine($"e2e: reporter \"{subscriber.Name}\" threw on {EventType(fact)}: {cause.Message}; ignoring it for the rest of the run");
                }
            }
        }
    }

    // The fact as upstream's `type` names it: `SetupEvent` is `setup`, `RunStartedEvent` is `run-started`, `StepProgressEvent` is `step`.
    private static string EventType(RunEventFact fact)
    {
        if (fact is StepProgressEvent)
        {
            return "step";
        }

        var name = fact.GetType().Name;
        return TypeWords().Replace(name.EndsWith("Event", StringComparison.Ordinal) ? name[..^"Event".Length] : name, "$1-$2").ToLowerInvariant();
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex TypeWords();

    private sealed class Subscription(RunEventEmitter owner, RunEventSubscriber subscriber) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
            {
                owner._subscribers.Remove(subscriber);
                owner._quarantined.Remove(subscriber);
            }
        }
    }
}

/// <summary>
/// The process's run events. .NET-only: upstream's runner owns one emitter for the run it starts; a .NET test run has no such owner, so the
/// sessions and the run fixtures of one process write to this bus, and the reporters of the run subscribe to it (see <see cref="Reporters"/>).
/// <see cref="CreateRunEventEmitter"/> is upstream's own function, for a writer with sinks of its own.
/// </summary>
internal static class RunEvents
{
    private static readonly RunEventEmitter Bus = new();

    /// <summary>Whether a sink listens to the process's events.</summary>
    public static bool HasSubscribers => Bus.HasSubscribers;

    /// <summary>Writes a fact to the process's events. A fact nobody listens to costs nothing; a sink that throws is quarantined.</summary>
    public static void Emit(RunEventFact fact) => Bus.Emit(fact);

    /// <summary>Adds a sink to the process's events; disposing the result removes it.</summary>
    public static IDisposable Subscribe(RunEventSubscriber subscriber) => Bus.Subscribe(subscriber);

    /// <summary>
    /// Builds a run's event writer over <paramref name="subscribers"/>. Returns a no-op when there is no subscriber, so call sites never branch.
    /// </summary>
    public static Action<RunEventFact> CreateRunEventEmitter(IEnumerable<RunEventSubscriber?> subscribers)
    {
        var active = subscribers.OfType<RunEventSubscriber>().ToList();
        if (active.Count == 0)
        {
            return _ => { };
        }

        var emitter = new RunEventEmitter();
        foreach (var subscriber in active)
        {
            emitter.Subscribe(subscriber);
        }

        return emitter.Emit;
    }
}
