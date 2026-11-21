// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.NUnit.Tests.LaunchInSerialGroup;

/// <summary>
/// .NET-only, no upstream <c>it</c> (follow-up of #1930, #1974): the later tests of a serial group share the first test's session, so their setup
/// starts none. The adapter hands the session's attempt slot to the flow of each later test, so a host its body launches records into that attempt.
/// </summary>
[Serial]
public sealed class ALaunchSessionOpenedInALaterTestOfASerialGroupTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task First_test_opens_the_page()
    {
        await App.OpenAsync();
    }

    [Test, DependsOnTest(nameof(First_test_opens_the_page))]
    public async Task Records_the_steps_of_a_session_launched_in_the_second_test()
    {
        var host = await Launch.LaunchAsync(
            new WebEngine(new WebEngineOptions()),
            new LaunchOptions { App = new LaunchApp { Url = E2E.Playground.Testbed.Url } }
        );
        try
        {
            var second = await host.NewSessionAsync();
            await second.App.OpenAsync("/");
        }
        finally
        {
            await host.CloseAsync();
        }

        Assert.That(CurrentSession!.StepRecords.Select(step => step.Actor), Does.Contain("launch-1"));
    }

    [Test, DependsOnTest(nameof(Records_the_steps_of_a_session_launched_in_the_second_test))]
    public async Task Abandons_what_the_third_test_left_running_on_a_host_it_launched()
    {
        var host = await Launch.LaunchAsync(
            new WebEngine(new WebEngineOptions()),
            new LaunchOptions { App = new LaunchApp { Url = E2E.Playground.Testbed.Url } }
        );
        var pending = host.NewSessionAsync();
        try
        {
            // The earlier members already settled once; this member's body ends at its own first settle.
            var error = await Assert.ThrowsAsync<TestException>(() => CurrentSession!.SettleAttemptAsync());
            Assert.That(error!.Code, Is.EqualTo("STEP_NOT_AWAITED"));
        }
        finally
        {
            await CurrentSession!.QuiesceGuestsAsync();
            await pending.ContinueWith(_ => { }, TaskScheduler.Default);
            await host.CloseAsync();
        }
    }
}
