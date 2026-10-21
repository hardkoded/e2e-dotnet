// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Limrun;
using E2E.Mobile.DevFlow;

namespace E2E.Mobile;

/// <summary>
/// The hosted device providers <c>E2E_MOBILE_PROVIDER</c> names, for the hosts that ship them: the NUnit and xUnit adapters and the
/// <c>e2e</c> tool. Compiled into each. There is no upstream counterpart: upstream's config holds the provider itself.
/// </summary>
internal static class HostedDeviceProviders
{
    /// <summary>The names <see cref="Create"/> knows.</summary>
    public const string Names = "limrun or devflow";

    /// <summary>
    /// The provider <paramref name="name"/> names, or null when it names none. Nothing is leased here: a provider leases a device when
    /// its engine starts, and releases it when that session ends.
    /// </summary>
    public static IDeviceProvider? Create(string name, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(environment);
        return name.ToLowerInvariant() switch
        {
            // A hosted device that outlives an interrupted run is billed until its hard timeout.
            "limrun" => new LimrunSimulators(new LimrunSimulatorsOptions { HardTimeout = "15m", Ios = new LimrunIosOptions { Model = LimrunIosModel.Iphone } }),
            // The agent inside the running Debug build. E2E_DEVFLOW_PORT names its port; without it the broker is asked, then 9223.
            "devflow" => new DevFlowAgents(new DevFlowAgentsOptions { Port = DevFlowPort(environment), AppName = environment("E2E_DEVFLOW_APP") is { Length: > 0 } app ? app : null }),
            _ => null,
        };
    }

    /// <summary><c>E2E_DEVFLOW_PORT</c>, or null when it is unset. A value that is not a port fails the run, not falls back to discovery.</summary>
    private static int? DevFlowPort(Func<string, string?> environment) => environment("E2E_DEVFLOW_PORT") switch
    {
        null or "" => null,
        var text when int.TryParse(text, out var port) && port is > 0 and <= 65535 => port,
        var text => throw new InvalidOperationException($"E2E_DEVFLOW_PORT must be a port number, not {text}."),
    };
}
