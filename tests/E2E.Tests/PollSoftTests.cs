// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests;

public sealed class PollSoftTests
{
    internal static readonly PollOptions Fast = new() { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(10) };

    internal static readonly PollOptions Short = new() { Timeout = TimeSpan.FromMilliseconds(100), Interval = TimeSpan.FromMilliseconds(10) };

    [Fact]
    public async Task Poll_retries_a_read_that_throws()
    {
        var reads = 0;
        await Expect.Poll(() => ++reads < 3 ? throw new InvalidOperationException("not ready") : "ready", Fast).ToBeAsync("ready");
        Assert.Equal(3, reads);
    }

    [Fact]
    public async Task Poll_times_out_with_the_last_value_and_message()
    {
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect.Poll(() => 1, new PollOptions { Timeout = TimeSpan.FromMilliseconds(100), Interval = TimeSpan.FromMilliseconds(10), Message = "count never grew" })
                .ToBeAsync(2));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal("expect.poll(...).toBe(...) timed out after 100 ms\ncount never grew\nlast: received 1, expected 2", error.Message);
    }

    [Fact]
    public async Task Poll_reports_the_last_read_error()
    {
        Func<int> read = () => throw new InvalidOperationException("offline");
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.Poll(read, Short).ToBeAsync(1));
        Assert.EndsWith("last: offline", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Soft_failures_let_the_body_run_on_and_fail_together()
    {
        await using var session = await StartAsync();
        await session.App.OpenAsync("/settings/billing");
        await Expect.Soft(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync();
        await Expect.Soft(session.Screen.GetByRole("heading", "Billing")).ToHaveTextAsync("Invoices");
        await Expect.Soft(session.Screen.GetByRole("heading", "Billing")).ToBeVisibleAsync();

        var error = session.CloseSoftFailures();
        Assert.NotNull(error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.StartsWith("2 soft assertions failed\n1. expect(", error.Message, StringComparison.Ordinal);
        Assert.Contains("\n2. expect(", error.Message, StringComparison.Ordinal);
        Assert.Null(session.CloseSoftFailures());
    }

    [Fact]
    public async Task Soft_wraps_negation_timeouts_and_every_matcher()
    {
        await using var session = await StartAsync();
        await session.App.OpenAsync("/settings/billing");
        var heading = session.Screen.GetByRole("heading", "Billing");
        var shortWait = TimeSpan.FromMilliseconds(100);
        await Expect.Soft(heading).Not.ToBeVisibleAsync(timeout: shortWait);
        await Expect.Soft(heading).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Invoices$"), timeout: shortWait);
        await Expect.Soft(heading).ToHaveAttributeAsync("data-missing", shortWait);
        await Expect.Soft(heading).ToBeFocusedAsync(shortWait);
        await Expect.Soft(heading).Not.ToHaveTextAsync("Invoices", timeout: shortWait);

        var error = session.CloseSoftFailures();
        Assert.NotNull(error);
        Assert.StartsWith("4 soft assertions failed\n", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Soft_still_throws_on_strict_mode()
    {
        var world = new DocumentWorld().Map("/twins", page =>
        {
            page.Button("Save");
            page.Button("Save");
        });
        await using var session = await StartAsync(world);
        await session.App.OpenAsync("/twins");
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.Soft(session.Screen.GetByRole("button", "Save")).ToBeVisibleAsync());
        Assert.Equal("STRICT_MODE", error.Code);
        Assert.Null(session.CloseSoftFailures());
    }

    [Fact]
    public async Task Soft_failures_go_to_the_handler_when_there_is_one()
    {
        var handled = new List<TestException>();
        await using var session = await StartAsync(onSoftFailure: handled.Add);
        await session.App.OpenAsync("/settings/billing");
        await Expect.Soft(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync();

        var failure = Assert.Single(handled);
        Assert.Equal("ASSERTION_FAILED", failure.Code);
        Assert.Null(session.CloseSoftFailures());
    }

    internal static Task<E2ESession> StartAsync(DocumentWorld? world = null, Action<TestException>? onSoftFailure = null)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world ?? BillingWorld.Create()),
            BaseUrl = "https://billing.test",
            CacheEnabled = false,
            TestTitle = "billing > soft",
            AssertionTimeout = TimeSpan.FromMilliseconds(100),
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(10),
            OnSoftFailure = onSoftFailure,
        });
    }
}
