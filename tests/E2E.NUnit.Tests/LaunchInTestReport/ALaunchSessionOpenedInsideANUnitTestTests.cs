// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.NUnit.Tests.LaunchInTestReport;

/// <summary>
/// .NET-only, no upstream <c>it</c> (follow-up of #1889): upstream finds the running attempt through one slot per process, the port through an
/// async-local slot that <c>E2EFixture.StartSessionAsync</c> reserves. NUnit starts the session in <c>[SetUp]</c>, so the slot has to reach the test body.
/// </summary>
public sealed class ALaunchSessionOpenedInsideANUnitTestTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Records_the_steps_of_a_launched_session_in_the_test()
    {
        await App.OpenAsync();
        var host = await Launch.LaunchAsync(
            new WebEngine(new WebEngineOptions()),
            new LaunchOptions { App = new LaunchApp { Url = E2E.Playground.Testbed.Url } }
        );
        var second = await host.NewSessionAsync();
        await second.App.OpenAsync("/");
        await host.CloseAsync();

        Assert.That(CurrentSession!.StepRecords.Select(step => step.Actor), Does.Contain("launch-1"));
    }
}
