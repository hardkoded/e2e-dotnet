// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AppLinkBase;

/// <summary>Upstream's <c>describe('joinLink')</c> (<c>tests/unit/app-link-base.test.ts</c>).</summary>
public sealed class JoinLinkTests
{
    [Fact]
    public void Joins_a_path_to_a_base_with_one_slash_and_none_after_a_bare_scheme()
    {
        Assert.Equal("https://shop.example.com/orders/42", TargetApps.JoinLink("https://shop.example.com", "/orders/42"));
        Assert.Equal("https://shop.example.com/app/orders/42", TargetApps.JoinLink("https://shop.example.com/app/", "orders/42"));
        Assert.Equal("shopapp://orders/42", TargetApps.JoinLink("shopapp://", "/orders/42"));
        Assert.Equal("shopapp://shop/orders/42", TargetApps.JoinLink("shopapp://shop", "/orders/42"));
    }

    [Fact]
    public void Opens_an_absolute_link_as_given() =>
        Assert.Equal("https://shop.example.com/verify?token=1", TargetApps.JoinLink("shopapp://", "https://shop.example.com/verify?token=1"));
}
