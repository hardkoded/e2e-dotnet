// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepHold;

/// <summary>
/// <c>describe('hold.ask on the worker path')</c> of upstream's <c>tests/integration/step-hold.test.ts</c> (tester-army/e2e#1972). Only the <c>it</c>
/// this port covers is here (tester-army/e2e#2068).
/// </summary>
public sealed class HoldAskOnTheWorkerPathTests
{
    // Partial: upstream runs a test through its runner and reads the labels the hook was asked about; the port has no runner of its own, so this
    // runs the step through a recorder that holds, inside the expect.poll read.
    [Fact]
    public async Task Partial_Never_holds_a_step_an_expect_poll_read_calls()
    {
        var asked = new List<string>();
        var steps = new StepRecorder(
            "attempt",
            new StepRecorderOptions
            {
                Hold = new RecorderHold(
                    (request, _) =>
                    {
                        asked.Add(request.Label);
                        return Task.CompletedTask;
                    },
                    Steps: true
                ),
            }
        )
        {
            Holdable = true,
        };

        await Expect
            .Poll(async _ =>
            {
                await steps.RunAsync(StepKind.App, "app.open", "/in-poll", () => Task.CompletedTask);
                return 1;
            })
            .ToBeAsync(1);

        Assert.DoesNotContain("/in-poll", asked);
        Assert.Equal("passed", steps.All()[0].Status.ToString().ToLowerInvariant());
    }
}
