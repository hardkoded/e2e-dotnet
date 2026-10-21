// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using E2E.Engine;

namespace E2E.Tests.EngineGuards;

/// <summary>
/// <c>describe('raceAbort')</c> of upstream <c>engine-guards.test.ts</c>: <c>raceAbort</c> never dispatches cancelled work and
/// never leaks the rejection of work it abandoned.
/// </summary>
public sealed class RaceAbortTests
{
    [Fact]
    public async Task Refuses_an_already_aborted_signal_before_dispatching_the_work()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var ran = false;

        var error = await Assert.ThrowsAsync<EngineException>(() => Timing.RaceAbortAsync(
            () =>
            {
                ran = true;
                return Task.FromResult("ran");
            },
            cancel.Token,
            "call"));

        Assert.Equal("CANCELLED", error.Code);
        Assert.Equal("call cancelled", error.Message);
        Assert.False(error.Retryable);
        Assert.False(ran);
    }

    [Fact]
    public async Task Abandons_pending_work_at_abort_and_absorbs_its_late_rejection()
    {
        var unobserved = new List<Exception>();
        void Record(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            lock (unobserved)
            {
                unobserved.AddRange(args.Exception.InnerExceptions.Where(failure => failure.Message == "late"));
            }
        }

        TaskScheduler.UnobservedTaskException += Record;
        try
        {
            Assert.Equal("CANCELLED", await AbandonThenFailLate());
            Collect();
            lock (unobserved)
            {
                Assert.Empty(unobserved);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Record;
        }
    }

    // Keeps the abandoned task out of the test's own state machine: once this returns, only a string is left, so nothing but
    // Timing can still reach the faulted task and the collection below finalizes it if its failure was never observed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<string> AbandonThenFailLate()
    {
        using var cancel = new CancellationTokenSource();
        var abandoned = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raced = Timing.RaceAbortAsync(() => abandoned.Task, cancel.Token, "call");
        await cancel.CancelAsync();
        var code = (await Assert.ThrowsAsync<EngineException>(() => raced)).Code;
        abandoned.SetException(new InvalidOperationException("late"));
        await Task.Yield();
        return code;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
