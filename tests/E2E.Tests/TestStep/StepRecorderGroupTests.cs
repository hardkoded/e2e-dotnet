// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;
using Progress = global::E2E.StepProgress;

namespace E2E.Tests.TestStep;

/// <summary><c>describe('StepRecorder.group')</c> of upstream's <c>tests/unit/test-step.test.ts</c> (tester-army/e2e#2312).</summary>
public sealed class StepRecorderGroupTests
{
    private static Task Tap(StepRecorder steps, string label, Func<Task>? body = null) =>
        steps.RunAsync(StepKind.Locator, "locator.tap", label, body ?? (() => Task.CompletedTask));

    /// <summary><c>[index, api, label, parent, status]</c> of every step, in record order.</summary>
    private static List<(int Index, string Api, string Label, int? Parent, string Status)> Told(StepRecorder steps) =>
        [.. steps.All().Select(step => (step.Index, step.Api, step.Label, step.Parent, StepNames.Of(step.Status)))];

    private static string? CodeOf(Exception? error) => (error as E2EException)?.Code;

    private static Task<Exception?> Fails(Func<Task> call) => Record.ExceptionAsync(call);

    private static Task Never() => new TaskCompletionSource().Task;

    [Fact]
    public async Task Records_the_step_with_the_calls_made_inside_it_as_its_children_and_returns_what_the_body_returns()
    {
        var steps = new StepRecorder("a");
        await Tap(steps, "before");
        var value = await steps.GroupAsync("Log in", async () =>
        {
            await Tap(steps, "user");
            await steps.GroupAsync("Submit", () => Tap(steps, "button"));
            return 7;
        });
        await Tap(steps, "after");

        Assert.Equal(7, value);
        Assert.Equal(
            [
                (0, "locator.tap", "before", (int?)null, "passed"),
                (1, "test.step", "Log in", null, "passed"),
                (2, "locator.tap", "user", 1, "passed"),
                (3, "test.step", "Submit", 1, "passed"),
                (4, "locator.tap", "button", 3, "passed"),
                (5, "locator.tap", "after", null, "passed"),
            ],
            Told(steps)
        );
        Assert.Equal(StepKind.Step, steps.All()[1].Kind);
    }

    [Fact]
    public async Task Keeps_steps_that_run_side_by_side_under_their_own_test_step()
    {
        var steps = new StepRecorder("a");
        var gate = new Gate();
        var both = Task.WhenAll(steps.GroupAsync("A", async () =>
            {
                await gate.Task;
                await Tap(steps, "in a");
            }), steps.GroupAsync("B", async () =>
            {
                await gate.Task;
                await Tap(steps, "in b");
            }));
        gate.Open();
        await both;

        int? ParentOf(string label) => steps.All().First(step => step.Label == label).Parent;
        Assert.Equal(0, ParentOf("in a"));
        Assert.Equal(1, ParentOf("in b"));
    }

    [Fact]
    public async Task Fails_with_a_failed_call_whose_error_only_the_call_keeps_and_throws_that_error_on()
    {
        var steps = new StepRecorder("a");
        var failure = new TestException("LOCATOR_NOT_FOUND", "no button");
        var thrown = await Fails(() => steps.GroupAsync("Log in", () => Tap(steps, "button", () => throw failure)));

        Assert.Same(failure, thrown);
        var all = steps.All();
        Assert.Equal(StepStatus.Failed, all[0].Status);
        Assert.Null(all[0].Error);
        Assert.Equal("LOCATOR_NOT_FOUND", all[1].Error?.Code);
    }

    [Fact]
    public async Task Keeps_the_error_its_own_body_threw_and_one_the_body_wrapped()
    {
        var steps = new StepRecorder("a");
        await Fails(() => steps.GroupAsync("Own", () => throw new TestException("ASSERTION_FAILED", "wrong total")));
        await Fails(() => steps.GroupAsync("Wrapped", async () =>
            {
                try
                {
                    await Tap(steps, "button", () => throw new TestException("LOCATOR_NOT_FOUND", "no button"));
                }
                catch (Exception cause)
                {
                    throw new TestException("ASSERTION_FAILED", "could not log in", cause);
                }
            }));

        var all = steps.All();
        Assert.Equal(("Own", StepStatus.Failed), (all[0].Label, all[0].Status));
        Assert.Equal("ASSERTION_FAILED", all[0].Error?.Code);
        Assert.Equal("wrong total", all[0].Error?.Message);
        Assert.Equal(("Wrapped", StepStatus.Failed), (all[1].Label, all[1].Status));
        Assert.Equal("ASSERTION_FAILED", all[1].Error?.Code);
        Assert.Equal("could not log in", all[1].Error?.Message);
    }

