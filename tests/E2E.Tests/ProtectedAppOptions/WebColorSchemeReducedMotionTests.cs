// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

public sealed class WebColorSchemeReducedMotionTests
{
    [Fact]
    public void Accepts_every_value_the_browser_emulates_on_a_persistent_context_too()
    {
        foreach (var colorScheme in Enum.GetValues<WebColorScheme>())
        {
            _ = new WebEngine(new WebEngineOptions { ColorScheme = colorScheme });
        }

        foreach (var reducedMotion in Enum.GetValues<WebReducedMotion>())
        {
            _ = new WebEngine(new WebEngineOptions { ReducedMotion = reducedMotion });
        }

        var connect = new WebConnectOptions
        {
            CdpEndpoint = _ => Task.FromResult("ws://127.0.0.1:9222"),
            ReconnectEndpoint = _ => Task.FromResult("ws://127.0.0.1:9222"),
        };
        _ = new WebEngine(new WebEngineOptions { Connect = connect, ColorScheme = WebColorScheme.Dark, ReducedMotion = WebReducedMotion.Reduce });
    }

    /// <summary>Partial: a .NET enum takes only its members, so a value outside them is a cast number, where upstream's is any string or other type.</summary>
    [Fact]
    public void Rejects_a_value_outside_the_ones_it_takes_as_INVALID_CONFIG_listing_the_ones_it_takes()
    {
        var scheme = Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions { ColorScheme = (WebColorScheme)42 }));
        Assert.Equal("INVALID_CONFIG", scheme.Code);
        Assert.Equal("web({ ColorScheme }) must be Light, Dark, or NoPreference, got 42", scheme.Message);
        var motion = Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions { ReducedMotion = (WebReducedMotion)(-1) }));
        Assert.Equal("INVALID_CONFIG", motion.Code);
        Assert.Equal("web({ ReducedMotion }) must be Reduce or NoPreference, got -1", motion.Message);
    }
}
