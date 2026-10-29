// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Dialogs;

/// <summary>Upstream's <c>describe('ErrorLatch')</c> (<c>packages/web/tests/unit/dialogs.test.ts</c>).</summary>
public sealed class ErrorLatchTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    [Fact]
    public void Keeps_the_first_error_until_it_is_thrown_then_forgets_it()
    {
        var latch = new ErrorLatch();
        var first = new InvalidOperationException("first");
        latch.Latch(first);
        latch.Latch(new InvalidOperationException("second"));
        Assert.Same(first, Assert.Throws<InvalidOperationException>(latch.ThrowPending));
        latch.ThrowPending();
    }

    [Fact]
    public async Task Settles_with_what_a_tracked_path_still_running_latches_once_it_finishes()
    {
        var latch = new ErrorLatch();
        var router = new DialogRouter(latch);
        var waiting = new TaskCompletionSource();
        router.Add((DialogHandler)(async dialog =>
        {
            await dialog.AcceptAsync();
            await waiting.Task;
            throw new TestException("ASSERTION_FAILED", "late");
        }));
        _ = router.DispatchAsync(new FakeDialog());
        var settling = latch.SettleAsync(Budget, CancellationToken.None);
        waiting.SetResult();
        var error = await Assert.ThrowsAsync<TestException>(() => settling);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        await latch.SettleAsync(Budget, CancellationToken.None);
    }

    [Fact]
    public async Task Fails_closed_with_CLEANUP_TIMEOUT_naming_a_handler_that_outlives_the_budget_and_drops_what_it_throws_later()
    {
        var latch = new ErrorLatch();
        var router = new DialogRouter(latch);
        router.Add((DialogHandler)(async dialog =>
        {
            await dialog.AcceptAsync();
            await Task.Delay(300);
            throw new TestException("ASSERTION_FAILED", "too late to count");
        }));
        var dispatched = router.DispatchAsync(new FakeDialog());
        var error = await Assert.ThrowsAsync<EngineException>(() => latch.SettleAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
        Assert.Equal("CLEANUP_TIMEOUT", error.Code);
        Assert.Contains("a dialog handler was still running", error.Message, StringComparison.Ordinal);
        await dispatched;
        latch.ThrowPending();
        await latch.SettleAsync(Budget, CancellationToken.None);
    }

    [Fact]
    public async Task Waits_only_for_the_paths_running_when_it_was_called_so_one_starting_during_the_wait_is_no_timeout()
    {
        var latch = new ErrorLatch();
        var first = new TaskCompletionSource();
        latch.Track("route", first.Task);
        _ = first.Task.ContinueWith(_ => latch.Track("route", Task.Delay(300)), TaskScheduler.Default);
        var settling = latch.SettleAsync(Budget, CancellationToken.None);
        first.SetResult();
        await settling;
    }

    [Fact]
    public async Task Reports_a_latched_error_at_once_ahead_of_handlers_still_running_and_names_every_kind_still_running_at_the_timeout()
    {
        var latch = new ErrorLatch();
        latch.Latch(new TestException("ASSERTION_FAILED", "landed in time"));
        latch.Track("route", new TaskCompletionSource().Task);
        latch.Track("dialog", new TaskCompletionSource().Task);
        var landed = await Assert.ThrowsAsync<TestException>(() => latch.SettleAsync(TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Equal("ASSERTION_FAILED", landed.Code);

        var bare = new ErrorLatch();
        bare.Track("route", new TaskCompletionSource().Task);
        bare.Track("dialog", new TaskCompletionSource().Task);
        var timedOut = await Assert.ThrowsAsync<EngineException>(() => bare.SettleAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));
        Assert.Contains("a dialog and route handler was still running", timedOut.Message, StringComparison.Ordinal);
    }

    private sealed class FakeDialog : IDialog
    {
        public string DefaultValue => "";

        public string Message => "Proceed?";

        public IPage Page => throw new NotSupportedException();

        public string Type => "confirm";

        public Task AcceptAsync(string? promptText = null) => Task.CompletedTask;

        public Task DismissAsync() => Task.CompletedTask;
    }
}
