// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Cli;

/// <summary>What an engine's example test is made of: the base class of the test class, the usings it needs, and its test method.</summary>
/// <param name="Namespace">The namespace of the engine's base class.</param>
/// <param name="BaseClass">The base class of the test class.</param>
/// <param name="Test">The test method, indented as a class member.</param>
internal sealed record EngineExample(string Namespace, string BaseClass, string Test);

/// <summary>One engine `e2e init` can scaffold (upstream's <c>EnginePreset</c>).</summary>
/// <param name="Id">The <c>--engine</c> value.</param>
/// <param name="Label">The engine's name.</param>
/// <param name="Hint">What drives it.</param>
/// <param name="Target">The <c>targets</c> entry of the config.</param>
/// <param name="Dependencies">The packages the engine needs for a test framework, as names (upstream's <c>dependencies</c>, whose ranges the scaffold adds).</param>
/// <param name="Example">The example test for a test framework.</param>
/// <param name="AiExample">The commented agent example that follows <paramref name="Example"/>, indented as a class member.</param>
/// <param name="NeedsAppUrl">Whether the engine drives a URL.</param>
internal sealed record EnginePreset(
    string Id,
    string Label,
    string Hint,
    Func<JsonObject> Target,
    Func<FrameworkPreset, IReadOnlyList<string>> Dependencies,
    Func<FrameworkPreset, EngineExample> Example,
    Func<FrameworkPreset, string> AiExample,
    bool NeedsAppUrl);

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
            new EnginePreset(
                "web",
                "Web",
                "Playwright",
                () => new JsonObject
                {
                    ["name"] = "web",
                    ["platform"] = "web",
                    ["app"] = new JsonObject { ["url"] = "http://localhost:3000" },
                },
                framework => ["E2E", "E2E." + framework.AdapterSuffix],
                framework => new EngineExample(
                    framework.Namespace,
                    "E2ETest",
                    $$"""
                        [{{framework.TestAttribute}}]
                        public async Task App_opens()
                        {
                            await App.OpenAsync("/");
                            await Expect.That(Browser.Locator("body")).ToBeVisibleAsync();
                        }
                    """),
                framework => $$"""

                        // With the model key in the environment, uncomment:
                        // [{{framework.TestAttribute}}]
                        // public async Task The_agent_drives_a_flow()
                        // {
                        //     await App.OpenAsync("/");
                        //     await Agent.ActAsync("one goal in natural language");
                        //     await Agent.AssertAsync("one question about the screen");
                        // }
                    """,
                true),
            new EnginePreset(
                "mobile",
                "Mobile (iOS/Android)",
                "agent-device",
                () => new JsonObject
                {
                    ["name"] = ios ? "ios" : "android",
                    ["platform"] = ios ? "ios" : "android",
                    // Replace the id with your app's bundle id or package name.
                    ["app"] = new JsonObject { ["bundleId"] = ios ? "Settings" : "com.android.settings" },
                },
                framework => ["E2E.Mobile." + framework.AdapterSuffix],
                framework => new EngineExample(
                    "E2E.Mobile." + framework.AdapterSuffix,
                    "MobileAppTest",
                    $$"""
                        [{{framework.TestAttribute}}]
                        public async Task Settings_opens()
                        {
                            await Expect.That({{(ios ? "Screen.GetByRole(\"button\", \"General\")" : "Screen.GetByText(\"Network & internet\")")}}).ToBeVisibleAsync();
                        }
                    """),
                framework => ios
                    ? $$"""

                            // With the model key in the environment, uncomment:
                            // [{{framework.TestAttribute}}]
                            // public async Task The_agent_opens_General()
                            // {
                            //     await Agent.ActAsync("open General settings");
                            //     await Expect.That(Device.Locator("role=NavigationBar id=General")).ToBeVisibleAsync();
                            // }
                        """
                    : $$"""

                            // With the model key in the environment, uncomment:
                            // [{{framework.TestAttribute}}]
                            // public async Task The_agent_opens_Network_settings()
                            // {
                            //     await Agent.ActAsync("open Network & internet settings");
                            //     await Expect.That(Device.Locator("id=com.android.settings:id/collapsing_toolbar")).ToHaveTextAsync("Network & internet");
                            // }
                        """,
                false),
        ];
    }

    /// <summary>Resolves a preset by id.</summary>
    public static EnginePreset GetEnginePreset(string id) =>
        GetEnginePresets().FirstOrDefault(entry => entry.Id == id) ?? throw new ArgumentException("unknown engine: " + id, nameof(id));
}
