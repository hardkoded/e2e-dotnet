// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Agent;

/// <summary>
/// The second <c>describe</c> of upstream's <c>agent.test.ts</c>. It also reads the act's result and the secret that reached the field.
/// </summary>
public sealed class AnInconclusiveJudgmentAfterASecretFillTests
{
    [Fact]
    public async Task Carries_the_vision_hint_a_secret_fill_does_not_withhold_pixels()
    {
        var typed = new List<string>();
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Password", Value = "", Secure = true, InputPurpose = "password", OnFill = typed.Add });
        });
        // The act model fills the password through fill_secret; a screenshot can still reach a judge
        // afterwards.
        var calls = 0;
        var actor = new ScriptedModel(_ => calls++ == 0
            ? ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "member-password" })
            : ModelResponses.Done("passed", "filled the password"));
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "no order total or line items are on this screen", "ASSERTION_INCONCLUSIVE"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ScreenshotEngine(world, _ => ScreenshotEngine.Png()),
            Model = actor,
            Judge = judge,
            BaseUrl = "https://app.test",
            TestTitle = "the judge is pointed at vision after a secret fill",
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
            ResultsDirectory = Path.Combine(Path.GetTempPath(), "e2e-filled-" + Guid.NewGuid().ToString("N"), "results"),
        });
        await session.App.OpenAsync("/");
        var filled = await session.Agent.ActAsync("sign in as the member", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["password"] = Secret.Create("member-password", "hunter2-secret", "password") },
        });
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the order total equals the sum of the line items"));

        var act = session.Steps.Single(step => step.Api == "agent.act");
        Assert.Equal(StepStatus.Passed, act.Status);
        Assert.Contains(act.Events, item => item is { Kind: StepEventKind.Engine, Name: "typeSecret", Status: StepEventStatus.Passed });
        Assert.Equal(StepStatus.Failed, session.Steps.Single(step => step.Api == "agent.assert").Status);
        Assert.Equal("filled the password", filled.Summary);
        Assert.Equal(["hunter2-secret"], typed);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        Assert.Contains("no order total or line items are on this screen", error.Message, StringComparison.Ordinal);
        Assert.Contains(Vision.HintUnproven, error.Message, StringComparison.Ordinal);
        session.Complete(error);
    }
}
