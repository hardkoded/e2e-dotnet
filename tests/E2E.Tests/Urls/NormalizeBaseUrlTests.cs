// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Urls;

public sealed class NormalizeBaseUrlTests
{
    private static E2E.Internal.NormalizedBaseUrl Normalize(string raw) => E2E.Internal.Urls.NormalizeBaseUrl(raw);

    private static void Rejects(string raw, string message)
    {
        var error = Assert.Throws<ConfigurationException>(() => Normalize(raw));
        Assert.Equal("INVALID_APP_URL", error.Code);
        Assert.Matches(message, error.Message);
    }

    [Fact]
    public void Normalizes_default_ports_dot_segments_and_IDNA_hosts()
    {
        Assert.Equal("https://example.test/b", Normalize("https://EXAMPLE.test:443/a/../b").Href);
        Assert.Equal("http://localhost:3000", Normalize("http://localhost:3000").Origin);
    }

    [Fact]
    public void Rejects_userinfo_query_and_fragment()
    {
        Rejects("https://user:pw@example.test", "userinfo");
        Rejects("https://example.test/?q=1", "query");
        Rejects("https://example.test/#frag", "fragment");
    }

    [Fact]
    public void Rejects_plain_HTTP_for_non_loopback_hosts()
    {
        Rejects("http://example.test", "loopback");
        Assert.Equal("http://127.0.0.1:8080", Normalize("http://127.0.0.1:8080").Origin);
    }

    [Fact]
    public void Accepts_port_0_on_a_literal_loopback_address_only_where_the_run_picks_a_free_port()
    {
        Assert.Equal("http://127.0.0.1:0", Normalize("http://127.0.0.1:0").Origin);
        Assert.Equal("http://[::1]:0/app", Normalize("http://[::1]:0/app").Href);

        // A name may resolve to another address than the one the command binds.
        Rejects("localhost:0/app", @"port 0 .* 127\.0\.0\.1 or \[::1\]");
        Rejects("https://app.test:0", @"port 0 .* 127\.0\.0\.1 or \[::1\]");
    }

    [Fact]
    public void Rejects_non_http_s_schemes()
    {
        Rejects("file:///tmp/app", "");
        Rejects("javascript:alert(1)", @"must be http\(s\)");

        // A single-slash scheme is still a scheme, never a host named after it.
        Rejects("file:/tmp/app", @"must be http\(s\)");
        Rejects("ftp:/server/path", @"must be http\(s\)");
        Rejects("data:text/html,hi", @"must be http\(s\)");
    }

    [Fact]
    public void Infers_https_for_a_schemeless_host_and_http_for_a_schemeless_loopback_host()
    {
        Assert.Equal("https://tester.army/", Normalize("tester.army").Href);
        Assert.Equal("https://www.tester.army/app", Normalize("www.tester.army/app").Href);
        Assert.Equal("http://localhost:3000", Normalize("localhost:3000").Origin);
        Assert.Equal("http://localhost:3000/app", Normalize("localhost:3000/app").Href);
        Assert.Equal("https://app.test:8443", Normalize("app.test:8443").Origin);
        Assert.Equal("http://localhost:3000", Normalize("http:/localhost:3000").Origin);
        Assert.Equal("/base/", Normalize("127.0.0.1:8080/base/").BasePath);
        Assert.Equal("http://[::1]:4000", Normalize("[::1]:4000").Origin);
        Rejects("tester.army?q=1", "query");
    }
}
