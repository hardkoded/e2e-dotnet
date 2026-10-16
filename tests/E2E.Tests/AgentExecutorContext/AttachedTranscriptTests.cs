// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>.NET-only: upstream keeps an executor's transcript only under <c>--debug</c>. The port keeps it whenever the attempt keeps results.</summary>
public sealed class AttachedTranscriptTests
{
    [Fact]
    public async Task Keeps_the_transcript_an_executor_attached_with_a_secret_it_knows_redacted()
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
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync(
            "talk",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass", "password") } });
        session.Complete();

        var file = Assert.Single(Directory.GetFiles(Path.Combine(root, ".e2e"), "transcript-*.txt", SearchOption.AllDirectories));
        Assert.Equal("the model said <secret:admin.password>", await File.ReadAllTextAsync(file));
    }
}
