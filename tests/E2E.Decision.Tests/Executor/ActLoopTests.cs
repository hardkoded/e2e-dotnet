// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('act loop')</c> of <c>executor.test.ts</c>.</summary>
public sealed class ActLoopTests
{
    private static string Tap0Done(int call) => call == 0 ? "tap" : "done";

    [Fact]
    public async Task Taps_a_button_and_passes_on_a_held_completion_check()
    {
        var (model, requests) = ScriptedDecision(Script(Tap0Done));
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(1, fixture.Actions.Count("tap"));
        Assert.Equal(4, requests.Count);
        Assert.Equal(["operation"], requests[0].Questions.Keys.ToList());
        Assert.Equal(["target"], requests[1].Questions.Keys.ToList());
        Assert.Equal(["1", "2", "none"], requests[1].Questions["target"].Keys);
        Assert.Contains("The next operation is \"tap\"", requests[1].Questions["target"].Instructions);
        Assert.Equal(4, fixture.Usage.Count);
        Assert.Equal("scripted", fixture.Usage[0].Provider);
        Assert.Equal("scripted-1", fixture.Usage[0].ModelId);
        Assert.NotEmpty(fixture.Turns);
        Assert.Single(fixture.Transcripts);
    }

    [Fact]
    public async Task Sends_providerOptions_with_every_decide_call_and_none_when_unset()
    {
        var providerOptions = new Dictionary<string, JsonElement>
        {
            ["gateway"] = JsonSerializer.SerializeToElement(new { zeroDataRetention = true }),
        };
        var withOptions = ScriptedDecision(Script(Tap0Done));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        AssertVerdict(
            await RunAsync(withOptions.Model, fixture, Options(withOptions.Model, providerOptions: providerOptions)),
            StepVerdictStatus.Passed
        );
        Assert.Equal(4, withOptions.Requests.Count);
        foreach (var request in withOptions.Requests)
        {
            Assert.Equal(JsonSerializer.Serialize(providerOptions), JsonSerializer.Serialize(request.ProviderOptions));
        }

        var without = ScriptedDecision(Script(Tap0Done));
        AssertVerdict(
            await RunAsync(without.Model, Context(new ContextOptions { Tree = Buttons })),
            StepVerdictStatus.Passed
        );
        // The SDK sends an empty object when the caller passes none.
        foreach (var request in without.Requests)
        {
            Assert.True(request.ProviderOptions is null || request.ProviderOptions.Count == 0);
        }
    }

    [Fact]
    public async Task Dispatches_a_lone_target_with_no_target_question()
    {
        var (model, requests) = ScriptedDecision(Script(Tap0Done));
        var fixture = Context(
            new ContextOptions
            {
                Tree =
                [
                    new ExecutorNode
                    {
                        Id = "only",
                        Role = "button",
                        Name = "Only",
                    },
                ],
            }
        );

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(["operation"], requests[0].Questions.Keys.ToList());
    }

    [Fact]
    public async Task Stops_on_blocked_without_acting()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => Pick(id == "operation" ? "blocked" : keys.FirstOrDefault() ?? "")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED");
        Assert.Equal(0, fixture.Actions.Count("tap"));
        Assert.Single(requests);
    }
}
