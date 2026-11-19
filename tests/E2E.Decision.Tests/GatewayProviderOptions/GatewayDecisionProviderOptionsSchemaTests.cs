// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Decision.Sdk.Gateway;
using Options = E2E.Decision.Sdk.Gateway.GatewayProviderOptions;

namespace E2E.Decision.Tests.GatewayProviderOptions;

/// <summary>Upstream's <c>describe('gatewayDecisionProviderOptionsSchema')</c> of <c>gateway-provider-options.test.ts</c>.</summary>
public sealed class GatewayDecisionProviderOptionsSchemaTests
{
    private const string ConditionalFallback =
        """{"model":"openai/gpt-5.6-sol","when":{"question":"intent","confidenceBelow":0.6}}""";

    public static TheoryData<string> AcceptedConditions =>
        new()
        {
            """{"question":"intent","confidenceBelow":0.6}""",
            """{"question":"refunded","probabilityBetween":[0.4,0.6]}""",
            """{"any":[{"question":"intent","confidenceBelow":0.6},{"question":"refunded","probabilityBetween":[0.4,0.6]}]}""",
            """{"all":[{"question":"intent","confidenceBelow":0.6},{"question":"severity","confidenceBelow":0.7}]}""",
            """
                {"atLeast":{"count":2,"conditions":[
                  {"question":"intent","confidenceBelow":0.6},
                  {"question":"refunded","probabilityBetween":[0.4,0.6]},
                  {"question":"severity","confidenceBelow":0.7}]}}
                """,
            """{"confidenceBelow":0.6}""",
            """{"probabilityBetween":[0.4,0.6]}""",
            """{"any":[{"confidenceBelow":0.6},{"question":"intent","confidenceBelow":0.8},{"probabilityBetween":[0.4,0.6]}]}""",
        };

    // NaN and Infinity, which JavaScript lets a confidenceBelow hold, are not JSON values, so a .NET caller cannot send them.
    public static TheoryData<string> RejectedConditions =>
        new()
        {
            """{"question":"","confidenceBelow":0.5}""",
            """{"question":"intent","confidenceBelow":-0.1}""",
            """{"question":"intent","confidenceBelow":1.1}""",
            """{"question":"refunded","probabilityBetween":[0.7,0.3]}""",
            """{"question":"refunded","probabilityBetween":[0,1,2]}""",
            """{"any":[]}""",
            """{"all":[]}""",
            """{"atLeast":{"count":0,"conditions":[{"question":"intent","confidenceBelow":0.5}]}}""",
            """{"atLeast":{"count":1.5,"conditions":[{"question":"intent","confidenceBelow":0.5}]}}""",
            """{"atLeast":{"count":2,"conditions":[{"question":"intent","confidenceBelow":0.5}]}}""",
            """{"question":"intent","confidenceBelow":0.5,"probabilityBetween":[0.4,0.6]}""",
            """{"question":"intent","confidenceBelow":0.5,"any":[{"question":"severity","confidenceBelow":0.7}]}""",
            """{"question":"intent","confidenceBelow":0.5,"extra":true}""",
            "{}",
            """{"question":"intent"}""",
            """{"confidenceBelow":0.5,"probabilityBetween":[0.4,0.6]}""",
            """{"confidenceBelow":0.5,"extra":true}""",
        };

    public static TheoryData<string> RejectedModelLists =>
        new()
        {
            """[{"model":"","when":{"question":"intent","confidenceBelow":0.6}}]""",
            "[" + ConditionalFallback + "," + ConditionalFallback + "]",
            "[\"anthropic/claude-sonnet-5\"," + ConditionalFallback + "]",
        };

    public static TheoryData<string> ListedConditions => new() { "any", "all", "atLeast" };

    [Theory]
    [MemberData(nameof(AcceptedConditions))]
    public void Accepts_condition(string when) => Assert.Null(Validate(WithWhen(when)));

    [Fact]
    public void Keeps_existing_string_fallback_lists_and_service_owned_options_valid() =>
        Assert.Null(
            Validate("""{"models":["openai/gpt-5.6-sol","anthropic/claude-sonnet-5"],"serviceOwnedOption":true}""")
        );

