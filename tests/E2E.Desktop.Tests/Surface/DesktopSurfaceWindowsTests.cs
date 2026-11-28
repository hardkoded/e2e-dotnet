// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: windows')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfaceWindowsTests : SurfaceTestBase
{
    [Fact]
    public async Task Reads_only_the_window_a_test_chose_until_it_goes_back_to_the_focused_one()
    {
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["listWindows"] =
                [
                    Windows(_ =>
                        [new { id = "w-mine", is_focused = true }, new { id = "w-panel", is_focused = false }]
                    ),
                ],
            }
        );
        await Rejects(surface.UseWindowAsync("w-gone", Operation(timeoutMs: 5_000)), "NOT_ACTIONABLE");
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        await surface.ObserveAsync(Operation(), null);
        Assert.Equal("w-panel", Last<SnapshotRequest>(fake, "snapshot")!.WindowId);
        await surface.UseWindowAsync(null, Operation(timeoutMs: 5_000));
        await surface.ObserveAsync(Operation(), null);
        Assert.Equal("w-mine", Last<SnapshotRequest>(fake, "snapshot")!.WindowId);
    }

    [Fact]
    public async Task Acts_on_no_node_read_in_another_window_than_the_one_a_test_chose()
    {
        var (surface, fake, byTestId) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Renamed("macos/appkit-form", "w-8032", "w-mine")],
                ["listWindows"] = [Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-panel" }])],
            }
        );
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        await Rejects(
            surface.PerformAsync(byTestId("sign-in"), new LocatorAction.Tap(), Operation()),
            "NODE_STALE",
            retryable: true
        );
        Assert.Empty(Inputs(fake));
    }

    [Fact]
    public async Task Refuses_to_read_a_chosen_window_that_closed_between_the_list_and_the_read()
    {
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] = [Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-panel" }])],
                ["snapshot"] =
                [
                    SnapshotAnswer("macos/appkit-form"),
                    SnapshotAnswer("macos/appkit-form"),
                    Failure("WINDOW_NOT_FOUND", "not_delivered"),
                ],
            }
        );
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "INVALID_STATE", @"useWindow\(\) chose");
    }

    [Fact]
    public async Task Refuses_to_read_a_chosen_window_once_it_closed_rather_than_show_an_empty_screen()
    {
        var both = Windows(_ => [new { id = "w-mine", is_focused = true }, new { id = "w-panel" }]);
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] = [both, both, both, Windows(_ => [new { id = "w-mine", is_focused = true }])],
            }
        );
        await surface.UseWindowAsync("w-panel", Operation(timeoutMs: 5_000));
        await Rejects(surface.LocateAsync(TestId("nowhere"), Operation()), "INVALID_STATE", @"useWindow\(\) chose");
        await Rejects(surface.PressKeyAsync("Enter", Operation()), message: @"useWindow\(\) chose");
    }
}
