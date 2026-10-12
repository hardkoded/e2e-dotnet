// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

public sealed class AgentExtractTests
{
    [Fact]
    public async Task Sends_the_caller_schema_as_a_shape_types_and_required_fields_no_value_constraints()
    {
        var model = new ScriptedModel(_ => ModelResponses.Call("extract", new { data = new { count = 3 } }));
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Paragraph("Buy milk");
            page.Paragraph("Walk the dog");
            page.Paragraph("Ship it");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://todos.test",
            TestTitle = "extract",
            CacheEnabled = false,
        });
        await session.App.OpenAsync("/");

        var value = await session.Agent.ExtractAsync<TodoCount>("the number of todos");

        Assert.Equal(3, value.Count);
        var sent = model.Requests[0].Tools.Single(tool => tool.Name == "extract").Parameters.GetRawText();
        Assert.Contains("\"count\":{\"type\":\"integer\"}", sent, StringComparison.Ordinal);
        Assert.Contains("\"label\":{\"type\":\"string\"}", sent, StringComparison.Ordinal);
        foreach (var constraint in new[] { "minimum", "maximum", "minLength", "pattern", "$schema" })
        {
            Assert.DoesNotContain(constraint, sent, StringComparison.Ordinal);
        }
    }

    private sealed class TodoCount
    {
        public required int Count { get; init; }

        public string Label { get; init; } = "";
    }
}
