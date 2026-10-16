// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>
/// <c>observe()</c> with the tree and the pixels. Not ported, because the port has no step record: "records the pixel decision as a
/// policy event on the step", which also reads the step's pixel byte metric. The pixels come from an engine that captures a masked
/// screenshot and proves its masking, a fake one, where upstream reads them from Chromium.
/// </summary>
public sealed class ObserveWithTheTreeAndPixelsTests : IAsyncLifetime
{
    private ExecutorObservation? _seen;
    private Exception? _error;

    public async Task InitializeAsync()
    {
        var looking = new TestExecutor("looker", async (context, _) =>
        {
            _seen = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true, Pixels = true });
            return Passed("looked");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ScreenshotEngine(CounterPage.World(), _ => ScreenshotEngine.Png()),
            AgentExecutor = looking,
            BaseUrl = "https://counter.test",
            TestTitle = "one act step",
        });
        try
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync("look at the screen");
        }
        catch (Exception ex)
        {
            _error = ex;
        }

        session.Complete(_error);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Returns_the_redacted_tree_beside_the_text_with_the_same_node_ids()
    {
        Assert.Null(_error);
        Assert.NotNull(_seen!.Tree);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Walk(IEnumerable<ExecutorNode> nodes)
        {
            foreach (var node in nodes)
            {
                ids.Add(node.Id);
                Walk(node.Children);
            }
        }

        Walk(_seen.Tree);
        Assert.Contains(TargetFor(_seen.Text, "button \"Increment\"").Id, ids);
        Assert.DoesNotContain("selector", JsonSerializer.Serialize(_seen.Tree), StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_masked_pixels_when_the_engine_can_prove_masking()
    {
        Assert.Null(_seen!.PixelsWithheld);
        Assert.NotNull(_seen.Pixels);
        Assert.True(_seen.Pixels.Width > 0);
        Assert.NotEmpty(_seen.Pixels.Data);
        Assert.StartsWith("image/", _seen.Pixels.MediaType, StringComparison.Ordinal);
    }
}
