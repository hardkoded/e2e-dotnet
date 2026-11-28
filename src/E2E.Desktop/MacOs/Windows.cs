// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Desktop.MacOs;

/// <summary>
/// The windows of one process: the window server's list (ids, bounds, whether on screen, layer) joined with the app's accessibility
/// windows (title, focus, minimized), matched by the window server id.
/// </summary>
internal static unsafe class Windows
{
    /// <summary>The window level menus open at (<c>kCGPopUpMenuWindowLevel</c>).</summary>
    private const long MenuLayer = 101;

    /// <summary><c>kCGWindowListOptionAll</c> and <c>kCGWindowListExcludeDesktopElements</c>.</summary>
    private const uint ListOptions = 0 | (1u << 4);

    /// <summary>
    /// The keys of a window server entry. Each <c>kCGWindow*</c> constant is a CFString equal to its own name, and a dictionary
    /// looks a key up by equality. Created on first use, so the pure logic of this class runs where CoreFoundation is not there.
    /// </summary>
    private static class Keys
    {
        public static readonly nint Number = Cf.CreateString("kCGWindowNumber");
        public static readonly nint OwnerPid = Cf.CreateString("kCGWindowOwnerPID");
        public static readonly nint Layer = Cf.CreateString("kCGWindowLayer");
        public static readonly nint Bounds = Cf.CreateString("kCGWindowBounds");
        public static readonly nint Onscreen = Cf.CreateString("kCGWindowIsOnscreen");
        public static readonly nint Name = Cf.CreateString("kCGWindowName");
    }

    /// <summary>One entry of the window server's list.</summary>
    private sealed record ServerWindow(uint Id, int Pid, long Layer, Rect? Bounds, bool Onscreen, string? Title);

    private static long? Number(nint dictionary, nint key)
    {
        var value = Native.CFDictionaryGetValue(dictionary, key);
        return value == 0 || Native.CFGetTypeID(value) != Cf.NumberType ? null : Cf.NumberToInt64(value);
    }

    private static bool Boolean(nint dictionary, nint key)
    {
        var value = Native.CFDictionaryGetValue(dictionary, key);
        return value != 0 && Native.CFGetTypeID(value) == Cf.BooleanType && Native.CFBooleanGetValue(value) != 0;
    }

    private static string? String(nint dictionary, nint key)
    {
        var value = Native.CFDictionaryGetValue(dictionary, key);
        return value == 0 || Native.CFGetTypeID(value) != Cf.StringType ? null : Cf.ToManaged(value);
    }

    private static Rect? Bounds(nint dictionary)
    {
        var value = Native.CFDictionaryGetValue(dictionary, Keys.Bounds);
        if (value == 0)
        {
            return null;
        }

        var rect = default(CGRect);
        return Native.CGRectMakeWithDictionaryRepresentation(value, &rect) != 0
            ? new Rect(rect.X, rect.Y, rect.Width, rect.Height)
            : null;
    }

    /// <summary>Every window the window server lists, front to back.</summary>
    private static List<ServerWindow> ServerWindows()
    {
        var list = Native.CGWindowListCopyWindowInfo(ListOptions, 0);
        if (list == 0)
        {
            return [];
        }

        try
        {
            var count = (int)Native.CFArrayGetCount(list);
            var windows = new List<ServerWindow>(Math.Max(count, 0));
            for (var index = 0; index < count; index++)
            {
                var dictionary = Native.CFArrayGetValueAtIndex(list, index);
                if (Number(dictionary, Keys.Number) is not { } id || Number(dictionary, Keys.OwnerPid) is not { } pid)
                {
                    continue;
                }

                windows.Add(
                    new ServerWindow(
                        (uint)id,
                        (int)pid,
                        Number(dictionary, Keys.Layer) ?? 0,
                        Bounds(dictionary),
                        Boolean(dictionary, Keys.Onscreen),
                        String(dictionary, Keys.Name)
                    )
                );
            }

            return windows;
        }
        finally
        {
            Native.CFRelease(list);
        }
    }

