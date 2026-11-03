// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>
/// The checks of <c>e2e doctor</c> for a mobile target: agent-device's own doctor (its install, the platform toolchain, the device, the
/// app) for each device the target names, and the build <c>appPath</c> points at. Upstream's <c>mobile/doctor.ts</c>
/// (<c>mobileDoctor</c>), reached through the engine's <c>doctor</c> hook.
/// </summary>
public sealed partial class MobileEngine
{
    /// <summary>Checks that apply to the whole machine, reported once however many devices the target names.</summary>
    private static readonly HashSet<string> MachineChecks = new(["agent-device", "toolchain", "ios-runner-cache"], StringComparer.Ordinal);

    /// <summary>
    /// Checks e2e drops: <c>metro</c> because only the app knows whether it loads its bundle from a dev server, <c>target-app-device</c>
    /// because it only says the app check had no single booted device to run on, which a run boots.
    /// </summary>
    private static readonly HashSet<string> DroppedChecks = new(["metro", "target-app-device"], StringComparer.Ordinal);

    /// <summary>
    /// Runs agent-device's doctor once per device the target names (once with no device, for the booted one a run picks) and checks the
    /// build <c>AppPath</c> names. A provider's devices are not on this machine, and the provider reads <c>AppPath</c> its own way, so
    /// neither is checked for one.
    /// </summary>
    public async Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (Options.Provider is { } provider)
        {
            return [new EngineDoctorCheck("device", EngineDoctorStatus.Skip, $"devices and the build come from the \"{provider.Name}\" provider")];
        }

        IReadOnlyList<string?> devices = Options.Devices is { } pool ? [.. pool] : [Options.Device];
        var targetApp = info.App.AppPath is null ? info.App.BundleId : null;
        var client = _createClient(Pool.Session(info.TargetName, 0), null);
        var checks = new List<EngineDoctorCheck>(BuildCheck(info));
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in devices)
        {
            info.Log(name is null ? "agent-device doctor" : $"agent-device doctor for \"{name}\"");
            var where = DevicePool.DeviceSelectionFor(Options.Platform, name is null ? null : new SlotBinding { Device = name });
            var result = await DeviceErrors.RunCommandAsync(
                "doctor",
                token => client.CallAsync(DeviceMethods.CommandDoctor, where.ToArgs(("targetApp", targetApp is null ? null : JsonValue.Create(targetApp))), token),
                cancellationToken).ConfigureAwait(false);
            foreach (var found in (result?["checks"] as JsonArray) ?? [])
            {
                var id = Text(found?["id"]);
                var status = StatusOf(Text(found?["status"]));
                var summary = Text(found?["summary"]);
                if (id is null || summary is null || status is null || DroppedChecks.Contains(id) || !CheckId().IsMatch(id))
                {
                    continue;
                }

                var machine = MachineChecks.Contains(id);
                if (machine && !reported.Add(id))
                {
                    continue;
                }

                var hint = Text(found?["hint"]);
                var message = status == EngineDoctorStatus.Pass || hint is null ? summary : $"{summary} {hint}";
                checks.Add(new EngineDoctorCheck(id, status.Value, name is null || machine ? message : $"\"{name}\": {message}"));
            }
        }

        return checks;
    }

    /// <summary>agent-device's status as an e2e one; <c>info</c> is evidence, not a finding, so it reports nothing.</summary>
    private static EngineDoctorStatus? StatusOf(string? status) => status switch
    {
        "pass" => EngineDoctorStatus.Pass,
        "warn" => EngineDoctorStatus.Warn,
        "fail" => EngineDoctorStatus.Fail,
        _ => null,
    };

    /// <summary>Whether the build <c>AppPath</c> names is on disk; nothing to check without one.</summary>
    private static EngineDoctorCheck[] BuildCheck(EngineDoctorInfo info)
    {
        if (info.App.AppPath is not { } appPath)
        {
            return [];
        }

        var resolved = Path.GetFullPath(appPath, info.ProjectRoot);
        return File.Exists(resolved) || Directory.Exists(resolved)
            ? [new EngineDoctorCheck("app-path", EngineDoctorStatus.Pass, resolved)]
            : [new EngineDoctorCheck("app-path", EngineDoctorStatus.Fail, $"app.appPath {resolved} does not exist; build the app or fix the path")];
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"\A[a-z][a-z0-9-]*\z")]
    private static partial Regex CheckId();
}
