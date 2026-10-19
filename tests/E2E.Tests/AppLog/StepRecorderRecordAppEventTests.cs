// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.AppLog;

/// <summary>
/// Upstream's <c>describe('StepRecorder.recordAppEvent')</c> (<c>tests/unit/app-log.test.ts</c>): what the app logged is checked at the
/// session seam and waits for a step recorder to take it. A log line goes to the attempt's app log with the step that was running,
/// else the last one, redacted and bounded; where the app went is an event of that step.
/// </summary>
public sealed class StepRecorderRecordAppEventTests
{
    private static readonly AppLogEntry Error = new(AppLogSource.Console, AppLogLevel.Error, "boom");

    private static AppEvent Log(AppLogEntry entry) => new AppEvent.Log(entry);

    [Fact]
    public async Task Keeps_a_line_with_the_running_step_a_late_one_with_the_last_step_and_an_early_one_with_none()
    {
        var steps = new StepRecorder("attempt");
        steps.RecordAppEvent(Log(new AppLogEntry(AppLogSource.Network, AppLogLevel.Warning, "GET /favicon.ico 404")), DateTimeOffset.Parse("2026-01-01T00:00:00.000Z"));
        await steps.RunAsync(StepKind.App, "app.open", "/", () =>
        {
            steps.RecordAppEvent(Log(Error));
            return Task.CompletedTask;
        });
        steps.RecordAppEvent(Log(new AppLogEntry(AppLogSource.Error, AppLogLevel.Error, "TypeError:   x\n  is undefined")));

        Assert.Equal(
            [
                (AppLogSource.Network, AppLogLevel.Warning, "GET /favicon.ico 404", (int?)null),
                (AppLogSource.Console, AppLogLevel.Error, "boom", 0),
                (AppLogSource.Error, AppLogLevel.Error, "TypeError: x is undefined", 0),
            ],
            steps.AppLog().Select(entry => (entry.Source, entry.Level, entry.Text, entry.Step)));
        Assert.Empty(steps.All()[0].Events);
    }

    [Fact]
    public async Task Keeps_a_line_made_inside_a_running_step_with_that_step_though_a_later_one_started_since()
    {
        var steps = new StepRecorder("attempt");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = steps.RunAsync(StepKind.App, "app.open", "/", async () =>
        {
            await held.Task;
            steps.RecordAppEvent(Log(Error));
        });
        var second = steps.RunAsync(StepKind.Screen, "screen.tap", "Save", () => Task.CompletedTask);
        await second;
        held.SetResult();
        await first;

        Assert.Equal([0], steps.AppLog().Select(entry => entry.Step));
    }

    [Fact]
    public async Task Redacts_a_line_before_cutting_it_to_the_report_bound_and_counts_errors_and_info_apart_against_their_caps()
    {
        var steps = new StepRecorder("attempt", new StepRecorderOptions { Redact = text => text.Replace("hunter2", "[secret]", StringComparison.Ordinal) });
        await steps.RunAsync(StepKind.App, "app.open", "/", () =>
        {
            steps.RecordAppEvent(Log(new AppLogEntry(AppLogSource.Console, AppLogLevel.Error, new string('x', 295) + "hunter2 and more")));
            for (var index = 0; index < 300; index++)
            {
                steps.RecordAppEvent(Log(Error));
            }

            for (var index = 0; index < 150; index++)
            {
                steps.RecordAppEvent(Log(new AppLogEntry(AppLogSource.Console, AppLogLevel.Info, $"tick {index}")));
            }

            return Task.CompletedTask;
        });

        var logged = steps.AppLog();
        Assert.Equal(200, logged.Count(entry => entry.Level == AppLogLevel.Error));
        Assert.Equal(100, logged.Count(entry => entry.Level == AppLogLevel.Info));
        Assert.Equal(new string('x', 295) + "[sec…", logged[0].Text);
    }

    [Fact]
    public async Task Tells_where_the_app_went_under_the_running_step_and_one_before_the_first_step_under_it()
    {
        var steps = new StepRecorder("attempt");
        steps.RecordAppEvent(new AppEvent.Navigation("navigated to /login"));
        await steps.RunAsync(StepKind.App, "app.open", "/", () =>
        {
            steps.RecordAppEvent(new AppEvent.Navigation("the app opened a new tab at /help; the test stays on its page"));
            return Task.CompletedTask;
        });

        Assert.Equal(
            [
                (StepEventKind.Navigation, "navigated to /login"),
                (StepEventKind.Navigation, "the app opened a new tab at /help; the test stays on its page"),
            ],
            steps.All()[0].Events.Select(item => (item.Kind, item.Detail)));
        Assert.Empty(steps.AppLog());
    }

    [Fact]
    public async Task Holds_no_more_navigations_before_the_first_step_than_a_step_takes_events()
    {
        var steps = new StepRecorder("attempt", new StepRecorderOptions { MaxEventsPerStep = 3 });
        for (var index = 0; index < 10; index++)
        {
            steps.RecordAppEvent(new AppEvent.Navigation($"navigated to /{index}"));
        }

        await steps.RunAsync(StepKind.App, "app.open", "/", () => Task.CompletedTask);

        Assert.Equal(["navigated to /0", "navigated to /1", "navigated to /2"], steps.All()[0].Events.Select(item => item.Detail));
    }
}