    /// <summary>The app's accessibility windows, front to back, each with its window server id when it has one.</summary>
    public static List<(Element Element, uint? Id)> AxWindows(Element app)
    {
        // Every window, however many a document app restored: one past a fixed cap would list but never read.
        var counted = app.Count("AXWindows");
        var count = Math.Max(counted.IsOk ? counted.Value : 256, 1);
        var read = app.Elements("AXWindows", count);
        if (read.IsOk)
        {
            return read.Value!.Select(element => (element, element.WindowId())).ToList();
        }

        switch (read.Error)
        {
            case AxError.NoValue or AxError.AttributeUnsupported:
                return [];
            case AxError.InvalidElement:
                throw Failure.Refused(Codes.AppNotFound, "the app is no longer running");
            case AxError.ApiDisabled:
                throw Failure.Refused(Codes.PermDenied, "this process has no Accessibility permission");
            case AxError.CannotComplete:
                throw Failure
                    .Refused(Codes.AppUnresponsive, "the app did not answer its window list in time")
                    .WithDetails(Failure.Retryable);
            // An app that has no accessibility for its windows will not have it on a later read: say so, do not wait.
            case AxError.NotImplemented:
                throw Failure.Refused(Codes.ActionNotSupported, "the app does not expose its windows to accessibility");
            default:
                throw Failure
                    .Refused(Codes.AppUnresponsive, $"reading the app's windows failed: {AxError.Describe(read.Error)}")
                    .WithDetails(Failure.Retryable);
        }
    }

    /// <summary>The developer identifiers in a small subtree, <paramref name="levels"/> deep and at most 8 children a level.</summary>
    private static List<string> Identifiers(Element element, int levels)
    {
        var found = new List<string>();
        if (element.Text("AXIdentifier") is { } identifier)
        {
            found.Add(identifier);
        }

        if (levels > 0)
        {
            var children = element.Elements("AXChildren", 8);
            foreach (var child in children.IsOk ? children.Value! : [])
            {
                found.AddRange(Identifiers(child, levels - 1));
            }
        }

        return found;
    }

