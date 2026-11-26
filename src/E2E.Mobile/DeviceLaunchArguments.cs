// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Mobile;

/// <summary>
/// What a device does with launch arguments (upstream's <c>mobile/src/launch-arguments.ts</c>: <c>launchArgumentsForOpen</c>,
/// <c>assertLaunchArgumentsFor</c>, <c>launchArgumentList</c>). .NET-only holder name: C# has no free functions.
/// </summary>
public static class DeviceLaunchArguments
{
    /// <summary>The Apple user-defaults arguments a suite is likely to set; Android reads none of them. A name not listed here passes.</summary>
    private static readonly HashSet<string> IosOnlyArguments = new(StringComparer.Ordinal)
    {
        "AppleLanguages",
        "AppleLocale",
        "AppleKeyboards",
        "AppleMeasurementUnits",
        "AppleMetricUnits",
        "AppleTemperatureUnit",
        "AppleFirstWeekday",
        "AppleTextDirection",
        "AppleICUForce24HourTime",
        "AppleICUForce12HourTime",
        "NSDoubleLocalizedStrings",
        "NSShowNonLocalizedStrings",
        "NSForceRightToLeftWritingDirection",
    };

    /// <summary>Java's <c>int</c> range; <c>am start</c> parses <c>--ei</c> with <c>Integer.parseInt</c>.</summary>
    private const double Int32Min = int.MinValue;

    private const double Int32Max = int.MaxValue;

    /// <summary>
    /// The launch arguments of one <c>device.openApp</c>: checked as declared, and against the platform. A test's own launch fails with
    /// <c>INVALID_ARGUMENT</c>. Upstream's <c>launchArgumentsForOpen</c>.
    /// </summary>
    public static ResolvedLaunchArguments? LaunchArgumentsForOpen(
        MobilePlatform platform,
        IReadOnlyList<string>? list,
        IReadOnlyDictionary<string, LaunchArgumentValue?>? values
    )
    {
        const string label = "device.openApp({ launchArguments })";
        var launchArguments = LaunchArgumentRules.CheckLaunchArguments(label, list, values, "INVALID_ARGUMENT");
        AssertLaunchArgumentsFor(platform, label, launchArguments, "INVALID_ARGUMENT");
        return launchArguments;
    }

    /// <summary>
    /// Refuses an object Android cannot carry as declared: one of Apple's arguments, or a list <c>am start</c> would read back changed.
    /// <c>--esa</c> splits its value on every comma and keeps a backslash before one, so an item holds no comma and ends in no backslash; an empty
    /// value arrives as one empty item, so neither the list nor an item is empty. Upstream's <c>assertLaunchArgumentsFor</c>.
    /// </summary>
    public static void AssertLaunchArgumentsFor(
        MobilePlatform platform,
        string label,
        ResolvedLaunchArguments? launchArguments,
        string code = "INVALID_CONFIG"
    )
    {
        ArgumentNullException.ThrowIfNull(label);
        if (platform != MobilePlatform.Android || launchArguments?.Values is not { } values)
        {
            return;
        }

        foreach (var (name, value) in values)
        {
            if (IosOnlyArguments.Contains(name))
            {
                throw Fail(
                    code,
                    $"{label}.{name} is an iOS launch argument, and Android reads none of Apple's; set the device's language before the run, or language and locale on a hosted device provider"
                );
            }

            if (value.Kind != LaunchArgumentValueKind.List)
            {
                continue;
            }

            if (AndroidListProblem(value.Items!) is { } problem)
            {
                throw Fail(code, $"{label}.{name} {problem}: am start splits a list extra on commas");
            }
        }
    }

    /// <summary>The process arguments of a launch on <paramref name="platform"/>; null for none. Upstream's <c>launchArgumentList</c>.</summary>
    public static IReadOnlyList<string>? LaunchArgumentList(
        MobilePlatform platform,
        ResolvedLaunchArguments? launchArguments
    )
    {
        if (launchArguments is null)
        {
            return null;
        }

        var list =
            platform == MobilePlatform.Android
                ? AndroidArguments(launchArguments)
                : LaunchArgumentRules.AppleUserDefaults(launchArguments);
        return list.Count == 0 ? null : list;
    }

    private static string? AndroidListProblem(IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return "is an empty list, which Android reads as one empty item";
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Length == 0)
            {
                return $"item {index} is empty";
            }

            if (item.Contains(',', StringComparison.Ordinal))
            {
                return $"item {index} holds a comma";
            }

            if (item.EndsWith('\\'))
            {
                return $"item {index} ends in a backslash";
            }
        }

        return null;
    }

    private static IReadOnlyList<string> AndroidArguments(ResolvedLaunchArguments launchArguments) =>
        launchArguments.List ?? [.. launchArguments.Values!.SelectMany(pair => AndroidExtra(pair.Key, pair.Value))];

    /// <summary>
    /// The extra of the value's type, so the app reads it with the matching <c>Intent</c> getter: <c>--ez</c> boolean, <c>--ei</c> int, <c>--el</c>
    /// long, <c>--ef</c> float, <c>--es</c> string, and <c>--esa</c> for a list, whose items <c>am</c> splits on commas.
    /// </summary>
    private static string[] AndroidExtra(string name, LaunchArgumentValue value) =>
        value.Kind switch
        {
            LaunchArgumentValueKind.Boolean => ["--ez", name, value.ToString()],
            LaunchArgumentValueKind.Number => [AndroidNumberFlag(value.Number!.Value), name, value.ToString()],
            LaunchArgumentValueKind.String => ["--es", name, value.ToString()],
            _ => ["--esa", name, value.ToString()],
        };

    private static string AndroidNumberFlag(double value)
    {
        if (Math.Floor(value) != value)
        {
            return "--ef";
        }

        return value is >= Int32Min and <= Int32Max ? "--ei" : "--el";
    }

    private static Exception Fail(string code, string message) =>
        code == "INVALID_CONFIG" ? new ConfigurationException(code, message) : new TestException(code, message);
}
