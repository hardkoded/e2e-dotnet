// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorActHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('secrets')</c> of <c>executor.test.ts</c>.</summary>
public sealed class SecretsTests
{
    private static readonly IReadOnlyList<ExecutorNode> Login =
    [
        new()
        {
            Id = "pw",
            Role = "textbox",
            Name = "Password",
            InputPurpose = "password",
        },
    ];

    [Fact]
    public async Task Asks_the_secret_question_only_with_two_or_more_secrets()
    {
        var scripted = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => Pick(call == 0 ? "typeSecret" : "done"),
                    "secret" => Pick("password"),
                    "verdict" => Pick("holds"),
                    _ => Pick(keys.FirstOrDefault() ?? ""),
                }
        );
        var fixture = Context(
            new ContextOptions
            {
                Tree = Login,
                Secrets = [new ExecutorSecret("password", "password"), new ExecutorSecret("token", "generic-secret")],
            }
        );

        var verdict = await RunAsync(scripted.Model, fixture);

        AssertVerdict(verdict, StepVerdictStatus.Passed);
        Assert.Contains(scripted.Requests, request => request.Questions.ContainsKey("secret"));
        Assert.Equal(1, fixture.Actions.Count("typeSecret"));
        Assert.Equal(["pw", "password"], fixture.Actions.Of("typeSecret").Single());
    }

    [Fact]
    public async Task Fills_a_lone_secret_with_no_secret_question()
    {
        var scripted = ScriptedDecision(Script(call => call == 0 ? "typeSecret" : "done"));
        var fixture = Context(new ContextOptions { Tree = Login });

        await RunAsync(scripted.Model, fixture);

        Assert.All(scripted.Requests, request => Assert.False(request.Questions.ContainsKey("secret")));
        Assert.Equal(1, fixture.Actions.Count("typeSecret"));
    }
}
