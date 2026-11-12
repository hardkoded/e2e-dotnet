// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using InternalUrls = E2E.Internal.Urls;

namespace E2E.Tests.Urls;

/// <summary>Upstream's <c>describe('siteOf and sameSite')</c> (<c>tests/unit/urls.test.ts</c>).</summary>
public sealed class SiteOfAndSameSiteTests
{
    [Fact]
    public void Reads_the_registrable_domain_without_a_public_suffix_list()
    {
        Assert.Equal("tester.army", InternalUrls.SiteOf("tester.army"));
        Assert.Equal("tester.army", InternalUrls.SiteOf("auth.tester.army"));
        Assert.Equal("example.com", InternalUrls.SiteOf("a.b.app.example.com"));
        Assert.Equal("example.co.uk", InternalUrls.SiteOf("shop.example.co.uk"));
        Assert.Equal("example.co.uk", InternalUrls.SiteOf("example.co.uk"));
        Assert.Equal("example.com", InternalUrls.SiteOf("Example.COM"));
    }

    [Fact]
    public void Treats_loopback_names_and_IP_literals_as_sites_of_their_own()
    {
        Assert.Equal("localhost", InternalUrls.SiteOf("localhost"));
        Assert.Equal("app.localhost", InternalUrls.SiteOf("app.localhost"));
        Assert.Equal("127.0.0.1", InternalUrls.SiteOf("127.0.0.1"));
        Assert.Equal("[::1]", InternalUrls.SiteOf("[::1]"));
    }

    [Fact]
    public void Compares_a_URL_to_a_site_by_host_alone_and_puts_an_unparseable_URL_off_every_site()
    {
        Assert.True(InternalUrls.SameSite("https://auth.tester.army/login", "tester.army"));
        Assert.True(InternalUrls.SameSite("http://127.0.0.1:4000/api", "127.0.0.1"));
        Assert.False(InternalUrls.SameSite("http://localhost:4000/", "127.0.0.1"));
        Assert.False(InternalUrls.SameSite("https://evil.test/", "tester.army"));
        Assert.False(InternalUrls.SameSite("null", "tester.army"));
    }
}
