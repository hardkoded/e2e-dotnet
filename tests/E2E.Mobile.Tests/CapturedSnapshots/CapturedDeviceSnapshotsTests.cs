// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using E2E.Engine;

namespace E2E.Mobile.Tests.CapturedSnapshots;

/// <summary>
/// Snapshots captured from a real iOS simulator and Android emulator
/// (<c>apps/mobile-benchmark/tests-capture/snapshots.e2e.ts</c> upstream), projected the way
/// <c>observe</c> projects them, against the committed tree each one reads as. A change to how the engine
/// reads a real hierarchy shows up here as a diff of <c>&lt;scenario&gt;.tree.txt</c>, without a device.
/// <c>E2E_GOLDEN_UPDATE=1</c> rewrites the trees from the snapshots in the source tree; review the diff
/// like any other change.
/// </summary>
public sealed class CapturedDeviceSnapshotsTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshots");

    private static readonly string SourceRoot = SourceFixtures();

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The title each iOS capture's location carries. A pushed screen's bar is labelled with the back button
    /// (<c>Benchmark Examples</c>) and identified by its own title; the tab bar and the PaymentSheet show no
    /// navigation bar. The <c>swiftui-*</c> captures (<c>tests-capture/swiftui-bar</c> upstream) give the bar
    /// no label, and identify an untitled screen's bar by a Swift class name.
    /// </summary>
    private static readonly Dictionary<string, string?> IosTitles = new()
    {
        ["apple-pay"] = "Apple Pay",
        ["apple-pay-billing-address"] = "Apple Pay Billing Address",
        ["async-states"] = "Async States",
        ["bottom-tabs"] = null,
        ["broken-accessibility"] = "Broken Accessibility",
        ["choice-controls"] = "Choice Controls",
        ["control-inventory"] = "Control Inventory",
        ["debounced-search"] = "Debounced Search",
        ["error-recovery"] = "Error Recovery",
        ["flattened-login"] = "Flattened Login",
        ["flattened-registration-form"] = "Flattened Registration Form",
        ["gestures"] = "Gestures",
        ["home"] = "Benchmark Examples",
        ["huge-virtualized-list"] = "Huge Virtualized List",
        ["infinite-scroll-list"] = "Infinite Scroll List",
        ["login-form"] = "Login Form",
        ["modal-flow"] = "Modal Flow",
        ["permission-prompt"] = "Permission Prompt",
        ["photo-picker"] = "Photo Picker",
        ["product-catalog"] = "Product Catalog",
        ["sequential-onboarding"] = "Sequential Onboarding",
        ["sticky-chrome-target"] = "Sticky Chrome Target",
        ["stripe-paymentsheet"] = null,
        ["swiftui-compose"] = "Compose",
        ["swiftui-inbox"] = "Inbox",
        ["swiftui-message"] = "Message",
        ["swiftui-untitled"] = null,
        ["text-input-variations"] = "Text Input Variations",
        ["vision-only"] = "Vision Only",
        ["webview-accessibility"] = "WebView Accessibility",
    };

    private static readonly bool Update = Environment.GetEnvironmentVariable("E2E_GOLDEN_UPDATE") == "1";

    public static TheoryData<string> Platforms => [.. PlatformNames()];

    public static TheoryData<string, string> Captures
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var platform in PlatformNames())
            {
                foreach (var capture in CaptureNames(platform))
                {
                    data.Add(platform, capture);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> IosCaptures => [.. CaptureNames("ios")];

    [Fact]
    public void Has_captures_for_both_platforms()
    {
        Assert.Equal(["android", "ios"], PlatformNames());
    }

    [Theory]
    [MemberData(nameof(Captures))]
    public void Reads_as_its_committed_tree(string platform, string capture)
    {
        var tree = TreeOf(Path.Combine(Root, platform, capture));
        var golden = capture.Replace(".json", ".tree.txt", StringComparison.Ordinal);
        if (Update)
        {
            var source = Path.Combine(SourceRoot, platform, golden);
            File.WriteAllText(source, tree);
        }

        var committed = Path.Combine(Root, platform, golden);
        Assert.True(File.Exists(committed) || Update, $"{committed} is missing; run with E2E_GOLDEN_UPDATE=1");
        if (!Update)
        {
            Assert.Equal(File.ReadAllText(committed), tree);
        }
    }

    [Theory]
    [MemberData(nameof(IosCaptures))]
    public void Takes_its_title_from_the_navigation_bar(string capture)
    {
        var scenario = capture.Replace(".json", "", StringComparison.Ordinal);
        Assert.True(IosTitles.ContainsKey(scenario), $"add {scenario} to IosTitles");
        Assert.Equal(IosTitles[scenario], SnapshotProjection.ScreenTitle(Project(Path.Combine(Root, "ios", capture)).Projected));
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void Keeps_no_tree_without_its_capture(string platform)
    {
        var captures = CaptureNames(platform);
        var trees = Directory.GetFiles(Path.Combine(Root, platform), "*.tree.txt").Select(Path.GetFileName);
        Assert.All(trees, tree => Assert.Contains(tree!.Replace(".tree.txt", ".json", StringComparison.Ordinal), captures));
    }

    /// <summary>The fixtures in the source tree, which <c>E2E_GOLDEN_UPDATE=1</c> rewrites.</summary>
    private static string SourceFixtures([CallerFilePath] string testFile = "") =>
        Path.Combine(Path.GetDirectoryName(testFile)!, "..", "Fixtures", "snapshots");

    private static string[] PlatformNames() =>
        Directory.Exists(Root) ? [.. Directory.GetDirectories(Root).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)] : [];

    private static string[] CaptureNames(string platform) =>
        [.. Directory.GetFiles(Path.Combine(Root, platform), "*.json").Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    /// <summary>A captured snapshot projected the way <c>observe</c> projects it.</summary>
    private static (Capture Raw, ProjectedSnapshot Projected) Project(string file)
    {
        var raw = JsonSerializer.Deserialize<Capture>(File.ReadAllText(file), Json)!;
        var counter = 0;
        return (raw, SnapshotProjection.ProjectSnapshot(raw.Nodes, () => $"n{++counter}", raw.Viewport));
    }

    /// <summary>The tree <c>observe</c> would report for a captured snapshot.</summary>
    private static string TreeOf(string file)
    {
        var (raw, projected) = Project(file);
        var root = SnapshotProjection.ScreenRoot(projected.Roots, projected.Viewport ?? new ViewportSize(0, 0));
        return $"{(raw.Truncated == true ? "# truncated\n" : "")}{string.Join('\n', Render(root, 0, []))}\n";
    }

    /// <summary>One line per node: role, name, text, value, states, test id, and attributes; geometry is left out as capture noise.</summary>
    private static List<string> Render(SemanticNode node, int depth, List<string> output)
    {
        var parts = new List<string> { node.Role ?? "(no role)" };
        if (!string.IsNullOrEmpty(node.Name))
        {
            parts.Add(JsonText.Quote(node.Name));
        }

        if (!string.IsNullOrEmpty(node.Text) && node.Text != node.Name)
        {
            parts.Add($"text={JsonText.Quote(node.Text)}");
        }

        if (node.Value is not null)
        {
            parts.Add($"value={JsonText.Quote(node.Value)}");
        }

        var states = new (string Name, bool Set)[]
        {
            ("checked", node.States.Checked),
            ("disabled", node.States.Disabled),
            ("selected", node.States.Selected),
            ("expanded", node.States.Expanded),
            ("pressed", node.States.Pressed),
            ("focused", node.States.Focused),
            ("hidden", node.States.Hidden),
            ("secure", node.States.Secure),
        }.Where(state => state.Set).Select(state => state.Name).ToList();
        if (states.Count > 0)
        {
            parts.Add($"[{string.Join(", ", states)}]");
        }

        if (node.TestId is not null)
        {
            parts.Add($"#{node.TestId}");
        }

        if (node.Placeholder is not null)
        {
            parts.Add($"placeholder={JsonText.Quote(node.Placeholder)}");
        }

        output.Add($"{new string(' ', depth * 2)}{string.Join(' ', parts)}");
        foreach (var child in node.Children)
        {
            Render(child, depth + 1, output);
        }

        return output;
    }

    private sealed record Capture(RawNode[] Nodes, ViewportSize? Viewport, bool? Truncated);
}
