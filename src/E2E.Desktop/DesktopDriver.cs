// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop;

/// <summary>A rectangle in screen points, as the driver reports bounds. Port of upstream's <c>Rect</c> in <c>nodes.ts</c>.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>A native name and value pair a node carries, upstream's <c>native_id</c>.</summary>
public sealed record NativeId(string Kind, string Value);

/// <summary>One node of a driver snapshot. Port of upstream's <c>RawNode</c>.</summary>
public sealed record RawNode
{
    public string? RefId { get; init; }

    public required string Role { get; init; }

    public string? Name { get; init; }

    public string? Value { get; init; }

    public string? Description { get; init; }

    public string? Hint { get; init; }

    /// <summary>A heading's level, from the app's <c>AXHeadingLevel</c>.</summary>
    public int? Level { get; init; }

    public string? Placeholder { get; init; }

    public NativeId? NativeId { get; init; }

    public IReadOnlyList<string>? States { get; init; }

    public IReadOnlyList<string>? AvailableActions { get; init; }

    /// <summary>The axes a node offered <c>Scroll</c> scrolls along.</summary>
    public IReadOnlyList<string>? ScrollAxes { get; init; }

    public Rect? Bounds { get; init; }

    public bool? SubtreeTruncated { get; init; }

    /// <summary>How many children the element has, when the read stopped at the depth limit and listed none of them.</summary>
    public int? ChildrenCount { get; init; }

    public IReadOnlyList<RawNode>? Children { get; init; }
}

/// <summary>The window a snapshot read, upstream's <c>RawSnapshot.window</c>.</summary>
public sealed record SnapshotWindow(string Id, string? Title);

/// <summary>The <c>data</c> of a driver <c>snapshot</c>. Port of upstream's <c>RawSnapshot</c>.</summary>
public sealed record RawSnapshot
{
    public string? App { get; init; }

    public required string SnapshotId { get; init; }

    public bool? Complete { get; init; }

    /// <summary>The deadline stopped the read, as opposed to a stall or a limit.</summary>
    public bool? TimedOut { get; init; }

    /// <summary>The app did not answer for the window itself: the tree is a bare window, no window read yet.</summary>
    public bool? RootUnread { get; init; }

    public bool? Truncated { get; init; }

    public required RawNode Tree { get; init; }

    public SnapshotWindow? Window { get; init; }
}

/// <summary>How the driver says whether a call reached the app. Upstream's <c>Disposition</c>.</summary>
public sealed record Disposition(string? Delivery);

/// <summary>The <c>error</c> of a failed envelope. Port of upstream's <c>DriverError</c>.</summary>
public sealed record DriverError(
    string Code,
    string Message,
    Disposition? Disposition = null,
    IReadOnlyDictionary<string, object?>? Details = null
);

/// <summary>What a driver call resolves with: <c>{ ok, data }</c> or <c>{ ok, error }</c>. Port of upstream's <c>Envelope</c>.</summary>
public sealed record Envelope<T>
{
    public bool Ok { get; init; }

    public T? Data { get; init; }

    public DriverError? Error { get; init; }

    public static Envelope<T> Success(T data) => new() { Ok = true, Data = data };

    public static Envelope<T> Failure(DriverError error) => new() { Ok = false, Error = error };
}

/// <summary>The window a point was taken in, which must still take a click at it. Port of upstream's <c>PointerTarget</c>.</summary>
public sealed record PointerTarget(
    int Pid,
    string WindowId,
    bool Sheet,
    string? SheetId = null,
    bool? Content = null
);

/// <summary>One window of a process, as <c>listWindows</c> answers. Port of upstream's <c>RawWindow</c>.</summary>
public sealed record RawWindow
{
    public required string Id { get; init; }

    public string? Title { get; init; }

    public bool? IsFocused { get; init; }

    public bool? SheetFocused { get; init; }

    public string? SheetId { get; init; }

    public bool? IsMain { get; init; }

    public bool? Visible { get; init; }

    public bool? Accessible { get; init; }

    public bool? Minimized { get; init; }

    public int? Pid { get; init; }

    public Rect? Bounds { get; init; }

    /// <summary>A dialog macOS itself adds to the app's windows, which a test never means by default.</summary>
    public bool? System { get; init; }
}

/// <summary>The process a launch started. Port of upstream's <c>Launched</c>.</summary>
public sealed record Launched(int Pid, string App, string? BundleId = null, long? Started = null);

/// <summary>What <see cref="IDesktopDriver.Status"/> answers.</summary>
public sealed record DriverStatus(bool Accessibility, bool ScreenRecording);

