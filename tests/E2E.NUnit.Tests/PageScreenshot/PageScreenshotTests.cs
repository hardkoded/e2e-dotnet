// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;

namespace E2E.NUnit.Tests.PageScreenshot;

/// <summary>
/// <c>test.describe('page screenshot')</c> of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c> (Playwright's
/// <c>page-screenshot.spec.ts</c>). Only <c>should work while navigating</c> is ported so far: the rest of the file is a later slice of
/// hardkoded/e2e-dotnet#1060. <c>App.ScreenshotAsync</c> stands for <c>page.screenshot()</c>.
/// </summary>
public sealed class PageScreenshotTests : PlaywrightTest
{
    [Test]
    public async Task Should_work_while_navigating()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/redirectloop1.html");
        for (var i = 0; i < 10; i++)
        {
            var (width, height) = SizeOfPng(await File.ReadAllBytesAsync(await App.ScreenshotAsync()));
            Assert.That((width, height), Is.EqualTo((500, 500)));
        }
    }

    /// <summary>The width and height in the IHDR chunk of the PNG in <paramref name="png"/>.</summary>
    private static (int Width, int Height) SizeOfPng(byte[] png) =>
        (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
}
