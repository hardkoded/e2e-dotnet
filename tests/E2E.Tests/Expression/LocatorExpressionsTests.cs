// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Expression;

public sealed class LocatorExpressionsTests
{
    [Fact]
    public void Rewrites_the_img_alias_to_the_image_role_before_the_expression_is_built()
    {
        var screen = ScreenFixture.Create();
        Assert.Equal(screen.GetByRole("image", "Logo").Query.Describe(), screen.GetByRole("img", "Logo").Query.Describe());
        Assert.Equal("image", screen.GetByRole("img").Query.Role);
        Assert.Equal(screen.GetByRole("image").Query.Describe(), screen.GetByRole("img").Query.Describe());
    }
}
