// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.Engine;

public sealed class DeviceFixtureIsKeyboardShownTests
{
    private static readonly Rect KeyboardRect = new(0, 583, 390, 261);

    private static readonly RawNode KeyboardNode = N(
        "@e20",
        index: 10,
        parentIndex: 0,
        depth: 1,
        type: "XCUIElementTypeKeyboard",
        rect: KeyboardRect
    );

    /// <summary>The same keyboard element left below the screen, as after the keyboard was dismissed.</summary>
    private static readonly RawNode OffScreen = N(
        "@e20",
        index: 10,
        parentIndex: 0,
        depth: 1,
        type: "XCUIElementTypeKeyboard",
        rect: new Rect(0, 900, 390, 261)
    );

    private static JsonObject Fact(string kind, params (string Key, JsonNode? Value)[] more)
    {
        var fact = new JsonObject { ["kind"] = kind };
        foreach (var (key, value) in more)
        {
            fact[key] = value;
        }

        return fact;
    }

    private static JsonObject FrameOf(Rect rect) =>
        new()
        {
            ["x"] = rect.X,
            ["y"] = rect.Y,
            ["width"] = rect.Width,
            ["height"] = rect.Height,
        };

    private static JsonObject Settings(IEnumerable<RawNode> nodes, JsonNode? keyboard = null, bool truncated = false)
    {
        var snapshot = FakeDeviceClient.SnapshotOf(nodes);
        if (keyboard is not null)
        {
            snapshot["keyboard"] = keyboard;
        }

        snapshot["truncated"] = truncated;
        return snapshot;
    }

    private static JsonObject SparseSnapshot(JsonNode? keyboard = null)
    {
        var sparse = new JsonObject
        {
            ["nodes"] = new JsonArray(new JsonObject { ["ref"] = "@e1", ["type"] = "application" }),
            ["snapshotQuality"] = new JsonObject { ["state"] = "sparse" },
        };
        if (keyboard is not null)
        {
            sparse["keyboard"] = keyboard;
        }

        return sparse;
    }

