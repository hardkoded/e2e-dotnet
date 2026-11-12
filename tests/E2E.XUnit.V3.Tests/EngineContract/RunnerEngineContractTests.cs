// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.XUnit.V3.Tests.EngineContract;

/// <summary>
/// The saved-session <c>it</c>s of <c>describe('runner &lt;-&gt; engine contract')</c> in upstream's <c>engine-contract.test.ts</c>, run through
/// xUnit with <c>[E2ESetup]</c> and <c>[E2ESession]</c>. The port has no collector, so a setup test runs whether or not a selected test needs its
/// session, and the state capability is checked when a session is consumed, not before the run: both <c>it</c>s about that are partial. Each
/// test runs its fixture in a test process of its own.
/// </summary>
public sealed class RunnerEngineContractTests
{
    [Fact]
    public async Task Partial_A_session_consumed_on_a_target_without_a_state_capability_fails_before_its_setup_test_saves()
    {
        var (results, _) = await NestedSessionRun.RunAsync(typeof(NoStateCapabilityFixture));

        // Upstream fails the run with COLLECTION_ERROR before any setup runs, and no attempt starts. xUnit has no such step: the setup fails
        // at session.save(), and the consumer, which no setup saved, fails when it is consumed.
        Assert.Equal("Fail", results["CaptureSession"].Result);
        Assert.Contains("has no engine state capability for session.save()", results["CaptureSession"].Message);
        Assert.Equal("Fail", results["Consumes_session"].Result);
        Assert.Contains("has no engine state capability for session restore", results["Consumes_session"].Message);
    }

    [Fact]
    public async Task Partial_Round_trips_captured_state_into_the_dependent_attempt_via_state_restore()
    {
        // Partial: the engine is the real web engine behind a recording wrapper, so the state is a real browser state ("playwright-storage-state")
        // and the check that the dependent sees the cookie replaces upstream's check of the fake state's data. The consumer makes the checks.
        var (results, _) = await NestedSessionRun.RunAsync(typeof(RoundTripFixture));

        Assert.True(results["CaptureSession"].Result == "Pass", results["CaptureSession"].Message);
        Assert.True(results["Consumes_session"].Result == "Pass", results["Consumes_session"].Message);
    }

    [Fact]
    public async Task Ends_the_attempt_when_session_restore_fails_after_it_started()
    {
        var (results, output) = await NestedSessionRun.RunAsync(typeof(FailingRestoreFixture));

        var consumer = results["Consumes_session"];
        Assert.Equal("Fail", consumer.Result);
        Assert.Contains("cannot seed storage", consumer.Message);
        Assert.Contains("E2E.EngineException", consumer.Message);
        Assert.Matches(@"engine-sessions started=(\d+) ended=\1\b", output);
        Assert.DoesNotContain("started=0", output);
    }

    [Fact]
    public async Task A_setup_test_breaks_the_session_contract_when_it_saves_wrongly()
    {
        // .NET-only: upstream's session contract is checked by its collector and by SessionStaging; the xUnit surface needs its own tests.
        var (results, _) = await NestedSessionRun.RunAsync(typeof(SessionContractBreaksFixture));

        Assert.Contains("setup must save each declared session exactly once; missing: [acct]", results["SavesNothing"].Message);
        Assert.Contains("session \"twice\" saved twice", results["SavesTwice"].Message);
        Assert.Contains("session \"other\" was not declared by this setup test", results["SavesUndeclared"].Message);
        Assert.Contains("only available in a setup test", results["Saves_outside_a_setup_test"].Message);
        Assert.Contains("session \"never-saved\" was not produced", results["Restores_a_session_nobody_saved"].Message);
    }
}
