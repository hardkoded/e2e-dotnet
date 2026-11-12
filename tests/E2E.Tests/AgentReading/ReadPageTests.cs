// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentReading.ReadingKit;

namespace E2E.Tests.AgentReading;

/// <summary>Upstream's <c>describe('readPage')</c> (<c>agent-reading.test.ts</c>).</summary>
public sealed class ReadPageTests
{
    [Fact]
    public void Changes_when_a_row_keeps_its_name_and_its_place_but_its_text_moves_on()
    {
        var rect = new BoundingBox(0, 100, 300, 40);
        var before = IndexOf(Node("r", role: "listitem", name: "Product", text: "Product 1", rect: rect));
        var after = IndexOf(Node("r", role: "listitem", name: "Product", text: "Product 2", rect: rect));
        var same = IndexOf(Node("r", role: "listitem", name: "Product", text: "Product 1", rect: rect));
        Assert.True(Reading.PageMoved(Reading.ReadPage(before), Reading.ReadPage(after)));
        Assert.False(Reading.PageMoved(Reading.ReadPage(before), Reading.ReadPage(same)));
        Assert.Empty(Reading.ReadPage(IndexOf(Node("box", role: "group", rect: rect))).Lines);
    }
}
