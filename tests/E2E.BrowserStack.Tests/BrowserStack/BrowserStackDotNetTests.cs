// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.BrowserStack.Tests.BrowserStack;

/// <summary>.NET-only tests. Upstream has no <c>it</c> for these: a plain <c>http</c> build (a check in upstream's <c>browserstack()</c>) and the credentials type, which is a record.</summary>
public sealed class BrowserStackDotNetTests
{
    [Fact]
    public void Refuses_a_plain_http_build_with_INVALID_CONFIG()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            new BrowserStackDevices(new BrowserStackDevicesOptions { Device = "Google Pixel 9", OsVersion = "16.0", App = "http://example.com/app.apk" }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("BrowserStackDevices: `App` must be an https URL, a `bs://` id, or a local path, not a plain http URL", error.Message);
    }

    [Fact]
    public void Credentials_never_print_the_access_key()
    {
        var credentials = new BrowserStackCredentials("ada", "bs-secret-key");

        Assert.DoesNotContain("bs-secret-key", credentials.ToString(), StringComparison.Ordinal);
        Assert.Contains("ada", credentials.ToString(), StringComparison.Ordinal);
    }
}
