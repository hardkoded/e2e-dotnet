// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.WinApp;

/// <summary>Shared helpers for the Windows app engine: error constructors, geometry, and the screen location.</summary>
public static partial class WinAppSupport
{
    public static EngineException Cancelled(string text) => new(EngineErrorCodes.Cancelled, text, retryable: false);

    public static EngineException InvalidState(string text) => new(EngineErrorCodes.InvalidState, text, retryable: false);

    public static EngineException NotActionable(string text) => new(EngineErrorCodes.NotActionable, text, retryable: false);

    public static EngineException Unsupported(string text) => new(EngineErrorCodes.UnsupportedCapability, text, retryable: false);

    public static EngineException Failure(string text, Exception? cause = null) => new(EngineErrorCodes.EngineFailure, text, retryable: false, cause);

    /// <summary>The app could not be started on this machine. The code <see cref="WebEngine"/> uses when the browser is missing.</summary>
    public static EngineException Unavailable(string text, Exception? cause = null) => new("ENVIRONMENT_UNAVAILABLE", text, retryable: false, cause);

    /// <summary>The middle of a box.</summary>
    public static Point Centre(Rect rect) => new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    /// <summary>A physical screen box as the observation reports it: relative to the window's origin, in device-independent pixels.</summary>
    public static BoundingBox Logical(Rect physical, Point origin, double scale)
    {
        var factor = scale > 0 ? scale : 1;
        return new BoundingBox((physical.X - origin.X) / factor, (physical.Y - origin.Y) / factor, physical.Width / factor, physical.Height / factor);
    }

    /// <summary>
    /// The location a window surface reports: the app and the title of its window, <c>&lt;app&gt; / &lt;title&gt;</c>. A desktop app has
    /// no address bar, so this is the opaque address the harness shows to the model and anchors trace replay on. It is deliberately
    /// not a URL. Null when neither is known.
    /// </summary>
    public static string? ScreenLocation(string? app, string? title)
    {
        var parts = new[] { app, title }
            .Select(part => part is null ? "" : Whitespace().Replace(part, " ").Trim())
            .Where(part => part.Length > 0)
            .ToList();
        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
