// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.FixturePool;

/// <summary>.NET-only (no upstream <c>it</c>: the cancellation of a worker fixture's setup is a .NET concern): what the pool does when the run is cancelled while a fixture's setup is pending.</summary>
public sealed class FixturePoolTests
{
    [Fact]
    public async Task Releases_a_fixture_whose_setup_a_cancelled_run_stopped_waiting_for()
    {
        var pool = new WorkerFixturePool();
        var gate = new TaskCompletionSource();
        var tornDown = new TaskCompletionSource();
        var fixture = new FixtureDefinition("late", async use =>
        {
            await gate.Task;
            await use("value");
            tornDown.SetResult();
        });
        using var cancel = new CancellationTokenSource();

        var acquiring = pool.AcquireAsync([fixture], cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acquiring);

        // The setup hands its value over after the run stopped waiting: the teardown still releases it.
        gate.SetResult();
        var failures = await pool.CloseAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(failures);
        Assert.True(tornDown.Task.IsCompletedSuccessfully);
    }
}