    /// <summary>
    /// Whether an untitled dialog with these identifiers inside is one macOS put among the app's windows: Siri's, which macOS 27
    /// shows over an app a few seconds after a press in a SwiftUI sheet, holds only its orb, identified <c>siri</c>.
    /// </summary>
    internal static bool SystemDialog(IEnumerable<string> identifiers) =>
        identifiers.Any(identifier => string.Equals(identifier, "siri", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether <paramref name="window"/> holds the app's focused window: is it, or carries it as a sheet or an alert. The app's
    /// focused window is the sheet then, and keys reach the window through it. A parent the app did not answer for in time is no proof
    /// either way: it fails as <see cref="AxError.CannotComplete"/>.
    /// </summary>
    public static AxResult<bool> HoldsFocus(Element focused, Element window)
    {
        if (focused.Equals(window))
        {
            return AxResult<bool>.Ok(true);
        }

        var parent = Actions.Stalled(focused.Attribute("AXParent"));
        if (!parent.IsOk)
        {
            return AxResult<bool>.Err(parent.Error);
        }

        return AxResult<bool>.Ok(Element.Same(parent.Value?.Element, window));
    }

    /// <summary>The windows of one process: every accessibility window, and every normal-level window server window accessibility does not list.</summary>
    public static List<RawWindow> List(int pid)
    {
        if (!ProcessInfo.Alive(pid))
        {
            throw Failure.Refused(Codes.AppNotFound, $"no process {pid} is running");
        }

        var app = Element.Application(pid);
        var accessible = AxWindows(app);

        // The app's focus decides which window is focused: a stall reading it fails the list, which a retry may get past,
        // rather than list every window as unfocused.
        var focused = Actions.ElementAttribute(app, "AXFocusedWindow", "list windows");
        var server = ServerWindows().Where(window => window.Pid == pid).ToList();
        var output = new List<RawWindow>();
        var listed = new List<uint>();
        var unresolved = 0;
        foreach (var (element, windowId) in accessible)
        {
            if (windowId is not { } id)
            {
                // Only a window counts: an app's AXWindows can hold other elements that never get an id (Finder's desktop, an
                // app's own AXApplication element), and they are no window the list leaves out.
                var roleRead = element.Attribute("AXRole");
                if (
                    MayBeWindow(
                        roleRead.IsOk
                            ? AxResult<string?>.Ok(roleRead.Value!.Text)
                            : AxResult<string?>.Err(roleRead.Error)
                    )
                )
                {
                    unresolved++;
                }

                continue;
            }

            var entry = server.FirstOrDefault(window => window.Id == id);
            var read = element.Attributes(["AXTitle", "AXMinimized", "AXPosition", "AXSize", "AXSubrole", "AXMain"]);
            var values = read.IsOk ? read.Value! : new AxValue?[6];
            var title = values[0]?.Text;
            var minimized = values[1]?.Boolean ?? false;
            var subrole = values[4]?.Text;

            // Only an untitled dialog is read further: an app's own windows nearly all have titles.
            var system =
                subrole == "AXDialog" && string.IsNullOrWhiteSpace(title) && SystemDialog(Identifiers(element, 3));
            var axBounds =
                values[2] is AxValue.PointValue point && values[3] is AxValue.SizeValue size
                    ? new Rect(point.X, point.Y, size.Width, size.Height)
                    : (Rect?)null;
            var holds = false;
            if (focused is not null)
            {
                holds = Actions.Holds(focused, element, "list windows");
            }

            listed.Add(id);
            var onSheet = holds && !Element.Same(focused, element);
            output.Add(
                new RawWindow
                {
                    Id = id.ToString(CultureInfo.InvariantCulture),
                    Title = title ?? entry?.Title,
                    IsFocused = holds,
                    SheetFocused = onSheet ? true : null,
                    SheetId = onSheet ? focused!.SheetKey() : null,
                    IsMain = values[5]?.Boolean ?? false,
                    Visible = entry?.Onscreen ?? false,
                    Accessible = true,
                    Minimized = minimized,
                    Pid = pid,
                    Bounds = axBounds ?? entry?.Bounds,
                    System = system ? true : null,
                }
            );
        }

        foreach (var window in server)
        {
            // A window accessibility does not list counts only while it is on screen: AppKit keeps hidden helper windows.
            if (window.Layer != 0 || listed.Contains(window.Id) || !window.Onscreen)
            {
                continue;
            }

            // A window the window server lists with no size is a placeholder, not something on screen.
            if (window.Bounds is not { } bounds || bounds.Width < 1.0 || bounds.Height < 1.0)
            {
                continue;
            }

            output.Add(
                new RawWindow
                {
                    Id = window.Id.ToString(CultureInfo.InvariantCulture),
                    Title = window.Title,
                    IsFocused = false,
                    IsMain = false,
                    Visible = window.Onscreen,
                    Accessible = false,
                    Minimized = false,
                    Pid = pid,
                    Bounds = window.Bounds,
                }
            );
        }

        Complete(listed.Count, unresolved);
        return output;
    }

    /// <summary>
    /// Whether an accessibility window entry with no window server id is a window the list leaves out: its role says so, or the app
    /// did not answer the role in time, which may hide one, so a stall never reads as an app with no window.
    /// </summary>
    internal static bool MayBeWindow(AxResult<string?> role) =>
        role.IsOk ? role.Value == "AXWindow" : role.Error == AxError.CannotComplete;

    /// <summary>
    /// Refuses a window list that holds none of the app's accessibility windows while some had no window server id yet: the app has a
    /// window, and a list without it would read as an app with none, which a negative assertion could take as proof. A retryable
    /// stall: the id is there a moment later.
    /// </summary>
    internal static void Complete(int listed, int unresolved)
    {
        if (listed == 0 && unresolved > 0)
        {
            throw Failure
                .Refused(Codes.AppUnresponsive, $"{unresolved} window(s) of the app have no window server id yet")
                .WithDetails(Failure.Retryable);
        }
    }

    /// <summary>The accessibility element of a window of <paramref name="pid"/> by its window server id.</summary>
    public static Element Find(int pid, uint id)
    {
        var app = Element.Application(pid);
        foreach (var (element, window) in AxWindows(app))
        {
            if (window == id)
            {
                return element;
            }
        }

        if (ServerWindows().Any(window => window.Pid == pid && window.Id == id))
        {
            throw Failure
                .Refused(Codes.WindowNotFound, $"window {id} has no accessibility element")
                .WithDetail("kind", "window_without_accessibility_element");
        }

        throw Failure.Refused(Codes.WindowNotFound, $"the app has no window {id}");
    }

    /// <summary>The window server's bounds of one window.</summary>
    public static Rect? ServerBounds(uint id) => ServerWindows().FirstOrDefault(window => window.Id == id)?.Bounds;

    /// <summary>How many menus <paramref name="pid"/> has open on screen: windows at the menu level.</summary>
    public static int OpenMenus(int pid) =>
        ServerWindows().Count(window => window.Pid == pid && window.Layer == MenuLayer && window.Onscreen);
}
