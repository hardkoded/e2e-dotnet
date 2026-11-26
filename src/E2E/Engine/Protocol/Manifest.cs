// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Engine.Protocol;

/// <summary>
/// What an engine server is, as JSON: the static members of an engine and the methods it answers. The client declares it at config load,
/// where the harness needs an engine's capabilities synchronously, and the server returns its own from <c>initialize</c>; the two must
/// agree. Upstream's <c>EngineManifest</c> (<c>engine/protocol/protocol.ts</c>).
/// </summary>
public sealed record EngineManifest(string Name, string Version, int SpiVersion, IReadOnlyList<string> Methods)
{
    public string? Platform { get; init; }

    public int? Workers { get; init; }

    /// <summary>Required with <c>engine/perform</c>; see <see cref="EngineHandle.Actions"/>.</summary>
    public IReadOnlyList<string>? Actions { get; init; }

    /// <summary>Required with <c>engine/performAt</c>; see <see cref="EngineHandle.PointerActions"/>.</summary>
    public IReadOnlyList<string>? PointerActions { get; init; }

    /// <summary>See <see cref="EngineHandle.TapModifiers"/>.</summary>
    public bool? TapModifiers { get; init; }

    /// <summary>See <see cref="EngineHandle.SelectsDevice"/>.</summary>
    public bool? SelectsDevice { get; init; }

    /// <summary>See <see cref="EngineHandle.GrantsAllPermissions"/>.</summary>
    public bool? GrantsAllPermissions { get; init; }

    /// <summary>See <see cref="EngineHandle.LaunchArgumentValues"/>.</summary>
    public bool? LaunchArgumentValues { get; init; }

    /// <summary>See <see cref="EngineHandle.FileApps"/>.</summary>
    public bool? FileApps { get; init; }

    /// <summary>See <see cref="EngineHandle.Isolation"/>; left out for <c>attempt</c>.</summary>
    public string? Isolation { get; init; }
}

/// <summary>
/// The manifest: <c>DescribeEngine</c> derives one from an engine, <c>CheckManifest</c> checks the shape of a declared one, and
/// <c>ManifestMismatch</c> compares the declared manifest with the one a server announces. Upstream's <c>engine/protocol/manifest.ts</c>.
/// </summary>
/// <remarks>.NET-only holder name: C# has no free functions, and <see cref="E2E.Engine.EngineManifest"/> already holds the engine manifest's.</remarks>
public static class ProtocolManifest
{
    private static readonly JsonSerializerOptions ShowOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Describes an engine as the manifest <c>serveEngine</c> returns from <c>initialize</c>: its static members, the methods its declared
    /// members answer, and the attempt hooks the server always answers. Upstream's <c>describeEngine</c>.
    /// </summary>
    public static EngineManifest DescribeEngine(EngineHandle engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return new EngineManifest(
            engine.Name,
            engine.Version,
            engine.SpiVersion,
            [
                .. Protocol.EngineMethods.Where(method =>
                    Protocol.ServedMethods.Contains(method)
                    || Protocol.EngineMember(
                        engine,
                        Protocol.Methods[method] switch
                        {
                            OperationMethod operation => operation.Member,
                            LifecycleMethod lifecycle => lifecycle.Member,
                            _ => string.Empty,
                        }
                    )
                        is not null
                ),
            ]
        )
        {
            Platform = engine.Platform,
            Workers = engine.Workers,
            Actions = engine.Actions,
            PointerActions = engine.PointerActions,
            TapModifiers = engine.TapModifiers == true ? true : null,
            SelectsDevice = engine.SelectsDevice == true ? true : null,
            GrantsAllPermissions = engine.GrantsAllPermissions == true ? true : null,
            LaunchArgumentValues = engine.LaunchArgumentValues == true ? true : null,
            FileApps = engine.FileApps == true ? true : null,
            Isolation = engine.Isolation == EngineIsolation.Worker ? EngineIsolation.Worker : null,
        };
    }

    /// <summary>
    /// Checks a declared manifest's shape at config load: an object whose methods are protocol methods, each once. The rest is checked by
    /// <see cref="E2E.Engine.EngineManifest.DefineEngine"/>. Upstream's <c>checkManifest</c>.
    /// </summary>
    public static EngineManifest CheckManifest(EngineManifest manifest)
    {
        if (manifest is null)
        {
            throw Invalid("manifest must be an object");
        }

        if (manifest.Methods is null)
        {
            throw Invalid("manifest.methods must be an array of engine methods");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in manifest.Methods)
        {
            if (method is null || !Protocol.Methods.ContainsKey(method))
            {
                throw Invalid($"manifest.methods names unknown method {JsonSerializer.Serialize(method)}");
            }

            if (!seen.Add(method))
            {
                throw Invalid($"manifest.methods lists \"{method}\" twice");
            }
        }

        // The client serves `prepare` for `lifecycle/finish` alone, so the hook defineEngine requires is there either way.
        if (manifest.SelectsDevice == true && !seen.Contains("lifecycle/prepare"))
        {
            throw Invalid("manifest.selectsDevice needs lifecycle/prepare: prepare binds the device --device names");
        }

        return manifest;
    }

    /// <summary>Names the fields where the server's manifest differs from the declared one, or null when they agree. Upstream's <c>manifestMismatch</c>.</summary>
    public static string? ManifestMismatch(EngineManifest declared, EngineManifest? announced)
    {
        ArgumentNullException.ThrowIfNull(declared);
        if (announced is null)
        {
            return "the server sent none";
        }

        var expected = Comparable(declared);
        var actual = Comparable(announced);
        var differing = expected
            .Where(field => Show(field.Value) != Show(actual[field.Key]))
            .Select(field => $"{field.Key} is {Show(actual[field.Key])}, declared {Show(field.Value)}")
            .ToList();
        return differing.Count == 0 ? null : string.Join("; ", differing);
    }

    private static ConfigurationException Invalid(string detail) => new("INVALID_CONFIG", $"connectEngine: {detail}");

    private static string Show(object? value) => value is null ? "none" : JsonSerializer.Serialize(value, ShowOptions);

    /// <summary>A manifest with its lists in one order and its optional flags explicit, so equal manifests compare equal.</summary>
    private static Dictionary<string, object?> Comparable(EngineManifest manifest)
    {
        static string[] Sorted(IReadOnlyList<string>? list) => [.. (list ?? []).Order(StringComparer.Ordinal)];
        return new Dictionary<string, object?>
        {
            ["name"] = manifest.Name,
            ["version"] = manifest.Version,
            ["spiVersion"] = manifest.SpiVersion,
            ["platform"] = manifest.Platform,
            ["workers"] = manifest.Workers,
            ["methods"] = Sorted(manifest.Methods),
            ["actions"] = Sorted(manifest.Actions),
            ["pointerActions"] = Sorted(manifest.PointerActions),
            ["tapModifiers"] = manifest.TapModifiers == true,
            ["selectsDevice"] = manifest.SelectsDevice == true,
            ["grantsAllPermissions"] = manifest.GrantsAllPermissions == true,
            ["launchArgumentValues"] = manifest.LaunchArgumentValues == true,
            ["fileApps"] = manifest.FileApps == true,
            ["isolation"] =
                manifest.Isolation == EngineIsolation.Worker ? EngineIsolation.Worker : EngineIsolation.Attempt,
        };
    }
}
