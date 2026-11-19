// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('field text')</c> of <c>executor.test.ts</c>.</summary>
public sealed class FieldTextTests
{
    [Fact]
    public async Task Types_the_text_model_value_into_the_chosen_field()
    {
        var scripted = ScriptedDecision(Script(call => call == 0 ? "type" : "done"));
        var text = ScriptedText("Buy milk");
        var fixture = Context(new ContextOptions { Tree = Field, Model = text });

        var verdict = await RunAsync(scripted.Model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(1, fixture.Actions.Count("type"));
        Assert.Equal(["name", "Buy milk"], fixture.Actions.Of("type").Single());
        Assert.Single(text.Requests);
        Assert.Contains("Buy milk", fixture.Turns[0].Calls[0]);
    }

    [Fact]
    public async Task Keeps_secret_values_out_of_the_text_prompt()
    {
        var scripted = ScriptedDecision(Script(call => call == 0 ? "type" : "done"));
        var text = ScriptedText("irrelevant");
        var fixture = Context(
            new ContextOptions
            {
                Tree = Field,
                Model = text,
                Params = new Dictionary<string, JsonElement>
                {
                    ["nickname"] = JsonSerializer.SerializeToElement("Ada"),
                    ["password"] = JsonSerializer.SerializeToElement(
                        new
                        {
                            kind = "secret",
                            name = "admin.password",
                            purpose = "password",
                        }
                    ),
                },
            }
        );

        await RunAsync(scripted.Model, fixture);

        Assert.Contains("Ada", text.Prompts);
        Assert.DoesNotContain("admin.password", text.Prompts);
    }

    [Fact]
    public async Task Continues_with_history_when_the_goal_supplies_no_value()
    {
        var operation = InTurn(["type", "tap"], "done");
        var scripted = ScriptedDecision(Script(_ => operation()));
        var text = ScriptedText((string?)null);
        var fixture = Context(new ContextOptions { Tree = Field, Model = text });

        var verdict = await RunAsync(scripted.Model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(0, fixture.Actions.Count("type"));
        Assert.Equal(1, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Never_offers_type_without_a_text_model()
    {
        var seen = new List<List<string>>();
        var scripted = ScriptedDecision(
            (id, keys, call) =>
            {
                seen.Add([id, .. keys]);
                return Pick(
                    id == "operation" ? (call == 0 ? "tap" : "done")
                    : id == "verdict" ? "holds"
                    : keys.FirstOrDefault() ?? ""
                );
            }
        );
        var fixture = Context(new ContextOptions { Tree = Field });

        await RunAsync(scripted.Model, fixture);

        Assert.DoesNotContain("\"type\"", JsonSerializer.Serialize(seen));
    }

    [Fact]
    public async Task Counts_the_text_call_toward_the_model_call_budget()
    {
        var scripted = ScriptedDecision(Script(_ => "type"));
        var text = ScriptedText("Buy milk");
        var fixture = Context(
            new ContextOptions
            {
                Tree = Field,
                Model = text,
                MaxModelCalls = 1,
            }
        );

        var verdict = await RunAsync(scripted.Model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED");
        Assert.Empty(text.Requests);
        Assert.Single(fixture.Usage);
    }
}
