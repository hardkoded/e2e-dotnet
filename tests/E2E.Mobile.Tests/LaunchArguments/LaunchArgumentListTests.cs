// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile.Tests.LaunchArguments;

/// <summary><c>describe('launchArgumentList')</c> of <c>packages/mobile/tests/unit/launch-arguments.test.ts</c>.</summary>
public sealed class LaunchArgumentListTests
{
    [Fact]
    public void Passes_a_list_through_as_given_and_sends_nothing_for_none()
    {
        Assert.Equal(
            ["-e2e", "YES"],
            DeviceLaunchArguments.LaunchArgumentList(MobilePlatform.Ios, Declared.List("-e2e", "YES"))
        );
        Assert.Equal(
            ["--ez", "e2e", "true"],
            DeviceLaunchArguments.LaunchArgumentList(MobilePlatform.Android, Declared.List("--ez", "e2e", "true"))
        );
        Assert.Null(DeviceLaunchArguments.LaunchArgumentList(MobilePlatform.Ios, null));
        Assert.Null(DeviceLaunchArguments.LaunchArgumentList(MobilePlatform.Ios, Declared.List()));
        Assert.Null(DeviceLaunchArguments.LaunchArgumentList(MobilePlatform.Android, Declared.Object()));
    }

    [Fact]
    public void Spells_an_object_as_iOS_process_arguments()
    {
        Assert.Equal(
            [
                "-AppleLocale",
                "fr_FR",
                "-e2e",
                "YES",
                "-dark",
                "NO",
                "-retries",
                "3",
                "-ratio",
                "1.5",
                "-url",
                "http://localhost:8081/x.json",
            ],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(
                    ("AppleLocale", "fr_FR"),
                    ("e2e", true),
                    ("dark", false),
                    ("retries", 3),
                    ("ratio", 1.5),
                    ("url", "http://localhost:8081/x.json")
                )
            )
        );
        Assert.Equal(
            ["-AppleLanguages", "(fr)"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("AppleLanguages", new[] { "fr" }))
            )
        );
        Assert.Equal(
            ["-AppleLanguages", "(fr, en-GB, zh-Hans)"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("AppleLanguages", new[] { "fr", "en-GB", "zh-Hans" }))
            )
        );
    }

    [Fact]
    public void Quotes_a_string_NSUserDefaults_would_otherwise_read_as_a_plist_as_a_value_and_as_a_list_item()
    {
        Assert.Equal(
            ["-title", "\"(draft)\"", "-greeting", "\"a b\"", "-json", "\"{a=b;}\"", "-quoted", "\"say \\\"hi\\\"\""],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("title", "(draft)"), ("greeting", "a b"), ("json", "{a=b;}"), ("quoted", "say \"hi\""))
            )
        );
        Assert.Equal(
            ["-names", "(\"a b\", \"say \\\"hi\\\"\", \"back\\\\slash\", \"x,y\", ok_1.2:3/4-5$)"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("names", new[] { "a b", "say \"hi\"", "back\\slash", "x,y", "ok_1.2:3/4-5$" }))
            )
        );
        Assert.Equal(
            ["-empty", "()"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("empty", Array.Empty<string>()))
            )
        );
        Assert.Equal(
            ["-tags", "(\"\", a)", "-blank", "\"\""],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("tags", new[] { "", "a" }), ("blank", ""))
            )
        );
    }

    [Fact]
    public void Quotes_a_string_that_starts_with_slash_slash_which_the_plist_parser_reads_as_a_comment()
    {
        Assert.Equal(
            ["-endpoints", "(\"//cdn.example/assets\", b)", "-cdn", "\"//cdn.example\"", "-path", "a//b"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(
                    ("endpoints", new[] { "//cdn.example/assets", "b" }),
                    ("cdn", "//cdn.example"),
                    ("path", "a//b")
                )
            )
        );
    }

    [Fact]
    public void Quotes_a_value_that_starts_with_a_dash_which_NSUserDefaults_would_read_as_the_next_name()
    {
        Assert.Equal(
            ["-offset", "\"-1\"", "-ratio", "\"-0.5\"", "-mode", "\"-x\"", "-range", "a-b"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("offset", -1), ("ratio", -0.5), ("mode", "-x"), ("range", "a-b"))
            )
        );
        Assert.Equal(
            ["-flags", "(\"-x\", y)"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Ios,
                Declared.Object(("flags", new[] { "-x", "y" }))
            )
        );
    }

    [Fact]
    public void Spells_an_object_as_am_start_extras_of_each_values_type()
    {
        Assert.Equal(
            [
                "--ez",
                "e2e",
                "true",
                "--ez",
                "dark",
                "false",
                "--es",
                "locale",
                "fr_FR",
                "--ei",
                "retries",
                "3",
                "--ef",
                "ratio",
                "1.5",
                "--el",
                "epoch",
                "2147483648",
                "--ei",
                "negative",
                "-1",
                "--es",
                "spaced",
                "a b",
            ],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Android,
                Declared.Object(
                    ("e2e", true),
                    ("dark", false),
                    ("locale", "fr_FR"),
                    ("retries", 3),
                    ("ratio", 1.5),
                    ("epoch", 2147483648L),
                    ("negative", -1),
                    ("spaced", "a b")
                )
            )
        );
        Assert.Equal(
            ["--ei", "max", "2147483647", "--ei", "min", "-2147483648", "--el", "below", "-2147483649"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Android,
                Declared.Object(("max", 2147483647), ("min", -2147483648), ("below", -2147483649L))
            )
        );
        Assert.Equal(
            ["--esa", "tags", "a,b c,d"],
            DeviceLaunchArguments.LaunchArgumentList(
                MobilePlatform.Android,
                Declared.Object(("tags", new[] { "a", "b c", "d" }))
            )
        );
    }
}
