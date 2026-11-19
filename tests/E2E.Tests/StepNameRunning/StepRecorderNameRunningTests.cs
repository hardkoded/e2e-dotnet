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
        steps.NameRunning(Cut);
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
        steps.NameRunning(Cut);
        await end.CancelAsync();
        Assert.Equal("the session closed while app.back was running", (await back)?.Message);
    }

    /// <summary>Partial: upstream's step holds the abort reason itself; a started-after step here is refused with the cancelled token, not named.</summary>
    [Fact]
    public async Task Partial_Leaves_a_step_started_after_the_abort_unnamed()
    {
        var (steps, end) = Recorder();
        steps.NameRunning(Cut);
        await end.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            steps.RunAsync(StepKind.App, "app.open", "/", Never)
        );
    }

    /// <summary>Partial: upstream's other abort is an abort reason that reached the signal first; here it is a step that failed with its own error before the cancellation.</summary>
    [Fact]
    public async Task Partial_Keeps_the_failure_of_a_step_that_failed_on_its_own()
    {
        var (steps, end) = Recorder();
        var timeout = new TestException("TEST_TIMEOUT", "session timed out after 1000 ms");
        var failing = new TaskCompletionSource();
        var step = Failure(steps.RunAsync(StepKind.App, "app.open", "/", () => failing.Task));
        failing.SetException(timeout);
        Assert.Same(timeout, await step);
        steps.NameRunning(Cut);
        await end.CancelAsync();
    }
}
