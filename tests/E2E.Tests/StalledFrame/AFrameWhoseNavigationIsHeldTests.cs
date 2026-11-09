// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.StalledFrame;

/// <summary>
/// Upstream's <c>describe('a frame whose navigation is held')</c> (<c>packages/web/tests/integration/stalled-frame.test.ts</c>,
/// tester-army/e2e#1551, <c>e99a6fa</c>): a frame whose first request is held shows its initial empty document. An observation reads and
/// screenshots the page around it without waiting for that navigation to commit.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AFrameWhoseNavigationIsHeldTests
{
    private const int Width = 320;

    private const int Height = 200;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Opens a page holding an iframe whose request for <c>/held.html</c> never gets an answer, and hands the session to
    /// <paramref name="body"/>. The held request is aborted when the body ends.
    /// </summary>
    private static async Task WithHeldFrameAsync(string attributes, Func<IEngineSession, IPage, Task> body)
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Held</title><p>Around the frame</p>");
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetViewportSizeAsync(Width, Height);
        var held = new List<IRoute>();
        var routed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/held.html", route =>
        {
            lock (held)
            {
                held.Add(route);
            }

            routed.TrySetResult();
            return Task.CompletedTask;
        });
        try
        {
            await page.EvaluateAsync("extra => document.body.insertAdjacentHTML('beforeend', `<iframe ${extra} src=\"/held.html\"></iframe>`)", attributes);
            await routed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal([site.Url, ""], page.Frames.Select(frame => frame.Url).ToArray());

            await body(session, page);
        }
        finally
        {
            IRoute[] pending;
            lock (held)
            {
                pending = [.. held];
            }

            foreach (var route in pending)
            {
                try
                {
                    await route.AbortAsync();
                }
                catch (PlaywrightException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task Leaves_the_observation_whole_and_its_pixels_proven_masked()
    {
        await WithHeldFrameAsync(string.Empty, async (session, page) =>
        {
            var observation = await session.ObserveAsync(new ObserveOptions { Pixels = true }, Budget, CancellationToken.None);

            Assert.Equal(Width, ImageWidth(observation.Pixels!.Png));
            Assert.False(observation.Truncated);
            Assert.Equal(0, observation.Pixels.MaskedRegionCount);
            var pixels = await CapturePixelsAsync(page);
            Assert.True(pixels.MaskingProven);
        });
    }

    [Fact]
    public async Task Masks_a_secure_field_the_parent_wrote_into_the_initial_document()
    {
        await WithHeldFrameAsync(string.Empty, async (_, page) =>
        {
            await page.EvaluateAsync("() => { document.querySelector('iframe').contentDocument.body.innerHTML = '<input type=\"password\" value=\"hunter2\">'; }");

            var pixels = await CapturePixelsAsync(page);

            Assert.Equal(1, pixels.MaskedRegionCount);
            Assert.True(pixels.MaskingProven);
        });
    }

    [Fact]
    public async Task Captures_past_a_sandboxed_frame_no_script_can_reach()
    {
        await WithHeldFrameAsync("sandbox", async (_, page) =>
        {
            var pixels = await CapturePixelsAsync(page);

            Assert.Equal(0, pixels.MaskedRegionCount);
            Assert.True(pixels.MaskingProven);
        });
    }

    private static Task<PixelCapture> CapturePixelsAsync(IPage page) =>
        WebObserve.CapturePixelsAsync(page, new OperationBudget(Budget, "observe", CancellationToken.None), new ViewportSize { Width = Width, Height = Height });

    /// <summary>The width in pixels of a PNG, read from its header.</summary>
    private static int ImageWidth(byte[] png) => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
}
