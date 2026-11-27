// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace E2E.Desktop;

/// <summary>What an observation captures. .NET-only enum for upstream's <c>'full' | 'interactive'</c> string union.</summary>
public enum DesktopSnapshotMode
{
    /// <summary>Includes static text, so judgments can read labels.</summary>
    Full,

    /// <summary>Keeps only the controls and the nodes that hold them. Locators read the whole window either way.</summary>
    Interactive,
}

/// <summary>
/// Options of the desktop engine: what the engine is given beside the app under test. The app is the target's <c>app</c>
/// (<c>bundleId</c>, <c>appPath</c>, <c>launchArguments</c>); these options only say how the engine drives it. Port of upstream's
/// <c>DesktopOptions</c> in <c>options.ts</c>.
/// </summary>
public sealed record DesktopOptions
{
    /// <summary>The default wait after an action the agent took, in milliseconds.</summary>
    public const int DefaultSettleMilliseconds = 150;

    /// <summary>The default depth of a read.</summary>
    public const int DefaultMaxDepth = 24;

    /// <summary>The deepest read the engine takes.</summary>
    public const int MaxDepthLimit = 50;

    /// <summary>What an observation captures. Default <see cref="DesktopSnapshotMode.Full"/>.</summary>
    public DesktopSnapshotMode? Snapshot { get; init; }

    /// <summary>
    /// Allows physical input: the real pointer and keyboard, which take focus and move the cursor. Off by default: every action goes through the
    /// accessibility API or into the app's own event queue, which leaves the user's pointer and focus alone, and the app stays in the background.
    /// On, <c>app.open()</c> brings the app forward, which some apps need before they show a window, keys and typing go through the real
    /// keyboard, and the engine also offers <c>doubleTap</c>, <c>hover</c>, and point-addressed taps.
    /// </summary>
    public bool? Headed { get; init; }

    /// <summary>
    /// How long the engine waits after an action the agent took before the agent observes again, in milliseconds. A test's own steps never wait:
    /// <c>expect</c> polls their outcome. Default 150. Upstream's <c>settle</c>, whose <c>false</c> is <see cref="Settle"/>.
    /// </summary>
    public int? SettleMilliseconds { get; init; }

    /// <summary>False skips the wait (upstream's <c>settle: false</c>); <see cref="SettleMilliseconds"/> is then not used. .NET-only split of the <c>number | false</c> union.</summary>
    public bool Settle { get; init; } = true;

    /// <summary>
    /// How many levels of a window's accessibility tree a read goes down, from 1 to 50. A read cut short at this depth cannot prove a control
    /// absent or the only one, so a locator on such a window reads again until its timeout: raise it for an app nested deeper. Default 24.
    /// </summary>
    public int? MaxDepth { get; init; }
}

/// <summary>The options with defaults applied, checked once at config load. Port of upstream's <c>ResolvedDesktopOptions</c>.</summary>
public sealed record ResolvedDesktopOptions(bool InteractiveOnly, bool Headed, int SettleMs, int MaxDepth);

/// <summary>
/// .NET-only holder name (C# has no free functions): upstream's <c>resolveOptions</c>, <c>unsupportedPlatform</c> and <c>requireMacOS</c> in
/// <c>options.ts</c>, and the <c>DesktopPlatform</c> label.
/// </summary>
public static class DesktopOptionsResolver
{
    /// <summary>The desktop operating system the engine drives, as its platform label: macOS only for now. Upstream's <c>DesktopPlatform</c>.</summary>
    public const string Platform = "macos";

    /// <summary>Checks the option bag the way every config option is checked: a wrong value fails the config load. Upstream's <c>resolveOptions</c>.</summary>
    public static ResolvedDesktopOptions Resolve(DesktopOptions? options)
    {
        var bag = options ?? new DesktopOptions();
        // .NET-only branch: a C# enum can hold a value no member names, where upstream's `snapshot` is checked against its two strings.
        if (bag.Snapshot is { } snapshot && !Enum.IsDefined(snapshot))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"desktop({{ snapshot }}) must be 'full' or 'interactive', got {(int)snapshot}"
            );
        }

        if (bag.SettleMilliseconds is < 0)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                "desktop({ settle }) must be a whole number of milliseconds, or false"
            );
        }

        if (bag.MaxDepth is { } depth && (depth < 1 || depth > DesktopOptions.MaxDepthLimit))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"desktop({{ maxDepth }}) must be a whole number from 1 to {DesktopOptions.MaxDepthLimit}"
            );
        }

        return new ResolvedDesktopOptions(
            bag.Snapshot == DesktopSnapshotMode.Interactive,
            bag.Headed == true,
            bag.Settle ? bag.SettleMilliseconds ?? DesktopOptions.DefaultSettleMilliseconds : 0,
            bag.MaxDepth ?? DesktopOptions.DefaultMaxDepth
        );
    }

    /// <summary>Why the engine refuses this machine: it drives macOS only, and the native driver is built for nothing else. Upstream's <c>unsupportedPlatform</c>.</summary>
    public static string UnsupportedPlatform() => UnsupportedPlatform(ProcessPlatform());

    /// <summary>
    /// The host's operating system as Node's <c>process.platform</c> names it (<c>darwin</c>, <c>linux</c>, <c>win32</c>), which upstream's messages
    /// carry. .NET-only: .NET has no <c>process.platform</c>.
    /// </summary>
    internal static string ProcessPlatform() =>
        OperatingSystem.IsMacOS() ? "darwin"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsWindows() ? "win32"
        : OperatingSystem.IsFreeBSD() ? "freebsd"
        : "unknown";

    /// <summary>The host's CPU architecture as Node's <c>process.arch</c> names it (<c>arm64</c>, <c>x64</c>). .NET-only: .NET has no <c>process.arch</c>.</summary>
    internal static string ProcessArch() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "ia32",
            Architecture.Arm => "arm",
            var other => other.ToString().ToLowerInvariant(),
        };

    /// <summary><see cref="UnsupportedPlatform()"/> for a host the caller names (.NET-only: tests answer for either OS).</summary>
    internal static string UnsupportedPlatform(string os) =>
        $"desktop() is not supported on {os}: it drives macOS apps only";

    /// <summary>Fails with <c>UNSUPPORTED_CAPABILITY</c> on any OS but macOS, the only one the native driver is built for. Upstream's <c>requireMacOS</c>.</summary>
    public static void RequireMacOS() => RequireMacOS(OperatingSystem.IsMacOS());

    /// <summary><see cref="RequireMacOS()"/> for a host the caller has already asked (.NET-only: tests answer for either OS).</summary>
    internal static void RequireMacOS(bool isMacOS, string? os = null)
    {
        if (!isMacOS)
        {
            throw new ConfigurationException(
                "UNSUPPORTED_CAPABILITY",
                os is null ? UnsupportedPlatform() : UnsupportedPlatform(os)
            );
        }
    }
}
