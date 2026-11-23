// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.ExpectPoll;

/// <summary>
/// Upstream's <c>describe('expect.poll in a poll scope')</c>. Not ported: the four tests of the runner's <c>realmSlot</c> (a runner from
/// before the marker, a standalone read with no runner scope, an older expect copy, and <c>AttemptBudget</c>'s signal), which exercise a
/// process-wide slot and a budget the port does not have.
/// </summary>
public sealed class ExpectPollInAPollScopeTests
{
    private static PollOptions Every(int milliseconds, int? timeoutMs = null) =>
        new()
        {
            Intervals = [TimeSpan.FromMilliseconds(milliseconds)],
            Timeout = timeoutMs is { } t ? TimeSpan.FromMilliseconds(t) : null,
        };

    private static async Task<Exception?> RejectionOf(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception cause)
        {
            return cause;
        }
    }

    [Fact]
    public async Task Fails_the_phase_that_returned_before_its_poll_finished_at_the_line_of_the_call_and_cancels_it()
    {
        var scope = new PollScope("the test body");
        var reads = 0;
        var poll = PollScopes.RunInPollScope(scope, () => Expect
                .Poll(
                    () =>
                    {
                        Interlocked.Increment(ref reads);
                        return "running";
                    },
                    Every(5, 5000)
                )
                .ToBeAsync("done"));
        await Task.Delay(20);
        var error = scope.Close();
        Assert.NotNull(error);
        Assert.Equal("STEP_NOT_AWAITED", error.Code);
        Assert.Equal(
            "the test body returned before expect.poll(...).toBe(...) finished; put `await` in front of every expect.poll matcher and toPass call",
            error.Message
        );
        Assert.Contains(
            nameof(ExpectPollInAPollScopeTests),
            string.Join('\n', AbandonedWork.RelocatedStack(error)!.GetFrames().Select(f => f.GetFileName()))
        );
        var rejection = await RejectionOf(poll);
        Assert.IsAssignableFrom<OperationCanceledException>(rejection);
        Assert.True(AbandonedWork.IsRejection(rejection!));
        var readsAtClose = Volatile.Read(ref reads);
        await Task.Delay(30);
        Assert.Equal(readsAtClose, Volatile.Read(ref reads));
    }

    [Fact]
    public async Task Names_one_poll_and_counts_the_rest_negation_included()
    {
        var scope = new PollScope("the afterEach hook");
        var polls = PollScopes.RunInPollScope(scope, () =>
            new[]
            {
                Expect.Poll(() => 1, Every(5, 5000)).Not.ToBeAsync(1),
                Expect.Poll(() => 1, Every(5, 5000)).ToBeAsync(2),
            }
        );
        var error = scope.Close();
        Assert.Matches(
            @"^the afterEach hook returned before expect\.poll\(\.\.\.\)\.not\.toBe\(\.\.\.\) and 1 more finished",
            error?.Message
        );
        Assert.Null(scope.Close());
        foreach (var poll in polls)
        {
            await RejectionOf(poll);
        }
    }

    [Fact]
    public async Task A_poll_that_would_have_passed_still_fails_its_phase_when_not_awaited()
    {
        var scope = new PollScope("the test body");
        var reads = 0;
        var poll = PollScopes.RunInPollScope(scope, () =>
            Expect.Poll(() => ++reads > 3 ? "done" : "running", Every(20)).ToBeAsync("done")
        );
        Assert.Equal("STEP_NOT_AWAITED", scope.Close()?.Code);
        Assert.IsAssignableFrom<OperationCanceledException>(await RejectionOf(poll));
    }

    [Fact]
    public async Task Closes_quietly_when_every_poll_was_awaited_whatever_its_outcome()
    {
        var scope = new PollScope("the test body");
        await PollScopes.RunInPollScope(scope, async () =>
        {
            var reads = 0;
            await Expect.Poll(() => ++reads > 1 ? "done" : "running", Every(5)).ToBeAsync("done");
            var error = await Assert.ThrowsAsync<TestException>(() =>
                Expect.Poll(() => "running", Every(5, 30)).ToBeAsync("done")
            );
            Assert.Contains("timed out after 30 ms", error.Message);
        });
        Assert.Null(scope.Close());
    }

    [Fact]
    public async Task A_poll_belongs_to_the_phase_that_started_it_not_to_the_phase_running_when_it_is_called()
    {
        var timedOut = new PollScope("the test body");
        var reads = 0;
        var resume = new TaskCompletionSource();
        var leftover = PollScopes.RunInPollScope(timedOut, async () =>
        {
            await resume.Task;
            await Expect
                .Poll(
                    () =>
                    {
                        Interlocked.Increment(ref reads);
                        return "running";
                    },
                    Every(5, 5000)
                )
                .ToBeAsync("done");
        });
        timedOut.Close();
        var next = new PollScope("the test body");
        await PollScopes.RunInPollScope(next, async () =>
        {
            resume.SetResult();
            var rejection = await RejectionOf(leftover);
            Assert.IsAssignableFrom<OperationCanceledException>(rejection);
            Assert.True(AbandonedWork.IsRejection(rejection!));
        });
        Assert.Equal(0, reads);
        Assert.Null(next.Close());
    }

    [Fact]
    public async Task Hands_a_lineage_over_to_the_scope_current_when_it_is_handed_over()
    {
        var body = new PollScope("the test body");
        var lineage = PollScopes.RunInPollScope(body, () => PollScopes.PollLineage());
        Assert.Same(body, lineage.Run(PollScopes.CurrentPollScope));
        body.Close();
        var teardown = new PollScope("the fixture \"workspace\" teardown");
        PollScopes.RunInPollScope(teardown, () =>
        {
            lineage.HandOver();
            return 0;
        });
        var poll = lineage.Run(() => Expect.Poll(() => "running", Every(5, 5000)).ToBeAsync("done"));
        Assert.StartsWith("the fixture \"workspace\" teardown returned before", teardown.Close()?.Message);
        await RejectionOf(poll);
    }

    [Fact]
    public async Task Marks_the_rejection_of_a_promise_derived_from_an_abandoned_poll()
    {
        var scope = new PollScope("the test body");
        var derived = PollScopes.RunInPollScope(scope, async () =>
        {
            await Expect.Poll(() => "running", Every(5, 5000)).ToBeAsync("done");
            return "never";
        });
        scope.Close();
        Assert.True(AbandonedWork.IsRejection((await RejectionOf(derived))!));
    }

    [Fact]
    public async Task Stays_out_of_the_way_of_a_poll_outside_any_scope()
    {
        Assert.Null(PollScopes.CurrentPollScope());
        var reads = 0;
        var poll = Expect.Poll(() => ++reads > 2 ? "done" : "running", Every(5)).ToBeAsync("done");
        var scope = new PollScope("the test body");
        Assert.Null(scope.Close());
        await poll;
    }
}
