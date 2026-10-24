// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;

namespace E2E.WinApp.Tests.Helpers;

/// <summary>How the fake answers a launch.</summary>
public enum LaunchBehavior
{
    /// <summary>The app shows its window at once.</summary>
    Normal,

    /// <summary>The app never shows a window: the launch waits until the token is cancelled.</summary>
    Hang,

    /// <summary>The app could not be started.</summary>
    Fail,
}

/// <summary>A scripted <see cref="IWindowDriver"/>: it records every call, answers the trees and windows a test gives it, and fails where a test says.</summary>
public sealed class FakeWindowDriver : IWindowDriver
{
    public const long MainHandle = 0x1000;

    private int _nextPid = 4000;

    /// <summary>Every call, in order, as a short line: <c>launch app.exe --flag</c>, <c>invoke btn Auto</c>, <c>sendKeys 4096 ctrl+a</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The windows of each process id.</summary>
    public Dictionary<int, List<WindowInfo>> Windows { get; } = [];

    /// <summary>The tree of each window handle.</summary>
    public Dictionary<long, RawElement> Trees { get; } = [];

    /// <summary>Apps <see cref="AttachAsync"/> finds, by the name asked.</summary>
    public Dictionary<string, LaunchedApp> Attachable { get; } = new(StringComparer.OrdinalIgnoreCase);

    public LaunchBehavior Launch { get; set; } = LaunchBehavior.Normal;

    /// <summary>The process ids <see cref="CloseAsync"/> was asked to end.</summary>
    public List<int> Closed { get; } = [];

    /// <summary>The handle of the focused element.</summary>
    public string? Focused { get; set; }

    /// <summary>Handles whose value pattern refuses the text.</summary>
    public HashSet<string> RejectsValue { get; } = [];

    /// <summary>Handles the driver no longer finds.</summary>
    public HashSet<string> Vanished { get; } = [];

    /// <summary>What <see cref="ScreenshotAsync"/> answers. Default: a 4x2 image of one colour.</summary>
    public RawScreenshot Screenshot { get; set; } = new(Enumerable.Repeat(new byte[] { 10, 20, 30, 255 }, 8).SelectMany(pixel => pixel).ToArray(), 4, 2);

    /// <summary>The failure to throw from the next call whose label starts with the key.</summary>
    public Dictionary<string, Exception> Faults { get; } = [];

    public bool Disposed { get; private set; }

    public LaunchedApp? LastLaunched { get; private set; }

    /// <summary>Gives a process one window with a tree, so an app can be launched and observed.</summary>
    public void Script(int processId, long handle, string title, RawElement tree, Rect? bounds = null, double scale = 1)
    {
        var box = bounds ?? new Rect(100, 50, 800, 600);
        if (!Windows.TryGetValue(processId, out var windows))
        {
            windows = [];
            Windows[processId] = windows;
        }

        windows.RemoveAll(window => window.Handle == handle);
        windows.Add(new WindowInfo(handle, processId, title, box, scale));
        Trees[handle] = tree;
    }