    [Fact]
    public async Task Passes_when_the_body_catches_a_failed_call()
    {
        var steps = new StepRecorder("a");
        await steps.GroupAsync("Dismiss the banner if any", async () =>
        {
            await Fails(() => Tap(steps, "banner", () => throw new TestException("LOCATOR_NOT_FOUND", "no banner")));
        });
        Assert.Equal(
            [
                (0, "test.step", "Dismiss the banner if any", (int?)null, "passed"),
                (1, "locator.tap", "banner", 0, "failed"),
            ],
            Told(steps)
        );
    }

    [Fact]
    public async Task Redacts_a_secret_spelled_into_the_title()
    {
        var steps = new StepRecorder(
            "a",
            new StepRecorderOptions { Redact = text => text.Replace("hunter2", "[redacted]", StringComparison.Ordinal) }
        );
        await steps.GroupAsync("Log in with hunter2", () => Task.CompletedTask);
        Assert.Equal("Log in with [redacted]", steps.All()[0].Label);
    }

    [Fact]
    public async Task Tells_reporters_which_test_step_a_step_runs_inside()
    {
        var progress = new List<Progress>();
        var steps = new StepRecorder("a", new StepRecorderOptions { OnProgress = progress.Add });
        await steps.GroupAsync("Log in", () => Tap(steps, "button"));

        var starts = progress.Where(fact => fact.Phase == StepProgressPhase.Start).ToList();
        Assert.Equal(
            [(StepKind.Step, "Log in", "a:0", (string?)null), (StepKind.Locator, "button", "a:1", "a:0")],
            starts.Select(fact => (fact.Kind!.Value, fact.Label!, fact.Identity?.StepId!, fact.Identity?.ParentStepId))
        );
        var end = progress.Last(fact => fact.Phase == StepProgressPhase.End);
        Assert.Equal(StepKind.Step, end.Kind);
        Assert.Equal("test.step", end.Api);
        Assert.Equal("Log in", end.Label);
        Assert.Equal(StepStatus.Passed, end.Status);
    }

    [Fact]
    public async Task Is_never_held_itself_in_a_stepped_run_while_each_call_inside_it_is()
    {
        var asked = new List<(StepHoldKind, string, string)>();
        var steps = new StepRecorder(
            "a",
            new StepRecorderOptions
            {
                Hold = new RecorderHold(
                    (request, _) =>
                    {
                        asked.Add((request.Kind, request.Api, request.Label));
                        return Task.CompletedTask;
                    },
                    Steps: true
                ),
            }
        )
        {
            Holdable = true,
        };
        await steps.GroupAsync("Log in", async () =>
        {
            await Tap(steps, "user");
            await Tap(steps, "button");
        });
        Assert.Equal([(StepHoldKind.Step, "locator.tap", "user"), (StepHoldKind.Step, "locator.tap", "button")], asked);
    }

    [Fact]
    public async Task Lets_a_verification_inside_it_prove_the_steps_before_that_verification()
    {
        var steps = new StepRecorder("a");
        await Tap(steps, "before");
        await steps.GroupAsync("Check", async () =>
        {
            await Tap(steps, "inside");
            await steps.RunAsync(
                StepKind.Assertion,
                "expect.toBeVisible",
                "heading",
                () => Task.CompletedTask,
                new StepRunOptions { Verifies = true }
            );
        });
        Assert.Equal(3, steps.LastVerifiedStepIndex);
    }

