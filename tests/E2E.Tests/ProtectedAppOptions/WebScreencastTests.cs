// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

public sealed class WebScreencastTests
{
    /// <summary>Partial: the options are typed, so a value that is not a plain object, a fractional size, and an unknown key cannot be written; the size and the quality are checked.</summary>
    [Fact]
    public void Rejects_a_fractional_or_empty_size_a_quality_outside_0_100_and_an_unknown_key_as_INVALID_CONFIG_PARTIAL_a_typed_option_has_no_fractional_size_or_unknown_key()
    {
        var invalid = new WebScreencastOptions[]
        {
            new() { Size = new WebViewport(0, 720) },
            new() { Size = new WebViewport(1280, -1) },
            new() { Quality = 101 },
            new() { Quality = -1 },
        };
        foreach (var screencast in invalid)
        {
            Assert.Equal("INVALID_CONFIG", Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions { Screencast = screencast })).Code);
        }

        _ = new WebEngine(new WebEngineOptions { Screencast = new WebScreencastOptions { Size = new WebViewport(1280, 720), Quality = 100 } });
        _ = new WebEngine(new WebEngineOptions { Screencast = new WebScreencastOptions { Quality = 0 } });
    }
}
