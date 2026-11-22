// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepHold;

/// <summary>
/// <c>describe('pausing on the worker path')</c> of upstream's <c>tests/integration/step-hold.test.ts</c> (tester-army/e2e#1972). Only the <c>it</c>
/// this port covers is here (tester-army/e2e#2068).
/// </summary>
public sealed class PausingOnTheWorkerPathTests
{
    // Partial: upstream runs three tests through its runner. The port has no runner of its own, so this checks the poll read refusal on a recorder
    // that holds; the locator and frame refusals are Screen's, covered where Screen is tested.
    [Fact]
    public async Task Partial_Refuses_a_pause_inside_an_expect_poll_read_without_asking_the_hook()
    {
        var asked = 0;
        var steps = new StepRecorder(
            "attempt",
            new StepRecorderOptions
            {
                Hold = new RecorderHold(
                    (_, _) =>
                    {
                        Interlocked.Increment(ref asked);
                        return Task.CompletedTask;
                    },
                    Steps: false
                ),
            }
        )
        {
            Holdable = true,
        };

        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect
                .Poll(
                    async _ =>
                    {
                        await steps.PauseAsync();
                        return 1;
                    },
                    new PollOptions { Timeout = TimeSpan.FromMilliseconds(300) }
                )
                .ToBeAsync(1)
        );

        // The poll retries the failing read. Its error names the last refusal.
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains(
            "last: screen.pause() was called inside another step (an OnAction hook or a project tool), an expect.poll read, or a toPass block",
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Equal(0, asked);
    }
}
