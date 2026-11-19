// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Text;

/// <summary><c>describe('askText')</c> of <c>text.test.ts</c>.</summary>
public sealed class AskTextTests
{
    private static FieldInput Input() =>
        new()
        {
            Goal = "Fill Name",
            Context = null,
            Params = new JsonObject { ["nickname"] = "Ada" },
            Field = new FieldDescription("Name", "textbox"),
            Page = "Name",
            RecentActions = [],
        };

    [Fact]
    public async Task Returns_the_model_value_and_records_usage()
    {
        var text = ScriptedText("Ada");
        var fixture = Context(new ContextOptions { Model = text });
        var value = await global::E2E.Decision.Text.AskTextAsync(
            fixture.Ctx,
            text,
            global::E2E.Decision.Text.TextAsk,
            Input()
        );
        Assert.Equal(new TextAnswer("Ada"), value);
        var call = Assert.Single(fixture.Usage);
        Assert.Equal("scripted-text", call.Provider);
    }

    [Fact]
    public async Task Names_the_answer_key_for_providers_without_JSON_schema_output()
    {
        var text = ScriptedText("Ada");
        await global::E2E.Decision.Text.AskTextAsync(
            Context(new ContextOptions { Model = text }).Ctx,
            text,
            global::E2E.Decision.Text.TextAsk,
            Input()
        );
        Assert.Contains("exactly one key, \"text\"", text.Prompts, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Passes_null_and_empty_through_for_the_caller_to_read_as_no_value(string? queued)
    {
        var text = ScriptedText(queued);
        var fixture = Context(new ContextOptions { Model = text });
        Assert.Equal(
            new TextAnswer(queued),
            await global::E2E.Decision.Text.AskTextAsync(fixture.Ctx, text, global::E2E.Decision.Text.TextAsk, Input())
        );
    }

    [Fact]
    public async Task Maps_a_provider_failure_without_its_text()
    {
        var broken = ScriptedText("Ada");
        broken.Throws = new InvalidOperationException("upstream detail");
        var fixture = Context(new ContextOptions { Model = broken });
        var first = await Assert.ThrowsAsync<AgentException>(() =>
            global::E2E.Decision.Text.AskTextAsync(fixture.Ctx, broken, global::E2E.Decision.Text.TextAsk, Input())
        );
        Assert.Equal("MODEL_PROVIDER_FAILED", first.Code);
        var second = await Assert.ThrowsAsync<AgentException>(() =>
            global::E2E.Decision.Text.AskTextAsync(fixture.Ctx, broken, global::E2E.Decision.Text.TextAsk, Input())
        );
        Assert.DoesNotContain("upstream", second.Message, StringComparison.Ordinal);
        Assert.Equal(2, fixture.Usage.Count);
    }

    [Fact]
    public async Task Maps_a_missing_key_to_unavailable_and_bad_output_to_invalid()
    {
        var missing = ScriptedText("Ada");
        missing.Throws = new LoadApiKeyException(
            "OpenRouter API key is missing. Pass it using the 'apiKey' parameter or the OPENROUTER_API_KEY environment variable."
        );
        var malformed = ScriptedText("Ada");
        malformed.Throws = new InvalidResponseDataException("bad");
        var unavailable = await Assert.ThrowsAsync<AgentException>(() =>
            global::E2E.Decision.Text.AskTextAsync(
                Context(new ContextOptions { Model = missing }).Ctx,
                missing,
                global::E2E.Decision.Text.TextAsk,
                Input()
            )
        );
        Assert.Equal("MODEL_UNAVAILABLE", unavailable.Code);
        Assert.Equal("Set OPENROUTER_API_KEY to the field-text model API key.", unavailable.Message);
        var invalid = await Assert.ThrowsAsync<AgentException>(() =>
            global::E2E.Decision.Text.AskTextAsync(
                Context(new ContextOptions { Model = malformed }).Ctx,
                malformed,
                global::E2E.Decision.Text.TextAsk,
                Input()
            )
        );
        Assert.Equal("MODEL_OUTPUT_INVALID", invalid.Code);
    }

    [Fact]
    public async Task Honors_abort()
    {
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        var text = ScriptedText("Ada");
        var fixture = Context(new ContextOptions { Model = text, Signal = controller.Token });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            global::E2E.Decision.Text.AskTextAsync(fixture.Ctx, text, global::E2E.Decision.Text.TextAsk, Input())
        );
    }
}
