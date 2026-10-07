// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Install;

public sealed class EnsureBrowsersInstalledTests
{
    [Fact]
    public void Does_not_collect_the_browsers_the_declared_cache_holds()
    {
        // Without it, the install removes every revision no installed Playwright
        // declares. Those revisions belong to the other tools sharing that cache.
        Assert.Equal("1", WebEngine.InstallSkipBrowserGc(null));
    }

    [Fact]
    public void Keeps_a_browser_collection_the_caller_asked_for()
    {
        Assert.Equal("0", WebEngine.InstallSkipBrowserGc("0"));
    }

    [Fact]
    public void Asks_for_the_shell_only_on_a_headless_chromium_run()
    {
        // A headless chromium run launches the shell, so `--only-shell` keeps the
        // full build out of its download. A headed run launches the full build.
        Assert.Equal(["--only-shell", "chromium"], WebEngine.InstallArgs(headed: false));
        Assert.Equal(["chromium"], WebEngine.InstallArgs(headed: true));
    }
}
