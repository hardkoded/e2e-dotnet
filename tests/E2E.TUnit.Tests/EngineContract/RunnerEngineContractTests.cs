// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.TUnit.Tests.EngineContract;

/// <summary>
/// The saved-session <c>it</c>s of <c>describe('runner &lt;-&gt; engine contract')</c> in upstream's <c>engine-contract.test.ts</c>, run through
/// TUnit with <c>[E2ESetup]</c> and <c>[E2ESession]</c>. The port has no collector, so a setup test runs whether or not a selected test needs its
/// session, and the state capability is checked when a session is consumed, not before the run: both <c>it</c>s about that are partial. Each
/// test runs its fixture in a test process of its own.
/// </summary>
public sealed class RunnerEngineContractTests
{
    [Test]
    public async Task Partial_A_session_consumed_on_a_target_without_a_state_capability_fails_before_its_setup_test_saves()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(NoStateCapabilityFixture));

        // Upstream fails the run with COLLECTION_ERROR before any setup runs, and no attempt starts. TUnit has no such step: the setup fails
        // at session.save(), and the consumer, which no setup saved, fails when it is consumed.
        await Assert.That(results["CaptureSession"].Result).IsEqualTo("Failed");
        await Assert.That(results["CaptureSession"].Message).Contains("has no engine state capability for session.save()");
        await Assert.That(results["Consumes_session"].Result).IsEqualTo("Failed");
        await Assert.That(results["Consumes_session"].Message).Contains("has no engine state capability for session restore");
    }

    [Test]
    public async Task Partial_Round_trips_captured_state_into_the_dependent_attempt_via_state_restore()
    {
        // Partial: the engine is the real web engine behind a recording wrapper, so the state is a real browser state ("playwright-storage-state")
        // and the check that the dependent sees the cookie replaces upstream's check of the fake state's data. The consumer makes the checks.
        var (results, _) = await NestedSessionRun.RunAsync(typeof(RoundTripFixture));

        await Assert.That(results["CaptureSession"].Result).IsEqualTo("Passed").Because(results["CaptureSession"].Message);
        await Assert.That(results["Consumes_session"].Result).IsEqualTo("Passed").Because(results["Consumes_session"].Message);
    }

    [Test]
    public async Task Ends_the_attempt_when_session_restore_fails_after_it_started()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(FailingRestoreFixture));

        var consumer = results["Consumes_session"];
        await Assert.That(consumer.Result).IsEqualTo("Failed");
        await Assert.That(consumer.Message).Contains("cannot seed storage");
        await Assert.That(consumer.Message).Contains("E2E.EngineException");
        await Assert.That(output).Matches(@"engine-sessions started=(\d+) ended=\1\b");
        await Assert.That(output).DoesNotContain("started=0");
    }

    [Test]
    public async Task A_setup_test_breaks_the_session_contract_when_it_saves_wrongly()
    {
        // .NET-only: upstream's session contract is checked by its collector and by SessionStaging; the TUnit surface needs its own tests.
        var (results, _) = await NestedSessionRun.RunAsync(typeof(SessionContractBreaksFixture));

        await Assert.That(results["SavesNothing"].Message).Contains("setup must save each declared session exactly once; missing: [acct]");
        await Assert.That(results["SavesTwice"].Message).Contains("session \"twice\" saved twice");
        await Assert.That(results["SavesUndeclared"].Message).Contains("session \"other\" was not declared by this setup test");
        await Assert.That(results["Saves_outside_a_setup_test"].Message).Contains("only available in a setup test");
        await Assert.That(results["Restores_a_session_nobody_saved"].Message).Contains("session \"never-saved\" was not produced");
    }
}