/// <summary>The options of <see cref="IDesktopDriver.LaunchAsync"/>.</summary>
public sealed record LaunchRequest
{
    public string? Path { get; init; }

    public string? BundleId { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    public required string Token { get; init; }

    public bool Activate { get; init; }

    /// <summary>The whole launch's budget: <c>open</c>, then the search for the process it started.</summary>
    public int TimeoutMs { get; init; }

    /// <summary>How long that search may take once <c>open</c> has exited.</summary>
    public int ClaimMs { get; init; }

    /// <summary>Names the launch, so <see cref="IDesktopDriver.CancelInput"/> can stop it before it starts the app.</summary>
    public uint? InputId { get; init; }
}

/// <summary>The options of <see cref="IDesktopDriver.SnapshotAsync"/>.</summary>
public sealed record SnapshotRequest(int Pid, string WindowId, int MaxDepth, bool Interactive, int TimeoutMs);

/// <summary>The options of <see cref="IDesktopDriver.ActAsync"/>.</summary>
public sealed record ActRequest
{
    public required string Ref { get; init; }

    public required string Action { get; init; }

    public string? Value { get; init; }

    public string? Direction { get; init; }

    public int? Pages { get; init; }

    public bool? Physical { get; init; }

    /// <summary>Names the action, so <see cref="IDesktopDriver.CancelInput"/> can stop it before it sends input.</summary>
    public uint? InputId { get; init; }

    public int TimeoutMs { get; init; }
}

/// <summary>The options of <see cref="IDesktopDriver.PressAsync"/>.</summary>
public sealed record PressRequest
{
    public int Pid { get; init; }

    public required string WindowId { get; init; }

    public required string Combo { get; init; }

    public bool Physical { get; init; }

    public uint? InputId { get; init; }

    public int TimeoutMs { get; init; }

    public string? FocusedRef { get; init; }
}

/// <summary>The options of <see cref="IDesktopDriver.ScreenshotAsync"/>.</summary>
public sealed record ScreenshotRequest(string WindowId, string Path, int TimeoutMs);

/// <summary>What <see cref="IDesktopDriver.ScreenshotAsync"/> answers.</summary>
public sealed record ScreenshotResult(double? ScaleFactor);

/// <summary>An empty answer, upstream's <c>Record&lt;string, never&gt;</c>.</summary>
public sealed record Nothing
{
    public static Nothing Value { get; } = new();
}

/// <summary>
/// What the driver can do: one function per native call. Port of upstream's <c>DesktopDriver</c> in <c>driver.ts</c>, whose native
/// side is the <c>e2e-desktop-napi</c> crate. The macOS implementation is <see cref="MacOsDriver"/>; tests script a fake one.
/// </summary>
public interface IDesktopDriver
{
    /// <summary>The version the driver came from.</summary>
    string Version { get; }

    DriverStatus Status();

    /// <summary>Whether the screen is locked; null when that cannot be told.</summary>
    bool? ScreenLocked();

    /// <summary>When a process started, in microseconds since the epoch; null when it is not running.</summary>
    long? ProcessStarted(int pid);

    /// <summary>Every process that carries the launch <paramref name="token"/>, in its environment or its arguments; null when the processes cannot be listed.</summary>
    Task<IReadOnlyList<int>?> ProcessesCarryingAsync(string token);

    /// <summary>Drops the refs of a process that quit.</summary>
    void ForgetProcess(int pid);

    Task<Envelope<Launched>> LaunchAsync(LaunchRequest request);

    Task<Envelope<IReadOnlyList<RawWindow>>> ListWindowsAsync(int pid);

    Task<Envelope<RawSnapshot>> SnapshotAsync(SnapshotRequest request);

    Task<Envelope<Nothing>> ActAsync(ActRequest request);

    /// <summary>A fresh id for one call's input, unique across every worker thread of the process.</summary>
    uint InputId();

    /// <summary>Stops the action given <paramref name="inputId"/>: it sends no input from then on.</summary>
    void CancelInput(uint inputId);

    Task<Envelope<Nothing>> PressAsync(PressRequest request);

    /// <summary>Fails as <see cref="PressAsync"/> would for a combo this Mac's keyboard layout cannot type, and sends nothing.</summary>
    Task<Envelope<Nothing>> CheckComboAsync(string combo);

    Task<Envelope<Nothing>> FocusWindowAsync(int pid, string windowId, uint? inputId = null, int? timeoutMs = null);

    Task<Envelope<Nothing>> PointerAsync(
        double x,
        double y,
        string kind,
        uint? inputId = null,
        int? timeoutMs = null,
        PointerTarget? target = null
    );

    Task<Envelope<ScreenshotResult>> ScreenshotAsync(ScreenshotRequest request);
}
