// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0


namespace E2E.Tests.AgentFallbackHandoff;

/// <summary>
/// Upstream's <c>describe('agent semantic fallback through the engine contract')</c> (<c>agent-fallback-handoff.test.ts</c>): the pixel
/// fallback reaches the vision judgment and the act a secret was filled before, and a tree-only judgment stops before a model call. The
/// four sessions run once, in <see cref="MaskedFallbackRuns"/>.
/// </summary>
public sealed class AgentSemanticFallbackThroughTheEngineContractTests(MaskedFallbackRuns runs) : IClassFixture<MaskedFallbackRuns>
{
    [Fact]
    public void Judges_the_current_screenshot_with_vision_enabled_a_secret_filled_or_not()
    {
        foreach (var title in new[] { MaskedFallbackRuns.VisionJudgment, MaskedFallbackRuns.JudgmentAfterFill })
        {
            Assert.Null(runs.Outcomes[title].Error);
        }

        Assert.Equal(2, runs.Judge.Requests.Count);
        foreach (var call in runs.Judge.Requests)
        {
            var judged = call.Messages[0];
            var image = Assert.Single(judged.Images!);
            Assert.Equal("image/png", image.MediaType);
            Assert.True(image.Data.Length > 0);
            Assert.Contains("semantic capture unavailable", judged.Content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Acts_on_the_fallback_screenshot_after_a_secret_fill()
    {
        Assert.Null(runs.Outcomes[MaskedFallbackRuns.ActAfterFill].Error);
        Assert.True(runs.Outcomes[MaskedFallbackRuns.ActAfterFill].ActPassed);
        Assert.Contains(runs.Captures, capture => capture.Filled && capture.Fallback);
        var loop = Assert.Single(runs.Loop.Requests);
        Assert.Equal(1, loop.Messages.Sum(message => message.Images?.Count ?? 0));
    }

    [Fact]
    public void Stops_tree_only_judgments_before_a_model_call()
    {
        var error = Assert.IsAssignableFrom<E2EException>(runs.Outcomes[MaskedFallbackRuns.TreeOnlyJudgment].Error);
        Assert.Contains("timed out", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(runs.Judge.Requests, request => request.Messages[0].Content!.Contains("nothing is visible", StringComparison.Ordinal));
    }
}
