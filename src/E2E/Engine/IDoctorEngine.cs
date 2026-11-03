// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// An engine with upstream's optional <c>doctor</c> hook (declared in <c>engine/index.ts</c>): what <c>e2e doctor</c> asks once per
/// target, instead of starting a run. Without it the target gets one <c>engine</c> check with status <c>skip</c>.
/// .NET-only name: upstream's <c>Engine</c> declares the member, and the port keeps its optional hooks in their own interfaces
/// (<see cref="IPreparableEngine"/>), so an engine without the hook compiles unchanged.
/// </summary>
public interface IDoctorEngine : IEngine
{
    /// <summary>
    /// Checks this machine for what a run on the target needs. Read-only and quick: never install, launch, lease, or start anything a
    /// check could instead look up, and never call <see cref="IPreparableEngine.PrepareAsync"/>. A check that cannot tell reports nothing
    /// rather than guess. A throw is reported as one failed <c>engine</c> check of the target. <c>e2e doctor</c> gives the call 30 seconds
    /// and cancels <paramref name="cancellationToken"/> then, or when it is interrupted; return within 15 seconds of it.
    /// </summary>
    Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken);
}
