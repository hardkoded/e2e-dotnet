// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('context')</c> of <c>executor.test.ts</c>.</summary>
public sealed class ContextTests
{
    [Fact]
    public async Task Sends_the_ledger_and_non_secret_params_never_secret_handles()
    {
        var (model, requests) = ScriptedDecision((id, keys, call) => Pick(id, keys, call == 0 ? "tap" : "done"));
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Ledger = "previous step done",
                Params = ParseParams(
                    """
                    {"city":"Lisbon",
                     "password":{"kind":"secret","name":"admin.password","purpose":"password"},
                     "nested":{"theme":"dark","token":{"kind":"secret","name":"nested.token","purpose":"token"}}}
                    """
                ),
            }
        );
        await Run(model, fixture);
        var state = requests[0].State;
        Assert.Equal("previous step done", state.GetProperty("previousSteps").GetString());
        Assert.Equal("Lisbon", state.GetProperty("params").GetProperty("city").GetString());
        Assert.Equal("dark", state.GetProperty("params").GetProperty("nested").GetProperty("theme").GetString());
        var raw = string.Join('\n', Strings(state));
        Assert.DoesNotContain("admin.password", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("nested.token", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seeds_history_from_a_replayed_prefix_and_flags_the_uncertain_action()
    {
        var (model, requests) = ScriptedDecision((id, keys, call) => Pick(id, keys, call == 0 ? "tap" : "done"));
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                ReplayedPrefix = new ReplayedPrefix(
                    ["tap button Save"],
                    2,
                    ReplayHandOffReason.ActionUncertain,
                    "type Name"
                ),
            }
        );
        await Run(model, fixture);
        var history = requests[0].State.GetProperty("recentActions");
        Assert.Equal("tap button Save", history[0].GetProperty("action").GetString());
        Assert.True(history[0].GetProperty("replayed").GetBoolean());
        Assert.Equal("type Name", history[1].GetProperty("action").GetString());
        Assert.True(history[1].GetProperty("uncertain").GetBoolean());
    }

    [Fact]
    public async Task Keeps_secret_handles_out_of_replayed_summaries()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                new ScriptedAnswer(
                    id == "operation" ? "done"
                    : id == "verdict" ? "holds"
                    : keys[0]
                )
        );
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                ReplayedPrefix = new ReplayedPrefix(
                    [
                        "tap button \"password\"",
                        "fill secret \"password\" into textbox \"Password\"",
                        "fill secret \"a.very.long.secret.handle.name.cut.at.40…\" into textbox \"Token\"",
                    ],
                    4,
                    ReplayHandOffReason.ActionUncertain,
                    "tap button \"Sign in\""
                ),
            }
        );
        await Run(model, fixture);
        string[] expected =
        [
            "tap button \"password\"",
            "fill secret <secret> into textbox \"Password\"",
            "fill secret <secret> into textbox \"Token\"",
        ];
        var recent = requests[0].State.GetProperty("recentActions").EnumerateArray().Take(3).ToList();
        Assert.Equal(expected, recent.Select(entry => entry.GetProperty("action").GetString()));
        var check = requests[1].Questions["verdict"].Instructions!;
        foreach (var line in expected)
        {
            Assert.Contains(line, check, StringComparison.Ordinal);
        }

        foreach (var request in requests)
        {
            Assert.DoesNotContain(
                "secret \"password\"",
                string.Join('\n', request.Questions.Values.Select(question => question.Instructions)),
                StringComparison.Ordinal
            );
            Assert.DoesNotContain(
                "secret \"password\"",
                string.Join('\n', Strings(request.State)),
                StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public async Task Sends_only_the_last_10_actions()
    {
        var operation = InTurn(Enumerable.Repeat("tap", 12).ToList(), "done");
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                new ScriptedAnswer(
                    id == "operation" ? operation()
                    : id == "verdict" ? "holds"
                    : keys[0]
                )
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        fixture.ObserveWith = look =>
            Obs(
                [
                    new ExecutorNode
                    {
                        Id = "a",
                        Role = "button",
                        Name = "Page " + (look + 1).ToString(CultureInfo.InvariantCulture),
                    },
                ],
                look
            );
        await Run(model, fixture);
        var state = requests.Where(request => request.Questions.ContainsKey("operation")).ElementAt(12).State;
        Assert.Equal(12, fixture.Actions.Count("tap"));
        var actions = state
            .GetProperty("recentActions")
            .EnumerateArray()
            .Select(e => e.GetProperty("action").GetString());
        Assert.Equal(Enumerable.Range(0, 10).Select(index => "tap button \"Page " + (index + 3) + "\""), actions);
    }

    [Fact]
    public async Task Gives_the_completion_check_the_inputs_typed_values_and_actions_never_claims_or_secrets()
    {
        var operation = InTurn(["type"], "done");
        var check = InTurn(["inconclusive"], "holds");
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(operation()),
                    "verdict" => new ScriptedAnswer(check()),
                    _ => new ScriptedAnswer(keys[0]),
                }
        );
        var text = ScriptedText("Ada");
        var fixture = Context(
            new ContextOptions
            {
                Tree = Field,
                Model = text,
                Params = ParseParams(
                    """{"nickname":"Ada","password":{"kind":"secret","name":"admin.password","purpose":"password"}}"""
                ),
            }
        );
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        var checks = requests.Where(request => request.Questions.ContainsKey("verdict")).ToList();
        Assert.Equal(2, checks.Count);
        Assert.Equal("Ada", checks[1].State.GetProperty("params").GetProperty("nickname").GetString());
        // The actions reach the verdict in its question only; the same list in the state was measured to hurt.
        Assert.False(checks[1].State.TryGetProperty("actions", out _));
        Assert.Contains(
            "1. type into textbox \"Name\" = \"Ada\"",
            checks[1].Questions["verdict"].Instructions,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("admin.password", string.Join('\n', Strings(checks[1].State)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shows_a_named_statuss_text_to_the_completion_check()
    {
        var tree = new[]
        {
            new ExecutorNode
            {
                Id = "greeting",
                Role = "status",
                Name = "Greeting",
                Text = "Welcome back, admin!",
            },
            new ExecutorNode
            {
                Id = "out",
                Role = "button",
                Name = "Sign out",
            },
        };
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                new ScriptedAnswer(
                    id == "operation" ? "done"
                    : id == "verdict" ? "holds"
                    : keys[0]
                )
        );
        var fixture = Context(new ContextOptions { Tree = tree });
        await Run(model, fixture);
        var check = VerdictRequest(requests);
        Assert.Contains(
            "Greeting text=\"Welcome back, admin!\"",
            check.State.GetProperty("page").GetProperty("text").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Shows_filled_fields_to_the_completion_check_even_when_the_step_cannot_type()
    {
        var tree = new[]
        {
            new ExecutorNode
            {
                Id = "name",
                Role = "textbox",
                Name = "Name",
                Value = "Ada",
            },
            new ExecutorNode
            {
                Id = "save",
                Role = "button",
                Name = "Save",
            },
        };
        var (model, requests) = ScriptedDecision(
            (id, keys, _) =>
                new ScriptedAnswer(
                    id == "operation" ? "done"
                    : id == "verdict" ? "holds"
                    : keys[0]
                )
        );
        // No text model and no press verb: in the act loop the field takes a tap only.
        var fixture = Context(
            new ContextOptions
            {
                Tree = tree,
                Verbs = new HashSet<ExecutorVerb> { ExecutorVerb.Tap, ExecutorVerb.Type },
            }
        );
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        var decision = requests[0].State.GetProperty("elements").EnumerateArray().ToList();
        Assert.Equal(["Name", "Save"], decision.Select(e => e.GetProperty("label").GetString()));
        Assert.All(decision, element =>
            Assert.Equal(["tap"], element.GetProperty("operations").EnumerateArray().Select(o => o.GetString()))
        );
        var check = requests[1].State.GetProperty("elements").EnumerateArray().ToList();
        Assert.Contains(check, element =>
            element.GetProperty("label").GetString() == "Name" && element.GetProperty("value").GetString() == "Ada"
        );
    }

    [Fact]
    public async Task Shows_checked_state_in_check_target_criteria()
    {
        var tree = new[]
        {
            new ExecutorNode
            {
                Id = "milk",
                Role = "checkbox",
                Name = "Buy milk",
                States = new NodeStates { Checked = true },
            },
            new ExecutorNode
            {
                Id = "dog",
                Role = "checkbox",
                Name = "Walk the dog",
                States = new NodeStates { Checked = false },
            },
        };
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                new ScriptedAnswer(
                    id == "operation" ? (call == 0 ? "check" : "done")
                    : id == "verdict" ? "holds"
                    : keys[1]
                )
        );
        var fixture = Context(new ContextOptions { Tree = tree });
        await Run(model, fixture);
        var criteria = requests[1].Questions["target"].Choices!;
        Assert.Equal(
            ["[1] checkbox \"Buy milk\" checked", "[2] checkbox \"Walk the dog\" unchecked"],
            criteria.Take(2).Select(choice => choice.Description)
        );
        Assert.Equal(["dog", true], fixture.Actions.Of("check").Single());
    }

    [Fact]
    public async Task Shows_selected_state_in_tap_target_criteria_so_the_model_can_tell_which_tab_is_active()
    {
        var tree = new[]
        {
            new ExecutorNode
            {
                Id = "profile",
                Role = "tab",
                Name = "Profile",
                States = new NodeStates { Selected = true },
            },
            new ExecutorNode
            {
                Id = "settings",
                Role = "tab",
                Name = "Settings",
                States = new NodeStates { Selected = false },
            },
        };
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                new ScriptedAnswer(
                    id == "operation" ? (call == 0 ? "tap" : "done")
                    : id == "verdict" ? "holds"
                    : keys[1]
                )
        );
        var fixture = Context(new ContextOptions { Tree = tree });
        await Run(model, fixture);
        var criteria = requests[1].Questions["target"].Choices!;
        Assert.Equal(
            ["[1] tab \"Profile\" selected", "[2] tab \"Settings\""],
            criteria.Take(2).Select(choice => choice.Description)
        );
        Assert.Equal(["settings"], fixture.Actions.Of("tap").Single());
    }

    [Fact]
    public async Task Rejects_an_undeclared_secret_name()
    {
        var login = new[]
        {
            new ExecutorNode
            {
                Id = "pw",
                Role = "textbox",
                Name = "Password",
                InputPurpose = "password",
            },
        };
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id switch
                {
                    "operation" => new ScriptedAnswer("typeSecret"),
                    "secret" => new ScriptedAnswer("intruder"),
                    _ => new ScriptedAnswer(keys[0]),
                }
        );
        var fixture = Context(
            new ContextOptions
            {
                Tree = login,
                Secrets = [new ExecutorSecret("password", "password"), new ExecutorSecret("token", "generic-secret")],
            }
        );
        var error = await Assert.ThrowsAsync<AgentException>(() => Run(model, fixture));
        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
    }

    [Fact]
    public async Task Sends_the_step_origin_to_the_completion_check_when_the_page_moved()
    {
        var (model, requests) = ScriptedDecision((id, keys, call) => Pick(id, keys, call == 0 ? "tap" : "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons });
        fixture.ObserveWith = look => Obs(Buttons, look, look < 1 ? "/edit.html" : "/item.html");
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        var state = VerdictRequest(requests).State;
        Assert.Equal("/edit.html", state.GetProperty("origin").GetProperty("path").GetString());
        Assert.Equal("/item.html", state.GetProperty("page").GetProperty("path").GetString());
    }

    [Fact]
    public async Task Omits_the_origin_when_the_step_never_left_its_page()
    {
        var (model, requests) = ScriptedDecision((id, keys, call) => Pick(id, keys, call == 0 ? "tap" : "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons, Observation = o => o with { Path = "/item.html" } });
        fixture.ObserveWith = look => Obs(Buttons, 0, "/item.html");
        await Run(model, fixture);
        Assert.False(VerdictRequest(requests).State.TryGetProperty("origin", out _));
    }
}
