// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Install;

/// <summary><c>describe('browserCheck')</c> of upstream <c>install.test.ts</c>; the message names <c>e2e install</c>, the port's installer.</summary>
public sealed class BrowserCheckTests
{
    private static readonly Dictionary<string, string?> NoEnv = [];

    [Fact]
    public void Passes_an_installed_browser_warns_of_a_first_run_download_and_reports_nothing_it_cannot_tell()
    {
        Assert.Equal([new EngineDoctorCheck("browser", EngineDoctorStatus.Pass, "chromium is installed")], WebEngine.BrowserCheck(BrowserName.Chromium, NoEnv, false, _ => true));
        Assert.Equal(
            [new EngineDoctorCheck("browser", EngineDoctorStatus.Warn, "firefox is not installed; the first run downloads it, or run \"e2e install firefox\" now")],
            WebEngine.BrowserCheck(BrowserName.Firefox, NoEnv, false, _ => false));
        Assert.Empty(WebEngine.BrowserCheck(BrowserName.Chromium, NoEnv, false, _ => null));
    }
}
