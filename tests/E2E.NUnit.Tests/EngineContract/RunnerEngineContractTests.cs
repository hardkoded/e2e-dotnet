// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.EngineContract;

/// <summary>
/// The saved-session <c>it</c>s of <c>describe('runner &lt;-&gt; engine contract')</c> in upstream's <c>engine-contract.test.ts</c>, run through
/// NUnit with <c>[E2ESetup]</c> and <c>[E2ESession]</c>. The port has no collector, so a setup test runs whether or not a selected test needs its
/// session, and the state capability is checked when a session is consumed, not before the run: both <c>it</c>s about that are partial.
/// </summary>
public sealed class RunnerEngineContractTests
{
    [OneTimeTearDown]
    public void RemoveSessions() => SessionRun.Cleanup();

    [Test]
    public void Partial_A_session_consumed_on_a_target_without_a_state_capability_fails_before_its_setup_test_saves()
    {
        SessionFixtureBase.Spy = new StateSpyEngine(new WebEngine(new WebEngineOptions()), state: false);

        var results = NestedSessionRun.Run("capture session", nameof(SessionContractFixture.Consumes_session));

        // Upstream fails the run with COLLECTION_ERROR before any setup runs, and no attempt starts. NUnit has no such step: the setup fails
        // at session.save(), and the consumer, which no setup saved, fails when it is consumed.
        Assert.That(results["capture session"].ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(results["capture session"].Message, Does.Contain("has no engine state capability for session.save()"));
        Assert.That(results[nameof(SessionContractFixture.Consumes_session)].ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(results[nameof(SessionContractFixture.Consumes_session)].Message, Does.Contain("has no engine state capability for session restore"));
        Assert.That(SessionFixtureBase.Spy.Captured, Is.Empty);
    }

    [Test]
    public void Partial_Round_trips_captured_state_into_the_dependent_attempt_via_state_restore()
    {
        // Partial: the engine is the real web engine behind a recording wrapper, so the state is a real browser state ("playwright-storage-state")
        // and the check that the dependent sees the cookie replaces upstream's check of the fake state's data.
        var spy = SessionFixtureBase.Spy = new StateSpyEngine(new WebEngine(new WebEngineOptions()));
        SessionContractFixture.Seen = null;

        var results = NestedSessionRun.Run("capture session", nameof(SessionContractFixture.Consumes_session));

        Assert.That(results["capture session"].ResultState.Status, Is.EqualTo(TestStatus.Passed), results["capture session"].Message);
        Assert.That(results[nameof(SessionContractFixture.Consumes_session)].ResultState.Status, Is.EqualTo(TestStatus.Passed), results[nameof(SessionContractFixture.Consumes_session)].Message);
        Assert.That(spy.Captured, Has.Count.EqualTo(1));
        Assert.That(spy.Restored, Has.Count.EqualTo(1));
        Assert.That(spy.Restored[0].Format, Is.EqualTo("playwright-storage-state"));
        Assert.That(SessionContractFixture.Seen, Is.EqualTo("admin"));
    }

    [Test]
    public void Ends_the_attempt_when_session_restore_fails_after_it_started()
    {
        var spy = SessionFixtureBase.Spy = new StateSpyEngine(new WebEngine(new WebEngineOptions()), failRestore: true);

        var results = NestedSessionRun.Run("capture session", nameof(SessionContractFixture.Consumes_session));

        var consumer = results[nameof(SessionContractFixture.Consumes_session)];
        Assert.That(consumer.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(consumer.Message, Does.Contain("cannot seed storage"));
        Assert.That(consumer.Message, Does.Contain("ENGINE_FAILURE"));
        Assert.That(spy.Ended, Is.EqualTo(spy.Started));
    }

    [Test]
    public void A_setup_test_breaks_the_session_contract_when_it_saves_wrongly()
    {
        // .NET-only: upstream's session contract is checked by its collector and by SessionStaging; the NUnit surface needs its own tests.
        SessionFixtureBase.Spy = new StateSpyEngine(new WebEngine(new WebEngineOptions()));

        var results = NestedSessionRun.Run("saves nothing", "saves twice", "saves an undeclared name", nameof(SessionContractBreaksFixture.Saves_outside_a_setup_test), nameof(SessionContractBreaksFixture.Restores_a_session_nobody_saved));

        Assert.That(results["saves nothing"].Message, Does.Contain("setup must save each declared session exactly once; missing: [acct]"));
        Assert.That(results["saves twice"].Message, Does.Contain("session \"twice\" saved twice"));
        Assert.That(results["saves an undeclared name"].Message, Does.Contain("session \"other\" was not declared by this setup test"));
        Assert.That(results[nameof(SessionContractBreaksFixture.Saves_outside_a_setup_test)].Message, Does.Contain("only available in a setup test"));
        Assert.That(results[nameof(SessionContractBreaksFixture.Restores_a_session_nobody_saved)].Message, Does.Contain("session \"never-saved\" was not produced"));
    }
}
