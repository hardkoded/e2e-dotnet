// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ProtectedAppOptions;

public sealed class WebViewportTests
{
    /// <summary>Partial: the size is a typed record of ints, so a non-object, a missing side, a string, NaN, and a fraction cannot be written; a side below 1 is checked.</summary>
    [Fact]
    public void Rejects_a_viewport_that_is_not_a_size_of_whole_pixels_each_side_at_least_1_PARTIAL_a_typed_size_has_only_integer_sides()
    {
        var invalid = new WebViewport[] { new(10, -5), new(0, 0), new(100, 0) };
        foreach (var viewport in invalid)
        {
            var error = Assert.Throws<EngineException>(() =>
                new WebEngine(new WebEngineOptions { Viewport = viewport })
            );
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Equal("web({ Viewport }) must be { Width, Height } in whole pixels, each at least 1", error.Message);
        }
    }

    [Fact]
    public void Takes_a_size_and_null_for_the_browser_window()
    {
        foreach (var viewport in new WebViewport?[] { new(390, 844), new(1, 1), null })
        {
            _ = new WebEngine(new WebEngineOptions { Viewport = viewport });
        }
    }
}
