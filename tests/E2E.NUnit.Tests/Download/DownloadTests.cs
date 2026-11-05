// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Download;

/// <summary>
/// <c>test.describe('download')</c> of upstream's <c>apps/testbed/tests/download.e2e.ts</c> (Playwright's
/// <c>download.spec.ts</c>); see <see cref="DownloadEventTests"/> for how downloads are read.
/// </summary>
public sealed class DownloadTests : PlaywrightTest
{
    [Test]
    public async Task Should_be_able_to_download_a_PDF_file()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content("""<a href="/empty.pdf" download>download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        await AssertDownloadToPdf(download, Asset("empty.pdf"));
    }

    // Upstream marks this test `fail`: `route.fulfill` bodies are strings, so binary bytes do not survive the round trip.
    [Test]
    public async Task Should_be_able_to_download_a_inline_PDF_file_via_response_interception()
    {
        await ExpectedToFailAsync("the downloaded bytes differ from the asset", async () =>
        {
            await Browser.GotoAsync("/empty.html");
            await Browser.RouteAsync("**/empty.pdf", async route =>
            {
                var response = await route.FetchAsync();
                var headers = new Dictionary<string, string>(response.Headers) { ["Content-Disposition"] = "attachment" };
                await route.FulfillAsync(new RouteFulfillResponse { Status = response.Status, Headers = headers, Body = await response.TextAsync() });
            });
            await Browser.GotoAsync(Content("""<a href="/empty.pdf">open</a>"""));
            var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
            await AssertDownloadToPdf(download, Asset("empty.pdf"), thrown: true);
        });
    }

    [Test]
    public async Task Should_be_able_to_download_a_inline_PDF_file_via_navigation()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content("""<a href="/empty.pdf">open</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        await AssertDownloadToPdf(download, Asset("empty.pdf"));
    }

    [Test]
    public async Task Should_download_even_if_there_is_no_attachment_value()
    {
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}{Respond(new Dictionary<string, string> { ["content-type"] = "application/octet-stream", ["content-disposition"] = "filename=foo.txt" })}">download</a>"""));
        await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
    }

    [Test]
    public async Task Should_convert_navigation_to_a_resource_with_unsupported_mime_type_into_download()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync("/empty.html");
        await Browser.WaitForDownloadAsync(async () =>
        {
            try
            {
                await Browser.GotoAsync(Respond(new Dictionary<string, string> { ["content-type"] = "application/octet-stream" }));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Upstream: `.catch(() => undefined)`.
            }
        });
    }

    [Test]
    public async Task Should_download_links_with_data_url()
    {
        await Browser.GotoAsync(Content("""<a download="SomeFile.txt" href="data:text/plain;charset=utf8;,hello world">Download!</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("SomeFile.txt"));
    }

    [Test]
    public async Task Should_download_successfully_when_routing()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/chromium-linux.zip" download="foo.zip">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("foo.zip"));
    }

    /// <summary>
    /// Checks a saved download is the file at <paramref name="filePath"/> byte for byte, PDF magic first, Playwright's
    /// <c>assertDownloadToPDF</c>. With <paramref name="thrown"/> it throws instead of asserting (for a body run by <see cref="ExpectedToFailAsync"/>).
    /// </summary>
    private static async Task AssertDownloadToPdf(BrowserDownload download, string filePath, bool thrown = false)
    {
        var data = await File.ReadAllBytesAsync(download.AbsolutePath);
        var expected = await File.ReadAllBytesAsync(filePath);
        var magic = "%PDF"u8.ToArray();
        if (thrown)
        {
            // The known difference is the bytes only: the name is right and the file is a different size or has other content.
            Equal(Path.GetFileName(filePath), download.SuggestedFilename);
            if (!data.AsSpan().StartsWith(magic) || !data.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException($"the downloaded bytes differ from the asset ({data.Length} bytes, expected {expected.Length})");
            }

            return;
        }

        Assert.That(download.SuggestedFilename, Is.EqualTo(Path.GetFileName(filePath)));
        Assert.That(data.AsSpan().StartsWith(magic), Is.True);
        Assert.That(data.AsSpan().SequenceEqual(expected), Is.True);
    }
}
