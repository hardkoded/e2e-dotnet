// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// Downloads from a browser on another machine (upstream's <c>downloads.ts</c>). Playwright asks a CDP-attached browser to save each
/// file under a directory of the runner's, which a remote browser does not have, so the download is canceled. A provider that serves
/// downloads names a directory on the browser's own disk; the browser saves there, and the provider reads each finished file back.
/// </summary>
internal static class Downloads
{
    /// <summary>One browser-level CDP session per connection: a download behavior lives as long as the session that set it.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IBrowser, Lazy<Task<ICDPSession>>> BrowserSessions = new();

    /// <summary>
    /// Has the browser behind <paramref name="page"/> save the downloads of the page's context under <paramref name="dir"/> on its own
    /// disk, each file named by its download id, with the download events Playwright's waiter reads left on (upstream's
    /// <c>saveDownloadsTo</c>).
    /// </summary>
    public static async Task SaveDownloadsToAsync(IPage page, string dir)
    {
        var context = page.Context;
        var browser = context.Browser ?? throw new EngineException(EngineErrorCodes.InvalidState, "the page has no browser to set a download directory on");
        var pageSession = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
        string? browserContextId;
        try
        {
            var info = (await pageSession.SendAsync("Target.getTargetInfo").ConfigureAwait(false))!.Value.GetProperty("targetInfo");
            browserContextId = info.TryGetProperty("browserContextId", out var id) ? id.GetString() : null;
        }
        finally
        {
            await DetachQuietlyAsync(pageSession).ConfigureAwait(false);
        }

        // A session that failed to open is not kept, so the next download opens another.
        var entry = BrowserSessions.GetValue(browser, static candidate => new Lazy<Task<ICDPSession>>(candidate.NewBrowserCDPSessionAsync));
        ICDPSession session;
        try
        {
            session = await entry.Value.ConfigureAwait(false);
        }
        catch
        {
            BrowserSessions.Remove(browser);
            throw;
        }

        var arguments = new Dictionary<string, object>
        {
            ["behavior"] = "allowAndName",
            ["downloadPath"] = dir,
            ["eventsEnabled"] = true,
        };
        if (browserContextId is not null)
        {
            arguments["browserContextId"] = browserContextId;
        }

        await session.SendAsync("Browser.setDownloadBehavior", arguments).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies a finished download off the browser's disk to <paramref name="file"/> through the provider: it lies under the provider's
    /// directory, named by its download id, as <see cref="SaveDownloadsToAsync"/> had the browser name it. A download that failed or
    /// was canceled fails with the browser's reason (upstream's <c>saveFromBrowser</c>).
    /// </summary>
    public static async Task SaveFromBrowserAsync(IDownload download, LeaseDownloads downloads, string file, CancellationToken cancellationToken)
    {
        var failure = await download.FailureAsync().ConfigureAwait(false);
        if (failure is not null)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"download failed: {failure}", retryable: false);
        }

        // Playwright names its own record of the file by the same download id.
        // The provider's machine may not share the runner's separator, so the directory keeps its own.
        var separator = downloads.Dir.Contains('\\', StringComparison.Ordinal) && !downloads.Dir.Contains('/', StringComparison.Ordinal) ? '\\' : '/';
        var saved = downloads.Dir.TrimEnd('/', '\\') + separator + Path.GetFileName(await download.PathAsync().ConfigureAwait(false));
        var bytes = await downloads.ReadAsync(saved, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(file, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves a download Playwright received for the runner to <paramref name="file"/>. When the file may have stayed on another
    /// machine, <paramref name="unserved"/> says why, and a failed save names it (upstream's <c>saveLocally</c>).
    /// </summary>
    public static async Task SaveLocallyAsync(IDownload download, string file, string? unserved)
    {
        try
        {
            await download.SaveAsAsync(file).ConfigureAwait(false);
        }
        catch (Exception cause) when (unserved is not null && WebErrors.IsPlaywright(cause))
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"download failed: {WebErrors.Message(cause)}; {unserved}", retryable: false, cause);
        }
    }

    private static async Task DetachQuietlyAsync(ICDPSession session)
    {
        try
        {
            await session.DetachAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // The target or connection is already gone.
        }
    }
}
