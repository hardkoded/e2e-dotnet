// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.NodeAttributes;

/// <summary>
/// What <c>getAttribute</c> reads on a device node: the attributes the projection carries beside the tree's own fields, read from
/// snapshots captured on a real iOS simulator and Android emulator (<c>CapturedDeviceSnapshotsTests</c> holds the trees, which leave
/// these out).
/// </summary>
public sealed class DeviceNodeAttributesTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshots");

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Reads_label_resource_id_class_and_placeholder_on_iOS()
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Name field",
                ["resource-id"] = "name-input",
                ["class"] = "TextField",
                ["placeholder"] = "Type your name",
            },
            AttributesOf("ios", "control-inventory", "name-input")
        );
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Benchmark Examples",
                ["resource-id"] = "BackButton",
                ["class"] = "Button",
            },
            AttributesOf("ios", "control-inventory", "BackButton")
        );
    }

    [Fact]
    public void Reads_the_content_description_as_label_and_content_desc_on_Android()
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Name field",
                ["resource-id"] = "name-input",
                ["class"] = "android.widget.EditText",
                ["content-desc"] = "Name field",
                ["placeholder"] = "Type your name",
            },
            AttributesOf("android", "control-inventory", "name-input")
        );
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Inventory heading",
                ["resource-id"] = "inventory-header",
                ["class"] = "android.view.View",
                ["content-desc"] = "Inventory heading",
            },
            AttributesOf("android", "control-inventory", "inventory-header")
        );
    }

    [Fact]
    public void Reads_a_description_only_node_and_a_plain_text_view_on_Android()
    {
        var icon = Project(N(index: 0, type: "android.widget.ImageButton", label: "Close", text: "")).Roots[0];
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Close",
                ["class"] = "android.widget.ImageButton",
                ["content-desc"] = "Close",
            },
            icon.Attributes
        );
        var text = Project(N(index: 0, type: "android.widget.TextView", label: "Log in")).Roots[0];
        Assert.Equal(
            new Dictionary<string, string> { ["label"] = "Log in", ["class"] = "android.widget.TextView" },
            text.Attributes
        );
        var same = Project(N(index: 0, type: "android.widget.Button", label: "Save", text: "Save")).Roots[0];
        Assert.Equal(
            new Dictionary<string, string> { ["label"] = "Save", ["class"] = "android.widget.Button" },
            same.Attributes
        );
        var hint = Project(
            N(index: 0, type: "android.widget.EditText", label: "Type your name", hintShowing: true)
        ).Roots[0];
        Assert.DoesNotContain("label", hint.Attributes.Keys);
        Assert.DoesNotContain("content-desc", hint.Attributes.Keys);
        var described = Project(
            N(index: 0, type: "android.widget.EditText", label: "Email address", text: "", hintShowing: true)
        ).Roots[0];
        Assert.Equal("Email address", described.Attributes["label"]);
        Assert.Equal("Email address", described.Attributes["content-desc"]);
    }

    [Fact]
    public void Drops_the_XCUIElementType_prefix_and_leaves_out_what_the_platform_did_not_report()
    {
        Assert.Equal(
            new Dictionary<string, string> { ["class"] = "Other" },
            Project(N(index: 0, type: "Other")).Roots[0].Attributes
        );
        Assert.Equal(
            new Dictionary<string, string> { ["class"] = "TextField" },
            Project(N(index: 0, type: "XCUIElementTypeTextField")).Roots[0].Attributes
        );
        Assert.Empty(Project(N(index: 0)).Roots[0].Attributes);
    }

    [Fact]
    public void Reads_content_desc_only_on_Android()
    {
        var android = Project(N(index: 0, type: "android.view.View", contentDescription: "Close")).Roots[0];
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["label"] = "Close",
                ["class"] = "android.view.View",
                ["content-desc"] = "Close",
            },
            android.Attributes
        );
        var ios = Project(N(index: 0, type: "Button", label: "Done", contentDescription: "Close")).Roots[0];
        Assert.Equal(new Dictionary<string, string> { ["label"] = "Done", ["class"] = "Button" }, ios.Attributes);
    }

    /// <summary>The attributes of the node with <paramref name="testId"/> in a captured scenario.</summary>
    private static IReadOnlyDictionary<string, string> AttributesOf(string platform, string scenario, string testId)
    {
        var raw = JsonSerializer.Deserialize<Capture>(
            File.ReadAllText(Path.Combine(Root, platform, $"{scenario}.json")),
            Json
        )!;
        var counter = 0;
        var projected = SnapshotProjection.ProjectSnapshot(raw.Nodes, () => $"n{++counter}", raw.Viewport);
        var node = projected.Index.Select(entry => entry.Node).FirstOrDefault(candidate => candidate.TestId == testId);
        return node?.Attributes ?? throw new InvalidOperationException($"no node #{testId} in {platform}/{scenario}");
    }

    private sealed record Capture(RawNode[] Nodes, ViewportSize? Viewport);
}
