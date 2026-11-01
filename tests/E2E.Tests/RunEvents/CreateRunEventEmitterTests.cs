// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RunEvents;

/// <summary>Upstream: packages/e2e/tests/unit/run-events.test.ts, describe('createRunEventEmitter').</summary>
public sealed class CreateRunEventEmitterTests
{
    /// <summary>
    /// Partial: the port's events carry no <c>seq</c>, so the healthy sink's events are compared by their facts. Not ported: the two async-sink
    /// <c>it</c> (<c>quarantines an async sink whose promise rejects ...</c>, <c>names a sink once when several of its pending promises reject</c>),
    /// since the port's sinks are synchronous.
    /// </summary>
    [Fact]
    public void Partial_Quarantines_a_throwing_sink_without_silencing_the_healthy_one_and_says_so_once_on_stderr()
    {
        var broken = 0;
        var events = new List<RunEventFact>();
        var emit = E2E.RunEvents.CreateRunEventEmitter(
        [
            new RunEventSubscriber("broken", _ =>
            {
                broken++;
                throw new InvalidOperationException("broken host sink");
            }),
            new RunEventSubscriber("collecting", events.Add),
        ]);
        var previous = Console.Error;
        using var stderr = new StringWriter();
        Console.SetError(stderr);
        try
        {
            var facts = new[] { new PlanEvent(1, []), new PlanEvent(2, []), new PlanEvent(3, []) };
            emit(facts[0]);
            emit(facts[1]);
            emit(facts[2]);
            Assert.Equal(1, broken);
            Assert.Equal<RunEventFact>(facts, events);
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Equal(
            "e2e: reporter \"broken\" threw on plan: broken host sink; ignoring it for the rest of the run" + Environment.NewLine,
            stderr.ToString());
    }
}
