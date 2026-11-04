// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Download;

/// <summary>
/// <c>test.describe('download event')</c> of upstream's <c>apps/testbed/tests/download.e2e.ts</c> (Playwright's
/// <c>download.spec.ts</c>). <c>page.waitForEvent('download')</c> is <c>Browser.WaitForDownloadAsync(trigger)</c>, which saves the
/// file, and <c>download.path()</c> is the saved file's <c>AbsolutePath</c>. e2e has no <c>download.url()</c>, so its assertions are
/// dropped. Not ported, as upstream: should report downloads with acceptDownloads: false, should report proper download url when
/// download is from download attribute, should save to user-specified path without updating original path, should save to two
/// different paths with multiple saveAs calls, should save to overwritten filepath, should create subdirectories when saving to
/// non-existent user-specified path, should error when saving with downloads disabled, should error when saving after deletion,
/// should delete file, should expose stream, should delete downloads on context destruction, should delete downloads on browser
/// gone, should save downloads to artifactsDir, should close the context without awaiting the failed download, should close the
/// context without awaiting the download, should throw if browser dies, should be able to cancel pending downloads, should not fail
/// explicitly to cancel a download even if that is already finished, should save to user-specified path (no
/// <c>download.saveAs</c>, <c>delete</c>, <c>createReadStream</c>, <c>cancel</c>, <c>failure</c>, <c>acceptDownloads</c>, context or
/// browser control from the test).
/// </summary>
public sealed class DownloadEventTests : PlaywrightTest
{
    [Test]
    public async Task Should_report_download_when_navigation_turns_into_download_smoke()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync("/empty.html");
        Exception? responseOrError = null;
        var download = await Browser.WaitForDownloadAsync(async () =>
        {
            responseOrError = await Record(() => Browser.GotoAsync($"{PREFIX}/download"));
        });
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
        Assert.That(responseOrError, Is.Not.Null);
        Assert.That(responseOrError!.Message, Does.Contain("Download is starting"));
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/empty.html"));
    }

    [Test]
    public async Task Should_work_with_Cross_Origin_Opener_Policy()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync("/empty.html");
        Exception? responseOrError = null;
        var url = Respond(new Dictionary<string, string>
        {
            ["content-type"] = "application/octet-stream",
            ["content-disposition"] = "attachment",
            ["cross-origin-opener-policy"] = "same-origin",
        });
        var download = await Browser.WaitForDownloadAsync(async () =>
        {
            responseOrError = await Record(() => Browser.GotoAsync(url));
        });
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
        Assert.That(responseOrError, Is.Not.Null);
        Assert.That(responseOrError!.Message, Does.Contain("Download is starting"));
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/empty.html"));
    }

    [Test]
    public async Task Should_report_downloads_with_acceptDownloads_true()
    {
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/download">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_report_downloads_for_download_attribute()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/chromium-linux.zip" download="foo.zip">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("foo.zip"));
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
    }

    [Test]
    public async Task Should_report_non_navigation_downloads()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""<a download="file.txt" href="{PREFIX}{Respond(new Dictionary<string, string> { ["content-type"] = "application/octet-stream" })}">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_report_download_path_within_page_on_download_handler_for_Files()
    {
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/download">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_report_download_path_within_page_on_download_handler_for_Blobs()
    {
        await Browser.GotoAsync("/download-blob.html");
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_report_alt_click_downloads()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}{Respond(new Dictionary<string, string> { ["content-type"] = "application/octet-stream" })}">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync(new ClickOptions { Modifiers = [KeyModifier.Alt] }));
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_report_new_window_downloads()
    {
        await Browser.GotoAsync(Content($"""<a target=_blank href="{PREFIX}/download">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
    }

    [Test]
    public async Task Should_download_large_binary_zip()
    {
        var fileContent = new byte[1 << 20];
        for (var i = 0; i < fileContent.Length; i++)
        {
            fileContent[i] = (byte)(((uint)(i + 1) * 2654435761u) >> 24);
        }

        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/binary?bytes={1 << 20}" download="binary.zip">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        var downloaded = await File.ReadAllBytesAsync(download.AbsolutePath);
        Assert.That(downloaded, Has.Length.EqualTo(fileContent.Length));
        Assert.That(downloaded.AsSpan().SequenceEqual(fileContent), Is.True);
    }

    [Test]
    public async Task Should_report_downloads_with_interception()
    {
        await Browser.RouteAsync(new System.Text.RegularExpressions.Regex(".*"), route => route.ContinueAsync());
        await Browser.GotoAsync(Content($"""<a href="{PREFIX}/download">download</a>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.Locator("a").TapAsync());
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task Should_emit_download_event_from_nested_iframes()
    {
        var third = Content($""" <a href="{PREFIX}/download">download</a>""");
        var second = Content($"""<iframe src="{PREFIX}{third}"></iframe>""");
        await Browser.GotoAsync(Content($"""<iframe src="{PREFIX}{second}"></iframe>"""));
        var download = await Browser.WaitForDownloadAsync(() => Browser.FrameLocator("iframe").FrameLocator("iframe").Locator("a").TapAsync());
        Assert.That(File.Exists(download.AbsolutePath), Is.True);
        Assert.That(await File.ReadAllTextAsync(download.AbsolutePath), Is.EqualTo("Hello world"));
    }

    /// <summary><c>/respond</c> answering with <paramref name="headers"/> and <paramref name="body"/>.</summary>
    internal static string Respond(Dictionary<string, string> headers, string body = "Hello world") =>
        $"/respond?headers={Uri.EscapeDataString(JsonSerializer.Serialize(headers))}&body={Uri.EscapeDataString(body)}";

    private static async Task<Exception?> Record(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex;
        }
    }
}
