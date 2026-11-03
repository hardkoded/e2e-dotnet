// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.Reporters;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.RunEvents;

/// <summary>
/// .NET-only (upstream's runner owns one emitter that one thread writes to; a .NET test process has sessions on many threads and reporters that
/// subscribe while it runs): the process's event bus.
/// </summary>
[Collection(nameof(ConsoleCollection))]
public sealed class RunEventBusTests : IDisposable
{
    private readonly ReporterRunKit _kit = new();

    public void Dispose()
    {
        _kit.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Numbers_the_events_of_parallel_sessions_in_the_order_every_sink_receives_them()
    {
        var first = new List<RunEvent>();
        var second = new List<RunEvent>();
        using var one = E2E.RunEvents.Subscribe(new RunEventSubscriber("one", first.Add));
        using var two = E2E.RunEvents.Subscribe(new RunEventSubscriber("two", second.Add));
        string[] titles = [.. Enumerable.Range(1, 8).Select(number => $"parallel test {number}")];

        await Task.WhenAll(titles.Select(title => Task.Run(() => _kit.PassAsync(title))));

        // Delivery holds one lock, so the sinks see the same events in the same order, numbered without a gap.
        Assert.Equal(first.Select(runEvent => runEvent.Seq), second.Select(runEvent => runEvent.Seq));
        Assert.Equal(Enumerable.Range(0, first.Count).Select(offset => first[0].Seq + offset), first.Select(runEvent => runEvent.Seq));
        foreach (var title in titles)
        {
            var started = first.FindIndex(runEvent => runEvent.Fact is TestStartedEvent test && test.Title == title);
            var finished = first.FindIndex(runEvent => runEvent.Fact is TestFinishedEvent { Result.Test.TitlePath: [var each] } && each == title);
            Assert.InRange(started, 0, finished - 1);

            // The steps of a test come between its start and its end.
            var testId = ((TestStartedEvent)first[started].Fact).TestId;
            var steps = first.Select((runEvent, index) => (runEvent.Fact, index)).Where(entry => entry.Fact is StepProgressEvent step && step.TestId == testId).Select(entry => entry.index).ToList();
            Assert.NotEmpty(steps);
            Assert.All(steps, index => Assert.InRange(index, started + 1, finished - 1));
        }
    }

    [Fact]
    public void Quarantines_each_sink_on_its_own_and_names_it_once()
    {
        var emitter = new RunEventEmitter();
        var healthy = new List<RunEvent>();
        var calls = new Dictionary<string, int> { ["loud"] = 0, ["louder"] = 0 };
        emitter.Subscribe(new RunEventSubscriber("loud", _ =>
        {
            calls["loud"]++;
            throw new InvalidOperationException("one");
        }));
        emitter.Subscribe(new RunEventSubscriber("healthy", healthy.Add));
        emitter.Subscribe(new RunEventSubscriber("louder", _ =>
        {
            calls["louder"]++;
            throw new InvalidOperationException("two");
        }));
        var previous = Console.Error;
        using var stderr = new StringWriter();
        Console.SetError(stderr);
        try
        {
            emitter.Emit(new NoticeEvent("app", "first"));
            emitter.Emit(new RunInterruptedEvent(InterruptMode.Graceful));
            emitter.Emit(new RunStoppedEvent(1, 1));
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Equal(new Dictionary<string, int> { ["loud"] = 1, ["louder"] = 1 }, calls);
        Assert.Equal(3, healthy.Count);
        Assert.Equal(
            [
                "e2e: reporter \"loud\" threw on notice: one; ignoring it for the rest of the run",
                "e2e: reporter \"louder\" threw on notice: two; ignoring it for the rest of the run",
            ],
            stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Gives_a_sink_the_events_from_when_it_subscribed_until_it_is_disposed()
    {
        var emitter = new RunEventEmitter();
        var seen = new List<RunEvent>();
        emitter.Emit(new NoticeEvent("app", "before"));
        var subscription = emitter.Subscribe(new RunEventSubscriber("late", seen.Add));
        emitter.Emit(new NoticeEvent("app", "during"));
        subscription.Dispose();
        emitter.Emit(new NoticeEvent("app", "after"));

        var heard = Assert.Single(seen);
        Assert.Equal(new NoticeEvent("app", "during"), heard.Fact);

        // Nobody listened to the first event, so nothing numbered it.
        Assert.Equal(1, heard.Seq);
        Assert.False(emitter.HasSubscribers);
    }
}