    [Fact]
    public async Task Fails_as_not_awaited_when_the_test_returns_before_it_ends()
    {
        var steps = new StepRecorder("a");
        var pending = steps.GroupAsync("Log in", Never);
        _ = pending.ContinueWith(
            finished => _ = finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
        var error = steps.AbandonRunning();
        Assert.Equal("STEP_NOT_AWAITED", error?.Code);
        Assert.Contains("test.step \"Log in\"", error!.Message, StringComparison.Ordinal);
        Assert.Contains("a fixture can't call use() inside one", error.Message, StringComparison.Ordinal);
        Assert.Equal(StepStatus.Failed, steps.All()[0].Status);
        Assert.Equal("STEP_NOT_AWAITED", steps.All()[0].Error?.Code);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Is_no_parent_to_a_call_its_body_left_running_after_it_ended()
    {
        var steps = new StepRecorder("a");
        Task late = Task.CompletedTask;
        var gate = new Gate();
        await steps.GroupAsync("Outer", async () =>
        {
            await steps.GroupAsync("Inner", () =>
            {
                late = gate
                    .Task.ContinueWith(
                        _ => Tap(steps, "late"),
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default
                    )
                    .Unwrap();
                return Task.CompletedTask;
            });
            gate.Open();
            await late;
        });
        // Called in Inner's async context after Inner ended: Outer is the one still running.
        Assert.Equal(0, steps.All().First(step => step.Label == "late").Parent);
    }

    [Fact]
    public async Task Keeps_an_error_a_step_outside_it_recorded_when_its_body_throws_that_error_later()
    {
        var steps = new StepRecorder("a");
        var failure = new TestException("LOCATOR_NOT_FOUND", "no button");
        await Fails(() => Tap(steps, "button", () => throw failure));
        await Fails(() => steps.GroupAsync("Later", async () =>
            {
                await Tap(steps, "other");
                throw failure;
            }));
        var later = steps.All().First(step => step.Label == "Later");
        Assert.Equal(StepStatus.Failed, later.Status);
        Assert.Equal("LOCATOR_NOT_FOUND", later.Error?.Code);
    }

    [Fact]
    public async Task Records_no_error_for_either_of_two_calls_that_fail_side_by_side_in_it()
    {
        var steps = new StepRecorder("a");
        await Fails(() =>
            steps.GroupAsync("Both", () =>
                Task.WhenAll(
                    Tap(steps, "one", () => throw new TestException("LOCATOR_NOT_FOUND", "no one")),
                    Tap(steps, "two", () => throw new TestException("LOCATOR_NOT_FOUND", "no two"))
                )
            )
        );
        Assert.Equal(StepStatus.Failed, steps.All()[0].Status);
        Assert.Null(steps.All()[0].Error);
    }

    private sealed class Verdict : Exception;

    [Fact]
    public async Task Passes_when_its_body_throws_what_the_caller_calls_a_verdict_and_throws_it_on()
    {
        var steps = new StepRecorder("a");
        var skip = new Verdict();
        var thrown = await Fails(() =>
            steps.GroupAsync(
                "Check the platform",
                () => throw skip,
                new StepGroupOptions { PassesThrough = value => ReferenceEquals(value, skip) }
            )
        );
        Assert.Same(skip, thrown);
        Assert.Equal(StepStatus.Passed, steps.All()[0].Status);
        Assert.Null(steps.All()[0].Error);
    }

    [Fact]
    public async Task Is_cut_with_the_attempt_as_the_call_running_in_it_is_and_when_its_body_waits_on_no_call()
    {
        using var cut = new CancellationTokenSource();
        var steps = new StepRecorder("a", new StepRecorderOptions { Signal = () => cut.Token });
        var withCall = Fails(() => steps.GroupAsync("Log in", () => Tap(steps, "button", Never)));
        var alone = Fails(() => steps.GroupAsync("Wait", Never));
        cut.Cancel();
        Assert.Equal("CANCELLED", CodeOf(await withCall));
        Assert.Equal("CANCELLED", CodeOf(await alone));
        Assert.Equal(
            [
                ("Log in", StepStatus.Cancelled, (string?)null),
                ("button", StepStatus.Cancelled, "CANCELLED"),
                ("Wait", StepStatus.Cancelled, "CANCELLED"),
            ],
            steps.All().Select(step => (step.Label, step.Status, step.Error?.Code))
        );
        Assert.False(steps.Busy);
    }

    [Fact]
    public async Task Keeps_the_cancellation_on_the_call_alone_however_many_awaits_sit_between_the_call_and_the_step()
    {
        using var cut = new CancellationTokenSource();
        var steps = new StepRecorder("a", new StepRecorderOptions { Signal = () => cut.Token });
        var pending = Fails(() => steps.GroupAsync("Log in", async () =>
            {
                await (
                    (Func<Task>)(
                        async () =>
                        {
                            await Tap(steps, "button", Never);
                        }
                    )
                )();
            }));
        cut.Cancel();
        Assert.Equal("CANCELLED", CodeOf(await pending));
        Assert.Equal(
            [("Log in", StepStatus.Cancelled, (string?)null), ("button", StepStatus.Cancelled, "CANCELLED")],
            steps.All().Select(step => (step.Label, step.Status, step.Error?.Code))
        );
    }

    [Fact]
    public async Task Ends_after_a_call_another_session_runs_inside_it()
    {
        using var cut = new CancellationTokenSource();
        var steps = new StepRecorder("a");
        GuestRecord? guest = null;
        var pending = Fails(() =>
            steps.GroupAsync(
                "Second user waits",
                () =>
                {
                    guest = steps.Guest(StepKind.Locator, "locator.waitFor", "button", "launch-1", null);
                    return Never();
                },
                new StepGroupOptions { Signal = cut.Token }
            )
        );
        cut.Cancel();
        await Task.Delay(50);
        Assert.False(pending.IsCompleted);
        guest!.End(new GuestResult(StepStatus.Cancelled, 1));
        Assert.Equal("CANCELLED", CodeOf(await pending));
        Assert.Equal(
            [("Second user waits", StepStatus.Cancelled, (int?)null), ("button", StepStatus.Cancelled, 0)],
            steps.All().Select(step => (step.Label, step.Status, step.Parent))
        );
    }

    [Fact]
    public async Task Ends_after_a_call_its_body_starts_in_answer_to_the_cut()
    {
        using var cut = new CancellationTokenSource();
        var steps = new StepRecorder("a", new StepRecorderOptions { Signal = () => cut.Token });
        GuestRecord? cleanup = null;
        var pending = Fails(() => steps.GroupAsync("Log in", async () =>
            {
                try
                {
                    await Tap(steps, "button", Never);
                }
                catch (E2EException)
                {
                    cleanup = steps.Guest(StepKind.Locator, "locator.tap", "cleanup", "launch-1", null);
                    await Never();
                }
            }));
        cut.Cancel();
        await Task.Delay(50);
        Assert.False(pending.IsCompleted);
        cleanup!.End(new GuestResult(StepStatus.Passed, 1));
        Assert.Equal("CANCELLED", CodeOf(await pending));
        Assert.Equal(
            [("Log in", StepStatus.Cancelled), ("button", StepStatus.Cancelled), ("cleanup", StepStatus.Passed)],
            steps.All().Select(step => (step.Label, step.Status))
        );
    }

    [Fact]
    public async Task Is_cut_by_the_signal_it_is_given_beside_the_recorders_own()
    {
        using var phase = new CancellationTokenSource();
        using var own = new CancellationTokenSource();
        var steps = new StepRecorder("a", new StepRecorderOptions { Signal = () => own.Token });
        var pending = Fails(() => steps.GroupAsync("Wait", Never, new StepGroupOptions { Signal = phase.Token }));
        phase.Cancel();
        Assert.Equal("CANCELLED", CodeOf(await pending));
        Assert.Equal(StepStatus.Cancelled, steps.All()[0].Status);
    }

    [Fact]
    public async Task Ends_after_the_call_the_attempt_cut_inside_it_so_the_call_is_cancelled_and_not_left_running()
    {
        using var cut = new CancellationTokenSource();
        var steps = new StepRecorder("a");
        var call = new TaskCompletionSource();
        var pending = Fails(() =>
            steps.GroupAsync(
                "Log in",
                () => Tap(steps, "button", () => call.Task),
                new StepGroupOptions { Signal = cut.Token }
            )
        );
        cut.Cancel();
        await Task.Delay(50);
        Assert.False(pending.IsCompleted);
        call.SetException(new TestException("CANCELLED", "the engine cut the call"));
        Assert.Equal("CANCELLED", CodeOf(await pending));
        Assert.Equal(
            [("Log in", StepStatus.Cancelled), ("button", StepStatus.Cancelled)],
            steps.All().Select(step => (step.Label, step.Status))
        );
        Assert.Null(steps.AbandonRunning());
    }

    [Fact]
    public async Task Stays_the_parent_of_a_held_call_the_test_returned_before_releasing()
    {
        var steps = new StepRecorder(
            "a",
            new StepRecorderOptions { Hold = new RecorderHold((_, _) => Never(), Steps: true) }
        )
        {
            Holdable = true,
        };
        var pending = Fails(() => steps.GroupAsync("Log in", () => Tap(steps, "held")));
        await Task.Yield();
        steps.AbandonRunning();
        var held = steps.All().First(step => step.Label == "held");
        Assert.Equal(0, held.Parent);
        Assert.Equal(StepStatus.Failed, held.Status);
        Assert.Equal("STEP_NOT_AWAITED", held.Error?.Code);
        Assert.False(pending.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Leaves_what_the_app_does_between_its_calls_to_the_last_call_not_to_itself()
    {
        var steps = new StepRecorder("a");
        await Tap(steps, "before");
        await steps.GroupAsync("Log in", () =>
        {
            steps.RecordAppEvent(new AppEvent.Navigation("navigated to /login"));
            steps.AttachArtifact("artifact-1");
            return Task.CompletedTask;
        });
        var all = steps.All();
        Assert.Equal(["artifact-1"], all[0].Artifacts);
        Assert.Equal([StepEventKind.Navigation], all[0].Events.Select(item => item.Kind));
        Assert.Empty(all[1].Artifacts);
        Assert.Empty(all[1].Events);
    }

    [Fact]
    public async Task Is_left_out_of_the_prior_steps_an_agent_step_is_told()
    {
        var steps = new StepRecorder("a");
        await steps.GroupAsync("Log in", () => Tap(steps, "button"));
        Assert.Equal(["locator.tap"], Ledger.ProjectPriorSteps(steps.Completed()).Select(step => step.Api));
    }
}
