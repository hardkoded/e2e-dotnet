// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// Upstream's <c>describe('agent.act pixel verbs')</c> (<c>agent-act-pixel-tools.test.ts</c>). The built-in agent prints no <c>#id</c>
/// nodes and no diff results, so the ported <c>it</c>s read the full screen the port prints, and a screenshot rides a user turn after the
/// tool results. The point its are in <c>AgentActPixelVerbsTests.Points.cs</c>. Not ported: the keyboard its (<c>typeText</c>, <c>pressKey</c>),
/// "reports a moved screenshot under an unchanged listing" (no diff presenter).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class AgentActPixelVerbsTests
{
    private static DocumentWorld Page() => new DocumentWorld().Map("/", page =>
    {
        var clicks = 0;
        var count = page.Status("Clicked 0 times");
        page.Button("Save", () => count.Name = count.Text = $"Clicked {++clicks} times");
    });

    private static DocumentWorld Canvas() => new DocumentWorld().Map("/", page => page.Status("Chart"));

    /// <summary>What one model request carried. The loop grows one list across turns, so the requests are copied as they arrive.</summary>
    private sealed record Sent(IReadOnlyList<ModelMessage> Messages, IReadOnlyList<ModelTool> Tools, string System = "");

    private static async Task<(List<Sent> Requests, PixelsEngine Engine, StepRecord Record, Exception? Error)> ActAsync(
        DocumentWorld world,
        Func<int, ModelResponse> script,
        Action<PixelsEngine>? setup = null)
    {
        PixelsEngine? engine = null;
        var turn = 0;
        var requests = new List<Sent>();
        var model = new ScriptedModel(request =>
        {
            requests.Add(new Sent([.. request.Messages], [.. request.Tools]));
            return script(turn++);
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine = new PixelsEngine(new DocumentEngine(world)),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "pixels",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });
        setup?.Invoke(engine);
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync("/");
            await session.Context.Agent.ActAsync("look at the screen");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return (requests, engine, session.StepRecords.Single(step => step.Api == "agent.act"), error);
    }

    private static IEnumerable<ModelMessage> Tools(Sent request) => request.Messages.Where(message => message.Role == "tool");

    [Fact]
    public async Task Partial_opens_tree_first_with_the_screenshot_tool_on_offer()
    {
        var (requests, engine, record, error) = await ActAsync(Page(), _ => ModelResponses.Done("passed", "Done."));
        Assert.Null(error);
        Assert.Contains(requests[0].Tools, tool => tool.Name == "screenshot");
        Assert.All(requests[0].Messages, message => Assert.Null(message.Images));
        Assert.DoesNotContain("Screenshot attached", requests[0].Messages[0].Content, StringComparison.Ordinal);
        Assert.Equal(0, engine.Shots);
        Assert.Null(record.VisionInput);
        Assert.Null(record.Metrics?.PixelBytes);
    }

    [Fact]
    public async Task Partial_returns_the_screenshot_as_an_image_and_a_fresh_one_after_each_later_action()
    {
        var (requests, engine, record, error) = await ActAsync(Page(), turn => turn switch
        {
            0 => ModelResponses.Call("screenshot", new { }),
            1 => ModelResponses.Tap("button", "Save"),
            _ => ModelResponses.Done("passed", "Done."),
        });
        Assert.Null(error);
        // The screenshot answers the first call: a note in the result, the image on the user turn that follows.
        var first = requests[1];
        Assert.Contains("Screenshot attached: 768 by 432 pixels (0.6 per CSS pixel).", Tools(first).Last().Content, StringComparison.Ordinal);
        Assert.Equal(1, first.Messages[^1].Images?.Count);
        Assert.Equal("user", first.Messages[^1].Role);
        // The tap in pixel mode answers with the screen and a fresh screenshot.
        var second = requests[2];
        Assert.Contains("Screenshot attached:", Tools(second).Last().Content, StringComparison.Ordinal);
        Assert.Equal(2, second.Messages.Sum(message => message.Images?.Count ?? 0));
        Assert.Equal(2, engine.Shots);
        Assert.True(record.VisionInput);
        Assert.True(record.Metrics?.PixelBytes > 0);
    }

    [Fact]
    public async Task Partial_opens_the_prompt_with_a_screenshot_for_a_screen_with_nothing_to_tap_and_not_for_an_ordinary_page()
    {
        var (requests, engine, record, error) = await ActAsync(Canvas(), _ => ModelResponses.Done("passed", "Done."));
        Assert.Null(error);
        var opening = requests[0].Messages[0];
        Assert.Contains("Screenshot attached: 768 by 432 pixels", opening.Content, StringComparison.Ordinal);
        Assert.Equal(1, opening.Images?.Count);
        Assert.Equal(1, engine.Shots);
        Assert.True(record.VisionInput);
    }

    [Fact]
    public async Task Partial_opens_a_step_with_a_screenshot_when_the_semantic_tree_cannot_be_read_and_finds_no_control_to_act_on()
    {
        var (requests, _, _, error) = await ActAsync(
            Page(),
            turn => turn == 0 ? ModelResponses.Tap("button", "Save") : ModelResponses.Done("passed", "Done."),
            engine => engine.TreeUnavailable = true);
        Assert.Null(error);
        var opening = requests[0].Messages[0];
        Assert.Contains("[semantic capture unavailable", opening.Content, StringComparison.Ordinal);
        Assert.Equal(1, opening.Images?.Count);
        Assert.Contains("LOCATOR_NOT_FOUND", Tools(requests[1]).Single().Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_ends_the_step_blocked_when_the_tree_cannot_be_read_and_no_proven_masked_screenshot_replaces_it()
    {
        var (_, _, _, error) = await ActAsync(Page(), _ => ModelResponses.Done("passed", "Done."), engine =>
        {
            engine.TreeUnavailable = true;
            engine.WithholdPixels = true;
        });
        var failure = Assert.IsType<AgentException>(error);
        // The step ends blocked: the agent maps an UNSUPPORTED_CAPABILITY raised inside a step to POLICY_DENIED.
        Assert.Equal("POLICY_DENIED", failure.Code);
        Assert.Contains("semantic capture is unavailable", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_shows_a_screenshot_when_an_action_leaves_a_screen_the_engine_cannot_read_as_a_tree()
    {
        PixelsEngine? held = null;
        var (requests, _, record, error) = await ActAsync(
            Page(),
            turn =>
            {
                if (turn == 0)
                {
                    held!.TreeUnavailable = true;
                    return ModelResponses.Tap("button", "Save");
                }

                return ModelResponses.Done("passed", "Done.");
            },
            engine => held = engine);
        Assert.Null(error);
        var result = Tools(requests[1]).Single().Content;
        Assert.Contains("Screenshot attached:", result, StringComparison.Ordinal);
        Assert.Equal(1, requests[1].Messages[^1].Images?.Count);
        Assert.True(record.VisionInput);
    }
}
