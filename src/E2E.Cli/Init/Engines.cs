// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Cli;

/// <summary>One engine `e2e init` can scaffold (upstream's <c>EnginePreset</c>).</summary>
/// <param name="Id">The <c>--engine</c> value.</param>
/// <param name="Label">The engine's name.</param>
/// <param name="Hint">What drives it.</param>
/// <param name="Target">The <c>targets</c> entry of the config.</param>
internal sealed record EnginePreset(string Id, string Label, string Hint, Func<JsonObject> Target);

/// <summary>Upstream's <c>cli/init/engines.ts</c>: CLI scaffold presets, with a `targets` entry in place of generated source.</summary>
internal static class Engines
{
    /// <summary>The engine <c>--yes</c> picks.</summary>
    public static readonly IReadOnlyList<string> DefaultEngineIds = ["web"];

    /// <summary>Builds the presets for the machine running init: the device engine targets iOS on macOS and Android elsewhere. <paramref name="darwin"/> is a test seam for the host.</summary>
    public static IReadOnlyList<EnginePreset> GetEnginePresets(bool? darwin = null)
    {
        var ios = darwin ?? OperatingSystem.IsMacOS();
        return
        [
            new EnginePreset("web", "Web", "Playwright", () => new JsonObject
            {
                ["name"] = "web",
                ["platform"] = "web",
                ["app"] = new JsonObject { ["url"] = "http://localhost:3000" },
            }),
            new EnginePreset("mobile", "Mobile (iOS/Android)", "agent-device", () => new JsonObject
            {
                ["name"] = ios ? "ios" : "android",
                ["platform"] = ios ? "ios" : "android",
                // Replace the id with your app's bundle id or package name.
                ["app"] = new JsonObject { ["bundleId"] = ios ? "Settings" : "com.android.settings" },
            }),
        ];
    }

    /// <summary>Resolves a preset by id.</summary>
    public static EnginePreset GetEnginePreset(string id) =>
        GetEnginePresets().FirstOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("unknown engine: " + id, nameof(id));
}
