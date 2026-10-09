// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Urls;

public sealed class IsLoopbackHostTests
{
    [Fact]
    public void Accepts_localhost_star_localhost_127_0_0_0_8_and_1()
    {
        Assert.True(E2E.Internal.Urls.IsLoopbackHost("localhost"));
        Assert.True(E2E.Internal.Urls.IsLoopbackHost("app.localhost"));
        Assert.True(E2E.Internal.Urls.IsLoopbackHost("127.0.0.1"));
        Assert.True(E2E.Internal.Urls.IsLoopbackHost("127.1.2.3"));
        Assert.False(E2E.Internal.Urls.IsLoopbackHost("example.test"));
    }
}
