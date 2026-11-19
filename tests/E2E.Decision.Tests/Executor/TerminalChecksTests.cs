// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('terminal checks')</c> of <c>executor.test.ts</c>.</summary>
public sealed class TerminalChecksTests
{
    private static ScriptedAnswer Keys(IReadOnlyList<string> keys) => Pick(keys.FirstOrDefault() ?? "");

    [Fact]
    public async Task Passes_done_on_holds()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id == "operation" ? Pick(call == 0 ? "done" : "blocked")
                : id == "verdict" ? Pick("holds")
                : Keys(keys)
        );

        var verdict = await RunAsync(model, Context(new ContextOptions { Tree = Buttons }));

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task Fails_failed_on_fails_with_ACTION_FAILED()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id == "operation" ? Pick(call == 0 ? "failed" : "blocked")
                : id == "verdict" ? Pick("fails")
                : Keys(keys)
        );

        var verdict = await RunAsync(model, Context(new ContextOptions { Tree = Buttons }));

        AssertVerdict(verdict, StepVerdictStatus.Failed, "ACTION_FAILED");
    }

    [Fact]
    public async Task Continues_after_one_rejected_claim_with_history()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id == "operation"
                    ? Pick(
                        call == 0 ? "done"
                        : call <= 2 ? "tap"
                        : "done"
                    )
                : id == "verdict" ? Pick(call <= 1 ? "fails" : "holds")
                : Keys(keys)
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Equal(1, fixture.Actions.Count("tap"));
    }

    [Theory]
    [InlineData("done", "fails", StepVerdictStatus.Failed, "ACTION_FAILED")]
    [InlineData("done", "inconclusive", StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED")]
    [InlineData("failed", "holds", StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED")]
    [InlineData("failed", "inconclusive", StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED")]
    public async Task Ends_the_step_on_the_second_rejected_claim_decided_by_the_check(
        string claim,
        string check,
        StepVerdictStatus status,
        string code
    )
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                id == "operation" ? Pick(claim)
                : id == "verdict" ? Pick(check)
                : Keys(keys)
        );

        var verdict = await RunAsync(model, Context(new ContextOptions { Tree = Buttons }));

        AssertVerdict(verdict, status, code);
        Assert.Equal(2, requests.Count(request => request.Questions.ContainsKey("verdict")));
    }

    [Fact]
    public async Task Counts_a_completion_check_under_the_gate_as_a_rejection()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id == "operation" ? Pick("done")
                : id == "verdict"
                    ? new ScriptedAnswer(
                        "holds",
                        new Dictionary<string, double>
                        {
                            ["holds"] = 0.4,
                            ["fails"] = 0.3,
                            ["inconclusive"] = 0.3,
                        },
                        0.95
                    )
                : Keys(keys)
        );

        var verdict = await RunAsync(
            model,
            Context(new ContextOptions { Tree = Buttons }),
            Options(model, minProbability: 0.9)
        );

        AssertVerdict(verdict, StepVerdictStatus.Blocked, "AUTOMATION_UNSUPPORTED");
    }

    [Fact]
    public async Task Sends_action_descriptions_but_no_model_output_to_the_check()
    {
        var (model, requests) = ScriptedDecision(Script(call => call == 0 ? "tap" : "done"));

        await RunAsync(model, Context(new ContextOptions { Tree = Buttons }));

        var check = requests.Find(request => request.Questions.ContainsKey("verdict"));
        var state = check!.State.GetRawText();
        Assert.Contains("tap", state);
        Assert.DoesNotContain("recentActions", state);
    }

    [Fact]
    public async Task Retries_the_claim_when_the_check_screen_is_incomplete()
    {
        var (model, requests) = ScriptedDecision(Script(_ => "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var good = Look(Buttons);
        fixture.ObserveWith = look => look == 1 ? good with { TreeUnavailable = true } : good;

        var verdict = await RunAsync(model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        // No verdict request goes out for the incomplete screen: the claim is retried on the next loop instead of passing or failing the step.
        Assert.Equal(1, requests.Count(request => request.Questions.ContainsKey("verdict")));
    }

    [Fact]
    public async Task Names_select_options_in_the_target_criteria()
    {
        IReadOnlyList<ExecutorNode> tree =
        [
            new()
            {
                Id = "size",
                Role = "combobox",
                Name = "Size",
                Children =
                [
                    new ExecutorNode
                    {
                        Id = "s",
                        Role = "option",
                        Name = "Small",
                    },
                    new ExecutorNode
                    {
                        Id = "l",
                        Role = "option",
                        Name = "Large",
                    },
                ],
            },
            new()
            {
                Id = "save",
                Role = "button",
                Name = "Save",
            },
        ];
        var (model, requests) = ScriptedDecision(Script(call => call == 0 ? "select" : "done"));

        await RunAsync(model, Context(new ContextOptions { Tree = tree }));

        var criteria = requests[1].Questions["target"].Choices!;
        Assert.Equal(
            ["option \"Small\"", "option \"Large\""],
            criteria.Take(2).Select(choice => choice.Description?.ToText())
        );
        Assert.Equal(["1:0", "1:1", "none"], criteria.Select(choice => choice.Value));
    }
}
