// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentAct;

public sealed class AgentActVerdictMappingTests
{
    // Upstream also checks the configuration exit code and the blocked report status. Those belong to its runner,
    // which this port does not have: a blocked verdict is marked Blocked, and the caller maps that.
    [Fact]
    public async Task Maps_a_blocked_verdict_onto_its_error_code()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("log in with the staging account");
            },
            CounterPage.World(),
            new ScriptedModel(_ => ModelResponses.Done("blocked", "the staging credential is not configured", "AUTH_CREDENTIAL_UNAVAILABLE")));

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
        Assert.True(error.Blocked);
        Assert.Equal("the staging credential is not configured", error.Message);
    }

    [Fact]
    public async Task Fails_closed_when_the_model_claims_success_over_an_exhausted_action_budget()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return text.Contains("exhausted its action budget of 2", StringComparison.Ordinal)
                ? ModelResponses.Done("passed", "all good, nothing to see")
                : ModelResponses.Tap("button", "Increment");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("keep clicking forever", new ActOptions { MaxSteps = 2 });
            },
            CounterPage.World(),
            model);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.True(error.Blocked);
        Assert.Equal(error.Message, error.Explanation);
        Assert.Equal(4, model.CallCount);
    }

    [Fact]
    public async Task Cuts_a_hanging_model_at_the_step_timeout()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("do something eventually", new ActOptions { Timeout = TimeSpan.FromMilliseconds(500) });
            },
            CounterPage.World(),
            new HangingModel());

        Assert.Equal("STEP_TIMEOUT", Assert.IsType<AgentException>(result.Error).Code);
        // The step settled at its own 500 ms deadline, not the test timeout.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4), "settled after " + watch.Elapsed);
    }

    [Fact]
    public async Task Hard_stops_a_model_that_overspends_the_model_call_budget()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("think very hard", new ActOptions { MaxModelCalls = 2 });
            },
            CounterPage.World(),
            new ScriptedModel(_ => new ModelResponse { Content = "thinking" }));

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.True(error.Blocked);
        Assert.Equal("agent.act exhausted its model-call budget of 2", error.Message);
        Assert.Equal(2, result.ModelCalls);
    }

    [Fact]
    public async Task Stamps_the_budget_code_onto_a_code_less_failed_verdict_after_a_hard_stop()
    {
        var model = new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            return text.Contains("exhausted its action budget of 1", StringComparison.Ordinal)
                ? ModelResponses.Done("failed", "the counter looked wrong")
                : ModelResponses.Tap("button", "Increment");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap twice", new ActOptions { MaxSteps = 1 });
            },
            CounterPage.World(),
            model);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.True(error.Blocked);
        Assert.Contains("the counter looked wrong", error.Message, StringComparison.Ordinal);
        Assert.Equal(error.Message, error.Explanation);
        Assert.Equal(3, model.CallCount);
    }

    // Upstream's generic secret in a plain textbox and its provider-backed secret are not ported: the port has no
    // secret purpose check for a plain field and no credential provider functions.
    [Fact]
    public async Task Fills_a_declared_secret_through_fill_secret_and_refuses_an_undeclared_one()
    {
        string? typed = null;
        var world = new DocumentWorld().Map("/", page => page.Roots.Add(new DocumentElement
        {
            Role = "textbox",
            Name = "Password",
            Secure = true,
            InputPurpose = "password",
            OnFill = value => typed = value,
        }));
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "admin.password" }),
            // A configured secret the step never declared is refused.
            1 => ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "stripe-key" }),
            _ => ModelResponses.Done("passed", "filled the declared secret only"),
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("sign in with the given credentials", new ActOptions
                {
                    Params = new Dictionary<string, object?>
                    {
                        ["password"] = Secret.Create("admin.password", "admin-pass-1", "member password"),
                    },
                });
            },
            world,
            model);

        Assert.Null(result.Error);
        Assert.Equal("admin-pass-1", typed);
        Assert.DoesNotContain("admin-pass-1", CounterPage.Everything(model), StringComparison.Ordinal);
        var results = CounterPage.ToolResults(model);
        Assert.Contains("<secret:admin.password>", results[0], StringComparison.Ordinal);
        Assert.Contains("secret \"stripe-key\" was not declared in this step's params; only declared secrets can be filled", results[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: the port has no report, so the exit code and the step's blocked status are not checked, and the error message is the
    /// executor's summary alone, not <c>agent.act blocked: &lt;summary&gt;</c>.
    /// </summary>
    [Fact]
    public async Task Partial_Maps_a_blocked_verdict_onto_its_error_code_and_the_configuration_exit_code()
    {
        var executor = new TestExecutor("blocked-executor", (_, _) => Task.FromResult(Blocked("the staging credential is not configured", "AUTH_CREDENTIAL_UNAVAILABLE")));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("log in with the staging account");
            },
            CounterPage.World(),
            executor: executor);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
        Assert.True(error.Blocked);
        Assert.Equal("the staging credential is not configured", error.Message);
    }

    [Fact]
    public async Task Fails_closed_when_the_executor_claims_success_over_an_exhausted_budget()
    {
        var executor = new TestExecutor("over-budget-executor", async (context, _) =>
        {
            var observation = await context.ObserveAsync();
            var increment = TargetFor(observation.Text, "button \"Increment\"");
            // Swallow every failure and claim success: the runtime must not let the verdict outrank its own budget accounting.
            for (var round = 0; round < 5; round++)
            {
                try
                {
                    await context.Actions.TapAsync(increment);
                }
                catch (AgentException)
                {
                    break;
                }
            }

            return Passed("all good, nothing to see");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("keep clicking forever", new ActOptions { MaxSteps = 2 });
            },
            CounterPage.World(),
            executor: executor);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.True(error.Blocked);
    }

    [Fact]
    public async Task Cuts_a_hanging_executor_at_the_step_timeout()
    {
        // An executor that ignores the token, and everything else, and never answers.
        var executor = new TestExecutor("hanging-executor", (_, _) => new TaskCompletionSource<StepVerdict>().Task);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("do something eventually", new ActOptions { Timeout = TimeSpan.FromMilliseconds(500) });
            },
            CounterPage.World(),
            executor: executor);

        Assert.Equal("STEP_TIMEOUT", Assert.IsType<AgentException>(result.Error).Code);
        // The step settled at its own deadline, not the test timeout.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), "settled after " + watch.Elapsed);
    }

    [Fact]
    public async Task Hard_stops_an_executor_that_overspends_the_model_call_budget()
    {
        var executor = new TestExecutor("overspending-executor", (context, _) =>
        {
            for (var call = 0; call < 10; call++)
            {
                context.Budgets.RecordModelCall(new ExecutorModelCall { InputTokens = 1, OutputTokens = 1 });
            }

            return Task.FromResult(Passed("thought about it a lot"));
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("think very hard", new ActOptions { MaxModelCalls = 2 });
            },
            CounterPage.World(),
            executor: executor);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.True(error.Blocked);
    }

    [Fact]
    public async Task Stamps_the_runtime_code_onto_a_code_less_failed_verdict_after_a_hard_stop()
    {
        var executor = new TestExecutor("code-less-executor", async (context, _) =>
        {
            var observation = await context.ObserveAsync();
            var increment = TargetFor(observation.Text, "button \"Increment\"");
            try
            {
                await context.Actions.TapAsync(increment);
                await context.Actions.TapAsync(increment);
            }
            catch (AgentException)
            {
                // Swallow the budget error and report a bare product failure.
            }

            return Failed("the counter looked wrong");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap twice", new ActOptions { MaxSteps = 1 });
            },
            CounterPage.World(),
            executor: executor);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
        Assert.Contains("the counter looked wrong", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Routes_agent_assert_through_a_custom_executor_with_assert_semantics()
    {
        var seen = new List<string>();
        var executor = new TestExecutor("judging-executor", async (context, _) =>
        {
            seen.Add(context.Step.Kind.ToString().ToLowerInvariant() + ":" + context.Step.Instruction);
            if (context.Step.Instruction.Contains("checkout", StringComparison.Ordinal))
            {
                return Failed("no checkout page exists here");
            }

            var observation = await context.ObserveAsync();
            return observation.Text.Contains("status \"Counter\" text=\"0\"", StringComparison.Ordinal) ? Passed("the counter reads 0") : Failed("the counter is not zero");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.AssertAsync("the counter shows zero");
                await ctx.Agent.AssertAsync("the checkout page is visible");
            },
            CounterPage.World(),
            executor: executor);

        // The failed assertion maps to ASSERTION_FAILED, not ACTION_FAILED. The first one passed, so the second one ran.
        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal("assert:the counter shows zero", seen[0]);
        Assert.Equal(["assert:the counter shows zero", "assert:the checkout page is visible"], seen);
    }

    [Fact]
    public async Task Fills_a_declared_secret_through_typeSecret_never_exposing_the_value()
    {
        string? typed = null;
        var executor = new TestExecutor("login-executor", async (context, _) =>
        {
            // The executor sees only the placeholder, never the plaintext.
            var parameters = System.Text.Json.JsonSerializer.Serialize(context.Step.Params);
            if (parameters.Contains("admin-pass", StringComparison.Ordinal))
            {
                return Failed("plaintext leaked into params");
            }

            if (context.Step.Secrets.FirstOrDefault()?.Name != "admin.password")
            {
                return Failed("secret was not declared");
            }

            var observation = await context.ObserveAsync();
            var password = TargetFor(observation.Text, "textbox \"Password\"");
            await context.Actions.TypeSecretAsync(password, "admin.password");
            // A secret the step never declared is refused before any policy check runs.
            try
            {
                await context.Actions.TypeSecretAsync(password, "stripe-key");
                return Failed("undeclared secret was accepted");
            }
            catch (AgentException error)
            {
                if (error.Code != "POLICY_DENIED" || !error.Message.Contains("was not declared in this step", StringComparison.Ordinal))
                {
                    return Failed($"unexpected refusal: {error.Code} {error.Message}");
                }

                return Passed("filled the declared secret only");
            }
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("sign in with the given credentials", new ActOptions
                {
                    Params = new Dictionary<string, object?>
                    {
                        ["username"] = "admin",
                        ["password"] = Secret.Create("admin.password", "admin-pass", "password"),
                    },
                });
            },
            ExecutorKit.WorldWithAPassword(value => typed = value),
            executor: executor);

        Assert.Null(result.Error);
        Assert.Equal("admin-pass", typed);
    }

    /// <summary>Partial: the port declares the secret's purpose as free text, so the purposes this reads are the ones the test gave.</summary>
    [Fact]
    public async Task Partial_Fills_a_generic_secret_into_a_plain_textbox_through_typeSecret_and_refuses_a_password_there()
    {
        var executor = new TestExecutor("api-key-executor", async (context, _) =>
        {
            if (System.Text.Json.JsonSerializer.Serialize(context.Step.Params).Contains("sk_live_generic", StringComparison.Ordinal))
            {
                return Failed("plaintext leaked into params");
            }

            var declared = string.Join(",", context.Step.Secrets.Select(secret => secret.Name + ":" + secret.Purpose).Order(StringComparer.Ordinal));
            if (declared != "admin.password:password,stripe-key:generic-secret")
            {
                return Failed("unexpected secrets " + declared);
            }

            var observation = await context.ObserveAsync();
            var focusTarget = TargetFor(observation.Text, "textbox \"Focus target\"");
            // A password belongs in a password field only.
            try
            {
                await context.Actions.TypeSecretAsync(focusTarget, "admin.password");
                return Failed("a password was accepted by a plain textbox");
            }
            catch (AgentException ex) when (ex.Message.Contains("incompatible", StringComparison.Ordinal))
            {
                // Refused, as it must be.
            }

            // A generic secret goes wherever the test points it.
            await context.Actions.TypeSecretAsync(focusTarget, "stripe-key");
            var after = await context.ObserveAsync();
            if (after.Text.Contains("sk_live_generic", StringComparison.Ordinal))
            {
                return Failed("the generic secret value reached the observation");
            }

            return after.Text.Contains("<secret:stripe-key>", StringComparison.Ordinal)
                ? Passed("filled the generic secret into a plain field")
                : Failed("the filled value was not redacted by name");
        });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("paste the API key into the focus target field", new ActOptions
                {
                    Params = new Dictionary<string, object?>
                    {
                        ["password"] = Secret.Create("admin.password", "admin-pass", "password"),
                        ["apiKey"] = Secret.Create("stripe-key", "sk_live_generic_4242", "generic-secret"),
                    },
                });
            },
            ExecutorKit.WorldWithAPassword(_ => { }),
            executor: executor);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Rejects_a_verdict_outside_the_closed_grammar()
    {
        var executor = new TestExecutor("rogue-executor", (_, _) => Task.FromResult(Passed("")));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("log in with the staging account");
            },
            CounterPage.World(),
            executor: executor);

        Assert.Equal("MODEL_OUTPUT_INVALID", Assert.IsType<AgentException>(result.Error).Code);
    }

    private sealed class HangingModel : IAgentModel
    {
        public string Name => "hanging";

        public async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ModelResponse();
        }
    }
}
