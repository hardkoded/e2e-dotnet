// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.StepNameRunning;

/// <summary>
/// Upstream's <c>describe('StepRecorder.nameRunning')</c> (<c>tests/unit/step-name-running.test.ts</c>): the steps a close cut, each failing by its own
/// name. Upstream aborts the recorder's signal with a reason error and a step carrying that reason is replaced; a .NET step is cut by a cancelled token,
/// so the recorder names what a cancellation cut and leaves any other failure alone.
/// </summary>
public sealed class StepRecorderNameRunningTests
{
    private static readonly Func<string, Exception> Cut = call => new TestException(
        "CANCELLED",
        "the session closed while " + call + " was running"
    );

    private static TestException Closed() => new("CANCELLED", "the session is closed");

    private static Task Never() => new TaskCompletionSource().Task;

    private static async Task<Exception?> Failure(Task step)
    {
        try
        {
            await step;
            return null;
        }
        catch (Exception cause)
        {
            return cause;
        }
    }

    private static (StepRecorder Steps, CancellationTokenSource End) Recorder(
        [System.Runtime.CompilerServices.CallerFilePath] string file = ""
    )
    {
        var end = new CancellationTokenSource();
        var steps = new StepRecorder(
            "attempt",
            new StepRecorderOptions
            {
                ProjectRoot = Path.GetDirectoryName(Path.GetDirectoryName(file)),
                Cancellation = end.Token,
                Signal = () => end.Token,
            }
        );
        return (steps, end);
    }

    [Fact]
    public async Task Fails_each_running_step_with_its_own_error_at_the_line_that_called_it_a_nested_one_too()
    {
        var (steps, end) = Recorder();
        Task<Exception?>? inner = null;
        var outer = Failure(steps.RunAsync(StepKind.Agent, "agent.act", "check out", async () =>
            {
                inner = Failure(
                    steps.RunAsync(StepKind.Locator, "locator.tap", "getByRole(\"button\", \"Pay\")", Never)
                );
                await Never();
            }));
        steps.NameRunning(Closed(), Cut);
        await end.CancelAsync();

        var named = Assert.IsAssignableFrom<E2EException>(await outer);
        Assert.Equal("CANCELLED", named.Code);
        Assert.Equal("the session closed while agent.act \"check out\" was running", named.Message);
        var nested = Assert.IsAssignableFrom<E2EException>(await inner!);
        Assert.Equal("CANCELLED", nested.Code);
        Assert.Equal(
            "the session closed while locator.tap \"getByRole(\"button\", \"Pay\")\" was running",
            nested.Message
        );
        Assert.Contains(nameof(StepRecorderNameRunningTests), named.StackTrace, StringComparison.Ordinal);
        Assert.Equal("StepNameRunning/StepRecorderNameRunningTests.cs", steps.All()[0].Error?.Source?.File);
        Assert.Equal([StepStatus.Cancelled, StepStatus.Cancelled], steps.All().Select(step => step.Status));
    }

    [Fact]
    public async Task Names_a_step_that_has_no_label_by_its_api_alone()
    {
        var (steps, end) = Recorder();
        var back = Failure(steps.RunAsync(StepKind.App, "app.back", "", Never));
        steps.NameRunning(Closed(), Cut);
        await end.CancelAsync();
        Assert.Equal("the session closed while app.back was running", (await back)?.Message);
    }

    [Fact]
    public async Task Leaves_a_step_started_after_the_abort_with_the_reason_itself()
    {
        var (steps, end) = Recorder();
        var reason = Closed();
        steps.NameRunning(reason, Cut);
        await end.CancelAsync();
        var failure = await Assert.ThrowsAsync<TestException>(() =>
            steps.RunAsync(StepKind.App, "app.open", "/", Never)
        );
        Assert.Same(reason, failure);
    }

    [Fact]
    public async Task Keeps_the_failure_of_a_step_another_abort_reached_first()
    {
        var timeout = new TestException("TEST_TIMEOUT", "session timed out after 1000 ms");
        var end = new CancellationTokenSource();
        var steps = new StepRecorder(
            "attempt",
            new StepRecorderOptions
            {
                Cancellation = end.Token,
                Signal = () => end.Token,
                Reason = () => end.IsCancellationRequested ? timeout : null,
            }
        );
        var step = Failure(steps.RunAsync(StepKind.App, "app.open", "/", Never));
        await end.CancelAsync();
        steps.NameRunning(Closed(), Cut);
        Assert.Same(timeout, await step);
    }
}
