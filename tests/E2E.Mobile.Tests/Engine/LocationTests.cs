// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class LocationTests
{
    [Fact]
    public async Task Locates_against_a_fresh_snapshot_and_keeps_observation_ids_valid()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");
        var before = h.Fake.Calls.Count;
        var located = await session.LocateAsync(
            LocatorExpression.ForRole("button", null, new RoleOptions()),
            TimeSpan.FromSeconds(5),
            CancellationToken.None
        );
        var back = Assert.Single(located);
        Assert.Equal("Back", back.Name);
        await session.PerformAsync(back, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Calls(
            h.Fake.Calls.Skip(before).Where(call => call.Method == "interactions.press"),
            0,
            ("interactions.press", """{"ref":"@e3","settle":true,"settleQuietMs":150}"""),
            ("interactions.press", """{"ref":"@e4","settle":true,"settleQuietMs":150}""")
        );
    }

    private const string AndroidAppPackage = "dev.e2e.examples.maui";

    private static LocatorExpression TestId(string value) =>
        LocatorExpression.For("testid", TextMatch.FromString(value), new TextMatchOptions(), nameof(value));

    [Fact]
    public async Task Finds_an_Android_view_by_its_id_without_its_package()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await OpenAttempt(h);
        RawNode[] nodes =
        [
            N(
                "@e1",
                index: 0,
                depth: 0,
                type: "android.widget.FrameLayout",
                identifier: "android:id/content",
                rect: new Rect(0, 0, 390, 844)
            ),
            N(
                "@e2",
                index: 1,
                parentIndex: 0,
                depth: 1,
                type: "android.view.ViewGroup",
                identifier: $"{AndroidAppPackage}:id/form",
                rect: new Rect(0, 100, 390, 300)
            ),
            N(
                "@e3",
                index: 2,
                parentIndex: 1,
                depth: 2,
                type: "android.widget.EditText",
                identifier: $"{AndroidAppPackage}:id/name",
                label: "Ada Lovelace",
                hintShowing: true,
                rect: new Rect(0, 120, 390, 44)
            ),
            N(
                "@e4",
                index: 3,
                parentIndex: 1,
                depth: 2,
                type: "android.widget.Button",
                identifier: $"{AndroidAppPackage}:id/greet",
                label: "Greet",
                rect: new Rect(0, 180, 390, 44)
            ),
            N(
                "@e5",
                index: 4,
                parentIndex: 0,
                depth: 1,
                type: "android.widget.TextView",
                identifier: "com.example.other:id/banner",
                label: "Elsewhere",
                rect: new Rect(0, 420, 390, 44)
            ),
            N(
                "@e6",
                index: 5,
                parentIndex: 0,
                depth: 1,
                type: "android.widget.TextView",
                identifier: "greeting",
                label: "Hello, Ada!",
                rect: new Rect(0, 480, 390, 44)
            ),
        ];
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            FakeDeviceClient.SnapshotOf(nodes, ("appName", "with-maui"), ("appBundleId", AndroidAppPackage))
        );
        var greet = Assert.Single(
            await session.LocateAsync(TestId("greet"), TimeSpan.FromSeconds(5), CancellationToken.None)
        );
        Assert.Equal($"{AndroidAppPackage}:id/greet", greet.TestId);
        Assert.Empty(await session.LocateAsync(TestId("content"), TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task Finds_a_view_whose_resource_package_is_not_the_app_package_as_in_an_applicationIdSuffix_build()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await OpenAttempt(h);

        // AGP renames the manifest package to com.foo.debug; the resource table keeps the namespace com.foo.
        RawNode[] nodes =
        [
            N(
                "@e1",
                index: 0,
                depth: 0,
                type: "android.widget.FrameLayout",
                identifier: "android:id/content",
                rect: new Rect(0, 0, 390, 844)
            ),
            N(
                "@e2",
                index: 1,
                parentIndex: 0,
                depth: 1,
                type: "android.widget.Button",
                identifier: "com.foo:id/greet",
                label: "Greet",
                rect: new Rect(0, 100, 390, 44)
            ),
        ];
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            FakeDeviceClient.SnapshotOf(nodes, ("appBundleId", "com.foo.debug"))
        );
        var greet = Assert.Single(
            await session.LocateAsync(TestId("greet"), TimeSpan.FromSeconds(5), CancellationToken.None)
        );
        Assert.Equal("com.foo:id/greet", greet.TestId);
    }

    private static LocatorExpression ExactText(string value) =>
        LocatorExpression.For(
            "text",
            TextMatch.FromString(value),
            new TextMatchOptions { Exact = true },
            nameof(value)
        );

    private static RawNode[] SettingsWith(params RawNode[] extra) => [.. SettingsNodes, .. extra];

    [Fact]
    public async Task Answers_whether_a_located_node_is_on_the_screen_never_the_share_agent_device_clipped_away()
    {
        var h = Harness.Create();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            FakeDeviceClient.SnapshotOf(
                SettingsWith(
                    N("@e11", index: 11, parentIndex: 0, depth: 1, type: "button", label: "Unplaced"),
                    N(
                        "@e12",
                        index: 12,
                        parentIndex: 0,
                        depth: 1,
                        type: "button",
                        label: "Lower",
                        rect: new Rect(10, 600, 100, 40)
                    )
                )
            )
        );
        var session = await OpenAttempt(h);
        async Task<ViewportIntersection> Measure(string label)
        {
            var node = Assert.Single(
                await session.LocateAsync(ExactText(label), TimeSpan.FromSeconds(5), CancellationToken.None)
            );
            var issued = h.Fake.Calls.Count;
            try
            {
                // Read off the capture the locate took: no second snapshot, no device command.
                return await session.ViewportIntersectionAsync(node, OperationContext.Test, CancellationToken.None);
            }
            finally
            {
                Assert.Equal(issued, h.Fake.Calls.Count);
            }
        }

        Assert.Equal(new ViewportIntersection(true), await Measure("About"));
        // Below y 390 on the 390x844 screen: the height is measured against the height, not the width.
        Assert.Equal(new ViewportIntersection(true), await Measure("Lower"));
        Assert.Equal(new ViewportIntersection(true), await Measure("Password"));
        // Below the 844-point screen.
        Assert.Equal(new ViewportIntersection(false), await Measure("Hidden"));
        var unplaced = await Assert.ThrowsAsync<EngineException>(() => Measure("Unplaced"));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, unplaced.Code);
    }

    [Fact]
    public async Task Measures_against_a_resized_screen_once_the_device_confirms_it_without_waiting_for_an_observe()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var below = N(
            "@e11",
            index: 11,
            parentIndex: 0,
            depth: 1,
            type: "button",
            label: "Below",
            rect: new Rect(10, 850, 100, 20)
        );
        async Task<ViewportIntersection> LocateBelow()
        {
            var node = Assert.Single(
                await session.LocateAsync(ExactText("Below"), TimeSpan.FromSeconds(5), CancellationToken.None)
            );
            return await session.ViewportIntersectionAsync(node, OperationContext.Test, CancellationToken.None);
        }

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(SettingsWith(below)));
        Assert.Equal(new ViewportIntersection(false), await LocateBelow());

        // A foldable unfolds: the tree grows to 402x874, and the device's screen agrees.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            FakeDeviceClient.SnapshotOf(
                SettingsWith(below),
                ("viewport", new JsonObject { ["width"] = 402, ["height"] = 874 })
            )
        );
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject
        {
            ["logicalWidth"] = 402,
            ["logicalHeight"] = 874,
        });
        Assert.Equal(new ViewportIntersection(true), await LocateBelow());
    }

    [Fact]
    public async Task Refuses_to_say_whether_a_node_is_in_the_viewport_when_its_window_has_no_known_place_on_the_screen()
    {
        var h = Harness.Create();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
            FakeDeviceClient.SnapshotOf(SettingsNodes, ("warnings", new JsonArray(DeviceErrors.OriginWarning)))
        );
        var session = await OpenAttempt(h);
        var hidden = Assert.Single(
            await session.LocateAsync(ExactText("Hidden"), TimeSpan.FromSeconds(5), CancellationToken.None)
        );
        var error = await Assert.ThrowsAsync<EngineException>(() =>
            session.ViewportIntersectionAsync(hidden, OperationContext.Test, CancellationToken.None)
        );
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Contains(DeviceErrors.OriginUnknownBounds, error.Message);
    }
}
