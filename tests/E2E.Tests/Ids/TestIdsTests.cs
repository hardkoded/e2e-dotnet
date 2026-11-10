// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Ids;

/// <summary>Upstream's <c>describe('test IDs')</c> (<c>tests/unit/ids.test.ts</c>).</summary>
public sealed class TestIdsTests
{
    [Fact]
    public void Joins_the_file_and_encoded_title_path_with_double_colon()
    {
        Assert.Equal("tests/auth.e2e.ts::login::user%20can%20sign%20in", E2E.Internal.Ids.TestId("tests/auth.e2e.ts", ["login", "user can sign in"]));
    }

    [Fact]
    public void Is_unambiguous_because_percent_and_colon_are_encoded()
    {
        Assert.Equal("t.ts::a%3A%3Ab", E2E.Internal.Ids.TestId("t.ts", ["a::b"]));
    }

    [Fact]
    public void Pins_the_id_of_a_non_ASCII_title_path_last_failed_matches_ids_from_an_earlier_report()
    {
        Assert.Equal(
            "tests/koszyk.e2e.ts::zakupy::p%C5%82aci%20kart%C4%85::caf%C3%A9",
            E2E.Internal.Ids.TestId("tests/koszyk.e2e.ts", ["zakupy", "płaci kartą", "café"]));
    }

    [Fact]
    public void Prefixes_setup_IDs()
    {
        Assert.Equal("setup::t.ts::auth", E2E.Internal.Ids.SetupTestId("t.ts", ["auth"]));
    }
}
