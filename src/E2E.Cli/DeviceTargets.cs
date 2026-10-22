// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;

namespace E2E.Cli;

/// <summary>
/// The device engine of a config target in <c>e2e explore</c> and <c>e2e mcp</c>: the local simulator or emulator, or the hosted device
/// <c>E2E_MOBILE_PROVIDER</c> names (<c>limrun</c> or <c>devflow</c>). There is no upstream counterpart: upstream's config holds the
/// provider, and the port's JSON config names the provider by environment variable, as the NUnit and xUnit adapters do.
/// </summary>
internal static class DeviceTargets
{
    /// <summary>The environment variables whose values grant access to a hosted device service.</summary>
    private static readonly string[] CredentialVariables = ["LIMRUN_API_KEY"];

    /// <summary>Builds the engine of <paramref name="target"/>. A hosted device is leased when the session opens, not here.</summary>
    public static MobileEngine Create(TargetConfig target) => Create(target, Environment.GetEnvironmentVariable);

    internal static MobileEngine Create(TargetConfig target, Func<string, string?> environment) =>
        MobileTargetEngine.Create(target, environment, name => Provider(name, environment));

    /// <summary>
    /// The credentials of the hosted device services in the environment, as secrets: an error from a service may quote the key it was sent,
    /// so what the tool prints or returns passes them through the redactor like any other secret.
    /// </summary>
    public static IReadOnlyList<Secret> Credentials() => Credentials(Environment.GetEnvironmentVariable);

    internal static IReadOnlyList<Secret> Credentials(Func<string, string?> environment) =>
    [
        // A value too short to redact safely is the service's problem to refuse, not a reason to rewrite ordinary text.
        .. CredentialVariables
            .Select(name => (Name: name, Value: environment(name)))
            .Where(variable => variable.Value is not null && Secret.Problem(variable.Value) is null)
            .Select(variable => Secret.Create(variable.Name, variable.Value!)),
    ];

    private static IDeviceProvider Provider(string name, Func<string, string?> environment)
    {
        try
        {
            return HostedDeviceProviders.Create(name, environment)
                ?? throw new ConfigurationException("INVALID_CONFIG", $"E2E_MOBILE_PROVIDER must be local, {HostedDeviceProviders.Names}, not {name}");
        }
        catch (InvalidOperationException cause)
        {
            throw new ConfigurationException("INVALID_CONFIG", cause.Message);
        }
    }
}
