// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit.Tests.SerialSession;

/// <summary>
/// .NET-only: no upstream <c>it</c> covers a stale <c>SetTimeout</c> callback reaching another test, or a serial member counting its timeout from
/// its own start (upstream's serial groups belong to its runner, tester-army/e2e#1056, and the port leaves them to NUnit; hardkoded/e2e-dotnet#2023,
/// #2055). Runs <see cref="SerialSetTimeoutFixture"/> in a runner of its own and reads each member's result.
/// </summary>
public sealed class SerialSetTimeoutTests
{
    [Test]
    public void A_stale_SetTimeout_callback_cannot_move_the_timeout_of_a_later_member_and_each_member_counts_its_own_timeout()
    {
        var results = Task.Run(() =>
            {
                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(typeof(SerialSetTimeoutFixture).Assembly, new Dictionary<string, object>());
                var run = runner.Run(
                    TestListener.NULL,
                    TestFilter.FromXml(
                        $"<filter><class>{typeof(SerialSetTimeoutFixture).FullName}</class></filter>"
                    )
                );
                return Leaves(run).ToDictionary(result => result.Test.MethodName!);
            })
            .GetAwaiter()
            .GetResult();

        foreach (var name in new[]
        {
            nameof(SerialSetTimeoutFixture.A_keeps_its_attempt_for_a_later_member),
            nameof(SerialSetTimeoutFixture.B_is_not_moved_by_the_callback_the_first_member_left),
            nameof(SerialSetTimeoutFixture.C_counts_its_set_timeout_from_its_own_start),
        })
        {
            Assert.That(results[name].ResultState.Status, Is.EqualTo(TestStatus.Passed), name + ": " + results[name].Message);
        }
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) =>
        result.HasChildren ? result.Children.SelectMany(Leaves) : [result];
}

[Explicit("Run by SerialSetTimeoutTests")]
[Serial]
public sealed class SerialSetTimeoutFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    private static PublishedAttempt? s_firstAttempt;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // Each member has 10 s from its own start; the three members run for more than that together.
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(10);

    [Test]
    public async Task A_keeps_its_attempt_for_a_later_member()
    {
        s_firstAttempt = E2E.Attempts.CurrentAttempt();
        Assert.That(s_firstAttempt, Is.Not.Null);
        await Task.Delay(TimeSpan.FromSeconds(6));
    }

    [Test, DependsOnTest(nameof(A_keeps_its_attempt_for_a_later_member))]
    public async Task B_is_not_moved_by_the_callback_the_first_member_left()
    {
        // The first member's code, still running: it fails and leaves this member's timeout where it was.
        var stale = Assert.Throws<TestException>(() =>
            E2E.Internal.TestTimeout.SetRunningTestTimeout(s_firstAttempt!, TimeSpan.FromMilliseconds(100))
        );
        Assert.That(stale!.Message, Does.Contain("code left running after its test ended"));
        await Task.Delay(TimeSpan.FromSeconds(6));
    }

    [Test, DependsOnTest(nameof(B_is_not_moved_by_the_callback_the_first_member_left))]
    public async Task C_counts_its_set_timeout_from_its_own_start()
    {
        // 18 s into the group, 10 s from this member's start is still ahead.
        SetTimeout(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(6));
    }
}
