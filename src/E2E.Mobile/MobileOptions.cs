// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>What an observation captures.</summary>
public enum SnapshotMode
{
    /// <summary>Includes static text, so judgments can read values.</summary>
    Full,

    /// <summary>Keeps only actionable nodes. Cheaper on screens with long lists.</summary>
    Interactive,
}

/// <summary>Every permission agent-device can grant, deny, or reset on an app.</summary>
public enum DevicePermission
{
    Camera,
    Microphone,
    Photos,
    Contacts,
    Notifications,
    Calendar,
    Location,
    Reminders,
    Motion,
    Siri,
    MediaLibrary,
}

/// <summary>A permission's state: held, refused, or not asked for yet, so the OS asks again.</summary>
public enum PermissionState
{
    Grant,
    Deny,
    Reset,
}

/// <summary>The names agent-device knows the permissions and their states by.</summary>
public static class DevicePermissions
{
    public static string WireName(this DevicePermission permission) => permission switch
    {
        DevicePermission.Camera => "camera",
        DevicePermission.Microphone => "microphone",
        DevicePermission.Photos => "photos",
        DevicePermission.Contacts => "contacts",
        DevicePermission.Notifications => "notifications",
        DevicePermission.Calendar => "calendar",
        DevicePermission.Location => "location",
        DevicePermission.Reminders => "reminders",
        DevicePermission.Motion => "motion",
        DevicePermission.Siri => "siri",
        DevicePermission.MediaLibrary => "media-library",
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission."),
    };

    public static string WireName(this PermissionState state) => state switch
    {
        PermissionState.Grant => "grant",
        PermissionState.Deny => "deny",
        PermissionState.Reset => "reset",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown permission state."),
    };

    public static string WireName(this MobilePlatform platform) => platform switch
    {
        MobilePlatform.Ios => "ios",
        MobilePlatform.Android => "android",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };
}

/// <summary>
/// The app a device target tests: an installed app by <see cref="BundleId"/>, or a build by <see cref="AppPath"/>.
/// Upstream reads these from the target's <c>app</c>; the port has no target config for a device yet, so
/// <see cref="MobileOptions.App"/> carries them.
/// </summary>
public sealed record MobileApp
{
    /// <summary>The bundle id, package, or display name <c>app.open()</c> launches.</summary>
    public string? BundleId { get; init; }

    /// <summary>The build the suite installs with <c>device.installApp()</c>, relative to the project root.</summary>
    public string? AppPath { get; init; }

    /// <summary>Arguments every fresh launch of the app takes: process arguments on iOS, <c>am start</c> arguments on Android.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }

    /// <summary>Permissions the app holds before every fresh launch.</summary>
    public IReadOnlyDictionary<DevicePermission, PermissionState>? Permissions { get; init; }
}

/// <summary>
/// Options of the device engine: which devices it drives and how. At most one of <see cref="Device"/>,
/// <see cref="Devices"/>, and <see cref="Provider"/> is set.
/// </summary>
public sealed record MobileOptions
{
    /// <summary>Platform the target's device runs.</summary>
    public required MobilePlatform Platform { get; init; }

    /// <summary>
    /// Device to use: a simulator or emulator by name, simulator UDID, or emulator serial (<c>emulator-5554</c>),
    /// or a connected phone by name. Omit all three device options to use every booted device of the platform that no other agent-device session holds; with none booted, agent-device boots a shut-down one (a phone or tablet over a TV, then the newest OS, then name order), and with every booted one held, prepare fails.
    /// </summary>
    public string? Device { get; init; }

    /// <summary>
    /// A pool of devices: the engine declares one worker per entry, and worker slot <c>n</c> drives the
    /// <c>n</c>th.
    /// </summary>
    public IReadOnlyList<string>? Devices { get; init; }

    /// <summary>A provider leases hosted devices instead: one per worker slot at prepare, all released when the run ends.</summary>
    public IDeviceProvider? Provider { get; init; }

    /// <summary>
    /// agent-device session name, before the worker slot: slot <c>n</c> drives its device under
    /// <c>&lt;session&gt;-&lt;n&gt;</c>, <c>e2e-&lt;target name&gt;-&lt;n&gt;</c> by default.
    /// </summary>
    public string? Session { get; init; }

    /// <summary>The name of the target the engine serves, which names its sessions and its pool variable.</summary>
    public string TargetName { get; init; } = "mobile";

    /// <summary>The app under test.</summary>
    public MobileApp App { get; init; } = new();

    public SnapshotMode Snapshot { get; init; } = SnapshotMode.Full;

    /// <summary>
    /// How long the UI must hold still after a tap, fill, screen scroll, or back before the agent observes
    /// again, in milliseconds; <see cref="Settle"/> false skips the wait. The wait keeps that observation
    /// off a transition frame; the runner's own polling (<c>Expect</c>, actions waiting for their node) covers
    /// the rest, so a short window suffices. Default 150. Every frame of a running animation changes the tree, so a
    /// 150 ms window still catches a transition in flight; raise it for slow devices or apps that settle in stages, or
    /// lower it to 0 to keep only the first re-observation. A negative value is <c>INVALID_CONFIG</c>. The agent's
    /// actions and <c>Device.BackAsync</c> carry the wait; a test's steps do not settle, and <c>Expect</c> verifies their outcome; they rely on <see cref="TransitionMilliseconds"/> and polling.
    /// </summary>
    public int SettleMilliseconds { get; init; } = DefaultSettleMilliseconds;

    /// <summary>
    /// False skips the settle wait (upstream's <c>settle: false</c>); <see cref="SettleMilliseconds"/> is then not used.
    /// </summary>
    public bool Settle { get; init; } = true;

    /// <summary>
    /// How long a control that appeared or moved with the last action is given to finish arriving before a test
    /// acts on it, in milliseconds. Default 500, the length of a modal or screen transition. If the control changes during the
    /// wait, the action fails with a retryable <c>NODE_STALE</c> and a locator resolves it again. Unmoved controls are acted
    /// on immediately only when keyboard visibility is unchanged. Opening or closing the keyboard also uses the remaining
    /// budget. A test's screen swipe waits out the remaining budget before it scrolls. Raise it for slower transitions.
    /// </summary>
    public int TransitionMilliseconds { get; init; } = DefaultTransitionMilliseconds;

    /// <summary>
    /// Whether a video recording draws agent-device's touch indicator where a test tapped. Default true. False records the
    /// plain screen and sends agent-device <c>hideTouches</c>, so agent-device skips the pass that draws the indicator once the
    /// recording stops: on a hosted daemon such as Expo Simulators' that pass can outlast the attempt's <c>CleanupTimeout</c>.
    /// </summary>
    public bool VideoTouches { get; init; } = true;

    public const int DefaultSettleMilliseconds = 150;

    public const int DefaultTransitionMilliseconds = 500;
}
