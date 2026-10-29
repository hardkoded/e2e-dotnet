// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// Hides the text caret for the duration of a screenshot without touching the <c>style</c> attribute of an editable
/// element (upstream <c>screenshot-caret.ts</c>).
/// </summary>
/// <remarks>
/// Playwright's <c>caret: 'hide'</c> writes an inline <c>caret-color</c> on every editable element and restores it
/// after the frame, so a framework hydrating at that moment reads the written style as a server mismatch. A
/// constructed stylesheet changes no element's style, and unlike a <c>&lt;style&gt;</c> element it is not blocked by
/// a <c>style-src</c> policy.
/// </remarks>
internal static class ScreenshotCaret
{
    /// <summary>The most one frame may hold the capture while taking or giving back the sheet.</summary>
    private static readonly TimeSpan FrameBudget = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Runs in the page (upstream <c>hideCaret</c>). Adopts the caret stylesheet into the frame's document and every
    /// shadow root below it, open or closed, and returns the function that takes it back. Closed roots are reached
    /// through the record the init script keeps, since nothing else can name them.
    /// </summary>
    private const string HideCaret = """
        () => {
          const closed = Reflect.get(globalThis, Symbol.for("e2e.closedShadowRoots"));
          const sheet = new CSSStyleSheet();
          sheet.replaceSync("input, textarea, [contenteditable] { caret-color: transparent !important; }");
          const roots = [];
          const visit = (root) => {
            roots.push(root);
            root.adoptedStyleSheets = [...root.adoptedStyleSheets, sheet];
            for (const element of root.querySelectorAll("*")) {
              const shadow = element.shadowRoot ?? (closed instanceof WeakMap ? closed.get(element) : null);
              if (shadow instanceof ShadowRoot) visit(shadow);
            }
          };
          visit(document);
          return () => {
            for (const root of roots) root.adoptedStyleSheets = root.adoptedStyleSheets.filter((other) => other !== sheet);
          };
        }
        """;

    /// <summary>Best effort: a frame that stalls or navigated away must not hold the capture.</summary>
    private static async Task<T?> InFrameAsync<T>(Task<T> work)
    {
        try
        {
            return await work.WaitAsync(FrameBudget).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>Runs the undo handle's function, then disposes the handle.</summary>
    private static async Task<bool> UndoAsync(IJSHandle undo)
    {
        try
        {
            await undo.EvaluateAsync("show => show()").ConfigureAwait(false);
        }
        finally
        {
            await undo.DisposeAsync().ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Adopts the sheet in one frame. The evaluate cannot be cancelled, so one that lands after the budget has its
    /// undo handle run at once: a late setup result must not leave the sheet adopted and the caret hidden.
    /// </summary>
    private static async Task<IJSHandle?> AdoptInFrameAsync(IFrame frame)
    {
        var work = frame.EvaluateHandleAsync(HideCaret);
        var undo = await InFrameAsync(work).ConfigureAwait(false);
        if (undo is not null)
        {
            return undo;
        }

        _ = work.ContinueWith(
            late => late.Status == TaskStatus.RanToCompletion ? UndoAsync(late.Result).ContinueWith(failed => _ = failed.Exception, TaskScheduler.Default) : Task.FromResult(late.Exception),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return null;
    }

    /// <summary>
    /// Runs <paramref name="capture"/> with the caret stylesheet adopted in every frame, and takes it back afterwards
    /// whatever the capture does (upstream <c>withHiddenCaret</c>).
    /// </summary>
    public static async Task<T> WithHiddenCaretAsync<T>(IPage page, Func<Task<T>> capture)
    {
        var undos = await Task.WhenAll(page.Frames.Select(AdoptInFrameAsync)).ConfigureAwait(false);
        try
        {
            return await capture().ConfigureAwait(false);
        }
        finally
        {
            await Task.WhenAll(undos.Select(async undo =>
            {
                if (undo is not null)
                {
                    await InFrameAsync(UndoAsync(undo)).ConfigureAwait(false);
                }
            })).ConfigureAwait(false);
        }
    }
}
