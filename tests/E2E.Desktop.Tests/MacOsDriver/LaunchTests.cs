// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/launch.rs</c>.</summary>
public sealed class LaunchTests
{
    [Fact]
    public void Spells_the_launch_token_as_one_argument()
    {
        Assert.Equal("-E2EDesktopLaunch=8ff2bdfb-142f", MacOs.Launch.TokenArgument("8ff2bdfb-142f"));
    }

    [Fact]
    public void Tells_which_signatures_hide_the_environment()
    {
        // Calculator, Numbers (an Apple App Store app), a `restrict` build, and a third-party Developer ID app.
        Assert.True(MacOs.Launch.HiddenBy("Platform identifier=27\nAuthority=macOS Software Signing\n"));
        Assert.True(
            MacOs.Launch.HiddenBy(
                "CodeDirectory v=20500 size=1 flags=0x12000(library-validation,runtime) hashes=1\nAuthority=Apple Mac OS Application Signing\nTeamIdentifier=JCRTNEU7GK\n"
            )
        );
        Assert.True(MacOs.Launch.HiddenBy("CodeDirectory v=20400 size=1 flags=0x800(restrict) hashes=1\n"));
        Assert.False(
            MacOs.Launch.HiddenBy(
                "CodeDirectory v=20500 size=1 flags=0x10000(runtime) hashes=1\nAuthority=Developer ID Application: Slack Technologies\n"
            )
        );
    }
}
