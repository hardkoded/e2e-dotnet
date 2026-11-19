// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.TelemetryRegistry;

/// <summary>Serializes the tests that touch the global registry and clears it before each one (the upstream <c>beforeEach</c>).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalTelemetryCollection
{
    public const string Name = "GlobalTelemetry";
}

public abstract class GlobalTelemetryTestBase : IDisposable
{
    protected GlobalTelemetryTestBase() => Sdk.TelemetryRegistry.Reset();

    public void Dispose() => Sdk.TelemetryRegistry.Reset();

    public sealed class Integration : ITelemetry
    {
        public int Starts { get; private set; }

        public Task OnDecideStartAsync(DecideStartEvent @event)
        {
            Starts++;
            return Task.CompletedTask;
        }
    }
}
