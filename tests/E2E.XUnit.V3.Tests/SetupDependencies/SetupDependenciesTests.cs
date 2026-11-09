// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.SetupDependencies;

/// <summary>
/// .NET-only: upstream's runner schedules a <c>test.setup</c> before each test that declares <c>{ session }</c> (#1244). Here the first test that restores
/// a session nobody saved runs the setup test that declares it, once per run. Each test runs its consumer class in a test process of its own.
/// </summary>
public sealed class SetupDependenciesTests
{
    [Fact]
    public async Task A_test_that_restores_a_session_runs_the_setup_test_that_saves_it_once()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(ConsumerFixture));

        Assert.True(results["Consumes_the_dependency_session"].Result == "Pass", results["Consumes_the_dependency_session"].Message);
        Assert.True(results["Consumes_the_dependency_session_again"].Result == "Pass", results["Consumes_the_dependency_session_again"].Message);
        Assert.Equal(1, output.Split("setup-ran").Length - 1);
    }

    [Fact]
    public async Task A_failed_setup_test_fails_each_dependent_without_running_again()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(BrokenConsumerFixture));

        Assert.Equal("Fail", results["Consumes_the_broken_session"].Result);
        Assert.Equal("Fail", results["Consumes_the_broken_session_again"].Result);
        Assert.Contains("session \"broken\" was not produced", results["Consumes_the_broken_session"].Message);
        Assert.Equal(1, output.Split("broken-setup-ran").Length - 1);
    }
}
