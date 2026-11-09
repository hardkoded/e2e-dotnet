// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.NUnit.Tests.EngineContract;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.SetupDependencies;

/// <summary>
/// .NET-only: upstream's runner schedules a <c>test.setup</c> before each test that declares <c>{ session }</c> (#1244). Here the first test that restores
/// a session nobody saved runs the setup test that declares it, once per run, in NUnit and in xUnit alike.
/// </summary>
public sealed class SetupDependenciesTests
{
    [OneTimeTearDown]
    public void RemoveSessions() => SessionRun.Cleanup();

    [SetUp]
    public void Reset()
    {
        SessionRun.Cleanup();
        SessionFixtureBase.Spy = new StateSpyEngine(new WebEngine(new WebEngineOptions()));
        DependencySetupFixture.Runs = 0;
        BrokenSetupFixture.Runs = 0;
        AConsumerFixture.Seen = null;
    }

    [Test]
    public void A_test_that_restores_a_session_runs_the_setup_test_that_saves_it_once()
    {
        var results = NestedSessionRun.Run(nameof(AConsumerFixture.Consumes_the_dependency_session), nameof(AConsumerFixture.Consumes_the_dependency_session_again));

        Assert.That(results[nameof(AConsumerFixture.Consumes_the_dependency_session)].ResultState.Status, Is.EqualTo(TestStatus.Passed), results[nameof(AConsumerFixture.Consumes_the_dependency_session)].Message);
        Assert.That(results[nameof(AConsumerFixture.Consumes_the_dependency_session_again)].ResultState.Status, Is.EqualTo(TestStatus.Passed), results[nameof(AConsumerFixture.Consumes_the_dependency_session_again)].Message);
        Assert.That(AConsumerFixture.Seen, Is.EqualTo("dependent"));
        Assert.That(DependencySetupFixture.Runs, Is.EqualTo(1));
    }

    [Test]
    public void The_setup_test_does_not_run_again_when_the_run_reaches_it()
    {
        var results = NestedSessionRun.Run(nameof(AConsumerFixture.Consumes_the_dependency_session), "save the dependency session");

        Assert.That(results[nameof(AConsumerFixture.Consumes_the_dependency_session)].ResultState.Status, Is.EqualTo(TestStatus.Passed), results[nameof(AConsumerFixture.Consumes_the_dependency_session)].Message);
        Assert.That(DependencySetupFixture.Runs, Is.EqualTo(1));
    }

    [Test]
    public void A_failed_setup_test_fails_each_dependent_without_running_again()
    {
        var results = NestedSessionRun.Run(nameof(BrokenSetupFixture.Consumes_the_broken_session), nameof(BrokenSetupFixture.Consumes_the_broken_session_again));

        foreach (var name in new[] { nameof(BrokenSetupFixture.Consumes_the_broken_session), nameof(BrokenSetupFixture.Consumes_the_broken_session_again) })
        {
            Assert.That(results[name].ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(results[name].Message, Does.Contain("SESSION_UNAVAILABLE").Or.Contain("session \"broken\" was not produced"));
        }

        Assert.That(BrokenSetupFixture.Runs, Is.EqualTo(1));
    }
}
