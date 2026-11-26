// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.Tests.LaunchArguments;

/// <summary><c>describe('assertLaunchArgumentsFor')</c> of <c>packages/mobile/tests/unit/launch-arguments.test.ts</c>.</summary>
public sealed class AssertLaunchArgumentsForTests
{
    private const string Label = "target \"pixel\" app.launchArguments";

    [Fact]
    public void Refuses_Apples_arguments_in_an_object_on_Android_and_nothing_else()
    {
        foreach (
            var name in new[]
            {
                "AppleLanguages",
                "AppleLocale",
                "AppleKeyboards",
                "AppleFirstWeekday",
                "AppleTextDirection",
                "NSDoubleLocalizedStrings",
            }
        )
        {
            var error = Assert.Throws<ConfigurationException>(() =>
                DeviceLaunchArguments.AssertLaunchArgumentsFor(
                    MobilePlatform.Android,
                    Label,
                    Declared.Object((name, "x"))
                )
            );
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Contains($"{Label}.{name} is an iOS launch argument", error.Message, StringComparison.Ordinal);
        }

        DeviceLaunchArguments.AssertLaunchArgumentsFor(
            MobilePlatform.Android,
            "label",
            Declared.Object(("e2e", true), ("ApplePayEnabled", true))
        );
        DeviceLaunchArguments.AssertLaunchArgumentsFor(
            MobilePlatform.Android,
            "label",
            Declared.List("-AppleLocale", "fr_FR")
        );
        DeviceLaunchArguments.AssertLaunchArgumentsFor(MobilePlatform.Android, "label", null);
        DeviceLaunchArguments.AssertLaunchArgumentsFor(
            MobilePlatform.Ios,
            "label",
            Declared.Object(("AppleLocale", "fr_FR"))
        );
    }

    [Fact]
    public void Refuses_a_list_am_start_would_read_back_changed_on_Android_and_takes_any_list_on_iOS()
    {
        var cases = new (string[] Tags, string Message)[]
        {
            ([], $"{Label}.tags is an empty list, which Android reads as one empty item"),
            (["a", ""], $"{Label}.tags item 1 is empty"),
            (["a,b"], $"{Label}.tags item 0 holds a comma"),
            (["a\\", "b"], $"{Label}.tags item 0 ends in a backslash"),
        };
        foreach (var (tags, message) in cases)
        {
            var error = Assert.Throws<ConfigurationException>(() =>
                DeviceLaunchArguments.AssertLaunchArgumentsFor(
                    MobilePlatform.Android,
                    Label,
                    Declared.Object(("tags", tags))
                )
            );
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal($"{message}: am start splits a list extra on commas", error.Message);
            DeviceLaunchArguments.AssertLaunchArgumentsFor(MobilePlatform.Ios, Label, Declared.Object(("tags", tags)));
        }

        DeviceLaunchArguments.AssertLaunchArgumentsFor(
            MobilePlatform.Android,
            Label,
            Declared.Object(("tags", new[] { "a", "b c", "back\\slash" }))
        );
    }

    [Fact]
    public void Fails_a_launch_a_test_asked_for_with_INVALID_ARGUMENT()
    {
        var error = Assert.Throws<TestException>(() =>
            DeviceLaunchArguments.AssertLaunchArgumentsFor(
                MobilePlatform.Android,
                "device.openApp({ launchArguments })",
                Declared.Object(("AppleLocale", "fr_FR")),
                "INVALID_ARGUMENT"
            )
        );
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }
}
