// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.LaunchArguments;

/// <summary>Ported from <c>describe('launchArgumentList')</c> in upstream's <c>packages/desktop/tests/unit/launch-arguments.test.ts</c>.</summary>
public sealed class LaunchArgumentListTests
{
    [Fact]
    public void Passes_a_list_through_as_given_and_sends_nothing_for_none()
    {
        Assert.Equal(
            ["-e2e", "YES"],
            DesktopLaunchArguments.LaunchArgumentList(new EngineAppInfo { LaunchArguments = ["-e2e", "YES"] })
        );
        Assert.Empty(DesktopLaunchArguments.LaunchArgumentList(new EngineAppInfo()));
        Assert.Empty(
            DesktopLaunchArguments.LaunchArgumentList(
                new EngineAppInfo { LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue>() }
            )
        );
    }

    [Fact]
    public void Spells_an_object_as_user_defaults_the_way_NSUserDefaults_reads_them()
    {
        var values = new Dictionary<string, LaunchArgumentValue>
        {
            ["AppleLanguages"] = new[] { "fr", "en-GB" },
            ["AppleLocale"] = "fr_FR",
            ["e2e"] = true,
            ["dark"] = false,
            ["retries"] = 3,
            ["offset"] = -1,
            ["mode"] = "(draft)",
            ["flag"] = "-x",
            ["quote"] = "say \"hi\"\\",
        };
        Assert.Equal(
            [
                "-AppleLanguages",
                "(fr, en-GB)",
                "-AppleLocale",
                "fr_FR",
                "-e2e",
                "YES",
                "-dark",
                "NO",
                "-retries",
                "3",
                "-offset",
                "\"-1\"",
                "-mode",
                "\"(draft)\"",
                "-flag",
                "\"-x\"",
                "-quote",
                "\"say \\\"hi\\\"\\\\\"",
            ],
            DesktopLaunchArguments.LaunchArgumentList(new EngineAppInfo { LaunchArgumentValues = values })
        );
    }
}