    [Fact]
    public void Accepts_one_conditional_entry_followed_by_string_error_fallbacks() =>
        Assert.Null(Validate("{\"models\":[" + ConditionalFallback + ",\"anthropic/claude-sonnet-5\"]}"));

    [Theory]
    [MemberData(nameof(RejectedConditions))]
    public void Rejects_condition(string when) => Assert.NotNull(Validate(WithWhen(when)));

    [Fact]
    public void Enforces_the_maximum_condition_depth()
    {
        var condition = """{"question":"intent","confidenceBelow":0.5}""";
        for (var depth = 1; depth < Options.DecisionFallbackMaxConditionDepth; depth++)
        {
            condition = """{"any":[""" + condition + "]}";
        }

        Assert.Null(Validate(WithWhen(condition)));
        Assert.NotNull(Validate(WithWhen("{\"any\":[" + condition + "]}")));
    }

    [Fact]
    public void Accepts_five_condition_levels_and_rejects_six()
    {
        const string direct = """{"question":"intent","confidenceBelow":0.5}""";
        var fiveLevels = """{"any":[{"all":[{"atLeast":{"count":1,"conditions":[{"any":[""" + direct + "]}]}}]}]}";
        var sixLevels = """{"any":[""" + fiveLevels + "]}";

        Assert.Null(Validate(WithWhen(fiveLevels)));
        var invalid = Validate(WithWhen(sixLevels));
        Assert.Contains(
            "conditions can be nested at most "
                + Options.DecisionFallbackMaxConditionDepth.ToString(CultureInfo.InvariantCulture)
                + " levels deep",
            invalid,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [MemberData(nameof(RejectedModelLists))]
    public void Rejects_invalid_models_list(string models) => Assert.NotNull(Validate("""{"models":""" + models + "}"));

    [Fact]
    public void Applies_the_shared_condition_bounds()
    {
        Assert.Equal(5, Options.DecisionFallbackMaxConditionDepth);
        Assert.Equal(20, Options.DecisionFallbackMaxConditionsPerList);
        Assert.Equal(256, Options.DecisionFallbackMaxQuestionLength);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Bounds_question_length_at_length(int length, bool success) =>
        Assert.Equal(
            success,
            Validate(
                "{\"models\":[{\"model\":\"openai/gpt-5.6-sol\",\"when\":{\"question\":"
                    + JsonSerializer.Serialize(new string('q', length))
                    + ",\"confidenceBelow\":0.6}}]}"
            )
                is null
        );

    [Theory]
    [InlineData(1_000, true)]
    [InlineData(0, false)]
    public void Accepts_any_non_empty_conditional_model_length(int length, bool success) =>
        Assert.Equal(
            success,
            Validate(
                "{\"models\":[{\"model\":"
                    + JsonSerializer.Serialize(new string('m', length))
                    + ",\"when\":{\"question\":\"intent\",\"confidenceBelow\":0.6}}]}"
            )
                is null
        );

    [Theory]
    [MemberData(nameof(ListedConditions))]
    public void Bounds_name_condition_lists(string name)
    {
        string Validate(int count)
        {
            var conditions =
                "["
                + string.Join(
                    ",",
                    Enumerable
                        .Range(0, count)
                        .Select(index => "{\"question\":\"question" + index + "\",\"confidenceBelow\":0.6}")
                )
                + "]";
            var when =
                name == "atLeast"
                    ? """{"atLeast":{"count":1,"conditions":""" + conditions + "}}"
                    : "{\"" + name + "\":" + conditions + "}";
            return WithWhen(when);
        }

        Assert.Null(
            Options.ValidateDecisionProviderOptions(
                JsonDocument.Parse(Validate(Options.DecisionFallbackMaxConditionsPerList)).RootElement
            )
        );
        Assert.NotNull(
            Options.ValidateDecisionProviderOptions(
                JsonDocument.Parse(Validate(Options.DecisionFallbackMaxConditionsPerList + 1)).RootElement
            )
        );
    }

    private static string WithWhen(string when) =>
        "{\"models\":[{\"model\":\"openai/gpt-5.6-sol\",\"when\":" + when + "}]}";

    private static string? Validate(string options) =>
        Options.ValidateDecisionProviderOptions(JsonDocument.Parse(options).RootElement);
}
