// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Mobile.Tests.LaunchArguments;

/// <summary>.NET-only: the object literals of upstream's tests, as the resolved launch arguments the device code reads.</summary>
internal static class Declared
{
    public static ResolvedLaunchArguments Object(params (string Name, LaunchArgumentValue Value)[] entries) =>
        ResolvedLaunchArguments.Of(
            entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal)
        );

    public static ResolvedLaunchArguments List(params string[] items) => ResolvedLaunchArguments.Of(items);
}
