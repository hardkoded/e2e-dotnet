// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Ids;

/// <summary>
/// Upstream's <c>describe('test IDs')</c> (<c>tests/unit/ids.test.ts</c>). A .NET test has one title, not a title path, so every id
/// here is the file and one encoded title. Not ported: "prefixes setup IDs" (the port has no <c>setupTestId</c>; it has no setup tests).
/// </summary>
public sealed class TestIdsTests
{
    /// <summary>Partial: one title, not the title path <c>['login', 'user can sign in']</c>.</summary>
    [Fact]
    public void Partial_Joins_the_file_and_encoded_title_path_with_double_colon()
    {
        Assert.Equal("tests/auth.e2e.ts::user%20can%20sign%20in", AttemptReport.TestId("tests/auth.e2e.ts", "user can sign in"));
    }

    [Fact]
    public void Is_unambiguous_because_percent_and_colon_are_encoded()
    {
        Assert.Equal("t.ts::a%3A%3Ab", AttemptReport.TestId("t.ts", "a::b"));
    }

    /// <summary>Partial: one title, not the title path <c>['zakupy', 'płaci kartą', 'café']</c>.</summary>
    [Fact]
    public void Partial_Pins_the_id_of_a_non_ASCII_title_path_last_failed_matches_ids_from_an_earlier_report()
    {
        Assert.Equal("tests/koszyk.e2e.ts::p%C5%82aci%20kart%C4%85", AttemptReport.TestId("tests/koszyk.e2e.ts", "płaci kartą"));
        Assert.Equal("tests/koszyk.e2e.ts::caf%C3%A9", AttemptReport.TestId("tests/koszyk.e2e.ts", "café"));
    }
}
