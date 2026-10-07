// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.LocatorBasics;

public sealed class GetByRoleWithANameTests
{
    private readonly Screen _screen = ScreenFixture.Create(
        new SemanticNode { Ref = "save", Role = "button", Name = "Save draft" },
        new SemanticNode { Ref = "cancel", Role = "button", Name = "Cancel" });

    [Fact]
    public async Task Takes_the_accessible_name_as_its_second_argument_the_same_query_as_name()
    {
        Assert.Equal(1, await _screen.GetByRole("button", "Cancel").CountAsync());
        Assert.Equal(1, await _screen.GetByRole("button", new Regex("^save", RegexOptions.IgnoreCase)).CountAsync());
        Assert.Equal(0, await _screen.GetByRole("button", "Save").CountAsync());
        Assert.Equal(1, await _screen.GetByRole("button", "save", exact: false).CountAsync());
        Assert.Equal(
            await _screen.GetByRole("button", new RoleOptions { Name = "Cancel" }).TextContentAsync(),
            await _screen.GetByRole("button", "Cancel").TextContentAsync());
    }
}
