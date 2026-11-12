// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IdHelpers = E2E.Internal.Ids;

namespace E2E.Tests.Ids;

/// <summary>Upstream's <c>describe('resultId')</c> (<c>tests/unit/ids.test.ts</c>).</summary>
public sealed partial class ResultIdTests
{
    [Fact]
    public void Is_SHA_256_JCS_of_testId_targetId_agent_so_a_test_run_as_two_agents_has_two_ids()
    {
        Assert.Equal(
            IdHelpers.CanonicalDigest(new JsonObject { ["testId"] = "t.ts::a", ["targetId"] = "web", ["agent"] = "default" }),
            IdHelpers.ResultId("t.ts::a", "web", "default"));
        Assert.Matches(Hex64(), IdHelpers.ResultId("t.ts::a", "web", "default"));
        Assert.NotEqual(IdHelpers.ResultId("t.ts::a", "web", "buyer"), IdHelpers.ResultId("t.ts::a", "web", "admin"));

        // The first run's id ignores the repeat dimension; later runs get their own.
        Assert.Equal(IdHelpers.ResultId("t.ts::a", "web", "default"), IdHelpers.ResultId("t.ts::a", "web", "default", 0));
        Assert.Equal(
            IdHelpers.CanonicalDigest(new JsonObject { ["testId"] = "t.ts::a", ["targetId"] = "web", ["agent"] = "default", ["repeat"] = 1 }),
            IdHelpers.ResultId("t.ts::a", "web", "default", 1));
        Assert.NotEqual(IdHelpers.ResultId("t.ts::a", "web", "default", 1), IdHelpers.ResultId("t.ts::a", "web", "default", 2));
    }

    [Fact]
    public void Pins_the_bytes_last_failed_matches_ids_from_an_earlier_report_so_a_change_selects_nothing()
    {
        Assert.Equal("196879d953095f318e0c3fb8806bf7f9a0b34b076abc4214fcb7fbfaf0fbcf56", IdHelpers.ResultId("tests/cart.e2e.ts::checkout::pays", "web", "default"));
        Assert.Equal("327c8053efd3ab636e1049f34c89f7aec511679ff874655c07e047cca174b3a7", IdHelpers.ResultId("tests/cart.e2e.ts::checkout::pays", "web", "default", 1));
    }

    [GeneratedRegex(@"^[0-9a-f]{64}\z")]
    private static partial Regex Hex64();
}
