// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Desktop;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Desktop.Tests.Surface;

/// <summary>Ported from <c>describe('DesktopSurface: pixels')</c> in upstream's <c>packages/desktop/tests/unit/surface.test.ts</c>.</summary>
public sealed class DesktopSurfacePixelsTests : SurfaceTestBase
{
    private static readonly ObserveOptions WithPixels = new() { Pixels = true };

    private static Answer Form() => Renamed("macos/sign-in-form", "w-8321", "w-mine");

    private static Answer Plain() => Replaced(Form(), "\"secure\"", "\"required\"");

    private static Answer CaptureOf(Answer answer)
    {
        var (width, height) = TreeSize(answer);
        return Capture(width, height);
    }

    private static object Box(int x, int y, int width, int height) =>
        new
        {
            x,
            y,
            width,
            height,
        };

    [Fact]
    public async Task Captures_the_window_with_every_secure_field_painted_over_into_the_attempts_screenshots()
    {
        var shot = Form();
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [shot], ["screenshot"] = [CaptureOf(shot)] }
        );
        StartAttempt(surface);
        var relative = await surface.ScreenshotAsync("shot", Operation());
        Assert.Equal("screenshots/001-shot.png", relative);
        // The unmasked capture goes to the worker's private directory, and is removed.
        var file = Last<ScreenshotRequest>(fake, "screenshot")!;
        Assert.Equal("w-mine", file.WindowId);
        Assert.StartsWith(surface.Home!, file.Path, StringComparison.Ordinal);
        Assert.False(File.Exists(file.Path));
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        var image = ImageOps.DecodePng(observed.Pixels!.Png);
        Assert.Equal((420, 352, 1d), (image.Width, image.Height, observed.Pixels.Scale));
        Assert.Equal(1, observed.Pixels.MaskedRegionCount);
    }

    [Fact]
    public async Task Withholds_a_screenshot_when_the_snapshot_did_not_read_the_whole_window()
    {
        var partial = Merged(Form(), new { complete = false });
        var (surface, _, _) = await Launched(new Scenario { ["snapshot"] = [partial] });
        StartAttempt(surface);
        await Rejects(
            surface.ScreenshotAsync("partial", Operation()),
            "POLICY_DENIED",
            "did not read the whole window"
        );
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        // Withheld pixels still report the regions masked, none, so the harness reads unproven masking, not a missing screenshot.
        Assert.Equal(0, observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Tries_a_window_with_no_secure_field_again_when_its_read_ran_out_of_time_and_refuses_one_deeper_than_maxDepth()
    {
        var window = Plain();
        var outOfTime = Merged(window, new { complete = false });
        var timed = await Launched(
            new Scenario { ["screenshot"] = [CaptureOf(window)], ["snapshot"] = [window, outOfTime, window] },
            new LaunchOptions { Observe = false }
        );
        var first = await timed.Surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(first.Pixels);
        Assert.Null(first.MaskedRegionCount);
        var second = await timed.Surface.ObserveAsync(Operation(), WithPixels);
        Assert.NotNull(second.Pixels);
        Assert.Equal(0, second.Pixels.MaskedRegionCount);
        var deep = WithTree(window, tree => tree["children_count"] = 999);
        var (surface, _, _) = await Launched(
            new Scenario { ["screenshot"] = [CaptureOf(window)], ["snapshot"] = [window, deep] },
            new LaunchOptions { Observe = false }
        );
        StartAttempt(surface);
        await Rejects(
            surface.ScreenshotAsync("deep", Operation()),
            "POLICY_DENIED",
            @"deeper than desktop\(\{ maxDepth \}\) reads \(24 levels\)"
        );
        Assert.Equal(0, (await surface.ObserveAsync(Operation(), WithPixels)).MaskedRegionCount);
    }

    [Fact]
    public async Task Reports_a_capture_that_failed_as_no_screenshot_not_as_masking_it_could_not_prove()
    {
        var denied = Failure(
            "PERM_DENIED",
            "not_delivered",
            "screencapture could not capture the window; it needs Screen Recording permission"
        );
        var (surface, _, _) = await Launched(new Scenario { ["snapshot"] = [Form()], ["screenshot"] = [denied] });
        StartAttempt(surface);
        await Rejects(surface.ScreenshotAsync("denied", Operation()), message: "Screen Recording permission");
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        Assert.Null(observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Fails_an_observation_cancelled_while_its_pixels_are_captured_rather_than_return_it_without_them()
    {
        var (surface, fake, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [Form()],
                ["screenshot"] = [Answer.Of(new { scale_factor = 1 }) with { SleepMs = 5_000 }],
            }
        );
        using var controller = new CancellationTokenSource();
        var observed = Rejects(surface.ObserveAsync(Operation(signal: controller.Token), WithPixels), "CANCELLED");
        while (fake.Of("screenshot").Count == 0)
        {
            await Task.Delay(10);
        }

        await controller.CancelAsync();
        await observed;
    }

    [Fact]
    public async Task Refuses_a_screenshot_as_a_policy_when_a_password_field_has_no_bounds_to_mask()
    {
        // The AppKit form's password field was captured with no bounds.
        var (surface, _, _) = await Launched();
        StartAttempt(surface);
        await Rejects(surface.ScreenshotAsync("unmaskable", Operation()), "POLICY_DENIED");
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        Assert.Equal(0, observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Withholds_a_screenshot_when_a_secure_field_moved_while_it_was_captured_and_keeps_one_that_did_not()
    {
        var shot = Form();
        var moved = Replaced(shot, "\"y\":675", "\"y\":700", all: false);
        var steady = await Launched(new Scenario { ["screenshot"] = [CaptureOf(shot)], ["snapshot"] = [shot] });
        StartAttempt(steady.Surface);
        Assert.EndsWith(
            "steady.png",
            await steady.Surface.ScreenshotAsync("steady", Operation()),
            StringComparison.Ordinal
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["screenshot"] = [CaptureOf(shot)], ["snapshot"] = [shot, shot, shot, moved] }
        );
        StartAttempt(surface);
        await Rejects(
            surface.ScreenshotAsync("moved", Operation()),
            "POLICY_DENIED",
            "window changed while it was captured"
        );
    }

    [Fact]
    public async Task Withholds_a_capture_as_a_policy_when_the_read_after_it_fails_and_a_secure_field_is_on_screen()
    {
        var shot = Form();
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["screenshot"] = [CaptureOf(shot)],
                ["snapshot"] = [shot, shot, Failure("WINDOW_NOT_FOUND", "not_delivered")],
            },
            new LaunchOptions { Observe = false }
        );
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        Assert.Equal(0, observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Withholds_a_capture_of_the_wrong_size_as_a_policy_when_a_secure_field_is_on_screen()
    {
        var shot = Form();
        var (width, height) = TreeSize(shot);
        var (surface, _, _) = await Launched(
            new Scenario { ["screenshot"] = [Capture(width + 10, height)], ["snapshot"] = [shot] }
        );
        StartAttempt(surface);
        await Rejects(surface.ScreenshotAsync("wrong-size", Operation()), "POLICY_DENIED", "does not match the window");
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        Assert.Equal(0, observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Reports_a_window_with_no_secure_field_that_changed_size_during_the_capture_as_no_screenshot_so_the_next_capture_may_work()
    {
        var window = Plain();
        var grown = Replaced(window, "\"height\":352", "\"height\":360", all: false);
        var shot = await Launched(
            new Scenario { ["screenshot"] = [CaptureOf(window)], ["snapshot"] = [window, window, window, grown] }
        );
        StartAttempt(shot.Surface);
        await Rejects(
            shot.Surface.ScreenshotAsync("grown", Operation()),
            "ENGINE_FAILURE",
            "changed size while it was captured"
        );
        var (surface, _, _) = await Launched(
            new Scenario { ["screenshot"] = [CaptureOf(window)], ["snapshot"] = [window, window, window, grown] }
        );
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Null(observed.Pixels);
        Assert.Null(observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Takes_no_screenshot_with_interactive_snapshots_when_the_window_changed_size_between_the_read_and_the_whole_read()
    {
        static Answer Tree(int width) =>
            Screen(
                "w-mine",
                new
                {
                    name = "Form",
                    bounds = Box(0, 0, width, 100),
                    children = new object[]
                    {
                        new
                        {
                            role = "button",
                            name = "Save",
                            ref_id = "@s:e1",
                            bounds = Box(width - 50, 10, 40, 20),
                        },
                    },
                }
            );
        var (surface, _, _) = await Launched(
            new Scenario { ["snapshot"] = [Tree(100), Tree(100), Tree(200)], ["screenshot"] = [Capture(200, 100)] },
            new LaunchOptions
            {
                Observe = false,
                Options = new DesktopOptions { Snapshot = DesktopSnapshotMode.Interactive },
            }
        );
        var observed = await surface.ObserveAsync(Operation(), WithPixels);
        Assert.Equal(100, observed.Viewport!.Width);
        Assert.Null(observed.Pixels);
        Assert.Null(observed.MaskedRegionCount);
    }

    [Fact]
    public async Task Reads_the_whole_window_for_a_screenshot_even_with_interactive_snapshots()
    {
        var (surface, fake, _) = await Launched(
            new Scenario { ["snapshot"] = [Form()] },
            new LaunchOptions { Options = new DesktopOptions { Snapshot = DesktopSnapshotMode.Interactive } }
        );
        StartAttempt(surface);
        try
        {
            await surface.ScreenshotAsync("shot", Operation());
        }
        catch (Exception)
        {
            // As upstream's `.catch(() => undefined)`: the screenshot may be refused; the reads are what this test is about.
        }

        var reads = fake.Of("snapshot").Select(call => ((SnapshotRequest)call.Args[0]!).Interactive).ToArray();
        Assert.False(reads[^1]);
        Assert.Contains(true, reads);
    }

    [Fact]
    public async Task Keeps_the_ids_of_an_interactive_screen_across_the_whole_reads_a_screenshots_mask_takes()
    {
        var field = new
        {
            role = "textfield",
            name = "Name",
            ref_id = "@s:e1",
            bounds = Box(10, 10, 50, 20),
        };
        var window = new { name = "Form", bounds = Box(0, 0, 100, 100) };
        var interactive = Screen(
            "w-mine",
            new
            {
                window.name,
                window.bounds,
                children = new object[] { field },
            }
        );
        var whole = Screen(
            "w-mine",
            new
            {
                window.name,
                window.bounds,
                children = new object[]
                {
                    new
                    {
                        role = "group",
                        name = "Details",
                        children = new object[] { field },
                    },
                },
            }
        );
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["snapshot"] = [interactive, interactive, interactive, whole, whole, interactive],
                ["screenshot"] = [Capture(100, 100)],
            },
            new LaunchOptions
            {
                Observe = false,
                Options = new DesktopOptions { Snapshot = DesktopSnapshotMode.Interactive },
            }
        );
        StartAttempt(surface);
        var before = await surface.ObserveAsync(Operation(), null);
        await surface.ScreenshotAsync("shot", Operation());
        var after = await surface.ObserveAsync(Operation(), null);
        Assert.Equal(before.Roots[0].Children[0].Ref, after.Roots[0].Children[0].Ref);
    }

    [Fact]
    public async Task Refuses_a_screenshot_outside_an_attempt_and_of_an_app_with_no_window()
    {
        var (surface, _, _) = await Launched(
            new Scenario
            {
                ["listWindows"] = [Windows(_ => [new { id = "w-mine", is_focused = true }]), Ok(Array.Empty<object>())],
            },
            new LaunchOptions { Observe = false }
        );
        await Rejects(surface.ScreenshotAsync("outside", Operation()), "INVALID_STATE");
        StartAttempt(surface);
        await Rejects(surface.ScreenshotAsync("none", Operation()), "INVALID_STATE", "no window");
    }
}