    private static async Task<(Harness H, Device Device)> Ready()
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h);
        return (h, session.Device);
    }

    [Fact]
    public async Task Answers_from_the_keyboard_agent_device_measured_with_one_capture_and_nothing_sent_to_the_device()
    {
        var (h, device) = await Ready();
        var before = h.Fake.Calls.Count;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            Settings(SettingsNodes, Fact("visible", ("frame", FrameOf(KeyboardRect))))
        );
        Assert.True(await device.IsKeyboardShownAsync());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings([.. SettingsNodes, KeyboardNode], Fact("absent")));

        // The measurement wins over a keyboard element the tree still lists.
        Assert.False(await device.IsKeyboardShownAsync());
        Assert.Equal(["capture.snapshot", "capture.snapshot"], h.Fake.Methods().Skip(before));
    }

    [Fact]
    public async Task Trusts_a_keyboard_element_in_the_tree_when_the_capture_measured_nothing_and_never_reads_its_absence_as_no_keyboard()
    {
        var (h, device) = await Ready();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings([.. SettingsNodes, KeyboardNode]));
        Assert.True(await device.IsKeyboardShownAsync());

        // An older agent-device that reports no keyboard reading: no keys in the tree proves nothing.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings(SettingsNodes));
        var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.False(failure.Retryable);
        Assert.Contains("no keyboard reading", failure.Message);
    }

    [Fact]
    public async Task Reads_every_keyboard_fact_agent_device_reports_the_way_the_keyboard_band_does_whatever_the_tree_still_lists()
    {
        var (h, device) = await Ready();
        (JsonObject Keyboard, RawNode[] Nodes, bool Shown)[] cases =
        [
            (Fact("derived", ("derivation", "docked-tree"), ("frame", FrameOf(KeyboardRect))), SettingsNodes, true),
            (Fact("visibility-only", ("visible", true)), SettingsNodes, true),
            (Fact("visibility-only", ("visible", false)), [.. SettingsNodes, KeyboardNode], false),
            (
                Fact("tree-rejected", ("derivation", "docked-tree"), ("visible", false)),
                [.. SettingsNodes, KeyboardNode],
                false
            ),
            (Fact("tree-rejected", ("derivation", "docked-tree"), ("visible", true)), SettingsNodes, true),
            // A keyboard still sliding in: agent-device lists its keys but rejects the frame.
            (Fact("tree-rejected", ("derivation", "docked-tree")), [.. SettingsNodes, KeyboardNode], true),
            (Fact("unmeasurable", ("reason", "window-list-unavailable")), [.. SettingsNodes, KeyboardNode], true),
        ];
        foreach (var (keyboard, nodes, shown) in cases)
        {
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings(nodes, keyboard.DeepClone()));
            Assert.True(shown == await device.IsKeyboardShownAsync(), keyboard.ToJsonString());
        }
    }

    [Fact]
    public async Task Drags_the_keyboard_away_only_when_isKeyboardShown_would_say_it_is_up()
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h);
        await Screens.Observed(session, "Back");
        var device = session.Device;
        h.Fake.Respond(DeviceMethods.CommandKeyboard, _ =>
            throw new AgentDeviceError(
                "UNSUPPORTED_OPERATION",
                "Unable to dismiss the iOS keyboard: the keyboard exposes no dismiss key"
            )
        );

        // A probe that says hidden outranks keys the tree still lists, for both.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            Settings([.. SettingsNodes, KeyboardNode], Fact("visibility-only", ("visible", false)))
        );
        Assert.False(await device.IsKeyboardShownAsync());
        await device.DismissKeyboardAsync();
        Assert.DoesNotContain("interactions.swipe", h.Fake.Methods());
    }

    [Fact]
    public async Task Fails_rather_than_answer_false_from_a_tree_that_stays_sparse()
    {
        var (h, device) = await Ready();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => SparseSnapshot());
        var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.False(failure.Retryable);

        // A sparse capture that still carries the producer's reading answers from it.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => SparseSnapshot(Fact("absent")));
        Assert.False(await device.IsKeyboardShownAsync());
    }

    [Fact]
    public async Task Trusts_the_keys_a_cut_tree_lists_and_fails_only_when_a_cut_tree_lists_none()
    {
        var (h, device) = await Ready();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings([.. SettingsNodes, KeyboardNode], truncated: true));
        Assert.True(await device.IsKeyboardShownAsync());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Settings(SettingsNodes, truncated: true));
        var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.False(failure.Retryable);
        Assert.Contains("incomplete", failure.Message);
    }

    [Fact]
    public async Task Captures_again_while_a_rejected_frame_says_nothing_and_no_key_is_drawn_on_screen_and_fails_if_that_lasts()
    {
        var (h, device) = await Ready();
        JsonObject Moving(IEnumerable<RawNode> nodes) =>
            Settings(nodes, Fact("tree-rejected", ("derivation", "docked-tree")));
        var taken = 0;

        // A number pad sliding in lists no key until it lands.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            taken++ < 2
                ? Moving(SettingsNodes)
                : Settings(
                    SettingsNodes,
                    Fact("derived", ("derivation", "docked-tree"), ("frame", FrameOf(KeyboardRect)))
                )
        );
        Assert.True(await device.IsKeyboardShownAsync());
        Assert.Equal(3, taken);

        // No key at all, and a keyboard left below the screen after it was dismissed.
        foreach (var nodes in new[] { SettingsNodes, [.. SettingsNodes, OffScreen] })
        {
            taken = 0;
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            {
                taken++;
                return Moving(nodes);
            });
            var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
            Assert.Equal("ENGINE_FAILURE", failure.Code);
            Assert.False(failure.Retryable);
            Assert.Contains("Read it again", failure.Message);
            Assert.Equal(4, taken);
        }
    }

    [Fact]
    public async Task Fails_when_agent_device_did_not_prove_the_keyboard_absent_and_the_tree_lists_none()
    {
        var (h, device) = await Ready();

        // A keyboard left below the screen after it was dismissed is no keyboard shown, with no reading or an unmeasurable one.
        foreach (var keyboard in new JsonObject?[] { null, Fact("unmeasurable", ("reason", "keyboard-not-observed")) })
        {
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
                Settings([.. SettingsNodes, OffScreen], keyboard?.DeepClone())
            );
            var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
            Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        }

        // With no viewport and no window, nothing bounds the screen, so the keyboard cannot be placed on it.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => new JsonObject
        {
            ["nodes"] = FakeDeviceClient.NodesOnly([OffScreen])["nodes"]!.DeepClone(),
            ["keyboard"] = Fact("unmeasurable", ("reason", "keyboard-viewport-unavailable")),
        });
        var unbounded = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", unbounded.Code);
        string[] reasons =
        [
            "window-root-unavailable",
            "window-list-unavailable",
            "keyboard-window-probe-failed",
            "keyboard-not-observed",
            "keyboard-visibility-probe-unsupported",
        ];
        foreach (var reason in reasons)
        {
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
                Settings(SettingsNodes, Fact("unmeasurable", ("reason", reason)))
            );
            var failure = await Assert.ThrowsAsync<EngineException>(() => device.IsKeyboardShownAsync());
            Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
            Assert.False(failure.Retryable);
            Assert.Contains(reason, failure.Message);
        }
    }

    [Fact]
    public async Task Captures_a_sparse_tree_again_instead_of_reading_it_as_no_keyboard()
    {
        var (h, device) = await Ready();
        var taken = 0;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            taken++ == 0 ? SparseSnapshot() : Settings([.. SettingsNodes, KeyboardNode])
        );
        Assert.True(await device.IsKeyboardShownAsync());
        Assert.Equal(2, taken);
    }
}
