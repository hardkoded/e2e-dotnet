// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>An executor's transcript is kept only under debug, as upstream's <c>--debug</c> does.</summary>
public sealed class AttachedTranscriptTests
{
    [Fact]
    public async Task Keeps_nothing_when_debug_is_off()
    {
        var root = await RunAsync(debug: false);

        Assert.Empty(Directory.GetFiles(root, "transcript-*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Keeps_the_transcript_an_executor_attached_with_a_secret_it_knows_redacted()
    {
        var root = await RunAsync(debug: true);

        var file = Assert.Single(Directory.GetFiles(root, "transcript-*.txt", SearchOption.AllDirectories));
        Assert.Equal("the model said <secret:admin.password>", await File.ReadAllTextAsync(file));
    }

    private static async Task<string> RunAsync(bool debug)
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-transcript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executor = new TestExecutor("talker", (context, _) =>
        {
            context.AttachTranscript("the model said admin-pass");
            return Task.FromResult(Passed("done"));
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            ProjectRoot = root,
            TestTitle = "transcript",
            Debug = debug,
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync(
            "talk",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass", "password") } });
        session.Complete();
        return root;
    }
}
