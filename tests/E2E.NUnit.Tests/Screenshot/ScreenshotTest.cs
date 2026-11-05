// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
namespace E2E.NUnit.Tests.Screenshot;

/// <summary>
/// The helpers of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c> (Playwright's <c>page-screenshot.spec.ts</c>):
/// <c>storeMaskedReference</c>, <c>storeReference</c>, <c>attachFrame</c> and <c>addStyleTag</c>, and its <c>test.afterEach</c>.
/// </summary>
public abstract class ScreenshotTest : PlaywrightTest
{
    private readonly HashSet<string> _stored = [];

    /// <summary>One element a mask covers: a selector and its match index, inside the iframe <see cref="Frame"/> matches when set.</summary>
    protected sealed record MaskTarget(string Selector, int Index, string? Frame = null);

    /// <summary>Where <c>ToHaveScreenshotAsync(name)</c> in <paramref name="callerFile"/> reads its stored screenshot on the <c>web</c> target.</summary>
    private static string StoredPath(string name, string callerFile) =>
        Path.Combine(callerFile + "-snapshots", $"{name}-web-{ScreenshotContext.OperatingSystemName()}.png");

    /// <summary>
    /// Paints a <paramref name="color"/> box over each of <paramref name="targets"/>, as a <c>mask</c> would cover it, keeps the viewport
    /// as the stored screenshot <paramref name="name"/>, then takes the boxes away.
    /// </summary>
    protected async Task StoreMaskedReferenceAsync(string name, IReadOnlyList<MaskTarget> targets, string color = "#ff00ff", [CallerFilePath] string callerFile = "")
    {
        await Browser.EvaluateAsync<object>(
            """
            ({ targets, color }) => {
              for (const { frame, selector, index } of targets) {
                const host = frame === null ? undefined : document.querySelector(frame);
                const origin = host === undefined ? { left: 0, top: 0 } : host.getBoundingClientRect();
                const offset = host === undefined ? { left: 0, top: 0 } : { left: host.clientLeft, top: host.clientTop };
                const element = (host === undefined ? document : host.contentDocument).querySelectorAll(selector)[index];
                const rect = element.getBoundingClientRect();
                const box = document.createElement('div');
                box.setAttribute('data-test-mask', '');
                box.style.cssText = 'position: fixed; z-index: 2147483647; pointer-events: none; margin: 0; border: 0; padding: 0;';
                box.style.left = (origin.left + offset.left + rect.left) + 'px';
                box.style.top = (origin.top + offset.top + rect.top) + 'px';
                box.style.width = rect.width + 'px';
                box.style.height = rect.height + 'px';
                box.style.background = color;
                document.documentElement.appendChild(box);
              }
            }
            """,
            new { targets = targets.Select(target => new { frame = target.Frame, selector = target.Selector, index = target.Index }).ToArray(), color });
        await StoreReferenceAsync(name, callerFile);
        await Browser.EvaluateAsync<object>("() => { for (const box of document.querySelectorAll('[data-test-mask]')) box.remove(); }");
    }

    /// <summary>Keeps the viewport, as <c>app.screenshot</c> takes it, as the stored screenshot <paramref name="name"/> until the test ends.</summary>
    protected async Task StoreReferenceAsync(string name, [CallerFilePath] string callerFile = "")
    {
        var file = StoredPath(name, callerFile);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        _stored.Add(file);
        File.Copy(await App.ScreenshotAsync(name), file, overwrite: true);
    }

    /// <summary>Appends an iframe with <paramref name="id"/> loading <paramref name="url"/>, Playwright's <c>attachFrame</c>, once it has loaded.</summary>
    protected Task AttachFrameAsync(string id, string url) =>
        Browser.EvaluateAsync<object>(
            """
            async ({ id, url }) => {
              const frame = document.createElement('iframe');
              frame.src = url;
              frame.id = id;
              document.body.appendChild(frame);
              await new Promise((x) => (frame.onload = x));
            }
            """,
            new { id, url });

    /// <summary>Adds a <c>&lt;style&gt;</c> with <paramref name="css"/> to the page, Playwright's <c>addStyleTag({ content })</c>.</summary>
    protected Task AddStyleTagAsync(string css) =>
        Browser.EvaluateAsync<object>("(css) => { const style = document.createElement('style'); style.textContent = css; document.head.appendChild(style); }", css);

    [TearDown]
    public void RemoveStoredScreenshots()
    {
        foreach (var file in _stored)
        {
            File.Delete(file);
        }

        _stored.Clear();
    }
}
