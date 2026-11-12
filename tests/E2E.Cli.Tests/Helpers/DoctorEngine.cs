// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Cli.Tests.Helpers;

/// <summary>An engine whose doctor runs <paramref name="doctor"/>, for the <c>e2e doctor</c> tests. It starts no session.</summary>
internal sealed class DoctorEngine(string name, Func<EngineDoctorInfo, CancellationToken, Task<IReadOnlyList<EngineDoctorCheck>>> doctor) : IDoctorEngine
{
    public string Name => name;

    public string Platform => "web";

    public string Version => "1.0.0";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("doctor starts no session");

    public Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken) => doctor(info, cancellationToken);
}
