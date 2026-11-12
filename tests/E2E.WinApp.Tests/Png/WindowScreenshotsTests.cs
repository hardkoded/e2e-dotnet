// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Png;

/// <summary>.NET-only: upstream has no Windows engine. The screenshot path: BGRA in, a masked RGBA PNG out.</summary>
public sealed class WindowScreenshotsTests
{
    private static RawScreenshot Shot(int width, int height) =>
        new(Enumerable.Repeat(new byte[] { 10, 20, 30, 0 }, width * height).SelectMany(pixel => pixel).ToArray(), width, height);

    [Fact]
    public void Swaps_the_channels_and_makes_the_image_opaque()
    {
        var (png, masked) = WindowScreenshots.Render(Shot(2, 1), []);
        var image = PngReader.Read(png);
        Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 0, 0));
        Assert.Equal(0, masked);
    }

    [Fact]
    public void Paints_each_mask_black_rounding_outward_and_clipped_to_the_image()
    {
        var (png, masked) = WindowScreenshots.Render(Shot(4, 3), [new Rect(1.4, 0.6, 1.2, 1), new Rect(-5, 2, 100, 100)]);
        var image = PngReader.Read(png);
        Assert.Equal(2, masked);
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 1, 0));
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 2, 1));
        Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 3, 0));
        Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 0, 0));
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 0, 2));
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 3, 2));
    }

    [Fact]
    public void Refuses_a_capture_whose_bytes_do_not_match_its_size()
    {
        Assert.Throws<InvalidDataException>(() => WindowScreenshots.Render(new RawScreenshot(new byte[3], 1, 1), []));
    }

    [Fact]
    public async Task The_session_screenshot_covers_every_secure_field_in_the_window_capture()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Screenshot = Shot(800, 600);
        var shot = await session.ScreenshotAsync(CancellationToken.None);
        Assert.Equal(1, shot.MaskedRegionCount);
        Assert.Equal(1, shot.Scale);
        var image = PngReader.Read(shot.Png);

        // The password box sits at (120,120) 200x32 on the screen; the window's origin is (100,50).
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 25, 75));
        Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 219, 101));
        Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 25, 60));
        Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 221, 75));
    }

    [Fact]
    public async Task The_session_screenshot_reports_image_pixels_per_logical_pixel()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        harness.Driver.Script(session.App!.ProcessId, FakeWindowDriver.MainHandle, "Settings", RawElements.Settings(), new Rect(100, 50, 1600, 1200), scale: 2);
        harness.Driver.Screenshot = Shot(1600, 1200);
        var shot = await session.ScreenshotAsync(CancellationToken.None);
        Assert.Equal(2, shot.Scale);
    }

    [Fact]
    public async Task The_screenshot_artifact_is_numbered_masked_and_named_by_its_label()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-winapp-artifacts-").FullName;
        try
        {
            await using var harness = new Harness { ArtifactsDir = directory };
            var session = await harness.OpenAsync();
            harness.Driver.Screenshot = Shot(800, 600);
            Assert.Equal("screenshots/001-first_shot.png", await session.ScreenshotArtifactAsync("first shot", CancellationToken.None));
            Assert.Equal("screenshots/002-screenshot.png", await session.ScreenshotArtifactAsync(null, CancellationToken.None));
            var image = PngReader.Read(await File.ReadAllBytesAsync(Path.Combine(directory, "screenshots", "002-screenshot.png")));
            Assert.Equal((0, 0, 0, 255), PngReader.Pixel(image, 25, 75));
            Assert.Equal((30, 20, 10, 255), PngReader.Pixel(image, 25, 60));
            Assert.Empty(Directory.GetFiles(Path.Combine(directory, "screenshots"), "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task A_screenshot_artifact_outside_an_attempt_is_INVALID_STATE()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ScreenshotArtifactAsync("x", CancellationToken.None));
        Assert.Equal(EngineErrorCodes.InvalidState, error.Code);
    }

    [Fact]
    public async Task A_secure_field_without_a_box_withholds_the_screenshot()
    {
        await using var harness = new Harness();
        var session = await harness.OpenAsync(RawElements.Window("W", RawElements.PasswordBox("p", "Pin") with { Bounds = null }));
        var error = await Assert.ThrowsAsync<EngineException>(() => session.ScreenshotAsync(CancellationToken.None));
        Assert.Equal(EngineErrorCodes.EngineFailure, error.Code);
        Assert.Contains("screenshot withheld", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Driver.Calls, call => call.StartsWith("screenshot", StringComparison.Ordinal));
    }
}
