// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile.Tests.ToolPixels;

/// <remarks>
/// Upstream's <c>tool-pixels.test.ts</c>: the real tool pack and harness, driven by a fake device and a scripted model, through the run
/// pipeline. The test's source is an in-memory test rather than a file (see <see cref="MobileRun"/>).
/// </remarks>
public sealed class ToolPixelsTests
{
    [Fact]
    public async Task Offers_pixel_verbs_after_a_secret_fill()
    {
        var engine = Harness.Create().Engine;
        var offered = new List<string>();
        var model = new ScriptedModel(request =>
        {
            offered = request.Tools.Select(tool => tool.Name).ToList();
            return ModelResponses.Done("passed", "inspected");
        });
        var agents = new Dictionary<string, AgentOptions>
        {
            ["default"] = new AgentOptions { Model = model, Tools = MobileTools.Create(engine) },
        };

        var outcome = await MobileRun.RunAsync(
            engine,
            [
                new InMemoryTest("keep pixels", async session =>
                {
                    await session.Context.Screen.GetByRole("textbox", name: "Password").FillAsync(Secret.Create("audit.password", "synthetic-device-secret"));
                    await session.Context.Agent.ActAsync("inspect the pixels");
                }),
            ],
            agents);

        Assert.Equal(RunStatus.Passed, outcome.Status);
        Assert.Contains("open_app", offered);
        Assert.Contains("screenshot", offered);
        Assert.Contains("tap_at", offered);
    }
}
