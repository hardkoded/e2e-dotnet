// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

/// <summary>ManagedProcess: the readiness wait honours the run's interrupt, <c>reuseExisting</c> attaches (<c>tests/unit/managed-process.test.ts</c>).</summary>
public sealed class ManagedProcessTests
{
    [Theory]
    [InlineData("preflight")]
    [InlineData("startup")]
    public async Task Cancels_an_in_flight_readiness_probe(string phase)
    {
        var probing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new TestServer(request =>
        {
            if (phase == "startup" && request == 1)
            {
                return Task.FromResult<int?>(503);
            }

            // Keep the actual HTTP response pending until cancellation.
            probing.TrySetResult();
            return Task.FromResult<int?>(null);
        });
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["idle"], TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)),
            Directory.GetCurrentDirectory(),
            new Readiness(server.Url));
        using var cancel = new CancellationTokenSource();
        var pending = app.StartAsync(cancel.Token);
        try
        {
            await probing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancel.CancelAsync();

            // The start ends by cancellation, promptly.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(phase == "startup", app.Spawned);
        }
        finally
        {
            await cancel.CancelAsync();
            await pending.ContinueWith(_ => { }, TaskScheduler.Default);
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Stops_waiting_for_readiness_and_takes_the_process_down_when_the_signal_aborts()
    {
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["idle"], TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)),
            Directory.GetCurrentDirectory(),
            new Readiness("http://127.0.0.1:1/"));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var startedAt = DateTime.UtcNow;

        // A cancelled token surfaces as cancellation, not as a start that quietly returns.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.StartAsync(cancel.Token));
        Assert.True(DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(5));
        await app.StopAsync();
    }
}
