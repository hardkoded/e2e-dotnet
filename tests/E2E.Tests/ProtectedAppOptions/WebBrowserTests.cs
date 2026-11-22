// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

public sealed class WebBrowserTests
{
    /// <summary>Partial: the option is a typed enum, so a string name cannot be written; a value the enum does not define stands for it, and the whole message is checked.</summary>
    [Fact]
    public void Rejects_a_name_the_engine_does_not_launch_which_would_run_chromium_under_it_PARTIAL_a_typed_option_has_no_string_name()
    {
        var error = Assert.Throws<EngineException>(() =>
            new WebEngine(new WebEngineOptions { BrowserName = (BrowserName)99 })
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(
            "web({ Browser }) must be one of chromium, firefox, webkit, or a browser provider, got 99",
            error.Message
        );
    }

    [Fact]
    public void Takes_the_three_browser_names()
    {
        foreach (var browser in new[] { BrowserName.Chromium, BrowserName.Firefox, BrowserName.Webkit })
        {
            _ = new WebEngine(new WebEngineOptions { BrowserName = browser });
        }
    }
}
