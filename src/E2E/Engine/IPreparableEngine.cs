// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// An engine with upstream's optional <c>prepare</c> and <c>finish</c> hooks (declared in <c>engine/index.ts</c>): what a run provisions
/// once per target before any worker starts, such as a hosted device, and gives back after the last worker ended, so the provisioning
/// happens once per run instead of per worker. The port has no test runner that calls them: a host that starts the engine on its own, as
/// <c>e2e explore</c> and <c>e2e mcp</c> do, calls them around the one attempt (upstream's <c>openStandaloneAttempt</c>), prepare when
/// the session opens and finish when it closes, on every path; the mobile adapters' <c>MobileAppTest</c> runs them once per test process.
/// .NET-only name: upstream tests the engine handle for the members. <see cref="WebEngine"/> does not implement it, because it leases
/// for the attempt it starts.
/// </summary>
public interface IPreparableEngine : IEngine
{
    /// <summary>
    /// Runs once per run and target, before any worker, app command, and the run's clock start. Download dependencies or boot devices
    /// here, for exactly <see cref="EnginePrepareInfo.Slots"/> worker slots, and report progress through <see cref="EnginePrepareInfo.Log"/>.
    /// No launch timeout applies: <paramref name="cancellationToken"/> cancels on an interrupt only, because a first-run download is as
    /// long as the network makes it. A failure ends the run before any test: an <see cref="E2EException"/> keeps its code, a cancel stays
    /// a cancel (<c>CANCELLED</c> when <paramref name="cancellationToken"/> asked for it), an <see cref="OperationCanceledException"/> is kept as it is
    /// when <paramref name="cancellationToken"/> was not canceled, and any other exception is <c>ENGINE_FAILURE</c>.
    /// The result can lower the target's worker cap (upstream's contract; no host in this repository reads it), for an engine that only learns its capacity here, and hands the workers an
    /// environment (<see cref="EnginePrepareResult.Env"/>).
    /// </summary>
    Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken);

    /// <summary>
    /// Runs once per run and target, after every worker of the target ended, on every exit path: a passing run, a failure, an interrupt,
    /// or a <see cref="PrepareAsync"/> that threw part-way. Release what <see cref="PrepareAsync"/> acquired for the run here (a leased
    /// hosted device, billed by the minute); what one worker holds belongs in its session's dispose. Runs only when <see cref="PrepareAsync"/>
    /// was called, within <see cref="EngineFinishInfo.CleanupTimeout"/>, and is never cancelled by an interrupt: an interrupted run is
    /// exactly when a leased device must still be released. <paramref name="cancellationToken"/> cancels when the budget is spent. A
    /// failure is a cleanup error, reported with the attempt's other cleanup failures: <c>e2e explore</c> ends with it in place of the
    /// outcome, and <c>close_session</c> shows it as a <c>Cleanup:</c> line.
    /// </summary>
    Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken);
}
