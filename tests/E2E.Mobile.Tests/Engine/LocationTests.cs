// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
        var located = await session.LocateAsync(LocatorExpression.ForRole("button", null, new RoleOptions()), TimeSpan.FromSeconds(5), CancellationToken.None);
        var back = Assert.Single(located);
        Assert.Equal("Back", back.Name);
        await session.PerformAsync(back, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Calls(
            h.Fake.Calls.Skip(before).Where(call => call.Method == "interactions.press"),
            0,
            ("interactions.press", """{"ref":"@e3","settle":true,"settleQuietMs":150}"""),
            ("interactions.press", """{"ref":"@e4","settle":true,"settleQuietMs":150}"""));
    }

    private const string AndroidAppPackage = "dev.e2e.examples.maui";

    private static LocatorExpression TestId(string value) => LocatorExpression.For("testid", TextMatch.FromString(value), new TextMatchOptions(), nameof(value));

    [Fact]
    public async Task Finds_an_Android_view_by_its_id_without_its_package()
    {
        var h = Harness.Create(options => options with { Platform = MobilePlatform.Android });
        var session = await OpenAttempt(h);
        RawNode[] nodes =
        [
            N("@e1", index: 0, depth: 0, type: "android.widget.FrameLayout", identifier: "android:id/content", rect: new Rect(0, 0, 390, 844)),
            N("@e2", index: 1, parentIndex: 0, depth: 1, type: "android.view.ViewGroup", identifier: $"{AndroidAppPackage}:id/form", rect: new Rect(0, 100, 390, 300)),
            N("@e3", index: 2, parentIndex: 1, depth: 2, type: "android.widget.EditText", identifier: $"{AndroidAppPackage}:id/name", label: "Ada Lovelace", hintShowing: true, rect: new Rect(0, 120, 390, 44)),
            N("@e4", index: 3, parentIndex: 1, depth: 2, type: "android.widget.Button", identifier: $"{AndroidAppPackage}:id/greet", label: "Greet", rect: new Rect(0, 180, 390, 44)),
            N("@e5", index: 4, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "com.example.other:id/banner", label: "Elsewhere", rect: new Rect(0, 420, 390, 44)),
            N("@e6", index: 5, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "greeting", label: "Hello, Ada!", rect: new Rect(0, 480, 390, 44)),
        ];
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(nodes, ("appName", "with-maui"), ("appBundleId", AndroidAppPackage)));
        var greet = Assert.Single(await session.LocateAsync(TestId("greet"), TimeSpan.FromSeconds(5), CancellationToken.None));
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
            N("@e1", index: 0, depth: 0, type: "android.widget.FrameLayout", identifier: "android:id/content", rect: new Rect(0, 0, 390, 844)),
            N("@e2", index: 1, parentIndex: 0, depth: 1, type: "android.widget.Button", identifier: "com.foo:id/greet", label: "Greet", rect: new Rect(0, 100, 390, 44)),
        ];
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(nodes, ("appBundleId", "com.foo.debug")));
        var greet = Assert.Single(await session.LocateAsync(TestId("greet"), TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal("com.foo:id/greet", greet.TestId);
    }
}
