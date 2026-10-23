// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// An engine with upstream's optional <c>prepare</c> and <c>finish</c> hooks (<c>surface.ts</c>): what a run provisions once per target
/// before any worker starts, such as a hosted device, and gives back after the last worker ended. A host that starts the engine on its own,
/// as <c>e2e explore</c> and <c>e2e mcp</c> do, calls them around the one attempt (<c>Standalone</c>). .NET-only name: upstream
/// tests the engine handle for the members. <see cref="WebEngine"/> does not implement it, because it leases for the attempt it starts.
/// </summary>
public interface IPreparableEngine : IEngine
{
    /// <summary>Provisions the target for <see cref="EnginePrepareInfo.Slots"/> worker slots. The result's environment is what the workers read.</summary>
    Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken);

    /// <summary>Gives back what <see cref="PrepareAsync"/> acquired, even when it failed part-way. Runs within <see cref="EngineFinishInfo.CleanupTimeout"/>.</summary>
    Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken);
}
