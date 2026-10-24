// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using E2E.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Windows.SDK.BuildTools.WinApp.UIAutomation;

namespace E2E.WinApp;

/// <summary>
/// The <see cref="IWindowDriver"/> over the winapp UI Automation library: the one type that uses the library, so the rest of the
/// engine builds and tests on any platform. Element handles are the library's selector slugs (<c>btn-save-c3d4</c>, or the automation
/// id when it is unique), which stay the same across inspections of the same element; the driver keeps, per window, the elements its
/// newest inspection read, since the library acts on the element object, not the slug. Processes it launched are ended by <see cref="CloseAsync"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class UiAutomationDriver : IWindowDriver
{
    /// <summary>How deep one inspection walks. The library's own default depth is shallower than a WinUI page.</summary>
    private const int InspectDepth = 64;

    /// <summary>How many windows the driver keeps elements for; the window inspected longest ago goes first.</summary>
    private const int MaxWindows = 64;

    /// <summary>How long the foreground is given to change after it was asked to, before the request counts as refused.</summary>
    private static readonly TimeSpan ForegroundGrace = TimeSpan.FromSeconds(1);

    /// <summary>How often the foreground is read while waiting for it.</summary>
    private static readonly TimeSpan ForegroundPoll = TimeSpan.FromMilliseconds(25);

    /// <summary>Window classes that are never part of the app's UI: the drop shadow Windows paints under a menu or list.</summary>
    private static readonly string[] IgnoredWindowClasses = ["SysShadow"];

    private readonly ServiceProvider _services;
    private readonly IUiAutomation _automation;
    private readonly IUiTargetResolver _targets;
    private readonly IKeyboardInput _keyboard;
    private readonly IMouseInput _mouse;
    private readonly ISystemUiQuery _system;
    private readonly OrderedDictionary<long, Dictionary<string, (UiElement Element, UiTarget Target)>> _elements = [];
    private readonly Dictionary<int, WindowProcess> _processes = [];
    private readonly Lock _lock = new();
    private int _anonymous;

    public UiAutomationDriver()
    {
        _services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddWinAppUiAutomation()
            .BuildServiceProvider();
        _automation = _services.GetRequiredService<IUiAutomation>();
        _targets = _services.GetRequiredService<IUiTargetResolver>();
        _keyboard = _services.GetRequiredService<IKeyboardInput>();
        _mouse = _services.GetRequiredService<IMouseInput>();
        _system = _services.GetRequiredService<ISystemUiQuery>();
    }

    public async Task<LaunchedApp> LaunchAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, Action<AppLogEntry>? log, CancellationToken cancellationToken)
    {
        var process = await WindowProcess.StartAsync(executable, arguments, workingDirectory, timeout, log, pid => Windows(pid), cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            _processes[process.ProcessId] = process;
        }

        return new LaunchedApp(process.ProcessId, process.MainWindow, Path.GetFileNameWithoutExtension(executable), Owned: true);
    }

    public async Task<LaunchedApp> AttachAsync(string app, CancellationToken cancellationToken)
    {
        UiTarget target;
        try
        {
            target = await _targets.ResolveAsync(app, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw Translate(cause);
        }

        var window = target.WindowHandle != 0 ? target.WindowHandle : Windows(target.ProcessId).FirstOrDefault()?.Handle ?? 0;
        if (window == 0)
        {
            throw new WindowDriverException(DriverFault.AppNotFound, $"{app} has no visible window");
        }

        return new LaunchedApp(target.ProcessId, window, target.ProcessName is { Length: > 0 } name ? name : app, Owned: false);
    }

    public async Task CloseAsync(LaunchedApp app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        WindowProcess? process;
        lock (_lock)
        {
            _processes.Remove(app.ProcessId, out process);
        }

        if (process is not null)
        {
            await process.KillAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<WindowInfo>> WindowsAsync(int processId, CancellationToken cancellationToken) =>
        Run(() => Windows(processId), cancellationToken);

    public Task<RawElement> InspectAsync(long windowHandle, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var target = UiTarget.FromWindowHandle((IntPtr)windowHandle);
                var elements = await _automation.InspectAsync(target, null!, InspectDepth, token).ConfigureAwait(false);
                var (root, children) = Nest(elements, windowHandle);
                var read = new Dictionary<string, (UiElement, UiTarget)>(StringComparer.Ordinal);
                var selection = await ReadSelectionAsync(target, root, children, token).ConfigureAwait(false);
                var raw = ToRaw(root, children, target, read, selection, isRoot: true);
                Remember(windowHandle, read);
                return raw;
            },
            cancellationToken);

    public Task<string?> FocusedAsync(long windowHandle, CancellationToken cancellationToken) =>
        RunAsync<string?>(
            async token =>
            {
                try
                {
                    var focused = await _automation.GetFocusedElementAsync(UiTarget.FromWindowHandle((IntPtr)windowHandle), token).ConfigureAwait(false);
                    return focused is null ? null : ObservedHandleOf(windowHandle, focused);
                }
                catch (Exception cause) when (cause is not OperationCanceledException)
                {
                    // No focus is not a failure of the observation.
                    return null;
                }
            },
            cancellationToken);

    public Task<string> InvokeAsync(string handle, InvokeKind kind, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (element, target) = Lookup(handle);
                if (kind == InvokeKind.Auto)
                {
                    return await _automation.InvokeAsync(target, element, token).ConfigureAwait(false);
                }

                var result = await _automation.InvokeAsync(target, element, ToAction(kind), token).ConfigureAwait(false);
                return result.Pattern;
            },
            cancellationToken);

    public Task SetValueAsync(string handle, string text, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (element, target) = Lookup(handle);
                await _automation.SetValueAsync(target, element, text, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    public Task FocusAsync(string handle, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (element, target) = Lookup(handle);
                await _automation.FocusAsync(target, element, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    public Task ScrollIntoViewAsync(string handle, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (element, target) = Lookup(handle);
                await _automation.ScrollIntoViewAsync(target, element, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    public Task ScrollAsync(string handle, ScrollDirection direction, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (element, target) = Lookup(handle);
                await _automation.ScrollContainerAsync(target, element, direction.ToString().ToLowerInvariant(), null!, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    public Task SendKeysAsync(long windowHandle, string keys, bool verbatim, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                IReadOnlyList<KeyAction> actions;
                try
                {
                    actions = verbatim ? KeyStringParser.ParseVerbatim(keys) : KeyStringParser.Parse(keys);
                }
                catch (FormatException cause)
                {
                    throw new WindowDriverException(DriverFault.Other, $"the key string is not valid: {cause.Message}", cause);
                }

                // WinUI controls are windowless, so posted messages are dropped: input goes through SendInput, to the foreground window.
                _keyboard.Send(windowHandle, actions, KeyTransport.SendInput);
                return true;
            },
            cancellationToken);

    public Task ClickAsync(Point screen, bool doubleClick, bool rightClick, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                _mouse.Click(Round(screen.X), Round(screen.Y), doubleClick, rightClick);
                return true;
            },
            cancellationToken);

    public Task HoverAsync(Point screen, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                _mouse.Hover(Round(screen.X), Round(screen.Y));
                return true;
            },
            cancellationToken);

    public Task DragAsync(Point from, Point to, TimeSpan hold, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                _mouse.Drag(Round(from.X), Round(from.Y), Round(to.X), Round(to.Y), rightButton: false, holdMs: (int)Math.Clamp(hold.TotalMilliseconds, 0, int.MaxValue));
                return true;
            },
            cancellationToken);

    public Task ScrollWheelAsync(Point screen, int notches, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                _mouse.ScrollWheel(Round(screen.X), Round(screen.Y), notches * 120);
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Brings the window forward, restoring it first when it is minimized. Windows refuses the request when another process holds the
    /// input (the user is typing elsewhere, the desktop is locked); then the input that would follow would land in that other app, so
    /// the refusal is a failure, not a success.
    /// </summary>
    public Task ForegroundAsync(long windowHandle, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                var hwnd = (IntPtr)windowHandle;
                if (IsIconic(hwnd))
                {
                    ShowWindow(hwnd, SwRestore);
                }

                if (ForegroundGuard.NoInteractiveDesktop())
                {
                    throw new WindowDriverException(DriverFault.ForegroundLost, "there is no interactive desktop to bring the app window forward on: the session is locked or on a secure desktop");
                }

                SetForegroundWindow(hwnd);
                if (!AwaitForeground(hwnd))
                {
                    // Windows refuses the request from a process that has not had the input lately; attached to the input of the
                    // thread that has, the request is honoured. Then it is refused for good: another app holds the input.
                    var current = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                    var own = GetCurrentThreadId();
                    var attached = current != 0 && current != own && AttachThreadInput(own, current, true);
                    try
                    {
                        // SetForegroundWindow still answers false while attached; BringWindowToTop is what moves the foreground.
                        SetForegroundWindow(hwnd);
                        BringWindowToTop(hwnd);
                    }
                    finally
                    {
                        if (attached)
                        {
                            AttachThreadInput(own, current, false);
                        }
                    }

                    if (!AwaitForeground(hwnd))
                    {
                        throw new WindowDriverException(DriverFault.ForegroundLost, "the app window could not be brought to the foreground: another app holds the input");
                    }
                }

                return true;
            },
            cancellationToken);

    public Task<RawScreenshot> ScreenshotAsync(long windowHandle, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var (pixels, width, height) = await _automation.ScreenshotAsync(UiTarget.FromWindowHandle((IntPtr)windowHandle), null!, captureScreen: false, focus: false, token).ConfigureAwait(false);
                return new RawScreenshot(pixels, width, height);
            },
            cancellationToken);

    public Task ResizeAsync(long windowHandle, int width, int height, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                var hwnd = (IntPtr)windowHandle;
                var scale = ScaleOf(hwnd);
                if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, Round(width * scale), Round(height * scale), SwpNoMove | SwpNoZOrder | SwpNoActivate))
                {
                    throw new WindowDriverException(DriverFault.Other, $"the window could not be resized (error {Marshal.GetLastPInvokeError()})");
                }

                return true;
            },
            cancellationToken);

    public Task ShowAsync(long windowHandle, WindowState state, CancellationToken cancellationToken) =>
        Run(
            () =>
            {
                ShowWindow((IntPtr)windowHandle, state switch
                {
                    WindowState.Maximized => SwMaximize,
                    WindowState.Minimized => SwMinimize,
                    _ => SwRestore,
                });
                return true;
            },
            cancellationToken);

    /// <summary>Ends every process the driver started, each within the cleanup budget, then releases the library. One failure skips nothing.</summary>
    public async ValueTask DisposeAsync()
    {
        List<WindowProcess> processes;
        lock (_lock)
        {
            processes = [.. _processes.Values];
            _processes.Clear();
            _elements.Clear();
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    await process.KillAsync(E2EDefaults.CleanupTimeout, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best effort: the next process is still ended, and the library still released.
                }
            }
        }
        finally
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The visible top-level windows of a process with a non-empty box, the one the process calls main first, then the largest.</summary>
    private IReadOnlyList<WindowInfo> Windows(int processId)
    {
        var main = _system.GetProcessById(processId)?.MainWindowHandle ?? IntPtr.Zero;
        var windows = new List<WindowInfo>();
        foreach (var (hwnd, pid, title) in _automation.FindWindowsByPid(processId))
        {
            if (!IsWindowVisible(hwnd) || !_automation.TryGetWindowRect((long)hwnd, out var rect))
            {
                continue;
            }

            var bounds = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            if (bounds.Width <= 0 || bounds.Height <= 0 || IgnoredWindowClasses.Contains(_system.GetWindowClassName((long)hwnd), StringComparer.Ordinal))
            {
                continue;
            }

            windows.Add(new WindowInfo((long)hwnd, pid, title ?? "", bounds, ScaleOf(hwnd)));
        }

        return [.. windows.OrderBy(window => window.Handle == (long)main ? 0 : 1).ThenByDescending(window => window.Bounds.Width * window.Bounds.Height)];
    }

    /// <summary>
    /// The library lists elements flat, in document order, each with its depth. This nests them: an element's parent is the nearest
    /// element before it one level up. An element without a depth is a top-level one whose <c>Children</c> the library nested itself,
    /// and those are attached as they come. One top-level element is the root; several (or none) are put under a root that stands
    /// for the window.
    /// </summary>
    private (UiElement Root, Dictionary<UiElement, List<UiElement>> Children) Nest(UiElement[] elements, long windowHandle)
    {
        var children = new Dictionary<UiElement, List<UiElement>>(ReferenceEqualityComparer.Instance);
        var tops = new List<UiElement>();
        var stack = new List<UiElement>();
        foreach (var element in elements)
        {
            var depth = element.Depth ?? 0;
            while (stack.Count > depth)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count == 0)
            {
                tops.Add(element);
            }
            else
            {
                Add(children, stack[^1], element);
            }

            stack.Add(element);
            Attach(children, element);
        }

        if (tops.Count == 1)
        {
            return (tops[0], children);
        }

        var root = new UiElement { Type = "Window", Name = _system.GetWindowText(windowHandle), IsEnabled = true, WindowHandle = windowHandle };
        if (_automation.TryGetWindowRect(windowHandle, out var rect))
        {
            root.X = rect.Left;
            root.Y = rect.Top;
            root.Width = rect.Right - rect.Left;
            root.Height = rect.Bottom - rect.Top;
        }

        children[root] = tops;
        return (root, children);
    }

    /// <summary>Adds the children the library nested under <paramref name="element"/> itself, at every level.</summary>
    private static void Attach(Dictionary<UiElement, List<UiElement>> children, UiElement element)
    {
        foreach (var nested in element.Children ?? [])
        {
            Add(children, element, nested);
            Attach(children, nested);
        }
    }

    private static void Add(Dictionary<UiElement, List<UiElement>> children, UiElement parent, UiElement child)
    {
        if (!children.TryGetValue(parent, out var list))
        {
            list = [];
            children[parent] = list;
        }

        list.Add(child);
    }

    /// <summary>The control types that are one of a set of which one is selected; the selected one is read from its selection item pattern.</summary>
    private static readonly string[] SelectionTypes = ["RadioButton", "TabItem", "ListItem", "DataItem", "TreeItem"];

    private async Task<Dictionary<UiElement, bool>> ReadSelectionAsync(UiTarget target, UiElement root, Dictionary<UiElement, List<UiElement>> children, CancellationToken cancellationToken)
    {
        var selection = new Dictionary<UiElement, bool>();
        var pending = new Stack<UiElement>();
        pending.Push(root);
        while (pending.TryPop(out var element))
        {
            if (SelectionTypes.Contains(element.Type, StringComparer.Ordinal))
            {
                try
                {
                    var properties = await _automation.GetPropertiesAsync(target, element, "IsSelected", cancellationToken).ConfigureAwait(false);
                    if (properties.TryGetValue("IsSelected", out var value) && bool.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var selected))
                    {
                        selection[element] = selected;
                    }
                }
                catch (Exception cause) when (cause is not OperationCanceledException)
                {
                    // An element that vanished or has no selection item pattern is not selected as far as the tree can say.
                }
            }

            if (children.TryGetValue(element, out var nested))
            {
                foreach (var child in nested)
                {
                    pending.Push(child);
                }
            }
        }

        return selection;
    }

    /// <summary>
    /// One element as the projection reads it, collected under its handle into <paramref name="read"/>. The password flag is not read:
    /// the library's <c>IsPassword</c> answers true for a plain rich edit, so the projection goes by the framework class instead.
    /// </summary>
    private RawElement ToRaw(UiElement element, Dictionary<UiElement, List<UiElement>> children, UiTarget target, Dictionary<string, (UiElement, UiTarget)> read, Dictionary<UiElement, bool> selection, bool isRoot)
    {
        var handle = HandleOf(element);
        read[handle] = (element, target);
        var nested = children.TryGetValue(element, out var list) ? list : [];
        return new RawElement
        {
            Handle = handle,
            Type = element.Type,
            Name = element.Name,
            AutomationId = element.AutomationId,
            ClassName = element.ClassName,
            IsEnabled = element.IsEnabled,
            IsOffscreen = element.IsOffscreen && !isRoot,
            Bounds = element.Width > 0 && element.Height > 0 ? new Rect(element.X, element.Y, element.Width, element.Height) : null,
            Value = element.Value,
            ToggleState = element.ToggleState,
            IsSelected = selection.TryGetValue(element, out var selected) ? selected : null,
            ExpandState = element.ExpandState,
            ScrollDir = element.ScrollDir,
            IsInvokable = element.IsInvokable,
            IsPassword = null,
            HasMoreChildren = element.HasMoreChildren,
            Children = [.. nested.Select(child => ToRaw(child, children, target, read, selection, isRoot: false))],
        };
    }

    /// <summary>The element's selector slug, which the library computes from its runtime id or unique automation id; an element without one gets a key of this driver's.</summary>
    /// <summary>
    /// The handle the newest observation of <paramref name="windowHandle"/> gave the element the library reports as focused. The library
    /// derives a slug from the properties it read, and a focus query reads fewer of them than an inspection, so the two slugs of one
    /// element can differ; the element is matched by what identifies it instead.
    /// </summary>
    private string ObservedHandleOf(long windowHandle, UiElement focused)
    {
        lock (_lock)
        {
            if (_elements.TryGetValue(windowHandle, out var read))
            {
                // Two controls under different parents can share a type, name, and automation id: the box tells them apart, and an
                // ambiguous match names no element.
                var matches = read.Where(pair => SameElement(pair.Value.Element, focused)).ToList();
                if (matches.Count > 1)
                {
                    matches = [.. matches.Where(pair => pair.Value.Element.X == focused.X && pair.Value.Element.Y == focused.Y && pair.Value.Element.Width == focused.Width && pair.Value.Element.Height == focused.Height)];
                }

                if (matches.Count == 1)
                {
                    return matches[0].Key;
                }
            }
        }

        return HandleOf(focused);
    }

    private static bool SameElement(UiElement left, UiElement right) =>
        left.Type == right.Type
        && left.Name == right.Name
        && (string.IsNullOrEmpty(left.AutomationId)
            ? string.IsNullOrEmpty(right.AutomationId) && left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height
            : left.AutomationId == right.AutomationId);

    private string HandleOf(UiElement element)
    {
        if (element.Selector is { Length: > 0 } selector)
        {
            return selector;
        }

        if (element.Id is { Length: > 0 } id)
        {
            return id;
        }

        return $"uia-{Interlocked.Increment(ref _anonymous)}";
    }

    /// <summary>
    /// Keeps the elements one inspection read as the window's current ones, in place of the previous inspection's. A handle from an
    /// older observation still resolves while the element is in the newest read, since the slug is stable; one that is not has
    /// vanished. The windows inspected longest ago are forgotten first.
    /// </summary>
    private void Remember(long windowHandle, Dictionary<string, (UiElement, UiTarget)> read)
    {
        lock (_lock)
        {
            _elements.Remove(windowHandle);
            _elements[windowHandle] = read;
            while (_elements.Count > MaxWindows)
            {
                _elements.RemoveAt(0);
            }
        }
    }

    private (UiElement Element, UiTarget Target) Lookup(string handle)
    {
        lock (_lock)
        {
            // The newest window first: a slug names one element, but a popup can repeat a window's automation ids.
            foreach (var window in _elements.Values.Reverse())
            {
                if (window.TryGetValue(handle, out var entry))
                {
                    return entry;
                }
            }

            throw new WindowDriverException(DriverFault.ElementVanished, $"element {handle} is not part of the newest observation");
        }
    }

    private static UiInvokeAction ToAction(InvokeKind kind) => kind switch
    {
        InvokeKind.Invoke => UiInvokeAction.Invoke,
        InvokeKind.Select => UiInvokeAction.Select,
        InvokeKind.Toggle => UiInvokeAction.Toggle,
        InvokeKind.ToggleOn => UiInvokeAction.ToggleOn,
        InvokeKind.ToggleOff => UiInvokeAction.ToggleOff,
        InvokeKind.Expand => UiInvokeAction.Expand,
        InvokeKind.Collapse => UiInvokeAction.Collapse,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown invoke kind."),
    };

    /// <summary>A library failure as the engine classifies it.</summary>
    private static Exception Translate(Exception cause) => cause switch
    {
        OperationCanceledException or WindowDriverException => cause,
        UiElementNotFoundException => new WindowDriverException(DriverFault.ElementVanished, cause.Message, cause),
        UiAmbiguousSelectorException => new WindowDriverException(DriverFault.Ambiguous, cause.Message, cause),
        UiElementOffscreenException => new WindowDriverException(DriverFault.Offscreen, cause.Message, cause),
        AppNotFoundException => new WindowDriverException(DriverFault.AppNotFound, cause.Message, cause),
        ForegroundLostException => new WindowDriverException(DriverFault.ForegroundLost, cause.Message, cause),
        UiValueSetException => new WindowDriverException(DriverFault.ValueRejected, cause.Message, cause),
        COMException com when (uint)com.HResult is 0x80040201 or 0x80131509 => new WindowDriverException(DriverFault.ElementVanished, cause.Message, cause),
        _ => new WindowDriverException(DriverFault.Other, cause.Message, cause),
    };

    /// <summary>
    /// One library call under the caller's token: it runs on a worker thread, since the library makes blocking COM calls and
    /// input injection must not sit on the harness's thread, and its failure comes back classified.
    /// </summary>
    private static async Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw Translate(cause);
        }
    }

    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task.Run(() => work(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw Translate(cause);
        }
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Physical pixels per device-independent pixel of the monitor the window is on.</summary>
    private static double ScaleOf(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96.0 : 1;
    }

    /// <summary>Waits up to <see cref="ForegroundGrace"/> for the foreground to be this window, since the change lags the request.</summary>
    private static bool AwaitForeground(IntPtr hwnd)
    {
        var deadline = Environment.TickCount64 + (long)ForegroundGrace.TotalMilliseconds;
        while (true)
        {
            if (IsForeground(hwnd))
            {
                return true;
            }

            if (Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Thread.Sleep(ForegroundPoll);
        }
    }

    /// <summary>
    /// Whether the foreground window is this one or its root, as the library's own injection guard reads it, or another window of
    /// the same process: a popup or a dialog the app owns takes the app's input, not another app's.
    /// </summary>
    private static bool IsForeground(IntPtr hwnd)
    {
        if (ForegroundGuard.ForegroundBelongsTo((long)hwnd))
        {
            return true;
        }

        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var wanted);
        GetWindowThreadProcessId(foreground, out var actual);
        return wanted != 0 && wanted == actual;
    }

    private const int SwRestore = 9;
    private const int SwMinimize = 6;
    private const int SwMaximize = 3;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool attaching);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(IntPtr hwnd);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hwnd, int command);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
