// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.Reporters;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.RunEvents;

/// <summary>.NET-only (upstream's runner emits a test's events; a session is one attempt of a test): what a session tells the process's events.</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class SessionEventsTests : IDisposable
{
    private const string Title = "cart > keeps the item";
    private const string File = "E2E.Tests.Reporters.ReporterRun";

    private readonly ReporterRunKit _kit = new();

    public void Dispose()
    {
        _kit.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Tells_a_test_start_its_steps_and_its_result_with_the_attempt_the_report_holds()
    {
        var events = new List<RunEvent>();
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", events.Add));

        await _kit.PassAsync(Title);

        var facts = events.Select(runEvent => runEvent.Fact).ToList();
        var started = Assert.IsType<TestStartedEvent>(facts[0]);
        var testId = E2E.Internal.Ids.TestId(File, [Title]);
        Assert.Equal(new TestStartedEvent(testId, "default", 0, Title, File, null, "document"), started);
        var finished = Assert.IsType<TestFinishedEvent>(facts[^1]).Result;
        Assert.All(facts[1..^1], fact => Assert.Equal(new StepProgressEvent(testId, "default", 0, "document", ((StepProgressEvent)fact).Progress), fact));
        Assert.NotEmpty(facts[1..^1]);
        Assert.Equal([Title], finished.Test.TitlePath);
        Assert.Equal((testId, File, 0), (finished.Test.Id, finished.Test.File, finished.Test.DeclarationIndex));
        Assert.Equal(("document", "document"), finished.Target);
        Assert.Equal(Reports.ResultStatus.Passed, finished.Status);
        Assert.Equal(AttemptReport.ResultId(File, Title), finished.ResultId);
        var attempt = Assert.Single(finished.Attempts);
        Assert.Equal(AttemptStatus.Passed, attempt.Status);
        Assert.Contains(attempt.Steps, step => step.Api == "app.open");
        Assert.NotNull(finished.Recorded);
    }

    [Fact]
    public async Task Tells_each_attempt_of_a_retried_test_with_every_attempt_so_far()
    {
        var finished = new List<RunEventResult>();
        var starts = 0;
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", runEvent =>
        {
            switch (runEvent.Fact)
            {
                case TestStartedEvent:
                    starts++;
                    break;
                case TestFinishedEvent done:
                    finished.Add(done.Result);
                    break;
            }
        }));

        await using (var failing = await _kit.StartAsync(Title))
        {
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        await using (var passing = await _kit.StartAsync(Title, attempt: 2))
        {
            passing.Complete();
        }

        Assert.Equal(2, starts);
        Assert.Equal([Reports.ResultStatus.Failed, Reports.ResultStatus.Flaky], finished.Select(result => result.Status));
        Assert.Equal([1, 2], finished.Select(result => result.Attempts.Count));
    }

    [Fact]
    public async Task Tells_nothing_for_a_session_that_is_not_a_test_such_as_an_exploration()
    {
        var events = new List<RunEvent>();
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", events.Add));

        await using (var session = await _kit.StartAsync(Title, emitRunEvents: false))
        {
            session.Complete();
        }

        Assert.Empty(events);
    }
}
