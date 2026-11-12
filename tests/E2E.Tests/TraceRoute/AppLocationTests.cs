// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;

namespace E2E.Tests.TraceRoute;

/// <summary>Upstream's <c>appLocation</c> describe (<c>tests/unit/trace-route.test.ts</c>).</summary>
public sealed class AppLocationTests
{
    [Fact]
    public void Keeps_a_location_on_the_app_origin_as_its_path_so_another_deployment_of_the_app_reads_alike()
    {
        Assert.Equal("/shop?x=1#top", CacheRoute.AppLocation("https://pr-12.preview.test/shop?x=1#top", "https://pr-12.preview.test"));
        Assert.Equal("https://other.test/shop", CacheRoute.AppLocation("https://other.test/shop", "https://pr-12.preview.test"));
        Assert.Equal("https://other.test/shop", CacheRoute.AppLocation("https://other.test/shop", null));
        Assert.Equal("Settings", CacheRoute.AppLocation("Settings", "https://pr-12.preview.test"));
    }
}
