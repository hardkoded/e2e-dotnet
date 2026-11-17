// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentActVerbs;
using WebFixtureApp = E2E.Tests.WebPlatform.FixtureApp;

namespace E2E.Tests.AgentGrammarTools;

/// <summary>
/// The <c>describe('type_secret leaves the act tool set the same across steps')</c> of upstream's <c>agent-grammar-tools.test.ts</c>
/// (tester-army/e2e#1823). Upstream builds a fake executor context by hand; the port has no such context, so each test runs the step in a
/// real web session, whose context the tool loop hands to the tools.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TypeSecretLeavesTheActToolSetTheSameAcrossStepsTests
{
    private static readonly Dictionary<string, object?> Declared = new()
    {
        ["password"] = Secret.Create("admin", "admin-pass", "password"),
    };

    private static Task<E2ESession> StartAsync(string url, IAgentModel model, IStepExecutor? executor = null) =>
        E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                Model = model,
                AgentExecutor = executor,
                BaseUrl = url,
                CacheEnabled = false,
                TestTitle = "secret tools",
                AssertionTimeout = TimeSpan.FromSeconds(5),
                ActionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(20),
                TestTimeout = TimeSpan.FromSeconds(30),
            }
        );

    [Fact]
    public async Task Describes_it_the_same_with_the_secrets_listed_by_the_caller_and_fails_a_call_on_a_step_that_declares_none()
    {
        using var site = await WebFixtureApp.StartAsync();
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        var listed = new List<string?>();
        var public_ = new List<ToolLoopTool?>();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "listing",
                Version = "1",
                System = _ => DefaultAgent.BaseRules,
                BuildPrompt = context => Task.FromResult(context.Step.Instruction),
                Tools = (context, _) =>
                {
                    var tools = AgentTools.GrammarTools(
                        context,
                        null,
                        new GrammarToolsOptions { SecretsListedByCaller = true }
                    );
                    listed.Add(
                        tools.FirstOrDefault(tool => tool.Definition.Name == "type_secret")?.Definition.Description
                    );
                    public_.Add(
                        AgentTools
                            .CreateGrammarTools(context)
                            .FirstOrDefault(tool => tool.Definition.Name == "type_secret")
                    );
                    return tools;
                },
            }
        );
        await using var session = await StartAsync(site.Url, model, executor);
        await WebSemanticsTests.RunAsync(session, async () =>
        {
            await session.Context.App.OpenAsync("/");
            await session.Context.Agent.ActAsync("bare step");
            await session.Context.Agent.ActAsync("declared step", new ActOptions { Params = Declared });
        });

        Assert.NotNull(listed[0]);
        Assert.Equal(listed[0], listed[1]);
        Assert.DoesNotContain("admin", listed[1]!, StringComparison.Ordinal);
        // The public grammar keeps naming the secrets, and offers none to a step without them.
        Assert.Null(public_[0]);
        Assert.Contains("\"admin\" (password)", public_[1]!.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_a_call_on_a_step_that_declares_no_secrets_to_the_dispatcher_whose_refusal_reads_as_a_failed_action()
    {
        using var site = await WebFixtureApp.StartAsync();
        var results = new List<string>();
        var model = new ScriptedModel(request =>
        {
            var result = request.Messages.LastOrDefault(message => message.Role == "tool")?.Content;
            if (result is not null)
            {
                results.Add(result);
                return ModelResponses.Done("failed", "no secret");
            }

            return ModelResponses.Call(
                "type_secret",
                new { target = GesturesSession.RefFor(request, "textbox \"Password\""), name = "admin" }
            );
        });
        await using var session = await StartAsync(site.Url, model);
        await WebSemanticsTests.RunAsync(session, async () =>
        {
            await session.Context.App.OpenAsync("/");
            try
            {
                await session.Context.Agent.ActAsync("sign in");
            }
            catch (AgentException)
            {
                // The model ends the step as failed.
            }
        });

        var text = Assert.Single(results);
        Assert.Matches(
            "^type_secret \"admin\" into #\\S+ failed: POLICY_DENIED: secret \"admin\" was not declared",
            text
        );
        Assert.True(global::E2E.LoopGuards.IsFailedResult("type_secret", text));
    }

    [Fact]
    public async Task Offers_the_built_in_act_loop_the_same_tools_with_and_without_a_secret_and_lists_the_secret_in_the_opening_message()
    {
        using var site = await WebFixtureApp.StartAsync();
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));
        await using var session = await StartAsync(site.Url, model);
        await WebSemanticsTests.RunAsync(session, async () =>
        {
            await session.Context.App.OpenAsync("/");
            await session.Context.Agent.ActAsync("bare step");
            await session.Context.Agent.ActAsync("declared step", new ActOptions { Params = Declared });
        });

        Assert.Equal(2, model.Requests.Count);
        var first = model.Requests[0].Tools.Select(tool => tool.Name).ToList();
        Assert.Contains("type_secret", first);
        Assert.Equal(first, model.Requests[1].Tools.Select(tool => tool.Name));
        Assert.DoesNotContain("Secrets to fill", model.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains(
            "Secrets to fill with type_secret, by name: \"admin\" (password).",
            model.Requests[1].Messages[0].Content,
            StringComparison.Ordinal
        );
    }
}
