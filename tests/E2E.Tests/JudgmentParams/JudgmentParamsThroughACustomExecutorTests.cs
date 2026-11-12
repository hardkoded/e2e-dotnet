// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.JudgmentParams;

/// <summary>
/// An assert's params reach a custom executor as the step's params, a secret by name only, and the step can never fill the secret it
/// names. Upstream runs one project for the describe; the port runs each <c>it</c> in its own session.
/// </summary>
public sealed class JudgmentParamsThroughACustomExecutorTests
{
    private const string SecretValue = "hunter2-secret";

    [Fact]
    public async Task Hands_the_assert_step_the_projected_params_the_secret_by_name_only()
    {
        var (step, _) = await RunAsync();

        Assert.Equal(ExecutorStepKind.Assert, step.Kind);
        Assert.Equal("acme-9902", step.Params!["name"].GetString());
        var password = step.Params["password"];
        Assert.Equal("secret", password.GetProperty("kind").GetString());
        Assert.Equal("member.password", password.GetProperty("name").GetString());
        Assert.Equal("password", password.GetProperty("purpose").GetString());
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(step), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Never_lets_an_assert_step_fill_a_secret_it_names()
    {
        var (_, fillErrors) = await RunAsync();

        Assert.Equal("POLICY_DENIED", Assert.IsType<AgentException>(Assert.Single(fillErrors)).Code);
    }

    private static async Task<(ExecutorStep Step, List<Exception> FillErrors)> RunAsync()
    {
        ExecutorStep? seen = null;
        List<Exception> fillErrors = [];
        var probing = new TestExecutor("params-probe", async (context, _) =>
        {
            seen = context.Step;
            var observation = await context.ObserveAsync();
            try
            {
                await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Password\""), "member.password");
            }
            catch (Exception ex)
            {
                fillErrors.Add(ex);
            }

            return Passed("checked");
        })
        {
            Version = "1",
        };
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(WorldWithAPassword(_ => { })),
            AgentExecutor = probing,
            BaseUrl = "https://app.test",
            TestTitle = "judgment params through a custom executor",
        });
        await session.App.OpenAsync("/");
        await session.Agent.AssertAsync(
            "the table shows company {name}",
            new AssertOptions
            {
                Params = new Dictionary<string, object?>
                {
                    ["name"] = Values.Unique("acme-9902"),
                    ["password"] = Secret.Create("member.password", SecretValue, "password"),
                },
            });
        return (seen!, fillErrors);
    }
}
