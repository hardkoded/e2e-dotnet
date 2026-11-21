// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.AgentDerived;

/// <summary>
/// Upstream's <c>describe('derivedReason')</c> of <c>tests/unit/agent-derived.test.ts</c>: the rule that keeps a typed value this run
/// derived out of the replay cache. Each upstream <c>it</c> is one test, with its assertions in order.
/// </summary>
public sealed class DerivedReasonTests
{
    private static DerivedReason? Reason(
        string value,
        string instruction,
        JsonObject? parameters = null,
        StepEvidence? evidence = null
    ) => Derived.DerivedReason(value, instruction, parameters, evidence);

    private static StepEvidence Shown(params string[] shown) => new(shown);

    [Fact]
    public void Treats_values_spelled_out_by_the_instruction_or_the_params_as_literal()
    {
        Assert.Null(Reason("Nimbus Paper Co", "Add a supplier named \"Nimbus Paper Co\"."));
        Assert.Null(Reason("4.25", "unit price 4.25, unit Ream"));
        Assert.Null(
            Reason(
                "Helios Robotics",
                "Complete the wizard with the given details",
                new JsonObject { ["company"] = "Helios Robotics", ["seats"] = 25 }
            )
        );
        Assert.Null(Reason("25", "fill the form", new JsonObject { ["seats"] = 25 }));
        Assert.Null(Reason("  nimbus paper co ", "Add \"Nimbus Paper Co\""));
    }

