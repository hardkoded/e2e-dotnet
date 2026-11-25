// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.AgentDerived;

/// <summary>
/// Upstream's <c>describe('derivedJoinReason')</c> of <c>tests/unit/agent-derived.test.ts</c>: the rule that makes values typed in a row
/// into several fields this run's data once joined. Each upstream <c>it</c> is one test, with its assertions in order.
/// </summary>
public sealed class DerivedJoinReasonTests
{
    private static readonly string[] Shown = ["Your code is 482913"];

    [Fact]
    public void Names_the_token_rule_for_values_typed_in_a_row_that_join_into_a_code_the_screen_showed_as_a_word()
    {
        Assert.Equal(DerivedReason.MintedToken, Derived.DerivedJoinReason("482913", "enter the code", null, Shown));
        Assert.Equal(
            DerivedReason.WholeNode,
            Derived.DerivedJoinReason("482913", "enter the code", null, ["Code", "482913"])
        );
    }

    [Fact]
    public void Replays_a_joined_value_the_instruction_or_the_params_spell_out()
    {
        Assert.Null(
            Derived.DerivedJoinReason("482913", "enter the code", new JsonObject { ["code"] = "482913" }, Shown)
        );
        Assert.Null(Derived.DerivedJoinReason("482913", "enter the code 482913", null, Shown));
    }

    [Fact]
    public void Keeps_values_that_join_into_no_shown_text_or_into_part_of_a_longer_word()
    {
        Assert.Null(Derived.DerivedJoinReason("JaneDoe", "fill in the name", null, ["Welcome"]));
        Assert.Null(Derived.DerivedJoinReason("82913", "enter the code", null, Shown));
        Assert.Null(Derived.DerivedJoinReason("", "enter the code", null, Shown));
    }

    [Fact]
    public void Applies_none_of_the_date_rule_to_a_join_each_piece_was_already_judged_on_its_own()
    {
        Assert.Null(Derived.DerivedJoinReason("09:30", "book the morning slot", null, []));
    }
}
