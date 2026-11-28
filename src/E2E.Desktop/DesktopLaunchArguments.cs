// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Desktop;

/// <summary>.NET-only holder name: upstream's <c>launchArgumentList</c> in <c>launch-arguments.ts</c>.</summary>
public static class DesktopLaunchArguments
{
    /// <summary>
    /// The process arguments of a launch: the target's list as it is, or its object spelled as Apple user defaults (<c>-name value</c>).
    /// Upstream's <c>launchArgumentList</c>.
    /// </summary>
    public static IReadOnlyList<string> LaunchArgumentList(EngineAppInfo app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.LaunchArgumentValues is { } values)
        {
            return LaunchArgumentRules.AppleUserDefaults(ResolvedLaunchArguments.Of(values));
        }

        return app.LaunchArguments is { } list
            ? LaunchArgumentRules.AppleUserDefaults(ResolvedLaunchArguments.Of(list))
            : [];
    }
}