    [Fact]
    public void Names_the_token_rule_for_a_reference_read_off_a_screen_the_step_saw_and_the_node_rule_when_a_node_says_exactly_that()
    {
        string[] shown = ["Your ticket", "Reference TK-4972"];
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("TK-4972", "Enter the details of the ticket you were issued earlier.", null, Shown(shown))
        );
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("TK-4972", "Enter the ticket reference.", null, new StepEvidence(new HashSet<string>(shown)))
        );
        Assert.Equal(
            DerivedReason.WholeNode,
            Reason("TK-4972", "Enter the ticket reference.", null, Shown("Reference", "TK-4972"))
        );
    }

    [Fact]
    public void Names_pixels_for_any_non_literal_value_once_the_model_was_shown_a_screenshot_which_may_carry_text_the_tree_does_not()
    {
        Assert.Equal(
            DerivedReason.Pixels,
            Reason("TK-4972", "enter the code drawn on the canvas", null, new StepEvidence(["Code"], true))
        );
        Assert.Equal(
            DerivedReason.Pixels,
            Reason("Jane Merchant", "fill in the form with plausible details", null, new StepEvidence(Pixels: true))
        );
        Assert.Null(Reason("Nimbus Paper Co", "Add \"Nimbus Paper Co\"", null, new StepEvidence(Pixels: true)));
        Assert.Null(
            Reason("Jane Merchant", "fill in the form with plausible details", null, new StepEvidence(Pixels: false))
        );
    }

    [Fact]
    public void Names_the_date_rule_for_a_date_or_time_the_model_reckoned()
    {
        Assert.Equal(
            DerivedReason.Date,
            Reason("2026-09-03", "Set the date to today", new JsonObject { ["note"] = "Q3" })
        );
        Assert.Equal(DerivedReason.Date, Reason("12/31/2026", "pick the last day"));
        Assert.Equal(DerivedReason.Date, Reason("19-09-2026", "pick tomorrow"));
        Assert.Equal(DerivedReason.Date, Reason("09:30", "book the morning slot"));
        Assert.Equal(DerivedReason.Date, Reason("9:30am", "book the morning slot"));
        Assert.Equal(DerivedReason.Date, Reason("September 19, 2026", "schedule this for tomorrow"));
        Assert.Equal(DerivedReason.Date, Reason("Sept. 19", "schedule this for tomorrow"));
        Assert.Equal(DerivedReason.Date, Reason("19 Sep 2026", "schedule this for tomorrow"));
        Assert.Null(Reason("3rd of March", "pick a date"));
        Assert.Null(Reason("Jane March", "fill in the form"));
        Assert.Null(Reason("Mayfair 12", "fill in the address"));
    }

    [Fact]
    public void Treats_a_value_the_model_composed_itself_as_literal_so_the_flow_replays()
    {
        Assert.Null(Reason("Jane Merchant", "fill in the signup form with plausible details"));
        Assert.Null(Reason("jane@example.com", "fill in the signup form", null, Shown("Step 1 of 3")));
    }

    [Fact]
    public void Reads_a_value_off_the_screen_when_a_node_says_exactly_that_never_a_word_inside_a_sentence()
    {
        Assert.Equal(DerivedReason.WholeNode, Reason("Jane", "fill in the form", null, Shown("Jane", "Sign out")));
        Assert.Equal(DerivedReason.WholeNode, Reason("Jane", "fill in the form", null, Shown("  jane ")));
        // Decided: a name inside a greeting is not the screen showing the value, so the fill replays. The cost is a false negative
        // when the model did read it there: the replay types last run's name and self-finalizes, backstopped only when the value
        // resurfaces in the end anchors. Taken so that the words a model composes keep replaying, as below.
        Assert.Null(Reason("Jane", "fill in the form", null, Shown("Welcome back, Jane")));
        // The regression: a page listing "Row one" and "Row two" does not make the words the model typed into its fields the
        // screen's data.
        var rows = Shown("Row one", "Row two", "Filled");
        Assert.Null(Reason("one", "fill the two fields", null, rows));
        Assert.Null(Reason("two", "fill the two fields", null, rows));
    }

    [Fact]
    public void Matches_a_data_shaped_value_as_a_whole_token_of_shown_text_never_inside_a_longer_one()
    {
        Assert.Null(Reason("123", "pay with any CVC", null, Shown("CVC", "Pay 10,99 US$", "n20123")));
        Assert.Null(Reason("Jane", "fill the form", null, Shown("Janet Leigh")));
        Assert.Equal(DerivedReason.MintedToken, Reason("123", "enter the code", null, Shown("Your code is 123")));
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("TK-4972", "enter the reference", null, Shown("Reference: TK-4972."))
        );
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("A1B2", "enter the code", null, Shown("Code A1B2 expires soon"))
        );
    }

    [Fact]
    public void Flags_a_digit_the_model_chose_beside_a_numbered_label_a_known_limit_of_the_glyph_test()
    {
        // "1" typed into the field beside "Row 1" is the model's own choice, yet the digit makes it data-shaped and the label shows
        // it as a word of its own, so the token rule cannot tell it from a code. Accepted: the cost is a gap where a replay was
        // possible, and the executor runs that part live; the opposite error would replay a minted value as the flow's data.
        Assert.Equal(DerivedReason.MintedToken, Reason("1", "fill the fields", null, Shown("Row 1", "Row 2")));
    }

    [Fact]
    public void Prefers_the_node_rule_over_the_token_rule_when_both_apply()
    {
        Assert.Equal(DerivedReason.WholeNode, Reason("123", "enter the code", null, Shown("Your code is 123", "123")));
    }

    private static StepEvidence Otp(params string[] shown) => new(shown, null, "one-time-code");

    [Fact]
    public void Names_the_token_rule_for_any_value_typed_into_a_one_time_code_field_one_digit_per_box_included()
    {
        string[] shown = ["Your code is 482913"];
        const string goal = "type the code shown on the page into the code boxes";
        // One digit per box: no screen shows a lone digit as a word of its own, so only the field's purpose tells the fill is this run's
        // code.
        foreach (var digit in "482913")
        {
            Assert.Null(Reason(digit.ToString(), goal, null, Shown(shown)));
            Assert.Equal(DerivedReason.MintedToken, Reason(digit.ToString(), goal, null, Otp(shown)));
        }

        Assert.Equal(DerivedReason.MintedToken, Reason("482913", goal, null, Otp()));
        // Any other purpose keeps the rules above.
        Assert.Null(Reason("4", goal, null, new StepEvidence(shown, null, "username")));
        Assert.Null(Reason("4", goal, null, new StepEvidence(shown, null, "none")));
    }

    [Fact]
    public void Replays_a_whole_one_time_code_the_instruction_or_the_params_spell_out()
    {
        Assert.Null(Reason("482913", "enter the code 482913", null, Otp()));
        Assert.Null(Reason("482913", "enter the code", new JsonObject { ["code"] = "482913" }, Otp()));
    }

    [Fact]
    public void Never_replays_a_lone_digit_in_a_one_time_code_box_whatever_the_instruction_or_the_params_say()
    {
        // "6-digit" contains a 6, which is no spelling of the code: a code that starts with 6 must not replay its first box as last run's.
        Assert.Equal(DerivedReason.MintedToken, Reason("6", "enter the 6-digit code shown on the page", null, Otp()));
        // Accepted cost: a fixed code passed in the params and typed one digit per box runs live instead of replaying, since a lone digit
        // cannot tell that code from one the app minted.
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("4", "enter the code", new JsonObject { ["code"] = "482913" }, Otp())
        );
    }

    [Fact]
    public void Never_replays_a_chunk_of_a_longer_number_in_a_one_time_code_field()
    {
        // A code split three and three: each half sits inside a longer param or word, which spells neither the code nor the half.
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("482", "enter the code", new JsonObject { ["phone"] = "4825551234" }, Otp())
        );
        Assert.Equal(
            DerivedReason.MintedToken,
            Reason("482", "enter the code", new JsonObject { ["code"] = "482913" }, Otp())
        );
        Assert.Equal(DerivedReason.MintedToken, Reason("482", "enter the code 482913", null, Otp()));
        // A half the step spells out on its own still replays.
        Assert.Null(Reason("482", "enter 482 then 913", null, Otp()));
        Assert.Null(Reason("482", "enter the code", new JsonObject { ["first"] = "482" }, Otp()));
    }

    [Fact]
    public void Never_treats_an_empty_value_as_derived()
    {
        Assert.Null(Reason("", "anything"));
        Assert.Null(Reason("   ", "anything"));
    }
}
