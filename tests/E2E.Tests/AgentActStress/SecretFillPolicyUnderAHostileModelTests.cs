// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActStress;

public sealed class SecretFillPolicyUnderAHostileModelTests
{
    // Upstream also checks that the plaintext stays out of the --debug transcript and shows the param as
    // {"kind":"secret"}. The port has no transcript, and its prompt names a secret param as <secret:name>.
    // Upstream's generic secret in a contenteditable host is not ported: the port has no secret purpose check.
    [Fact]
    public async Task Never_leaks_the_plaintext_into_prompts_or_tool_results()
    {
        var world = new DocumentWorld().Map("/", page => page.Textbox("Password", secure: true));
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "filled the secret")
            : ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "admin.password" }));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("probe", new ActOptions
                {
                    Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass") },
                });
            },
            world,
            model);

        Assert.Null(result.Error);
        // The model never saw the value: not in any prompt or tool result, only the placeholder.
        Assert.DoesNotContain("admin-pass", CounterPage.Everything(model), StringComparison.Ordinal);
        Assert.Contains("<secret:admin.password>", model.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("<secret:admin.password>", CounterPage.ToolResults(model)[0], StringComparison.Ordinal);
    }
}
