// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.TUnit.Tests.EngineContract;

namespace E2E.TUnit.Tests.SetupDependencies;

/// <summary>
/// .NET-only: upstream's runner schedules a <c>test.setup</c> before each test that declares <c>{ session }</c> (#1244). Here the first test that restores
/// a session nobody saved runs the setup test that declares it, once per run. Each test runs its consumer class in a test process of its own.
/// </summary>
public sealed class SetupDependenciesTests
{
    [Test]
    public async Task A_test_that_restores_a_session_runs_the_setup_test_that_saves_it_once()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(ConsumerFixture));

        await Assert.That(results["Consumes_the_dependency_session"].Result).IsEqualTo("Passed").Because(results["Consumes_the_dependency_session"].Message);
        await Assert.That(results["Consumes_the_dependency_session_again"].Result).IsEqualTo("Passed").Because(results["Consumes_the_dependency_session_again"].Message);
        await Assert.That(output.Split("setup-ran").Length - 1).IsEqualTo(1);
    }

    [Test]
    public async Task A_failed_setup_test_fails_each_dependent_without_running_again()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(BrokenConsumerFixture));

        await Assert.That(results["Consumes_the_broken_session"].Result).IsEqualTo("Failed");
        await Assert.That(results["Consumes_the_broken_session_again"].Result).IsEqualTo("Failed");
        await Assert.That(results["Consumes_the_broken_session"].Message).Contains("session \"broken\" was not produced");
        await Assert.That(output.Split("broken-setup-ran").Length - 1).IsEqualTo(1);
    }
}
