// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>
/// The app a Windows target tests: a build by <see cref="AppPath"/>, which every <c>app.open()</c> launches fresh, or a running app by
/// <see cref="BundleId"/>, which it attaches to. The names are the config's <c>app.appPath</c> and <c>app.bundleId</c>, shared with the
/// device targets, so one config schema serves every pinned-app platform.
/// </summary>
public sealed record WinAppApp
{
    /// <summary>The process name, window title, or process id of a running app to attach to, and the name the app is known by.</summary>
    public string? BundleId { get; init; }

    /// <summary>The executable <c>app.open()</c> launches, relative to the project root.</summary>
    public string? AppPath { get; init; }

    /// <summary>Arguments every fresh launch of the app takes.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }
}

/// <summary>Options of the Windows app engine.</summary>
public sealed record WinAppOptions
{
    /// <summary>The name of the target the engine serves.</summary>
    public string TargetName { get; init; } = "windows";

    /// <summary>The app under test.</summary>
    public WinAppApp App { get; init; } = new();

    /// <summary>How long the engine waits after injecting input before it reads the screen again, in milliseconds. Default 150.</summary>
    public int SettleMilliseconds { get; init; } = DefaultSettleMilliseconds;

    /// <summary>False skips the settle wait.</summary>
    public bool Settle { get; init; } = true;

    /// <summary>How long a launch may take to show a window.</summary>
    public TimeSpan LaunchTimeout { get; init; } = E2EDefaults.LaunchTimeout;

    public const int DefaultSettleMilliseconds = 150;
}
