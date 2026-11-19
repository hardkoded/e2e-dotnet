// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('keyboard')</c> of <c>executor.test.ts</c>.</summary>
public sealed class KeyboardTests
{
    private static ExecutorNode Reps() =>
        new()
        {
            Id = "reps",
            Role = "textbox",
            Name = "Reps",
            Value = "8",
        };

    [Fact]
    public async Task Dismisses_an_on_screen_keyboard_through_the_runners_action()
    {
        var (model, requests) = ScriptedDecision(Script(call => call == 0 ? "dismiss_keyboard" : "done"));
        var fixture = Context(
            new ContextOptions
            {
                Tree =
                [
                    Reps(),
                    new ExecutorNode
                    {
                        Id = "k1",
                        Role = "key",
                        Name = "1",
                    },
                ],
            }
        );

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(1, fixture.Actions.Count("dismissKeyboard"));
        Assert.Contains("dismiss_keyboard", requests[0].Questions["operation"].Keys);
        Assert.Equal(["dismiss the keyboard"], fixture.Turns[0].Calls);
    }

    [Fact]
    public async Task Offers_dismiss_keyboard_for_a_keyboard_the_engine_measured_and_the_tree_does_not_list()
    {
        var (model, requests) = ScriptedDecision(Script(call => call == 0 ? "dismiss_keyboard" : "done"));
        var fixture = Context(
            new ContextOptions { Tree = [Reps()], Observation = o => o with { KeyboardVisible = true } }
        );

        await RunAsync(model, fixture);

        Assert.Contains("dismiss_keyboard", requests[0].Questions["operation"].Keys);
        Assert.Equal(1, fixture.Actions.Count("dismissKeyboard"));
    }
}
