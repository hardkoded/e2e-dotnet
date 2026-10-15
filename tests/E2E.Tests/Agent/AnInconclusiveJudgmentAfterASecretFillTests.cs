// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Agent;

/// <summary>
/// The second <c>describe</c> of upstream's <c>agent.test.ts</c>. Partial: the port has no step record, so the test cannot
/// read the fill's engine event (<c>typeSecret</c>); it reads the act's result and the secret that reached the field.
/// </summary>
public sealed class AnInconclusiveJudgmentAfterASecretFillTests
{
    [Fact]
    public async Task Carries_the_explanation_alone_when_vision_could_add_nothing_while_pixels_are_withheld()
    {
        var typed = new List<string>();
        var world = new DocumentWorld().Map("/", page =>
        {
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Password", Value = "", Secure = true, InputPurpose = "password", OnFill = typed.Add });
        });
        // The act model fills the password through fill_secret; from then on the attempt is pixel-tainted, and no
        // screenshot could reach a judge.
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
            TestTitle = "the judge is not pointed at vision once a secret was filled",
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
            ResultsDirectory = Path.Combine(Path.GetTempPath(), "e2e-tainted-" + Guid.NewGuid().ToString("N")),
        });
        await session.App.OpenAsync("/");
        var filled = await session.Agent.ActAsync("sign in as the member", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["password"] = Secret.Create("member-password", "hunter2-secret", "password") },
        });
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the order total equals the sum of the line items"));

        Assert.Equal("filled the password", filled.Summary);
        Assert.Equal(["hunter2-secret"], typed);
        Assert.Equal("ASSERTION_INCONCLUSIVE", error.Code);
        // The same judgment on an untainted attempt carries the hint (AgentJudgmentTierTests); here the engine declares
        // screenshots and no pixel request was degraded, so the fill is the only gate closing it.
        Assert.Equal("no order total or line items are on this screen", error.Message);
        session.Complete(error);
    }
}
