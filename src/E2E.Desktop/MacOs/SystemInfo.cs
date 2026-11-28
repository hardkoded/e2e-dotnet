// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Globalization;

namespace E2E.Desktop.MacOs;

/// <summary>The machine around the app: permissions, whether the screen is locked, whether a process runs, and window captures. Rust's <c>system.rs</c>.</summary>
internal static unsafe class SystemInfo
{
    private static class Keys
    {
        public static readonly nint ScreenLocked = Cf.CreateString("CGSSessionScreenIsLocked");
    }

    /// <summary>Whether this process may capture the screen; asks no question of the user.</summary>
    public static bool ScreenRecording() => Native.CGPreflightScreenCaptureAccess() != 0;

    /// <summary>Whether the console session's screen is locked; null when that cannot be told.</summary>
    public static bool? ScreenLocked()
    {
        var session = Native.CGSessionCopyCurrentDictionary();
        if (session == 0)
        {
            return null;
        }

        try
        {
            var value = Native.CFDictionaryGetValue(session, Keys.ScreenLocked);

            // The key is there only while the screen is locked.
            return value == 0 || Native.CFBooleanGetValue(value) != 0;
        }
        finally
        {
            Native.CFRelease(session);
        }
    }

    /// <summary>The backing scale of the display a window is on: image pixels per point.</summary>
    private static double ScaleOf(Rect bounds)
    {
        var rect = new CGRect(bounds.X, bounds.Y, Math.Max(bounds.Width, 1.0), Math.Max(bounds.Height, 1.0));
        uint display = 0;
        uint count = 0;
        if (Native.CGGetDisplaysWithRect(rect, 1, &display, &count) != 0 || count == 0)
        {
            return 1.0;
        }

        var mode = Native.CGDisplayCopyDisplayMode(display);
        if (mode == 0)
        {
            return 1.0;
        }

        var pixels = Native.CGDisplayModeGetPixelWidth(mode);
        var points = Native.CGDisplayModeGetWidth(mode);
        Native.CGDisplayModeRelease(mode);
        return points == 0 ? 1.0 : (double)pixels / points;
    }

    /// <summary>Captures one window, without its shadow, into a PNG at <paramref name="path"/>; answers the display's scale.</summary>
    public static double Screenshot(uint window, string path, Instant deadline)
    {
        var bounds = Windows.ServerBounds(window)
            ?? throw Failure.Refused(Codes.WindowNotFound, $"window {window} is not on screen");
        Subprocess child;
        try
        {
            child = Subprocess.Start(
                "/usr/sbin/screencapture",
                ["-x", "-o", "-t", "png", "-l", window.ToString(CultureInfo.InvariantCulture), path],
                null
            );
        }
        catch (Win32Exception error)
        {
            throw Failure.Refused(Codes.Internal, $"could not run screencapture: {error.Message}");
        }

        using (child)
        {
            while (true)
            {
                switch (child.TryWait())
                {
                    case 0:
                        goto Captured;
                    case not null:
                        throw CaptureFailed(window, child.Stderr.Trim());
                }

                if (Instant.Now >= deadline)
                {
                    child.Kill();
                    throw Failure.TimedOut("the window capture");
                }

                Thread.Sleep(10);
            }
        }

        Captured:
        if (!File.Exists(path))
        {
            throw CaptureFailed(window, "it wrote no image");
        }

        return ScaleOf(bounds);
    }

    /// <summary>Why a capture failed: no Screen Recording permission, a window that closed meanwhile, or what <c>screencapture</c> said.</summary>
    private static Failure CaptureFailed(uint window, string said)
    {
        if (!ScreenRecording())
        {
            return Failure.Refused(
                Codes.PermDenied,
                "screencapture could not capture the window; it needs Screen Recording permission"
            );
        }

        if (Windows.ServerBounds(window) is null)
        {
            return Failure.Refused(Codes.WindowNotFound, $"window {window} closed before it could be captured");
        }

        var detail = said.Length == 0 ? "it gave no reason" : said;
        return Failure.Refused(Codes.Internal, $"screencapture could not capture window {window}: {detail}");
    }
}
