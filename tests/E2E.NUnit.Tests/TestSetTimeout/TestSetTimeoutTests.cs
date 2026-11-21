// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.TestSetTimeout;

/// <summary>
/// <c>describe('test.setTimeout')</c> of upstream's <c>tests/integration/test-set-timeout.test.ts</c> (tester-army/e2e#1921), whose tests run the CLI over a
/// project. These run the fixtures through NUnit and read each result. The port's clock starts before the engine launches, so the timeouts are longer than
/// upstream's (10 s and 60 s for 300 ms and 30 s) and the bodies sleep past them. The first <c>it</c> is <c>Partial_</c>: it leaves out the <c>beforeAll</c> and
/// <c>afterAll</c> hooks (<c>describe('raised from beforeAll')</c>, <c>'lowered from beforeAll'</c>, <c>'from afterAll'</c>: the port has no suite hooks
/// that <c>E2E</c> times, NUnit owns <c>[OneTimeSetUp]</c>), the run's <c>report.json</c> (the results are NUnit's, read from each result), and the
/// duration bound of the lowered tests (they check the message). The second <c>it</c> (a <c>beforeAll</c> on the attempt's budget and the worker fixture
/// teardown) and the third (a worker's watchdog) need those hooks and the worker process, which the port does not have. The fourth is <c>Partial_</c>: NUnit's
/// <c>Assert.Ignore</c> stands for <c>test.fixme</c>, so it checks the skip and its time, not the <c>fixme</c> cause. The fifth is <c>Partial_</c>: the port throws
/// <see cref="InvalidOperationException"/> where upstream collects a <c>COLLECTION_ERROR</c>.
/// </summary>
public sealed class TestSetTimeoutTests
{
    [Test]
    public void Partial_Replaces_the_running_attempts_timeout_from_a_body_or_beforeEach_hook_and_refuses_afterEach_and_bad_values()
    {
        var raises = NestedRun.Run(nameof(ShortTimeoutFixture.SetTimeout_raises_the_timeout));
        Assert.That(raises.ResultState.Status, Is.EqualTo(TestStatus.Passed), raises.Message);

        var outlasts = NestedRun.Run(nameof(RaisedFromBeforeEachFixture.SetTimeout_outlasts_its_own_timeout));
        Assert.That(outlasts.ResultState.Status, Is.EqualTo(TestStatus.Passed), outlasts.Message);

        var lowered = NestedRun.Run(nameof(LongTimeoutFixture.SetTimeout_lowers_the_timeout));
        Assert.That(lowered.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(lowered.Message, Does.Contain("test timed out after 10000 ms in phase body"));

        var spent = NestedRun.Run(nameof(LongTimeoutFixture.SetTimeout_below_the_time_spent_times_out_at_once));
        Assert.That(spent.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(spent.Message, Does.Contain("test timed out after 100 ms"));

        var zero = NestedRun.Run(
            nameof(LongTimeoutFixture.SetTimeout_refuses_a_timeout_that_is_not_a_positive_integer)
        );
        Assert.That(zero.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(
            zero.Message,
            Does.Contain("SetTimeout() timeout must be a positive whole number of milliseconds, got 0")
        );

        var fraction = NestedRun.Run(nameof(LongTimeoutFixture.SetTimeout_refuses_a_fraction));
        Assert.That(fraction.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(fraction.Message, Does.Contain("got 1.5"));

        var late = NestedRun.Run(nameof(FromAfterEachFixture.SetTimeout_is_refused));
        Assert.That(late.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(late.Message, Does.Contain("cannot change the timeout from an afterEach hook"));
    }

    [Test]
    public void Partial_Skips_a_test_as_fixme_when_it_reaches_test_fixme_after_the_timeout_it_raised()
    {
        var result = NestedRun.Run(nameof(ShortTimeoutFixture.SetTimeout_fixme_past_the_first_timeout));
        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Skipped), result.Message);

        // The skip came after the 10 s the test started with.
        Assert.That(result.Duration, Is.GreaterThanOrEqualTo(12));
    }

    [Test]
    public void Partial_Rejects_a_call_outside_a_running_test_at_collection()
    {
        var outside = Assert.Throws<InvalidOperationException>(() =>
            new LongTimeoutFixture().SetTimeout(TimeSpan.FromSeconds(60))
        );
        Assert.That(outside!.Message, Does.Contain("must be called inside a test body"));
    }
}