    public Task<LaunchedApp> LaunchAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, Action<AppLogEntry>? log, CancellationToken cancellationToken)
    {
        Record($"launch {Path.GetFileName(executable)}{(arguments.Count == 0 ? "" : " " + string.Join(' ', arguments))}");
        Fault("launch");
        switch (Launch)
        {
            case LaunchBehavior.Fail:
                throw new WindowDriverException(DriverFault.Launch, $"{Path.GetFileName(executable)} exited with code 1 before it showed a window");
            case LaunchBehavior.Hang:
                return HangAsync(cancellationToken);
        }

        var pid = _nextPid++;
        log?.Invoke(new AppLogEntry(AppLogSource.Console, AppLogLevel.Info, $"{Path.GetFileName(executable)} started"));
        if (!Windows.ContainsKey(pid))
        {
            // A fresh process gets the windows and trees scripted for the main handle.
            var handle = MainHandle;
            var template = Windows.Values.SelectMany(list => list).FirstOrDefault(window => window.Handle == handle);
            var title = Trees.TryGetValue(handle, out var scripted) && scripted.Name is { Length: > 0 } named ? named : Path.GetFileNameWithoutExtension(executable);
            Windows[pid] = [template is null ? new WindowInfo(handle, pid, title, new Rect(100, 50, 800, 600), 1) : template with { ProcessId = pid }];
            Trees.TryAdd(handle, new RawElement { Handle = "root", Type = "Window", Name = Path.GetFileNameWithoutExtension(executable) });
        }

        LastLaunched = new LaunchedApp(pid, Windows[pid][0].Handle, Path.GetFileNameWithoutExtension(executable), Owned: true);
        return Task.FromResult(LastLaunched);
    }

    public Task<LaunchedApp> AttachAsync(string app, CancellationToken cancellationToken)
    {
        Record($"attach {app}");
        Fault("attach");
        return Attachable.TryGetValue(app, out var found)
            ? Task.FromResult(found)
            : throw new WindowDriverException(DriverFault.AppNotFound, $"no running app matches {app}");
    }

    public Task CloseAsync(LaunchedApp app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Record($"close {app.ProcessId}");
        Fault("close");
        Closed.Add(app.ProcessId);
        Windows.Remove(app.ProcessId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WindowInfo>> WindowsAsync(int processId, CancellationToken cancellationToken)
    {
        Record($"windows {processId}");
        Fault("windows");
        return Task.FromResult<IReadOnlyList<WindowInfo>>(Windows.TryGetValue(processId, out var windows) ? [.. windows] : []);
    }

    public Task<RawElement> InspectAsync(long windowHandle, CancellationToken cancellationToken)
    {
        Record($"inspect {windowHandle}");
        Fault("inspect");
        return Task.FromResult(Trees.TryGetValue(windowHandle, out var tree) ? tree : new RawElement { Handle = $"w{windowHandle}", Type = "Window" });
    }

    public Task<string?> FocusedAsync(long windowHandle, CancellationToken cancellationToken)
    {
        Record($"focused {windowHandle}");
        return Task.FromResult(Focused);
    }

    public Task<string> InvokeAsync(string handle, InvokeKind kind, CancellationToken cancellationToken)
    {
        Record($"invoke {handle} {kind}");
        Known(handle);
        Fault("invoke");
        return Task.FromResult(kind.ToString());
    }

    public Task SetValueAsync(string handle, string text, CancellationToken cancellationToken)
    {
        Record($"setValue {handle} {text}");
        Known(handle);
        Fault("setValue");
        if (RejectsValue.Contains(handle))
        {
            throw new WindowDriverException(DriverFault.ValueRejected, $"element {handle} has no value pattern");
        }

        return Task.CompletedTask;
    }

    public Task FocusAsync(string handle, CancellationToken cancellationToken)
    {
        Record($"focus {handle}");
        Known(handle);
        Fault("focus");
        return Task.CompletedTask;
    }

    public Task ScrollIntoViewAsync(string handle, CancellationToken cancellationToken)
    {
        Record($"scrollIntoView {handle}");
        Known(handle);
        Fault("scrollIntoView");
        return Task.CompletedTask;
    }

    public Task ScrollAsync(string handle, ScrollDirection direction, CancellationToken cancellationToken)
    {
        Record($"scroll {handle} {direction}");
        Known(handle);
        Fault("scroll");
        return Task.CompletedTask;
    }

    public Task SendKeysAsync(long windowHandle, string keys, bool verbatim, CancellationToken cancellationToken)
    {
        Record($"sendKeys {windowHandle} {(verbatim ? "verbatim:" : "")}{keys}");
        Fault("sendKeys");
        return Task.CompletedTask;
    }

    public Task ClickAsync(Point screen, bool doubleClick, bool rightClick, CancellationToken cancellationToken)
    {
        Record($"click {screen.X},{screen.Y}{(doubleClick ? " double" : "")}{(rightClick ? " right" : "")}");
        Fault("click");
        return Task.CompletedTask;
    }

    public Task HoverAsync(Point screen, CancellationToken cancellationToken)
    {
        Record($"hover {screen.X},{screen.Y}");
        return Task.CompletedTask;
    }

    public Task DragAsync(Point from, Point to, TimeSpan hold, CancellationToken cancellationToken)
    {
        Record($"drag {from.X},{from.Y} -> {to.X},{to.Y} hold {hold.TotalMilliseconds}");
        return Task.CompletedTask;
    }

    public Task ScrollWheelAsync(Point screen, int notches, CancellationToken cancellationToken)
    {
        Record($"wheel {screen.X},{screen.Y} {notches}");
        return Task.CompletedTask;
    }

    public Task ForegroundAsync(long windowHandle, CancellationToken cancellationToken)
    {
        Record($"foreground {windowHandle}");
        Fault("foreground");
        return Task.CompletedTask;
    }

    public Task<RawScreenshot> ScreenshotAsync(long windowHandle, CancellationToken cancellationToken)
    {
        Record($"screenshot {windowHandle}");
        Fault("screenshot");
        return Task.FromResult(Screenshot);
    }

    public Task ResizeAsync(long windowHandle, int width, int height, CancellationToken cancellationToken)
    {
        Record($"resize {windowHandle} {width}x{height}");
        return Task.CompletedTask;
    }

    public Task ShowAsync(long windowHandle, WindowState state, CancellationToken cancellationToken)
    {
        Record($"show {windowHandle} {state}");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Record("dispose");
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private void Record(string call) => Calls.Add(call);

    private void Known(string handle)
    {
        if (Vanished.Contains(handle))
        {
            throw new WindowDriverException(DriverFault.ElementVanished, $"element {handle} is not part of the newest observation");
        }
    }

    private void Fault(string operation)
    {
        var key = Faults.Keys.FirstOrDefault(operation.StartsWith);
        if (key is not null && Faults.Remove(key, out var failure))
        {
            throw failure;
        }
    }

    private static async Task<LaunchedApp> HangAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }
}
