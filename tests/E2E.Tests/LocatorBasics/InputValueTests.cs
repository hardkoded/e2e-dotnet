// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.LocatorBasics.Items;

namespace E2E.Tests.LocatorBasics;

public sealed class InputValueTests
{
    [Fact]
    public async Task Reads_the_value_and_a_value_role_with_none_reported_as_empty()
    {
        var field = new SemanticNode { Ref = "name", Role = "textbox", Value = "Ada" };
        var empty = new SemanticNode { Ref = "empty", Role = "searchbox" };
        Assert.Equal("Ada", await ScreenFixture.Create(field).GetByRole("textbox").InputValueAsync());
        Assert.Equal("", await ScreenFixture.Create(empty).GetByRole("searchbox").InputValueAsync());
    }

    [Fact]
    public async Task Fails_on_a_node_with_no_value_as_Playwright_does()
    {
        var error = await FailsWith("ACTION_FAILED", () => ScreenFixture.Create(Shown).GetByRole("listitem").InputValueAsync());
        Assert.Contains("Node is not an <input>, <textarea> or <select> element", error.Message);
    }
}
