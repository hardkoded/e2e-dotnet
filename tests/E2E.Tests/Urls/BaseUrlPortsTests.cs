// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Urls;

public sealed class BaseUrlPortsTests
{
    [Fact]
    public void Tells_a_free_port_request_from_a_fixed_or_default_port()
    {
        var requested = E2E.Internal.Urls.NormalizeBaseUrl("http://[::1]:0/app/");
        Assert.True(E2E.Internal.Urls.RequestsFreePort(requested));
        Assert.Equal(0, E2E.Internal.Urls.PortOf(requested));
        Assert.False(E2E.Internal.Urls.RequestsFreePort(E2E.Internal.Urls.NormalizeBaseUrl("https://app.test")));
        Assert.Equal(443, E2E.Internal.Urls.PortOf(E2E.Internal.Urls.NormalizeBaseUrl("https://app.test")));
        Assert.Equal(3000, E2E.Internal.Urls.PortOf(E2E.Internal.Urls.NormalizeBaseUrl("http://localhost:3000")));
    }
}
