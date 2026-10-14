// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ExpectSoft;

/// <summary>
/// The port's soft assertions take locator matchers only, so the value-matcher parts of upstream's describe are not
/// ported. Not ported at all: "throws like expect outside an attempt" (it checks <c>expect.soft(1).toBe(2)</c>, and a
/// locator matcher outside a session cannot be written), "keeps a usage failure too, since the matcher could not pass"
/// (only <c>ASSERTION_FAILED</c> is kept, and the port has no value matcher to misuse), and the <c>toMatchSchema</c>
/// describe. The tests below that name what they check are partial: each runs a locator matcher where upstream runs a value
/// matcher, and none reads a JavaScript stack.
/// </summary>
public sealed class ExpectSoftTests
{
    [Fact]
    public async Task Throws_at_once_from_an_afterEach_hook_where_nothing_collects_soft_failures()
    {
        // Partial: the hook's matcher is a locator matcher, so the message is the locator failure, not "expected 1 to be 2".
        await using var session = await StartAsync();
        await session.App.OpenAsync("/settings/billing");
        Assert.Null(session.CloseSoftFailures());
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.Soft(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync());
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("toBeVisible failed", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: the failures come from locator matchers, and there is no label option or value matcher among them.</summary>
    [Fact]
    public async Task Keeps_locator_failures_lets_the_caller_continue_and_numbers_each_in_order_under_one_header()
    {
        await using var session = await StartAsync();
        await session.App.OpenAsync("/settings/billing");
        var screen = session.Screen;

        await Expect.Soft(screen.GetByRole("status", "Pro")).ToBeVisibleAsync();
        await Expect.Soft(screen.GetByRole("button", "Upgrade to Pro")).Not.ToBeVisibleAsync();
        await Expect.Soft(screen.GetByRole("heading", "Billing")).ToBeVisibleAsync();
        await Expect.Soft(screen.GetByRole("heading", "Billing")).ToHaveTextAsync("Invoices");

        var error = Assert.IsType<TestException>(session.CloseSoftFailures());
        Assert.Equal("ASSERTION_FAILED", error.Code);
        var headings = error.Message.Split('\n').Where(line => !line.StartsWith(' ')).ToArray();
        Assert.Equal("3 soft assertions failed", headings[0]);
        Assert.StartsWith("1. ", headings[1], StringComparison.Ordinal);
        Assert.Contains("toBeVisible failed", headings[1], StringComparison.Ordinal);
        Assert.StartsWith("2. ", headings[2], StringComparison.Ordinal);
        Assert.Contains("toBeVisible failed", headings[2], StringComparison.Ordinal);
        Assert.StartsWith("3. ", headings[3], StringComparison.Ordinal);
        Assert.Contains("toHaveText failed", headings[3], StringComparison.Ordinal);
        Assert.Equal(4, headings.Length);
    }

    /// <summary>Partial: the failing matcher is a locator matcher, not <c>expect.soft(1).toBe(2)</c>.</summary>
    [Fact]
    public async Task Closes_with_nothing_when_every_soft_locator_matcher_passed_and_a_lone_failure_reads_in_the_singular()
    {
        await using var passed = await StartAsync();
        await passed.App.OpenAsync("/settings/billing");
        await Expect.Soft(passed.Screen.GetByRole("heading", "Billing")).ToBeVisibleAsync();
        Assert.Null(passed.CloseSoftFailures());

        await using var failed = await StartAsync();
        await failed.App.OpenAsync("/settings/billing");
        await Expect.Soft(failed.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync();
        var error = Assert.IsType<TestException>(failed.CloseSoftFailures());
        Assert.StartsWith("1 soft assertion failed\n1. ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Indents_a_multi_line_failure_under_its_number()
    {
        var failures = new SoftFailures();

        failures.Keep(new TestException("ASSERTION_FAILED", "expect.toHaveText failed\nlocator: getByRole('status')"));

        Assert.Equal("1 soft assertion failed\n1. expect.toHaveText failed\n   locator: getByRole('status')", failures.Close()?.Message);
    }

    /// <summary>Partial: the port keeps the first failure as the inner exception, where upstream points the stack at the first <c>expect.soft</c> line.</summary>
    [Fact]
    public void Unwinds_through_the_first_failure_by_keeping_it_as_the_inner_exception()
    {
        var failures = new SoftFailures();
        var first = new TestException("ASSERTION_FAILED", "first");
        failures.Keep(first);
        failures.Keep(new TestException("ASSERTION_FAILED", "second"));

        var error = failures.Close();

        Assert.Same(first, error?.InnerException);
    }

    /// <summary>Partial: the failure is kept by hand, where upstream gets it from <c>expect.soft(1).toBe(2)</c>.</summary>
    [Fact]
    public void Hands_each_kept_failure_out_once_so_a_second_close_returns_nothing()
    {
        var failures = new SoftFailures();
        failures.Keep(new TestException("ASSERTION_FAILED", "expected 1 to be 2"));

        Assert.Contains("expected 1 to be 2", failures.Close()?.Message, StringComparison.Ordinal);
        Assert.Null(failures.Close());
    }

    /// <summary>Partial: the port softens its locator matchers by method and has no proxy, so no getter or <c>boom()</c> call passes through.</summary>
    [Fact]
    public async Task Keeps_an_async_assertion_failure_softens_not_and_lets_other_errors_through()
    {
        var twins = new DocumentWorld().Map("/twins", page =>
        {
            page.Button("Save", () => { });
            page.Button("Save", () => { });
            page.Heading("Billing");
        });
        await using var session = await StartAsync(twins);
        await session.App.OpenAsync("/twins");
        var screen = session.Screen;

        await Expect.Soft(screen.GetByRole("heading", "Billing")).Not.ToBeVisibleAsync();
        await Expect.Soft(screen.GetByRole("heading", "Billing")).ToHaveCountAsync(1);
        var other = await Assert.ThrowsAsync<TestException>(() => Expect.Soft(screen.GetByRole("button", "Save")).ToBeVisibleAsync());
        Assert.Equal("STRICT_MODE", other.Code);

        var error = Assert.IsType<TestException>(session.CloseSoftFailures());
        Assert.StartsWith("1 soft assertion failed\n1. ", error.Message, StringComparison.Ordinal);
        Assert.Contains("toBeVisible failed", error.Message, StringComparison.Ordinal);
    }

    private static Task<E2ESession> StartAsync(DocumentWorld? world = null)
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
        });
    }
}
