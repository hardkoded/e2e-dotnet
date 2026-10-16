// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace E2E.Tests.ExplorePlan;

internal static class PlanAnswers
{
    /// <summary>Answers with every field present, as strict providers require. Unused ones are empty.</summary>
    public static string Step(string title = "", string instruction = "", string summary = "") => Answer("step", title, instruction, summary);

    public static string Finish(string title = "", string instruction = "", string summary = "") => Answer("finish", title, instruction, summary);

    public static PlanAnswer Parsed(string json) => PlanSchema.SafeParse(json).Value!;

    private static string Answer(string decision, string title, string instruction, string summary) =>
        JsonSerializer.Serialize(new { decision, title, instruction, summary });
}

public sealed class PlanSchemaTests
{
    [Fact]
    public void Accepts_a_step_with_a_title_and_an_instruction_and_a_finish_with_a_summary()
    {
        Assert.True(PlanSchema.SafeParse(PlanAnswers.Step(title: "Cart", instruction: "Add two items and open the cart")).Success);
        Assert.True(PlanSchema.SafeParse(PlanAnswers.Finish(summary: "Checkout works; the cart total is wrong.")).Success);
    }

    [Fact]
    public void Requires_every_field_to_be_present_as_strict_providers_demand()
    {
        Assert.False(PlanSchema.SafeParse("""{ "decision": "step", "title": "Cart", "instruction": "open" }""").Success);

        // The schema the model is sent lists every property as required.
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            },
            typeof(PlanAnswer),
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        var properties = ((JsonObject)schema["properties"]!).Select(pair => pair.Key).Order(StringComparer.Ordinal);
        var required = ((JsonArray)schema["required"]!).Select(node => node!.GetValue<string>()).Order(StringComparer.Ordinal);
        Assert.Equal(properties, required);
        Assert.Equal(["decision", "instruction", "summary", "title"], required);
    }

    [Fact]
    public void Accepts_a_step_with_only_one_of_title_and_instruction_filled_and_names_an_empty_one()
    {
        Assert.True(PlanSchema.SafeParse(PlanAnswers.Step(title: "Open the cart and check the total against the line prices")).Success);
        Assert.True(PlanSchema.SafeParse(PlanAnswers.Step(instruction: "Open the cart")).Success);
        var empty = PlanSchema.SafeParse(PlanAnswers.Step(title: " "));
        Assert.False(empty.Success);
        Assert.Equal(["instruction"], empty.Issues.Select(issue => issue.Path));
        var noSummary = PlanSchema.SafeParse(PlanAnswers.Finish());
        Assert.False(noSummary.Success);
        Assert.Equal(["summary"], noSummary.Issues.Select(issue => issue.Path));
    }
}
