// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Ids;

/// <summary>
/// Upstream's <c>describe('test IDs')</c> (<c>tests/unit/ids.test.ts</c>). Not ported: the pin of <c>resultId</c> (the port's
/// digest is its own, so the bytes differ).
/// </summary>
public sealed class TestIdsTests
{
    /// <summary>Partial: a .NET test has one title, not a title path, so the id is the file and one encoded title.</summary>
    [Fact]
    public void Pins_the_id_of_a_non_ASCII_title()
    {
        Assert.Equal("tests/koszyk.e2e.ts::p%C5%82aci%20kart%C4%85", AttemptReport.TestId("tests/koszyk.e2e.ts", "płaci kartą"));
        Assert.Equal("tests/koszyk.e2e.ts::caf%C3%A9", AttemptReport.TestId("tests/koszyk.e2e.ts", "café"));
    }
}
